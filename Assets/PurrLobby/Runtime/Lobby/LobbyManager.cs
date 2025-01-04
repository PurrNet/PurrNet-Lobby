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

        // Events exposed by the manager
        public UnityEvent<LobbyUser> OnInviteReceived = new UnityEvent<LobbyUser>();
        public UnityEvent<string> OnInviteAccepted = new UnityEvent<string>();
        public UnityEvent<string> OnInviteDeclined = new UnityEvent<string>();
        public UnityEvent<LobbyRoom> OnRoomJoined = new UnityEvent<LobbyRoom>();
        public UnityEvent<string> OnRoomJoinFailed = new UnityEvent<string>();
        public UnityEvent OnRoomLeft = new UnityEvent();
        public UnityEvent<LobbyRoom> OnRoomUpdated = new UnityEvent<LobbyRoom>();
        public UnityEvent<IEnumerable<LobbyUser>> OnPlayerListUpdated = new UnityEvent<IEnumerable<LobbyUser>>();
        public UnityEvent<string> OnError = new UnityEvent<string>();

        public UnityEvent onInitialized = new UnityEvent();
        public UnityEvent onShutdown = new UnityEvent();

        public ILobbyProvider CurrentProvider => currentProvider as ILobbyProvider;
        
        private LobbyRoom _currentLobby;
        private LobbyRoom _lastKnownRoomState;
        public LobbyRoom CurrentLobby => _currentLobby;

        private void Awake()
        {
            _lastKnownRoomState = new LobbyRoom { IsValid = false };

            if (CurrentProvider != null)
                SetProvider(CurrentProvider);
            else
                Debug.LogWarning("No lobby provider assigned to LobbyManager.");
        }

        private void Update()
        {
            // Process delayed actions
            while (_delayedActions.Count > 0)
            {
                _delayedActions.Dequeue()?.Invoke();
            }
        }

        private void InvokeDelayed(Action action)
        {
            _delayedActions.Enqueue(action);
        }

        // Set or switch the current provider
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
            _currentProvider.OnInviteReceived += user => InvokeDelayed(() => OnInviteReceived.Invoke(user));
            _currentProvider.OnInviteAccepted += inviteId => InvokeDelayed(() => OnInviteAccepted.Invoke(inviteId));
            _currentProvider.OnInviteDeclined += inviteId => InvokeDelayed(() => OnInviteDeclined.Invoke(inviteId));
            _currentProvider.OnRoomJoinFailed += message => InvokeDelayed(() => OnRoomJoinFailed.Invoke(message));
            _currentProvider.OnRoomLeft += () => InvokeDelayed(() =>
            {
                _currentLobby = default;
                OnRoomLeft.Invoke();
            });
            _currentProvider.OnRoomUpdated += room => InvokeDelayed(() =>
            {
                if (!HasRoomStateChanged(room)) return;

                _lastKnownRoomState = room;
                _currentLobby = room;
                OnRoomUpdated.Invoke(room);
            });

            _currentProvider.OnPlayerListUpdated += players => InvokeDelayed(() => OnPlayerListUpdated.Invoke(players));
            _currentProvider.OnError += error => InvokeDelayed(() => OnError.Invoke(error));

            _currentProvider.OnRoomUpdated += room =>
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
            _currentProvider.OnInviteReceived -= user => InvokeDelayed(() => OnInviteReceived.Invoke(user));
            _currentProvider.OnInviteAccepted -= inviteId => InvokeDelayed(() => OnInviteAccepted.Invoke(inviteId));
            _currentProvider.OnInviteDeclined -= inviteId => InvokeDelayed(() => OnInviteDeclined.Invoke(inviteId));
            _currentProvider.OnRoomJoinFailed -= message => InvokeDelayed(() => OnRoomJoinFailed.Invoke(message));
            _currentProvider.OnRoomLeft -= () => InvokeDelayed(() => OnRoomLeft.Invoke());
            _currentProvider.OnRoomUpdated -= room => InvokeDelayed(() => OnRoomUpdated.Invoke(room));
            _currentProvider.OnPlayerListUpdated -= players => InvokeDelayed(() => OnPlayerListUpdated.Invoke(players));
            _currentProvider.OnError -= error => InvokeDelayed(() => OnError.Invoke(error));

            // ReSharper disable once EventUnsubscriptionViaAnonymousDelegate
            _currentProvider.OnRoomUpdated -= room =>
            {
                if (room.IsValid)
                {
                    InvokeDelayed(() => OnRoomJoined?.Invoke(room));
                }
            };
        }

        public void Shutdown()
        {
            EnsureProviderSet();
            _currentProvider.Shutdown();
            onShutdown?.Invoke();
        }

        public void GetFriends()
        {
            RunTask(async () =>
            {
                EnsureProviderSet();
                var friends = await _currentProvider.GetFriendsAsync();
                OnPlayerListUpdated?.Invoke(friends);
            });
        }

        public void InviteFriend(LobbyUser user)
        {
            RunTask(async () =>
            {
                EnsureProviderSet();
                await _currentProvider.InviteFriendAsync(user);
            });
        }

        public void AcceptInvite(string inviteId)
        {
            RunTask(async () =>
            {
                EnsureProviderSet();
                await _currentProvider.AcceptInviteAsync(inviteId);
            });
        }

        public void DeclineInvite(string inviteId)
        {
            RunTask(async () =>
            {
                EnsureProviderSet();
                await _currentProvider.DeclineInviteAsync(inviteId);
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
                var room = await _currentProvider.CreateRoomAsync(maxPlayers, roomProperties);
                _currentLobby = room;
                OnRoomUpdated?.Invoke(room);
            });
        }

        public void LeaveRoom()
        {
            RunTask(async () =>
            {
                EnsureProviderSet();
                await _currentProvider.LeaveRoomAsync();
                OnRoomLeft?.Invoke();
            });
        }

        public void JoinRoom(string roomId)
        {
            if (string.IsNullOrEmpty(roomId))
            {
                OnRoomJoinFailed?.Invoke("Null or empty room ID.");
                return;
            }
            
            RunTask(async () =>
            {
                EnsureProviderSet();
                var room = await _currentProvider.JoinRoomAsync(roomId);
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

        public void SearchRooms(int maxRoomsToFind = 10, Dictionary<string, string> filters = null)
        {
            RunTask(async () =>
            {
                EnsureProviderSet();
                var rooms = await _currentProvider.SearchRoomsAsync(maxRoomsToFind, filters);
                foreach (var room in rooms)
                {
                    Debug.Log($"Found room: {room.RoomId}");
                }
            });
        }
        
        public void SetIsReady(string userId, bool isReady)
        {
            RunTask(async () =>
            {
                EnsureProviderSet();
                await _currentProvider.SetIsReadyAsync(userId, isReady);
            });
        }

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
                    Debug.LogError($"Task Error: {t.Exception.InnerException?.Message}");
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
        
        private bool HasRoomStateChanged(LobbyRoom newRoom)
        {
            if (!_lastKnownRoomState.IsValid || newRoom.RoomId != _lastKnownRoomState.RoomId || newRoom.Members.Count != _lastKnownRoomState.Members.Count)
                return true;

            for (int i = 0; i < newRoom.Members.Count; i++)
            {
                var newMember = newRoom.Members[i];
                var oldMember = _lastKnownRoomState.Members[i];

                if (newMember.Id != oldMember.Id || newMember.IsReady != oldMember.IsReady || newMember.DisplayName != oldMember.DisplayName)
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
