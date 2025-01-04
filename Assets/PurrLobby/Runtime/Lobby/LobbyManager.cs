using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PurrNet;
using PurrNet.Logging;
using UnityEngine;
using UnityEngine.Events;

namespace PurrLobby
{
    public class LobbyManager : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour currentProvider;
        private ILobbyProvider _currentProvider;

        private readonly Queue<Action> _delayedActions = new Queue<Action>();

        public CreateRoomArgs createRoomArgs = new();
        public SerializableDictionary<string, string> searchRoomArgs = new();

        // Events exposed by the manager
        public UnityEvent<LobbyUser> OnInviteReceived = new UnityEvent<LobbyUser>();
        public UnityEvent<string> OnInviteAccepted = new UnityEvent<string>();
        public UnityEvent<string> OnInviteDeclined = new UnityEvent<string>();
        public UnityEvent<Lobby> OnRoomJoined = new UnityEvent<Lobby>();
        public UnityEvent<string> OnRoomJoinFailed = new UnityEvent<string>();
        public UnityEvent OnRoomLeft = new UnityEvent();
        public UnityEvent<Lobby> OnRoomUpdated = new UnityEvent<Lobby>();
        public UnityEvent<List<LobbyUser>> OnPlayerListUpdated = new UnityEvent<List<LobbyUser>>();
        public UnityEvent<List<Lobby>> OnRoomSearchResults = new UnityEvent<List<Lobby>>();
        public UnityEvent<List<FriendUser>> OnFriendListPulled = new UnityEvent<List<FriendUser>>();
        public UnityEvent<string> OnError = new UnityEvent<string>();

        public UnityEvent onInitialized = new UnityEvent();
        public UnityEvent onShutdown = new UnityEvent();

        public ILobbyProvider CurrentProvider => currentProvider as ILobbyProvider;
        
        private Lobby _currentLobby;
        private Lobby _lastKnownState;
        public Lobby CurrentLobby => _currentLobby;

        private void Awake()
        {
            _lastKnownState = new Lobby { IsValid = false };

            if (CurrentProvider != null)
                SetProvider(CurrentProvider);
            else
                PurrLogger.LogWarning("No lobby provider assigned to LobbyManager.");
        }

        private void Update()
        {
            while (_delayedActions.Count > 0)
            {
                _delayedActions.Dequeue()?.Invoke();
            }
        }

        private void InvokeDelayed(Action action)
        {
            try
            {
                _delayedActions.Enqueue(action);
            }
            catch (Exception ex)
            {
                PurrLogger.LogError($"Error in InvokeDelayed: {ex.Message}");
            }
        }

        /// <summary>
        /// Set or switch the current provider
        /// </summary>
        /// <param name="provider"></param>
        public void SetProvider(ILobbyProvider provider)
        {
            if (_currentProvider != null)
            {
                UnsubscribeFromProviderEvents();
                _currentProvider.Shutdown();
            }

            _currentProvider = provider;

            if (_currentProvider != null)
            {
                SubscribeToProviderEvents();
                RunTask(async () =>
                {
                    await _currentProvider.InitializeAsync();
                    InvokeDelayed(() => onInitialized?.Invoke());
                });
            }
        }

        // Subscribe to provider events
        private void SubscribeToProviderEvents()
        {
            _currentProvider.OnLobbyJoinFailed += message => InvokeDelayed(() => OnRoomJoinFailed.Invoke(message));
            _currentProvider.OnLobbyLeft += () => InvokeDelayed(() =>
            {
                _currentLobby = default;
                OnRoomLeft.Invoke();
            });
            
            _currentProvider.OnLobbyUpdated += room => InvokeDelayed(() =>
            {
                if (!HasRoomStateChanged(room)) return;

                _lastKnownState = room;
                _currentLobby = room;
                OnRoomUpdated.Invoke(room);
            });

            _currentProvider.OnLobbyPlayerListUpdated += players => InvokeDelayed(() => OnPlayerListUpdated.Invoke(players));
            _currentProvider.OnError += error => InvokeDelayed(() => OnError.Invoke(error));
            
            _currentProvider.OnLobbyUpdated += room =>
            {
                if (room.IsValid)
                {
                    InvokeDelayed(() => OnRoomJoined?.Invoke(room));
                }
            };
        }

        // Unsubscribe from provider events
        private void UnsubscribeFromProviderEvents()
        {
            _currentProvider.OnLobbyJoinFailed -= message => InvokeDelayed(() => OnRoomJoinFailed.Invoke(message));
            _currentProvider.OnLobbyLeft -= () => InvokeDelayed(() => OnRoomLeft.Invoke());
            _currentProvider.OnLobbyUpdated -= room => InvokeDelayed(() => OnRoomUpdated.Invoke(room));
            _currentProvider.OnLobbyPlayerListUpdated -= players => InvokeDelayed(() => OnPlayerListUpdated.Invoke(players));
            _currentProvider.OnError -= error => InvokeDelayed(() => OnError.Invoke(error));

            // ReSharper disable once EventUnsubscriptionViaAnonymousDelegate
            _currentProvider.OnLobbyUpdated -= room =>
            {
                if (room.IsValid)
                {
                    InvokeDelayed(() => OnRoomJoined?.Invoke(room));
                }
            };
        }

        /// <summary>
        /// Shuts down and clears the current provider
        /// </summary>
        public void Shutdown()
        {
            EnsureProviderSet();
            _currentProvider.Shutdown();
            onShutdown?.Invoke();
        }

