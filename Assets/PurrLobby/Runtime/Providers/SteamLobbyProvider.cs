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
using System.Linq;
using System.Threading.Tasks;
using PurrNet.Logging;
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

        [SerializeField] private bool handleSteamInit = false;

        private bool _initialized;
        private bool _runCallbacks;
        private CSteamID _currentLobby;
        public CSteamID CurrentLobby => _currentLobby;
        private Callback<LobbyDataUpdate_t> _lobbyDataUpdateCallback;
        private Callback<AvatarImageLoaded_t> _avatarImageLoadedCallback;
        private Callback<LobbyChatUpdate_t> _lobbyChatUpdateCallback;

        public async Task InitializeAsync()
        {
            if (_initialized)
                return;

            if (handleSteamInit)
            {
                if (!SteamAPI.Init())
                {
                    PurrLogger.LogError("SteamAPI initialization failed.");
                    OnError?.Invoke("SteamAPI initialization failed.");
                    return;
                }
            }
            
            _avatarImageLoadedCallback = Callback<AvatarImageLoaded_t>.Create(OnAvatarImageLoaded);
            _lobbyDataUpdateCallback = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);
            _lobbyChatUpdateCallback = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);

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
                        PurrLogger.Log("Steamworks initialized successfully.");
                        return;
                    }
                }
                catch (System.InvalidOperationException) { }

                await Task.Delay(100);
                retries--;
            }

            PurrLogger.LogWarning("Steamworks is not initialized after retries. Initialization skipped.");
        }


        private async void RunSteamCallbacks()
        {
            while (_runCallbacks)
            {
                SteamAPI.RunCallbacks();
                await Task.Delay(16);
            }
        }
        
        private void OnLobbyChatUpdate(LobbyChatUpdate_t callback)
        {
            if (_currentLobby.m_SteamID != callback.m_ulSteamIDLobby)
                return;

            var stateChange = (EChatMemberStateChange)callback.m_rgfChatMemberStateChange;

            if (stateChange.HasFlag(EChatMemberStateChange.k_EChatMemberStateChangeEntered))
            {
                //PurrLogger.Log($"User {callback.m_ulSteamIDUserChanged} joined the lobby.");
            }

            if (stateChange.HasFlag(EChatMemberStateChange.k_EChatMemberStateChangeLeft) ||
                stateChange.HasFlag(EChatMemberStateChange.k_EChatMemberStateChangeDisconnected))
            {
                //PurrLogger.Log($"User {callback.m_ulSteamIDUserChanged} left the lobby.");
            }

            var data = SteamMatchmaking.GetLobbyData(_currentLobby, "Name");
            var properties = GetLobbyProperties(_currentLobby);
            var updatedLobbyUsers = GetLobbyUsers(_currentLobby);

            var updatedRoom = LobbyRoomFactory.Create(
                data,
                _currentLobby.m_SteamID.ToString(),
                SteamMatchmaking.GetLobbyMemberLimit(_currentLobby),
                updatedLobbyUsers,
                properties
            );

            OnRoomUpdated?.Invoke(updatedRoom);
        }
        
        private void OnLobbyDataUpdate(LobbyDataUpdate_t callback)
        {
            if (_currentLobby.m_SteamID != callback.m_ulSteamIDLobby)
                return;

            var updatedLobbyUsers = GetLobbyUsers(_currentLobby);

            var updatedRoom = LobbyRoomFactory.Create(
                SteamMatchmaking.GetLobbyData(_currentLobby, "Name"),
                _currentLobby.m_SteamID.ToString(),
                SteamMatchmaking.GetLobbyMemberLimit(_currentLobby),
                updatedLobbyUsers,
                GetLobbyProperties(_currentLobby)
            );
            
            OnRoomUpdated?.Invoke(updatedRoom);
        }

        public void Shutdown()
        {
            if (_initialized)
            {
                _runCallbacks = false;
                _lobbyDataUpdateCallback = null;
                _lobbyChatUpdateCallback = null;

                if (handleSteamInit)
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
            if (!_initialized) return;

            var lobbyId = _currentLobby;
            var userExists = GetLobbyUsers(lobbyId).Any(user => user.Id == userId);
            if (!userExists)
            {
                PurrLogger.LogError($"User {userId} no longer exists in the lobby.");
                return;
            }

            SteamMatchmaking.SetLobbyMemberData(lobbyId, "IsReady", isReady.ToString());

            SteamMatchmaking.SetLobbyData(lobbyId, "UpdateTrigger", DateTime.UtcNow.Ticks.ToString());
        }
        
        public async Task<List<LobbyUser>> GetLobbyMembersAsync()
        {
            if (!_initialized) return new List<LobbyUser>();
            return GetLobbyUsers(SteamUser.GetSteamID());
        }

        public async Task InviteFriendAsync(LobbyUser user)
        {
            if (!_initialized) return;

            var steamID = new CSteamID(ulong.Parse(user.Id));
            SteamMatchmaking.InviteUserToLobby(SteamUser.GetSteamID(), steamID);
        }

        public async Task AcceptInviteAsync(string inviteId)
        {
            PurrLogger.Log($"Invite {inviteId} accepted.");
        }

        public async Task DeclineInviteAsync(string inviteId)
        {
            PurrLogger.Log($"Invite {inviteId} declined.");
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
            SteamMatchmaking.SetLobbyData(_currentLobby, "Name", $"{SteamFriends.GetPersonaName()}'s Lobby");

            if (!await tcs.Task)
                return new LobbyRoom { IsValid = false };

            _currentLobby = lobbyId;

            if (roomProperties != null)
            {
                foreach (var prop in roomProperties)
                {
                    SteamMatchmaking.SetLobbyData(lobbyId, prop.Key, prop.Value);
                }
            }

            return LobbyRoomFactory.Create(
                SteamMatchmaking.GetLobbyData(_currentLobby, "Name"),
                lobbyId.m_SteamID.ToString(),
                maxPlayers,
                GetLobbyUsers(lobbyId),
                roomProperties
            );
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
            var lobbyId = new CSteamID(ulong.Parse(roomId));
            var handle = SteamMatchmaking.JoinLobby(lobbyId);
            callResult.Set(handle);

            if (!await tcs.Task)
            {
                OnRoomJoinFailed?.Invoke($"Failed to join lobby {roomId}.");
                return new LobbyRoom { IsValid = false };
            }

            var room = LobbyRoomFactory.Create(
                SteamMatchmaking.GetLobbyData(_currentLobby, "Name"),
                roomId,
                SteamMatchmaking.GetLobbyMemberLimit(_currentLobby),
                GetLobbyUsers(lobbyId),
                GetLobbyProperties(_currentLobby)
            );

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

        public async Task<List<LobbyRoom>> SearchRoomsAsync(int maxRoomsToFind = 10, Dictionary<string, string> filters = null)
        {
            if (!_initialized)
                return new List<LobbyRoom>();

            var tcs = new TaskCompletionSource<List<LobbyRoom>>();
            var results = new List<LobbyRoom>();

            if (filters != null)
            {
                foreach (var filter in filters)
                {
                    SteamMatchmaking.AddRequestLobbyListStringFilter(filter.Key, filter.Value, ELobbyComparison.k_ELobbyComparisonEqual);
                }
            }

            SteamMatchmaking.AddRequestLobbyListResultCountFilter(maxRoomsToFind);

            void OnLobbiesMatching(LobbyMatchList_t result, bool ioFailure)
            {
                int totalLobbies = (int)result.m_nLobbiesMatching;
                Debug.Log($"Found lobbies: {totalLobbies}");

                for (int i = 0; i < totalLobbies; i++)
                {
                    var lobbyId = SteamMatchmaking.GetLobbyByIndex(i);
                    var roomProperties = GetLobbyProperties(lobbyId);
                    int maxPlayers = SteamMatchmaking.GetLobbyMemberLimit(lobbyId);

                    results.Add(new LobbyRoom
                    {
                        Name = SteamMatchmaking.GetLobbyData(lobbyId, "Name"),
                        IsValid = true,
                        RoomId = lobbyId.m_SteamID.ToString(),
                        MaxPlayers = maxPlayers,
                        Properties = roomProperties,
                        Members = GetLobbyUsers(lobbyId)
                    });
                }

                tcs.TrySetResult(results);
            }

            var callResult = CallResult<LobbyMatchList_t>.Create(OnLobbiesMatching);
            SteamMatchmaking.RequestLobbyList();
            //callResult.Set(SteamMatchmaking.RequestLobbyList());

            return await tcs.Task;
        }
        
        private LobbyUser CreateLobbyUser(CSteamID steamId, CSteamID lobbyId)
        {
            var displayName = SteamFriends.GetFriendPersonaName(steamId);
            var isReadyString = SteamMatchmaking.GetLobbyMemberData(lobbyId, steamId, "IsReady");
            var isReady = !string.IsNullOrEmpty(isReadyString) && isReadyString == "True";

            var avatarHandle = SteamFriends.GetLargeFriendAvatar(steamId);
            Texture2D avatar = null;

            if (avatarHandle != -1 && SteamUtils.GetImageSize(avatarHandle, out uint width, out uint height))
            {
                byte[] imageBuffer = new byte[width * height * 4];
                if (SteamUtils.GetImageRGBA(avatarHandle, imageBuffer, imageBuffer.Length))
                {
                    avatar = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false);
                    avatar.LoadRawTextureData(imageBuffer);
                    FlipTextureVertically(avatar);
                    avatar.Apply();
                }
            }

            return new LobbyUser
            {
                Id = steamId.m_SteamID.ToString(),
                DisplayName = displayName,
                IsReady = isReady,
                Avatar = avatar
            };
        }
        
        private void OnAvatarImageLoaded(AvatarImageLoaded_t callback)
        {
            var steamId = callback.m_steamID;
            if (callback.m_iImage == -1)
            {
                PurrLogger.LogWarning($"Failed to load avatar for user {steamId}");
                return;
            }

            if (SteamUtils.GetImageSize(callback.m_iImage, out uint width, out uint height))
            {
                byte[] imageBuffer = new byte[width * height * 4];
                if (SteamUtils.GetImageRGBA(callback.m_iImage, imageBuffer, imageBuffer.Length))
                {
                    Texture2D avatar = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false);
                    avatar.LoadRawTextureData(imageBuffer);
                    FlipTextureVertically(avatar);
                    avatar.Apply();

                    UpdateUserAvatar(steamId, avatar);
                }
            }
        }
        
        private void UpdateUserAvatar(CSteamID steamId, Texture2D avatar)
        {
            var updatedMembers = GetLobbyUsers(_currentLobby);

            for (int i = 0; i < updatedMembers.Count; i++)
            {
                if (updatedMembers[i].Id == steamId.m_SteamID.ToString())
                {
                    var updatedUser = updatedMembers[i];
                    updatedUser.Avatar = avatar;
                    updatedMembers[i] = updatedUser;
                    break;
                }
            }
            
            var updatedRoom = new LobbyRoom
            {
                Name = SteamMatchmaking.GetLobbyData(_currentLobby, "Name"),
                IsValid = true,
                RoomId = _currentLobby.m_SteamID.ToString(),
                MaxPlayers = SteamMatchmaking.GetLobbyMemberLimit(_currentLobby),
                Properties = new Dictionary<string, string>(), // Use existing properties if needed
                Members = updatedMembers
            };

            OnRoomUpdated?.Invoke(updatedRoom);
        }
        
        public async Task<string> GetLocalUserIdAsync()
        {
            if (!_initialized)
                return null;

            return await Task.FromResult(SteamUser.GetSteamID().m_SteamID.ToString());
        }

        private void FlipTextureVertically(Texture2D texture)
        {
            var pixels = texture.GetPixels();
            int width = texture.width;
            int height = texture.height;

            for (int y = 0; y < height / 2; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var topPixel = pixels[y * width + x];
                    var bottomPixel = pixels[(height - 1 - y) * width + x];

                    pixels[y * width + x] = bottomPixel;
                    pixels[(height - 1 - y) * width + x] = topPixel;
                }
            }

            texture.SetPixels(pixels);
        }

        private List<LobbyUser> GetLobbyUsers(CSteamID lobbyId)
        {
            var users = new List<LobbyUser>();
            int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobbyId);

            for (int i = 0; i < memberCount; i++)
            {
                var steamId = SteamMatchmaking.GetLobbyMemberByIndex(lobbyId, i);
                users.Add(CreateLobbyUser(steamId, lobbyId));
            }

            return users;
        }
        
        private void OnApplicationQuit()
        {
            LeaveRoomIfInLobby();
        }

        private void OnDestroy()
        {
            LeaveRoomIfInLobby();
        }

        private void LeaveRoomIfInLobby()
        {
            if (_currentLobby.m_SteamID != 0)
            {
                SteamMatchmaking.LeaveLobby(_currentLobby);
                _currentLobby = default;
                PurrLogger.Log("Left the lobby as the application stopped.");
            }
        }
        
        private Dictionary<string, string> GetLobbyProperties(CSteamID lobbyId)
        {
            var properties = new Dictionary<string, string>();
            int propertyCount = SteamMatchmaking.GetLobbyDataCount(lobbyId);

            for (int i = 0; i < propertyCount; i++)
            {
                string key = string.Empty;
                string value = string.Empty;
                int keySize = 256;
                int valueSize = 256;

                bool success = SteamMatchmaking.GetLobbyDataByIndex(
                    lobbyId, 
                    i, 
                    out key, 
                    keySize, 
                    out value, 
                    valueSize
                );

                if (success)
                {
                    key = key.TrimEnd('\0');
                    value = value.TrimEnd('\0');
                    properties[key] = value;
                }
            }

            return properties;
        }
#endif
    }
}