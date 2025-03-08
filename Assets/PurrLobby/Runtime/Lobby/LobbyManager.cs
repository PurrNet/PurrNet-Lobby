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
        private int _taskLock;

        public CreateRoomArgs createRoomArgs = new();
        public SerializableDictionary<string, string> searchRoomArgs = new();

        // Events exposed by the manager
        public UnityEvent<Lobby> OnRoomJoined = new UnityEvent<Lobby>();
        public UnityEvent<string> OnRoomJoinFailed = new UnityEvent<string>();
        public UnityEvent OnRoomLeft = new UnityEvent();
        public UnityEvent<Lobby> OnRoomUpdated = new UnityEvent<Lobby>();
        public UnityEvent<List<LobbyUser>> OnPlayerListUpdated = new UnityEvent<List<LobbyUser>>();
        public UnityEvent<List<Lobby>> OnRoomSearchResults = new UnityEvent<List<Lobby>>();
        public UnityEvent<List<FriendUser>> OnFriendListPulled = new UnityEvent<List<FriendUser>>();
        public UnityEvent OnAllReady = new UnityEvent();
        public UnityEvent<string> OnError = new UnityEvent<string>();

        public UnityEvent onInitialized = new UnityEvent();
        public UnityEvent onShutdown = new UnityEvent();

        public ILobbyProvider CurrentProvider => currentProvider as ILobbyProvider;

        private Lobby _currentLobby
        {
            get
            {
                if (!_lobbyDataHolder)
                    return default;
                return _lobbyDataHolder.CurrentLobby;
            }
            set
            {
                _lobbyDataHolder.SetCurrentLobby(value);
            }
        }

        private Lobby _lastKnownState;
        public Lobby CurrentLobby => _currentLobby;
        private LobbyDataHolder _lobbyDataHolder;

        private bool IsStarting = false;

        private void Awake()
        {
            _lastKnownState = new Lobby { IsValid = false };

            if (CurrentProvider != null)
                SetProvider(CurrentProvider);
            else
                PurrLogger.LogWarning("No lobby provider assigned to LobbyManager.");

            SetupDataHolder();
        }

        private void SetupDataHolder()
        {
            _lobbyDataHolder = FindFirstObjectByType<LobbyDataHolder>();
            if (!_lobbyDataHolder)
            {
                var newObject = new GameObject("LobbyDataHolder");
                _lobbyDataHolder = newObject.AddComponent<LobbyDataHolder>();
                return;
            }

            if (_lobbyDataHolder.CurrentLobby.IsValid)
            {
                LeaveLobby(_lobbyDataHolder.CurrentLobby.LobbyId);
                _lobbyDataHolder.SetCurrentLobby(default);
            }
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
                OnRoomLeft?.Invoke();
            });
            
            _currentProvider.OnLobbyUpdated += room => InvokeDelayed(() =>
            {
                if(!_lastKnownState.HasChanged(room) || room.Members.Count <= 0 || !room.IsValid) return;

                _lastKnownState = room;
                _currentLobby = room;
                OnRoomUpdated?.Invoke(room);

                if (!IsStarting && room.Members.TrueForAll(x => x.IsReady))
                {
                    IsStarting = true; //Prevent calling ready again if lobby is updated after all ready
                    CallOnAllReady();
                }
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
        public void PullFriends(FriendFilter filter)
        {
            RunTask(async () =>
            {
                EnsureProviderSet();
                var friends = await _currentProvider.GetFriendsAsync(filter);
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
        /// Leave a specific lobby
        /// </summary>
        public void LeaveLobby(string lobbyId)
        {
            RunTask(async () =>
            {
                EnsureProviderSet();
                await _currentProvider.LeaveLobbyAsync(lobbyId);
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
        /// Sets meta data on the current lobby we're in
        /// </summary>
        /// <param name="key">Key/Identifier of the meta data</param>
        /// <param name="value">The value of the meta data to be stored</param>
        public void SetLobbyData(string key, string value)
        {
            RunTask(async () =>
            {
                EnsureProviderSet();
                await _currentProvider.SetLobbyDataAsync(key, value);
            });
        }
        
        /// <summary>
        /// Gets meta data from the current lobby we're in
        /// </summary>
        /// <param name="key">Key/Identifier of the meta data we want</param>
        /// <returns>Value of the meta data we get</returns>
        public async Task<String> GetLobbyData(string key) 
        {
            EnsureProviderSet();
            return await _currentProvider.GetLobbyDataAsync(key);
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

        private void OnDestroy()
        {
            _currentProvider = null;
            _lobbyDataHolder = null;
        }

        private async void CallOnAllReady()
        {
            await WaitForAllTasksAsync();
            if(_currentLobby.IsValid && _currentLobby.Members.TrueForAll(x => x.IsReady))
            {
                await _currentProvider.SetAllReadyAsync();

                OnAllReady?.Invoke();
            }
        }
        
        public async Task WaitForAllTasksAsync()
        {
            while (_taskLock > 0)
            {
                await Task.Yield();
            }
        }

        private async void RunTask(Func<Task> taskFunc)
        {
            if (taskFunc == null || _currentProvider == null) return;

            _taskLock++;
            try
            {
                await taskFunc();
            }
            catch (Exception ex)
            {
                PurrLogger.LogError($"Task Error: {ex.Message}");
            }
            finally
            {
                _taskLock--;
                if (_taskLock < 0)
                    _taskLock = 0;
            }
        }

        private void EnsureProviderSet()
        {
            if (_currentProvider == null)
                throw new InvalidOperationException("No lobby provider has been set.");
        }

        public void SetLobbyStarted()
        {
            _currentProvider.SetLobbyStartedAsync();
        }

        [System.Serializable]
        public class CreateRoomArgs
        {
            public int maxPlayers = 5;
            public SerializableDictionary<string, string> roomProperties = null;
        }
        
        [System.Serializable]
        public enum FriendFilter
        {
            InThisGame,
            Online,
            All
        }
    }
}