        /// <summary>
        /// Prompts the provider to pull friends from the platform's friend list.
        /// </summary>
        public void PullFriends()
        {
            RunTask(async () =>
            {
                EnsureProviderSet();
                var friends = await _currentProvider.GetFriendsAsync();
                OnFriendListPulled?.Invoke(friends);
            });
        }

        /// <summary>
        /// Invite the given user to the current lobby.
        /// </summary>
        /// <param name="user"></param>
        public void InviteFriend(FriendUser user)
        {
            RunTask(async () =>
            {
                EnsureProviderSet();
                await _currentProvider.InviteFriendAsync(user);
            });
        }

        /// <summary>
        /// Creates a room using the inspector CreateRoomArgs values.
        /// </summary>
        public void CreateRoom()
        {
            CreateRoom(createRoomArgs.maxPlayers, createRoomArgs.roomProperties.ToDictionary());
        }
        
        /// <summary>
        /// Creates a room using custom settings set through code
        /// </summary>
        public void CreateRoom(int maxPlayers, Dictionary<string, string> roomProperties = null)
        {
            RunTask(async () =>
            {
                EnsureProviderSet();
                var room = await _currentProvider.CreateLobbyAsync(maxPlayers, roomProperties);
                _currentLobby = room;
                OnRoomUpdated?.Invoke(room);
            });
        }

        /// <summary>
        /// Leave the lobby
        /// </summary>
        public void LeaveLobby()
        {
            RunTask(async () =>
            {
                EnsureProviderSet();
                await _currentProvider.LeaveLobbyAsync();
                OnRoomLeft?.Invoke();
            });
        }

        /// <summary>
        /// Join the lobby with the given ID
        /// </summary>
        /// <param name="roomId">ID of the lobby to join</param>
        public void JoinLobby(string roomId)
        {
            if (string.IsNullOrEmpty(roomId))
            {
                OnRoomJoinFailed?.Invoke("Null or empty room ID.");
                return;
            }
            
            RunTask(async () =>
            {
                EnsureProviderSet();
                var room = await _currentProvider.JoinLobbyAsync(roomId);
                if (room.IsValid)
                {
                    OnRoomJoined?.Invoke(room);
                }
                else
                {
                    OnRoomJoinFailed?.Invoke($"Failed to join room {roomId}");
                }
            });
        }

        /// <summary>
        /// Prompts the provider to search lobbies with given filters
        /// </summary>
        /// <param name="maxRoomsToFind">Max amount of rooms to find</param>
        /// <param name="filters">Filters to use for search - only works if the provider supports it</param>
        public void SearchLobbies(int maxRoomsToFind = 10, Dictionary<string, string> filters = null)
        {
            if(filters == null)
                filters = searchRoomArgs.ToDictionary();
            
            RunTask(async () =>
            {
                EnsureProviderSet();
                var rooms = await _currentProvider.SearchLobbiesAsync(maxRoomsToFind, filters);
                OnRoomSearchResults?.Invoke(rooms);
            });
        }
        
        /// <summary>
        /// Set's the given User to Ready
        /// </summary>
        /// <param name="userId">User ID of player</param>
        /// <param name="isReady">Ready state to set</param>
        public void SetIsReady(string userId, bool isReady)
        {
            RunTask(async () =>
            {
                EnsureProviderSet();
                await _currentProvider.SetIsReadyAsync(userId, isReady);
            });
        }

        /// <summary>
        /// Toggles the local users ready state automatically
        /// </summary>
        public void ToggleLocalReady()
        {
            if (!_currentLobby.IsValid)
            {
                PurrLogger.LogError($"Can't toggle ready state, current lobby is invalid.");
                return;
            }
            
            var localUserId = _currentProvider.GetLocalUserIdAsync().Result;
            if (string.IsNullOrEmpty(localUserId))
            {
                PurrLogger.LogError($"Can't toggle ready state, local user ID is null or empty.");
                return;
            }
            
            var localLobbyUser = _currentLobby.Members.Find(x => x.Id == localUserId);
            SetIsReady(localUserId, !localLobbyUser.IsReady);
        }

        private void RunTask(Task task)
        {
            if (task == null) return;

            task.ContinueWith(t =>
            {
                if (t.Exception != null)
                    PurrLogger.LogError($"Task Error: {t.Exception.InnerException?.Message}");
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void RunTask(Func<Task> taskFunc)
        {
            RunTask(taskFunc());
        }

        // Ensure a provider is set before calling any method
        private void EnsureProviderSet()
        {
            if (_currentProvider == null)
                throw new InvalidOperationException("No lobby provider has been set.");
        }
        
        private bool HasRoomStateChanged(Lobby @new)
        {
            if (!_lastKnownState.IsValid || @new.Name != _lastKnownState.Name || @new.lobbyId != _lastKnownState.lobbyId || @new.Members.Count != _lastKnownState.Members.Count || @new.Properties.Count != _lastKnownState.Properties.Count)
                return true;

            for (int i = 0; i < @new.Members.Count; i++)
            {
                var newMember = @new.Members[i];
                var oldMember = _lastKnownState.Members[i];

                if (newMember.Id != oldMember.Id || newMember.IsReady != oldMember.IsReady || newMember.DisplayName != oldMember.DisplayName || newMember.Avatar != oldMember.Avatar)
                    return true;
            }

            return false;
        }

        [System.Serializable]
        public class CreateRoomArgs
        {
            public int maxPlayers = 5;
            public SerializableDictionary<string, string> roomProperties = null;
        }
    }
}
