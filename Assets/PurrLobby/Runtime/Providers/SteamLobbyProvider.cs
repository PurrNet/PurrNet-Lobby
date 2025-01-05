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

        public event UnityAction<string> OnLobbyJoinFailed;
        public event UnityAction OnLobbyLeft;
        public event UnityAction<Lobby> OnLobbyUpdated;
        public event UnityAction<List<LobbyUser>> OnLobbyPlayerListUpdated;
        public event UnityAction<List<FriendUser>> OnFriendListPulled;
        public event UnityAction<string> OnError;

        [SerializeField] private bool handleSteamInit = false;

        private bool _initialized;
        private bool _runCallbacks;
        private CSteamID _currentLobby;
        public CSteamID CurrentLobby => _currentLobby;
        private Callback<LobbyDataUpdate_t> _lobbyDataUpdateCallback;
        private Callback<AvatarImageLoaded_t> _avatarImageLoadedCallback;
        private Callback<LobbyChatUpdate_t> _lobbyChatUpdateCallback;
        private Callback<GameLobbyJoinRequested_t> _gameLobbyJoinRequestedCallback;

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
            _gameLobbyJoinRequestedCallback = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);

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
            
            var ownerId = SteamMatchmaking.GetLobbyOwner(_currentLobby).m_SteamID.ToString();
            var localId = SteamUser.GetSteamID().m_SteamID.ToString();
            var isOwner = localId == ownerId;

            var data = SteamMatchmaking.GetLobbyData(_currentLobby, "Name");
            var properties = GetLobbyProperties(_currentLobby);
            var updatedLobbyUsers = GetLobbyUsers(_currentLobby);

            var updatedLobby = LobbyFactory.Create(
                data,
                _currentLobby.m_SteamID.ToString(),
                SteamMatchmaking.GetLobbyMemberLimit(_currentLobby),
                isOwner,
                updatedLobbyUsers,
                properties
            );

            OnLobbyUpdated?.Invoke(updatedLobby);
        }
        
        private void OnLobbyDataUpdate(LobbyDataUpdate_t callback)
        {
            if (_currentLobby.m_SteamID != callback.m_ulSteamIDLobby)
                return;

            var ownerId = SteamMatchmaking.GetLobbyOwner(_currentLobby).m_SteamID.ToString();
            var localId = SteamUser.GetSteamID().m_SteamID.ToString();
            var isOwner = localId == ownerId;

            var updatedLobbyUsers = GetLobbyUsers(_currentLobby);
            var updatedLobby = LobbyFactory.Create(
                SteamMatchmaking.GetLobbyData(_currentLobby, "Name"),
                _currentLobby.m_SteamID.ToString(),
                SteamMatchmaking.GetLobbyMemberLimit(_currentLobby),
                isOwner,
                updatedLobbyUsers,
                GetLobbyProperties(_currentLobby)
            );

            OnLobbyUpdated?.Invoke(updatedLobby);
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

        public Task<List<FriendUser>> GetFriendsAsync(LobbyManager.FriendFilter filter)
        {
            if (!_initialized) return null;

            var friends = new List<FriendUser>();
            int friendCount = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);

            for (int i = 0; i < friendCount; i++)
            {
                var steamID = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
                bool shouldAdd = filter switch
                {
                    LobbyManager.FriendFilter.InThisGame => SteamFriends.GetFriendGamePlayed(steamID, out FriendGameInfo_t gameInfo) &&
                                                            gameInfo.m_gameID.AppID() == SteamUtils.GetAppID(),
                    LobbyManager.FriendFilter.Online => SteamFriends.GetFriendPersonaState(steamID) == EPersonaState.k_EPersonaStateOnline,
                    LobbyManager.FriendFilter.All => true,
                    _ => false
                };

                if (shouldAdd)
                    friends.Add(CreateFriendUser(steamID));
            }

            return Task.FromResult(friends);
        }
        
        public Task SetIsReadyAsync(string userId, bool isReady)
        {
            if (!_initialized) 
                return Task.FromResult(Task.CompletedTask);

            var lobbyId = _currentLobby;
            var userExists = GetLobbyUsers(lobbyId).Any(user => user.Id == userId);
            if (!userExists)
            {
                PurrLogger.LogError($"User {userId} no longer exists in the lobby.");
                return Task.FromResult(Task.CompletedTask);
            }

            SteamMatchmaking.SetLobbyMemberData(lobbyId, "IsReady", isReady.ToString());

            SteamMatchmaking.SetLobbyData(lobbyId, "UpdateTrigger", DateTime.UtcNow.Ticks.ToString());
            return Task.FromResult(Task.CompletedTask);
        }
        
        public Task<List<LobbyUser>> GetLobbyMembersAsync()
        {
            if (!_initialized) return Task.FromResult(new List<LobbyUser>());
            return Task.FromResult<List<LobbyUser>>(GetLobbyUsers(SteamUser.GetSteamID()));
        }

        public Task InviteFriendAsync(FriendUser user)
        {
            if (!_initialized) 
                Task.FromResult(Task.CompletedTask);

            var steamID = new CSteamID(ulong.Parse(user.Id));
            //PurrLogger.Log($"Inviting: Steam ID: {steamID} | Friend ID: {user.Id} | Name: {user.DisplayName}");
            SteamMatchmaking.InviteUserToLobby(_currentLobby, steamID);
            return Task.FromResult(Task.CompletedTask);
        }
        
        private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
        {
            var lobbyId = callback.m_steamIDLobby;
            //PurrLogger.Log($"Invite accepted. Joining lobby {lobbyId.m_SteamID}");

            _ = JoinLobbyAsync(lobbyId.m_SteamID.ToString());
        }

        public async Task<Lobby> CreateLobbyAsync(int maxPlayers, Dictionary<string, string> lobbyProperties = null)
        {
            if (!_initialized)
                return default;

            var tcs = new TaskCompletionSource<bool>();
            CSteamID lobbyId = default;
            var lobbyName = $"{SteamFriends.GetPersonaName()}'s Lobby";

            void OnLobbyCreated(LobbyCreated_t result, bool ioFailure)
            {
                if (result.m_eResult == EResult.k_EResultOK)
                {
                    lobbyId = new CSteamID(result.m_ulSteamIDLobby);
                    tcs.TrySetResult(true);
                    SteamMatchmaking.SetLobbyData(lobbyId, "Name", lobbyName);
                    SteamMatchmaking.SetLobbyData(lobbyId, "Started", "False");
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
                return new Lobby { IsValid = false };

            _currentLobby = lobbyId;

            if (lobbyProperties != null)
            {
                foreach (var prop in lobbyProperties)
                {
                    SteamMatchmaking.SetLobbyData(lobbyId, prop.Key, prop.Value);
                }
            }

            return LobbyFactory.Create(
                lobbyName,
                lobbyId.m_SteamID.ToString(),
                maxPlayers,
                true,
                GetLobbyUsers(lobbyId),
                lobbyProperties
            );
        }
        
        public async Task<Lobby> JoinLobbyAsync(string lobbyId)
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
            var cLobbyId = new CSteamID(ulong.Parse(lobbyId));
            var handle = SteamMatchmaking.JoinLobby(cLobbyId);
            callResult.Set(handle);

            if (!await tcs.Task)
            {
                OnLobbyJoinFailed?.Invoke($"Failed to join lobby {lobbyId}.");
                return new Lobby { IsValid = false };
            }

            var lobby = LobbyFactory.Create(
                SteamMatchmaking.GetLobbyData(_currentLobby, "Name"),
                lobbyId,
                SteamMatchmaking.GetLobbyMemberLimit(_currentLobby),
                false,
                GetLobbyUsers(cLobbyId),
                GetLobbyProperties(_currentLobby)
            );

            OnLobbyUpdated?.Invoke(lobby);
            return lobby;
        }

        public Task LeaveLobbyAsync()
        {
            if (!_initialized || _currentLobby.m_SteamID == 0) return Task.CompletedTask;

            SteamMatchmaking.LeaveLobby(_currentLobby);
            _currentLobby = default;
            OnLobbyLeft?.Invoke();
            return Task.CompletedTask;
        }
        
        public Task LeaveLobbyAsync(string lobbyId)
        {
            if (!_initialized) return Task.CompletedTask;

            var cLobbyId = new CSteamID(ulong.Parse(lobbyId));
            SteamMatchmaking.LeaveLobby(cLobbyId);
            return Task.CompletedTask;
        }

        public async Task<List<Lobby>> SearchLobbiesAsync(int maxLobbiesToFind = 10, Dictionary<string, string> filters = null)
        {
            if (!_initialized)
                return new List<Lobby>();

            var tcs = new TaskCompletionSource<List<Lobby>>();
            var results = new List<Lobby>();

            if (filters != null)
            {
                foreach (var filter in filters)
                {
                    SteamMatchmaking.AddRequestLobbyListStringFilter(filter.Key, filter.Value, ELobbyComparison.k_ELobbyComparisonEqual);
                }
            }

            SteamMatchmaking.AddRequestLobbyListStringFilter("Started", "False", ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListResultCountFilter(maxLobbiesToFind);

            void OnLobbiesMatching(LobbyMatchList_t result, bool ioFailure)
            {
                int totalLobbies = (int)result.m_nLobbiesMatching;

                for (int i = 0; i < totalLobbies; i++)
                {
                    var lobbyId = SteamMatchmaking.GetLobbyByIndex(i);
                    var lobbyProperties = GetLobbyProperties(lobbyId);
                    int maxPlayers = SteamMatchmaking.GetLobbyMemberLimit(lobbyId);

                    results.Add(new Lobby
                    {
                        Name = SteamMatchmaking.GetLobbyData(lobbyId, "Name"),
                        IsValid = true,
                        lobbyId = lobbyId.m_SteamID.ToString(),
                        MaxPlayers = maxPlayers,
                        Properties = lobbyProperties,
                        Members = GetLobbyUsers(lobbyId)
                    });
                }

                tcs.TrySetResult(results);
            }

            var callResult = CallResult<LobbyMatchList_t>.Create(OnLobbiesMatching);
            callResult.Set(SteamMatchmaking.RequestLobbyList());

            return await tcs.Task;
        }
        
        public Task SetLobbyStartedAsync()
        {
            if (!_currentLobby.IsValid())
                return Task.FromResult(Task.CompletedTask);;
            
            SteamMatchmaking.SetLobbyData(_currentLobby, "Started", "True");
            return Task.FromResult(Task.CompletedTask);
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

        private FriendUser CreateFriendUser(CSteamID steamId)
        {
            var displayName = SteamFriends.GetFriendPersonaName(steamId);

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

            return new FriendUser()
            {
                Id = steamId.m_SteamID.ToString(),
                DisplayName = displayName,
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
            
            var updatedLobby = new Lobby
            {
                Name = SteamMatchmaking.GetLobbyData(_currentLobby, "Name"),
                IsValid = true,
                lobbyId = _currentLobby.m_SteamID.ToString(),
                MaxPlayers = SteamMatchmaking.GetLobbyMemberLimit(_currentLobby),
                Properties = new Dictionary<string, string>(), // Use existing properties if needed
                Members = updatedMembers
            };

            OnLobbyUpdated?.Invoke(updatedLobby);
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
            LeaveLobbyIfInLobby();
        }

        private void LeaveLobbyIfInLobby()
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