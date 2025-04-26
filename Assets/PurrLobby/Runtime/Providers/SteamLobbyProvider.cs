#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

#if STEAMWORKS_NET
#define STEAMWORKS_NET_PACKAGE
#endif

using PurrNet.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
#if STEAMWORKS_NET_PACKAGE && !DISABLESTEAMWORKS
using Steamworks;
#endif
using UnityEngine;
using UnityEngine.Events;

namespace PurrLobby.Providers
{
    public class SteamLobbyProvider : MonoBehaviour
#if STEAMWORKS_NET_PACKAGE && !DISABLESTEAMWORKS
        , ILobbyProvider
#endif
    {
        public enum LobbyType
        {
            Private,
            FriendsOnly,
            Public,
        }

#if STEAMWORKS_NET_PACKAGE && !DISABLESTEAMWORKS
        public LobbyType lobbyType = LobbyType.Public;
        public int maxLobbiesToFind = 10;

        public event UnityAction<string> OnLobbyJoinFailed;
        public event UnityAction OnLobbyLeft;
        public event UnityAction<Lobby> OnLobbyUpdated;
        public event UnityAction<List<LobbyUser>> OnLobbyPlayerListUpdated;
        public event UnityAction<List<FriendUser>> OnFriendListPulled;
        public event UnityAction<string> OnError;
        
        [SerializeField] private bool handleSteamInit = false;

        private Steamworks.CallResult<Steamworks.LobbyCreated_t> _LobbyCreated;
        private Steamworks.CallResult<Steamworks.LobbyEnter_t> _LobbyEnter;
        private Steamworks.CallResult<Steamworks.LobbyMatchList_t> _LobbyMatchList;

        private Steamworks.CSteamID _currentLobby = Steamworks.CSteamID.Nil;

#pragma warning disable IDE0052 // Remove unread private members
        private Steamworks.Callback<Steamworks.LobbyDataUpdate_t> _lobbyDataUpdateCallback;
        private Steamworks.Callback<Steamworks.AvatarImageLoaded_t> _avatarImageLoadedCallback;
        private Steamworks.Callback<Steamworks.LobbyChatUpdate_t> _lobbyChatUpdateCallback;
        private Steamworks.Callback<Steamworks.GameLobbyJoinRequested_t> _gameLobbyJoinRequestedCallback;
#pragma warning restore IDE0052 // Remove unread private members

        public bool IsSteamClientAvailable
        {
            get
            {
                try
                {
                    Steamworks.InteropHelp.TestIfAvailableClient();
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public async Task<Lobby> CreateLobbyAsync(int maxPlayers, Dictionary<string, string> lobbyProperties = null)
        {
            if (!IsSteamClientAvailable)
                return default;

            _LobbyCreated ??= Steamworks.CallResult<Steamworks.LobbyCreated_t>.Create();

            var tcs = new TaskCompletionSource<bool>();
            Steamworks.CSteamID lobbyId = Steamworks.CSteamID.Nil;
            var lobbyName = $"{Steamworks.SteamFriends.GetPersonaName()}'s Lobby";

            var handle = Steamworks.SteamMatchmaking.CreateLobby((Steamworks.ELobbyType)lobbyType, maxPlayers);
            _LobbyCreated.Set(handle, (result, ioError) =>
            {
                if(!ioError && result.m_eResult == Steamworks.EResult.k_EResultOK)
                {
                    lobbyId = new Steamworks.CSteamID(result.m_ulSteamIDLobby);
                    tcs.TrySetResult(true);
                    Steamworks.SteamMatchmaking.SetLobbyData(lobbyId, "Name", lobbyName);
                    Steamworks.SteamMatchmaking.SetLobbyData(lobbyId, "Started", "False");
                }
                else
                    tcs.TrySetResult(false);
            });

            if (!await tcs.Task)
                return new Lobby { IsValid = false };

            _currentLobby = lobbyId;

            if (lobbyProperties != null)
            {
                foreach (var prop in lobbyProperties)
                {
                    Steamworks.SteamMatchmaking.SetLobbyData(lobbyId, prop.Key, prop.Value);
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

        public Task<List<FriendUser>> GetFriendsAsync(LobbyManager.FriendFilter filter)
        {
            if (!IsSteamClientAvailable)
                return default;

            var friends = new List<FriendUser>();
            int friendCount = Steamworks.SteamFriends.GetFriendCount(Steamworks.EFriendFlags.k_EFriendFlagImmediate);

            for (int i = 0; i < friendCount; i++)
            {
                var steamID = Steamworks.SteamFriends.GetFriendByIndex(i, Steamworks.EFriendFlags.k_EFriendFlagImmediate);
                bool shouldAdd = filter switch
                {
                    LobbyManager.FriendFilter.InThisGame => Steamworks.SteamFriends.GetFriendGamePlayed(steamID, out Steamworks.FriendGameInfo_t gameInfo) &&
                                                            gameInfo.m_gameID.AppID() == Steamworks.SteamUtils.GetAppID(),
                    LobbyManager.FriendFilter.Online => Steamworks.SteamFriends.GetFriendPersonaState(steamID) == Steamworks.EPersonaState.k_EPersonaStateOnline,
                    LobbyManager.FriendFilter.All => true,
                    _ => false
                };

                if (shouldAdd)
                    friends.Add(CreateFriendUser(steamID));
            }

            return Task.FromResult(friends);
        }

        public Task<string> GetLobbyDataAsync(string key)
        {
            if (!IsSteamClientAvailable)
                return Task.FromResult(string.Empty);

            return Task.FromResult(Steamworks.SteamMatchmaking.GetLobbyData(_currentLobby, key));
        }

        public Task<List<LobbyUser>> GetLobbyMembersAsync()
        {
            if (!IsSteamClientAvailable)
                return Task.FromResult(new List<LobbyUser>());

            return Task.FromResult(GetLobbyUsers(Steamworks.SteamUser.GetSteamID()));
        }

        public Task<string> GetLocalUserIdAsync()
        {
            if (!IsSteamClientAvailable)
                return Task.FromResult(string.Empty);

            return Task.FromResult(Steamworks.SteamUser.GetSteamID().m_SteamID.ToString());
        }

        public Task InitializeAsync()
        {
            _avatarImageLoadedCallback = Steamworks.Callback<Steamworks.AvatarImageLoaded_t>.Create(OnAvatarImageLoaded);
            _lobbyDataUpdateCallback = Steamworks.Callback<Steamworks.LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);
            _lobbyChatUpdateCallback = Steamworks.Callback<Steamworks.LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
            _gameLobbyJoinRequestedCallback = Steamworks.Callback<Steamworks.GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);

            if (handleSteamInit)
                HandleSteamInit();
            
            return Task.CompletedTask;
        }

        private void HandleSteamInit()
        {
            if (handleSteamInit)
            {
                if (!SteamAPI.Init())
                {
                    PurrLogger.LogError("SteamAPI initialization failed.");
                    OnError?.Invoke("SteamAPI initialization failed.");
                    return;
                }
                RunSteamCallbacks();
            }
        }
        
        private async void RunSteamCallbacks()
        {
            var runCallbacks = true;
            while (runCallbacks)
            {
                SteamAPI.RunCallbacks();
                await Task.Delay(16);
            }
        }

        public Task InviteFriendAsync(FriendUser user)
        {
            if (IsSteamClientAvailable && !string.IsNullOrEmpty(user.Id) && ulong.TryParse(user.Id, out var id))
            {
                var steamID = new Steamworks.CSteamID(id);
                Steamworks.SteamMatchmaking.InviteUserToLobby(_currentLobby, steamID);
            }

            return Task.FromResult(Task.CompletedTask);
        }

        public async Task<Lobby> JoinLobbyAsync(string lobbyId)
        {
            if (!IsSteamClientAvailable || string.IsNullOrEmpty(lobbyId))
                return default;

            _LobbyEnter ??= Steamworks.CallResult<Steamworks.LobbyEnter_t>.Create();

            var tcs = new TaskCompletionSource<bool>();
            var cLobbyId = new Steamworks.CSteamID(ulong.Parse(lobbyId));
            var handle = Steamworks.SteamMatchmaking.JoinLobby(cLobbyId);

            _LobbyEnter.Set(handle, (result, ioError) =>
            {
                if (result.m_EChatRoomEnterResponse == (uint)Steamworks.EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
                {
                    _currentLobby = new Steamworks.CSteamID(result.m_ulSteamIDLobby);
                    tcs.TrySetResult(true);
                }
                else
                {
                    tcs.TrySetResult(false);
                }
            });

            if (!await tcs.Task)
            {
                OnLobbyJoinFailed?.Invoke($"Failed to join lobby {lobbyId}.");
                return new Lobby { IsValid = false };
            }

            var lobby = LobbyFactory.Create(
                Steamworks.SteamMatchmaking.GetLobbyData(_currentLobby, "Name"),
                lobbyId,
                Steamworks.SteamMatchmaking.GetLobbyMemberLimit(_currentLobby),
                false,
                GetLobbyUsers(cLobbyId),
                GetLobbyProperties(_currentLobby)
            );

            OnLobbyUpdated?.Invoke(lobby);
            return lobby;
        }

        public Task LeaveLobbyAsync()
        {
            if (!IsSteamClientAvailable || _currentLobby == Steamworks.CSteamID.Nil) 
                return Task.CompletedTask;

            Steamworks.SteamMatchmaking.LeaveLobby(_currentLobby);
            _currentLobby = default;
            OnLobbyLeft?.Invoke();
            return Task.CompletedTask;
        }

        public Task LeaveLobbyAsync(string lobbyId)
        {
            if (IsSteamClientAvailable && !string.IsNullOrEmpty(lobbyId) && ulong.TryParse(lobbyId, out var id))
            {
                var cLobbyId = new Steamworks.CSteamID(ulong.Parse(lobbyId));
                Steamworks.SteamMatchmaking.LeaveLobby(cLobbyId);
            }

            return Task.CompletedTask;
        }

        public async Task<List<Lobby>> SearchLobbiesAsync(int maxRoomsToFind = 10, Dictionary<string, string> filters = null)
        {
            if (!IsSteamClientAvailable)
                return new List<Lobby>();

            var tcs = new TaskCompletionSource<List<Lobby>>();
            var results = new List<Lobby>();

            if (filters != null)
            {
                foreach (var filter in filters)
                {
                    Steamworks.SteamMatchmaking.AddRequestLobbyListStringFilter(filter.Key, filter.Value, Steamworks.ELobbyComparison.k_ELobbyComparisonEqual);
                }
            }

            Steamworks.SteamMatchmaking.AddRequestLobbyListStringFilter("Started", "False", Steamworks.ELobbyComparison.k_ELobbyComparisonEqual);
            Steamworks.SteamMatchmaking.AddRequestLobbyListResultCountFilter(maxLobbiesToFind);

            _LobbyMatchList ??= Steamworks.CallResult<Steamworks.LobbyMatchList_t>.Create();
            _LobbyMatchList.Set(Steamworks.SteamMatchmaking.RequestLobbyList(), (result, ioError) =>
            {
                int totalLobbies = (int)result.m_nLobbiesMatching;

                for (int i = 0; i < totalLobbies; i++)
                {
                    var lobbyId = Steamworks.SteamMatchmaking.GetLobbyByIndex(i);
                    var lobbyProperties = GetLobbyProperties(lobbyId);
                    int maxPlayers = Steamworks.SteamMatchmaking.GetLobbyMemberLimit(lobbyId);

                    results.Add(new Lobby
                    {
                        Name = Steamworks.SteamMatchmaking.GetLobbyData(lobbyId, "Name"),
                        IsValid = true,
                        LobbyId = lobbyId.m_SteamID.ToString(),
                        MaxPlayers = maxPlayers,
                        Properties = lobbyProperties,
                        Members = GetLobbyUsers(lobbyId)
                    });
                }

                tcs.TrySetResult(results);
            });

            return await tcs.Task;
        }

        public Task SetIsReadyAsync(string userId, bool isReady)
        {
            //You can only set the ready state for your own user
            if (IsSteamClientAvailable && !string.IsNullOrEmpty(userId) && ulong.TryParse(userId, out var id)
                && Steamworks.SteamUser.GetSteamID().m_SteamID == id)
            {
                Steamworks.SteamMatchmaking.SetLobbyMemberData(_currentLobby, "IsReady", isReady.ToString());
                Steamworks.SteamMatchmaking.SetLobbyData(_currentLobby, "UpdateTrigger", DateTime.UtcNow.Ticks.ToString());
            }

            return Task.FromResult(Task.CompletedTask);
        }

        public Task SetLobbyDataAsync(string key, string value)
        {
            if (IsSteamClientAvailable)
                Steamworks.SteamMatchmaking.SetLobbyData(_currentLobby, key, value);

            return Task.FromResult(Task.CompletedTask);
        }

        public Task SetLobbyStartedAsync()
        {
            if (IsSteamClientAvailable)
            {
                Steamworks.SteamMatchmaking.SetLobbyGameServer(_currentLobby, 0, 0, Steamworks.SteamUser.GetSteamID());
                Steamworks.SteamMatchmaking.SetLobbyData(_currentLobby, "Started", "True");
            }

            return Task.FromResult(Task.CompletedTask);
        }

        public void SetLobbyStarted(Steamworks.CSteamID serverId)
        {
            if (IsSteamClientAvailable)
            {
                Steamworks.SteamMatchmaking.SetLobbyGameServer(_currentLobby, 0, 0, serverId);
                Steamworks.SteamMatchmaking.SetLobbyData(_currentLobby, "Started", "True");
            }
        }

        public void SetLobbyStarted(string address, short port)
        {
            if (IsSteamClientAvailable)
            {
                var ipAddress = System.Net.IPAddress.Parse(address);
                var ipBytes = ipAddress.GetAddressBytes();
                var ip = (uint)ipBytes[0] << 24;
                ip += (uint)ipBytes[1] << 16;
                ip += (uint)ipBytes[2] << 8;
                ip += (uint)ipBytes[3];
                Steamworks.SteamMatchmaking.SetLobbyGameServer(_currentLobby, ip, (ushort)port, Steamworks.CSteamID.Nil);
                Steamworks.SteamMatchmaking.SetLobbyData(_currentLobby, "Started", "True");
            }
        }

        public void SetLobbyStarted(string address, short port, Steamworks.CSteamID serverId)
        {
            if (IsSteamClientAvailable)
            {
                var ipAddress = System.Net.IPAddress.Parse(address);
                var ipBytes = ipAddress.GetAddressBytes();
                var ip = (uint)ipBytes[0] << 24;
                ip += (uint)ipBytes[1] << 16;
                ip += (uint)ipBytes[2] << 8;
                ip += (uint)ipBytes[3];
                Steamworks.SteamMatchmaking.SetLobbyGameServer(_currentLobby, ip, (ushort)port, Steamworks.CSteamID.Nil);
                Steamworks.SteamMatchmaking.SetLobbyData(_currentLobby, "Started", "True");
            }
        }

        public void Shutdown()
        {
            //Not needed
        }

        private List<LobbyUser> GetLobbyUsers(Steamworks.CSteamID lobbyId)
        {
            var users = new List<LobbyUser>();
            int memberCount = Steamworks.SteamMatchmaking.GetNumLobbyMembers(lobbyId);

            for (int i = 0; i < memberCount; i++)
            {
                var steamId = Steamworks.SteamMatchmaking.GetLobbyMemberByIndex(lobbyId, i);
                users.Add(CreateLobbyUser(steamId, lobbyId));
            }

            return users;
        }

        private LobbyUser CreateLobbyUser(Steamworks.CSteamID steamId, Steamworks.CSteamID lobbyId)
        {
            _avatarImageLoadedCallback ??= Steamworks.Callback<Steamworks.AvatarImageLoaded_t>.Create(OnAvatarImageLoaded);

            var displayName = Steamworks.SteamFriends.GetFriendPersonaName(steamId);
            var isReadyString = Steamworks.SteamMatchmaking.GetLobbyMemberData(lobbyId, steamId, "IsReady");
            var isReady = !string.IsNullOrEmpty(isReadyString) && isReadyString == "True";

            var avatarHandle = Steamworks.SteamFriends.GetLargeFriendAvatar(steamId);
            Texture2D avatar = null;

            if (avatarHandle != -1 && Steamworks.SteamUtils.GetImageSize(avatarHandle, out uint width, out uint height))
            {
                byte[] imageBuffer = new byte[width * height * 4];
                if (Steamworks.SteamUtils.GetImageRGBA(avatarHandle, imageBuffer, imageBuffer.Length))
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

        private FriendUser CreateFriendUser(Steamworks.CSteamID steamId)
        {
            var displayName = Steamworks.SteamFriends.GetFriendPersonaName(steamId);

            var avatarHandle = Steamworks.SteamFriends.GetLargeFriendAvatar(steamId);
            Texture2D avatar = null;

            if (avatarHandle != -1 && Steamworks.SteamUtils.GetImageSize(avatarHandle, out uint width, out uint height))
            {
                byte[] imageBuffer = new byte[width * height * 4];
                if (Steamworks.SteamUtils.GetImageRGBA(avatarHandle, imageBuffer, imageBuffer.Length))
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

        private void OnAvatarImageLoaded(Steamworks.AvatarImageLoaded_t callback)
        {
            var steamId = callback.m_steamID;
            if (callback.m_iImage == -1)
            {
                PurrLogger.LogWarning($"Failed to load avatar for user {steamId}");
                return;
            }

            if (Steamworks.SteamUtils.GetImageSize(callback.m_iImage, out uint width, out uint height))
            {
                byte[] imageBuffer = new byte[width * height * 4];
                if (Steamworks.SteamUtils.GetImageRGBA(callback.m_iImage, imageBuffer, imageBuffer.Length))
                {
                    Texture2D avatar = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false);
                    avatar.LoadRawTextureData(imageBuffer);
                    FlipTextureVertically(avatar);
                    avatar.Apply();

                    UpdateUserAvatar(steamId, avatar);
                }
            }
        }

        private void UpdateUserAvatar(Steamworks.CSteamID steamId, Texture2D avatar)
        {
            if (!_currentLobby.IsValid())
                return;
            var updatedMembers = GetLobbyUsers(_currentLobby);
            if (updatedMembers == null || updatedMembers.Count <= 0)
                return;

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
                Name = Steamworks.SteamMatchmaking.GetLobbyData(_currentLobby, "Name"),
                IsValid = true,
                LobbyId = _currentLobby.m_SteamID.ToString(),
                MaxPlayers = Steamworks.SteamMatchmaking.GetLobbyMemberLimit(_currentLobby),
                Properties = new Dictionary<string, string>(), // Use existing properties if needed
                Members = updatedMembers
            };

            OnLobbyUpdated?.Invoke(updatedLobby);
        }

        private void OnLobbyDataUpdate(Steamworks.LobbyDataUpdate_t callback)
        {
            if (_currentLobby.m_SteamID != callback.m_ulSteamIDLobby)
                return;

            var ownerId = Steamworks.SteamMatchmaking.GetLobbyOwner(_currentLobby).m_SteamID.ToString();
            var localId = Steamworks.SteamUser.GetSteamID().m_SteamID.ToString();
            var isOwner = localId == ownerId;

            var updatedLobbyUsers = GetLobbyUsers(_currentLobby);
            var updatedLobby = LobbyFactory.Create(
                Steamworks.SteamMatchmaking.GetLobbyData(_currentLobby, "Name"),
                _currentLobby.m_SteamID.ToString(),
                Steamworks.SteamMatchmaking.GetLobbyMemberLimit(_currentLobby),
                isOwner,
                updatedLobbyUsers,
                GetLobbyProperties(_currentLobby)
            );

            OnLobbyUpdated?.Invoke(updatedLobby);
        }

        private Dictionary<string, string> GetLobbyProperties(Steamworks.CSteamID lobbyId)
        {
            var properties = new Dictionary<string, string>();
            int propertyCount = Steamworks.SteamMatchmaking.GetLobbyDataCount(lobbyId);

            for (int i = 0; i < propertyCount; i++)
            {
                string key = string.Empty;
                string value = string.Empty;
                int keySize = 256;
                int valueSize = 256;

                bool success = Steamworks.SteamMatchmaking.GetLobbyDataByIndex(
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

        private void OnLobbyChatUpdate(Steamworks.LobbyChatUpdate_t callback)
        {
            if (_currentLobby.m_SteamID != callback.m_ulSteamIDLobby)
                return;

            var stateChange = (Steamworks.EChatMemberStateChange)callback.m_rgfChatMemberStateChange;

            if (stateChange.HasFlag(Steamworks.EChatMemberStateChange.k_EChatMemberStateChangeEntered))
            {
                //PurrLogger.Log($"User {callback.m_ulSteamIDUserChanged} joined the lobby.");
            }

            if (stateChange.HasFlag(Steamworks.EChatMemberStateChange.k_EChatMemberStateChangeLeft) ||
                stateChange.HasFlag(Steamworks.EChatMemberStateChange.k_EChatMemberStateChangeDisconnected))
            {
                //PurrLogger.Log($"User {callback.m_ulSteamIDUserChanged} left the lobby.");
            }

            var ownerId = Steamworks.SteamMatchmaking.GetLobbyOwner(_currentLobby).m_SteamID.ToString();
            var localId = Steamworks.SteamUser.GetSteamID().m_SteamID.ToString();
            var isOwner = localId == ownerId;

            var data = Steamworks.SteamMatchmaking.GetLobbyData(_currentLobby, "Name");
            var properties = GetLobbyProperties(_currentLobby);
            var updatedLobbyUsers = GetLobbyUsers(_currentLobby);

            var updatedLobby = LobbyFactory.Create(
                data,
                _currentLobby.m_SteamID.ToString(),
                Steamworks.SteamMatchmaking.GetLobbyMemberLimit(_currentLobby),
                isOwner,
                updatedLobbyUsers,
                properties
            );

            OnLobbyUpdated?.Invoke(updatedLobby);
        }

        private void OnGameLobbyJoinRequested(Steamworks.GameLobbyJoinRequested_t callback)
        {
            var lobbyId = callback.m_steamIDLobby;
            //PurrLogger.Log($"Invite accepted. Joining lobby {lobbyId.m_SteamID}");

            _ = JoinLobbyAsync(lobbyId.m_SteamID.ToString());
        }

        public Task SetAllReadyAsync()
        {
            return Task.FromResult(Task.CompletedTask);
        }
#endif
    }
}
