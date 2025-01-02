#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

#if STEAMWORKS_NET
#define STEAMWORKS_NET_PACKAGE
#endif

#if STEAMWORKS_NET_PACKAGE
using Steamworks;
#endif

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace PurrLobby.Providers
{
    public class SteamLobbyProvider : MonoBehaviour
#if STEAMWORKS_NET_PACKAGE && !DISABLESTEAMWORKS
        , ILobbyProvider
#endif
    {
#if STEAMWORKS_NET_PACKAGE && !DISABLESTEAMWORKS
        public event UnityAction<LobbyUser> OnInviteReceived;
        public event UnityAction<string> OnInviteAccepted;
        public event UnityAction<string> OnInviteDeclined;
        public event UnityAction<string> OnRoomJoinFailed;
        public event UnityAction OnRoomLeft;
        public event UnityAction<LobbyRoom> OnRoomUpdated;
        public event UnityAction<IEnumerable<LobbyUser>> OnPlayerListUpdated;
        public event UnityAction<string> OnError;

        [SerializeField] private bool forceSteamInit = false;

        private bool _initialized;
        private bool _runCallbacks;
        private CSteamID _currentLobby;
        public CSteamID CurrentLobby => _currentLobby;

        public async Task InitializeAsync()
        {
            if (_initialized)
                return;

            if (forceSteamInit)
            {
                if (!SteamAPI.Init())
                {
                    Debug.LogError("SteamAPI initialization failed.");
                    OnError?.Invoke("SteamAPI initialization failed.");
                    return;
                }
            }

            int retries = 100;
            while (retries > 0)
            {
                try
                {
                    var steamID = SteamUser.GetSteamID();
                    if (steamID.m_SteamID != 0)
                    {
                        _initialized = true;
                        _runCallbacks = true;
                        RunSteamCallbacks();
                        Debug.Log("Steamworks initialized successfully.");
                        return;
                    }
                }
                catch (System.InvalidOperationException)
                {
                    
                }

                await Task.Delay(100);
                retries--;
            }

            Debug.LogWarning("Steamworks is not initialized after retries. Initialization skipped.");
        }


        private async void RunSteamCallbacks()
        {
            while (_runCallbacks)
            {
                SteamAPI.RunCallbacks();
                await Task.Delay(16);
            }
        }

        public void Shutdown()
        {
            if (_initialized)
            {
                _runCallbacks = false;

                if (forceSteamInit)
                {
                    SteamAPI.Shutdown();
                }

                _initialized = false;
            }
        }

        public async Task<IEnumerable<LobbyUser>> GetFriendsAsync()
        {
            if (!_initialized) return null;

            var friends = new List<LobbyUser>();
            int friendCount = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);

            for (int i = 0; i < friendCount; i++)
            {
                var steamID = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
                friends.Add(new LobbyUser
                {
                    Id = steamID.m_SteamID.ToString(),
                    DisplayName = SteamFriends.GetFriendPersonaName(steamID),
                    IsReady = false
                });
            }

            return friends;
        }
        
        public async Task SetIsReadyAsync(string userId, bool isReady)
        {
            // Example placeholder logic; customize based on SteamLobby API
            var lobbyId = SteamUser.GetSteamID();
            SteamMatchmaking.SetLobbyMemberData(lobbyId, "IsReady", isReady.ToString());
            OnPlayerListUpdated?.Invoke(await GetLobbyMembersAsync());
        }
        
        public async Task<IEnumerable<LobbyUser>> GetLobbyMembersAsync()
        {
            var members = new List<LobbyUser>();
            if (!_initialized) return members;

            var lobbyId = SteamUser.GetSteamID();
            int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobbyId);

            for (int i = 0; i < memberCount; i++)
            {
                var memberId = SteamMatchmaking.GetLobbyMemberByIndex(lobbyId, i);
                var isReadyString = SteamMatchmaking.GetLobbyMemberData(lobbyId, memberId, "IsReady");
                var isReady = !string.IsNullOrEmpty(isReadyString) && isReadyString == "True";

                members.Add(new LobbyUser
                {
                    Id = memberId.m_SteamID.ToString(),
                    DisplayName = SteamFriends.GetFriendPersonaName(memberId),
                    IsReady = isReady
                });
            }

            return members;
        }

        public async Task InviteFriendAsync(LobbyUser user)
        {
            if (!_initialized) return;

            var steamID = new CSteamID(ulong.Parse(user.Id));
            SteamMatchmaking.InviteUserToLobby(SteamUser.GetSteamID(), steamID);
        }

        public async Task AcceptInviteAsync(string inviteId)
        {
            Debug.Log($"Invite {inviteId} accepted.");
        }

        public async Task DeclineInviteAsync(string inviteId)
        {
            Debug.Log($"Invite {inviteId} declined.");
        }

        public async Task<LobbyRoom> CreateRoomAsync(int maxPlayers, Dictionary<string, string> roomProperties = null)
        {
            if (!_initialized)
                return default;

            var tcs = new TaskCompletionSource<bool>();
            CSteamID lobbyId = default;

            void OnLobbyCreated(LobbyCreated_t result, bool ioFailure)
            {
                if (result.m_eResult == EResult.k_EResultOK)
                {
                    lobbyId = new CSteamID(result.m_ulSteamIDLobby);
                    tcs.TrySetResult(true);
                }
                else
                {
                    tcs.TrySetResult(false);
                }
            }

            var callResult = CallResult<LobbyCreated_t>.Create(OnLobbyCreated);
            var handle = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, maxPlayers);
            callResult.Set(handle);

            if (!await tcs.Task)
            {
                OnRoomJoinFailed?.Invoke("Failed to create lobby.");
                return new LobbyRoom { IsValid = false };
            }

            _currentLobby = lobbyId;

            var room = new LobbyRoom
            {
                IsValid = true,
                RoomId = lobbyId.m_SteamID.ToString(),
                MaxPlayers = maxPlayers,
                CurrentPlayers = 1,
                Properties = roomProperties ?? new Dictionary<string, string>()
            };

            foreach (var prop in room.Properties)
            {
                SteamMatchmaking.SetLobbyData(lobbyId, prop.Key, prop.Value);
            }

            OnRoomUpdated?.Invoke(room);
            return room;
        }
        
        public async Task<LobbyRoom> JoinRoomAsync(string roomId)
        {
            if (!_initialized) return default;

            var tcs = new TaskCompletionSource<bool>();

            void OnLobbyJoined(LobbyEnter_t result, bool bioFailure)
            {
                if (result.m_EChatRoomEnterResponse == (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
                {
                    _currentLobby = new CSteamID(result.m_ulSteamIDLobby);
                    tcs.TrySetResult(true);
                }
                else
                {
                    tcs.TrySetResult(false);
                }
            }

            var callResult = CallResult<LobbyEnter_t>.Create(OnLobbyJoined);
            var handle = SteamMatchmaking.JoinLobby(new CSteamID(ulong.Parse(roomId)));
            callResult.Set(handle);

            if (!await tcs.Task)
            {
                OnRoomJoinFailed?.Invoke($"Failed to join lobby {roomId}.");
                return new LobbyRoom { IsValid = false };
            }

            var room = new LobbyRoom
            {
                IsValid = true,
                RoomId = roomId,
                MaxPlayers = SteamMatchmaking.GetLobbyMemberLimit(_currentLobby),
                CurrentPlayers = SteamMatchmaking.GetNumLobbyMembers(_currentLobby),
                Properties = new Dictionary<string, string>()
            };

            OnRoomUpdated?.Invoke(room);
            return room;
        }

        public Task LeaveRoomAsync()
        {
            if (!_initialized || _currentLobby.m_SteamID == 0) return Task.CompletedTask;

            SteamMatchmaking.LeaveLobby(_currentLobby);
            _currentLobby = default;
            OnRoomLeft?.Invoke();
            return Task.CompletedTask;
        }

        public Task<IEnumerable<LobbyRoom>> SearchRoomsAsync(Dictionary<string, string> filters = null)
        {
            return Task.FromResult<IEnumerable<LobbyRoom>>(null);
        }
#endif
    }
}