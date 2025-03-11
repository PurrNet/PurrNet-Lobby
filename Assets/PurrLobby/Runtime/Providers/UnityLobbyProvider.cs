
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using PurrNet.Logging;
using PurrNet.Utils;

#if UTP_AUTH
using Unity.Services.Authentication;
using Unity.Services.Core;
#endif

#if UTP_LOBBYRELAY
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
#endif

namespace PurrLobby.Providers {
    public class UnityLobbyProvider : MonoBehaviour
#if UTP_LOBBYRELAY && UTP_AUTH
        , ILobbyProvider
#endif
        {
        public enum LobbyType {
            Private,
            Public,
        }

#if UTP_LOBBYRELAY && UTP_AUTH
        public event UnityAction<string> OnLobbyJoinFailed;
        public event UnityAction OnLobbyLeft;
        public event UnityAction<Lobby> OnLobbyUpdated;
        public event UnityAction<List<LobbyUser>> OnLobbyPlayerListUpdated;
        public event UnityAction<string> OnError;
#pragma warning disable CS0067
        public event UnityAction<List<FriendUser>> OnFriendListPulled;
#pragma warning restore CS0067

        [Header("Lobby")]
        public string lobbyName = "New Lobby";
        [Tooltip("Only public lobbies will display in lobby search results, a private lobby requires the host to share a lobby code.")]
        public LobbyType lobbyType = LobbyType.Public;
        [Tooltip("Optional password to require for joining a lobby, must be at least 8 characters in length.")]
        public string lobbyPassword = "";
        public int maxLobbiesToFind = 10;
        public string playerName = "Player";

        [Header("Relay")]
        [Tooltip("Use the Unity Relay Service for connection. Disable this if you wish to manually manage Relay Server allocation or are using a P2P connection.")]
        public bool UseUnityRelayService = true;
        [Tooltip("Relay Region ID to connect to, leave empty to automatically select most optimal.")]
        public string RegionId = "";
        [Tooltip("Join code assigned when starting host. A client must set this prior to connecting.")]
        public string RelayJoinCode = "";
        [Tooltip("Timeout in milliseconds for joining relay.")]
        public int RelayTimeout = 10000;

        private Allocation RelayServerAllocation;

        public bool IsUnityServiceAvailable {
            get { return UnityServices.State != ServicesInitializationState.Uninitialized && AuthenticationService.Instance.IsSignedIn; }
        }

        public string LocalPlayerId { get { return IsUnityServiceAvailable ? AuthenticationService.Instance.PlayerId : string.Empty; } }
        public Task<string> GetLocalUserIdAsync() => Task.FromResult(LocalPlayerId);
        private Player _localPlayer;
        public Player LocalPlayer {
            get {
                if(_localPlayer == null && CurrentLobby != null) {
                    _localPlayer = CurrentLobby.Players.Find(x => x.Id == LocalPlayerId);
                }
                return _localPlayer;
            }
        }
        public bool IsLocalPlayerHost { get { return CurrentLobby != null && LocalPlayerId == CurrentLobby.HostId; } }

        private Unity.Services.Lobbies.Models.Lobby CurrentLobby = null;

        private LobbyEventCallbacks LobbyEventCallbacks;

        public async Task InitializeAsync() {
            try {
                if(UnityServices.State == ServicesInitializationState.Uninitialized) {
                    //Must initialize with different profiles when connecting multiple clients
                    //Same as AuthenticationService.Instance.SwitchProfile
                    InitializationOptions options = new InitializationOptions();
                    if(ApplicationContext.isClone) {
                        options.SetProfile($"{UnityEngine.Random.Range(1, 10000)}");
                    }
                    await UnityServices.InitializeAsync(options);
                }

                if(!AuthenticationService.Instance.IsSignedIn) {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }
            } catch {
                OnError?.Invoke("UnityServices initialization failed.");
                return;
            }
        }

        private async Task SubscribeLobbyEventsAsync() {
            try {
                UnsubscribeLobbyEvents();

                LobbyEventCallbacks = new LobbyEventCallbacks();

                LobbyEventCallbacks.LobbyChanged += LobbyEventCallbacks_LobbyChanged;

                await LobbyService.Instance.SubscribeToLobbyEventsAsync(CurrentLobby.Id, LobbyEventCallbacks);
            } catch(Exception ex) {
                PurrLogger.LogError($"Failed to subscribe to callback events: {ex}");
            }
        }

        private void UnsubscribeLobbyEvents() {
            if(LobbyEventCallbacks == null) { return; }

            LobbyEventCallbacks.LobbyChanged -= LobbyEventCallbacks_LobbyChanged;

            LobbyEventCallbacks = null;
        }

        private void LobbyEventCallbacks_LobbyChanged(ILobbyChanges obj) {
            if(CurrentLobby == null) { return; }
            
            obj.ApplyToLobby(CurrentLobby);

            OnLobbyUpdate();
        }

        private Lobby OnLobbyUpdate() {
            if(!IsUnityServiceAvailable || CurrentLobby == null) { return default; }

            var updatedLobby = LobbyFactory.Create(
                CurrentLobby.Name,
                CurrentLobby.Id,
                CurrentLobby.LobbyCode,
                CurrentLobby.MaxPlayers,
                IsLocalPlayerHost,
                GetLobbyUsers(CurrentLobby),
                GetLobbyProperties(CurrentLobby),
                RelayServerAllocation
            );

            OnLobbyUpdated?.Invoke(updatedLobby);
            OnLobbyPlayerListUpdated?.Invoke(updatedLobby.Members);

            return updatedLobby;
        }

        public async Task<Lobby> CreateLobbyAsync(int maxPlayers, Dictionary<string, string> lobbyProperties = null) {
            try {
                if(!IsUnityServiceAvailable) { return default; }

                Dictionary<string, DataObject> lobbyData = new Dictionary<string, DataObject>() {
                    { "JoinCode", new DataObject(DataObject.VisibilityOptions.Member, "", 0) }
                };
                foreach(var prop in lobbyProperties) {
                    lobbyData.Add(prop.Key, new DataObject(DataObject.VisibilityOptions.Public, prop.Value, 0));
                }

                CurrentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, new CreateLobbyOptions() {
                    IsPrivate = lobbyType == LobbyType.Private,
                    Password = string.IsNullOrWhiteSpace(lobbyPassword) || lobbyType != LobbyType.Private ? null : lobbyPassword,
                    Data = lobbyData
                });

                await SubscribeLobbyEventsAsync();

                await InitializeLocalPlayerData();

                return OnLobbyUpdate();
            } catch(Exception ex) {
                PurrLogger.LogError($"Failed to Create Lobby: {ex}");
                return new Lobby { IsValid = false };
            }
        }

        public async Task<Lobby> JoinLobbyAsync(string lobbyId) {
            if(!IsUnityServiceAvailable || string.IsNullOrEmpty(lobbyId)) { return default; }

            try {
                CurrentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId, new JoinLobbyByIdOptions() {
                    Password = string.IsNullOrWhiteSpace(lobbyPassword) ? null : lobbyPassword
                });

                await SubscribeLobbyEventsAsync();

                await InitializeLocalPlayerData();

                return OnLobbyUpdate();
            } catch(Exception ex) {
                PurrLogger.LogError($"Failed to Join Lobby with ID '{lobbyId}': {ex}");
                OnLobbyJoinFailed?.Invoke($"Failed to Join Lobby with ID '{lobbyId}'");
                return new Lobby { IsValid = false };
            }
        }

        public async Task<Lobby> JoinLobbyByCodeAsync(string lobbyCode) {
            if(!IsUnityServiceAvailable || string.IsNullOrEmpty(lobbyCode)) { return default; }

            try {
                CurrentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode, new JoinLobbyByCodeOptions() {
                    Password = string.IsNullOrWhiteSpace(lobbyPassword) ? null : lobbyPassword
                });

                await SubscribeLobbyEventsAsync();

                await InitializeLocalPlayerData();

                return OnLobbyUpdate();
            } catch(Exception ex) {
                PurrLogger.LogError($"Failed to Join Lobby with Code '{lobbyCode}': {ex}");
                OnLobbyJoinFailed?.Invoke($"Failed to Join Lobby with Code '{lobbyCode}'");
                return new Lobby { IsValid = false };
            }
        }

        public async Task InitializeLocalPlayerData() {
            LocalPlayer.Data = new Dictionary<string, PlayerDataObject>() {
                { "Name", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, playerName) },
                { "IsReady", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, "False") }
            };

            await UpdatePlayerDataAsync();
        }

        public List<LobbyUser> GetLobbyUsers(Unity.Services.Lobbies.Models.Lobby lobby) {
            var users = new List<LobbyUser>();

            foreach(var player in lobby.Players) {
                try {
                    users.Add(new LobbyUser {
                        Id = player.Id,
                        DisplayName = player.Data["Name"]?.Value,
                        IsReady = player.Data["IsReady"]?.Value == "True",
                        Avatar = null
                    });
                } catch { } //player dataobject can throw
            }

            return users;
        }
        public Task<List<LobbyUser>> GetLobbyMembersAsync() {
            if(!IsUnityServiceAvailable || CurrentLobby == null) { return Task.FromResult(new List<LobbyUser>()); }

            return Task.FromResult(GetLobbyUsers(CurrentLobby));
        }

        public Task<string> GetLobbyDataAsync(string key) {
            if(!IsUnityServiceAvailable || CurrentLobby == null) { return Task.FromResult(string.Empty); }

            if(CurrentLobby.Data.TryGetValue(key, out var dataObject)) {
                return Task.FromResult(dataObject.Value);
            }

            PurrLogger.LogError($"Failed to Get '{key}' DataObject for Lobby");
            return Task.FromResult(string.Empty);
        }

        public Dictionary<string, string> GetLobbyProperties(Unity.Services.Lobbies.Models.Lobby lobby) {
            var properties = new Dictionary<string, string>();

            if(lobby.Data != null) {
                foreach(var prop in lobby.Data) {
                    properties[prop.Key] = prop.Value.Value;
                }
            }

            return properties;
        }

        public async Task LeaveLobbyAsync() {
            if(!IsUnityServiceAvailable || CurrentLobby == null) { return; }

            await LeaveLobbyAsync(CurrentLobby.Id);
        }

        public async Task LeaveLobbyAsync(string lobbyId) {
            if(!IsUnityServiceAvailable || string.IsNullOrEmpty(lobbyId)) { return; }

            try {
                //bool canDelete = _currentLobby.HostId == localUserId || _currentLobby.Players.Count == 1;
                await LobbyService.Instance.RemovePlayerAsync(lobbyId, LocalPlayerId);

                //maybe not necessary
                //if(canDelete) {
                //    await DeleteLobbyAsync();
                //}

                RelayServerAllocation = null;
                RelayJoinCode = "";
                UnsubscribeLobbyEvents();
                CurrentLobby = null;
                _localPlayer = null;
                OnLobbyLeft?.Invoke();
            } catch(Exception ex) {
                PurrLogger.LogError($"Failed to Leave Lobby '{lobbyId}': {ex}");
            }
        }

        public async Task DeleteLobbyAsync(bool requestedByHost) {
            if(!IsUnityServiceAvailable || CurrentLobby == null) { return; }

            if(CurrentLobby.Players.Count <= 1 || (requestedByHost && IsLocalPlayerHost)) {
                try {
                    await LobbyService.Instance.DeleteLobbyAsync(CurrentLobby.Id);

                    RelayServerAllocation = null;
                    RelayJoinCode = "";
                    CurrentLobby = null;
                } catch(Exception ex) {
                    PurrLogger.LogError($"Failed to Delete Lobby: {ex}");
                }
            }
        }

        public async Task<List<Lobby>> SearchLobbiesAsync(int maxRoomsToFind = 10, Dictionary<string, string> filters = null) {
            if(!IsUnityServiceAvailable) { return new List<Lobby>(); }

            var results = new List<Lobby>();
            try {
                List<QueryFilter> queryFilters = new List<QueryFilter>() {
                    new QueryFilter(QueryFilter.FieldOptions.IsLocked, "False", QueryFilter.OpOptions.EQ)
                };

                foreach(var filter in filters) {
                    switch(filter.Key.ToLower()) {
                        case "name":
                            queryFilters.Add(new QueryFilter(QueryFilter.FieldOptions.Name, filter.Value, QueryFilter.OpOptions.CONTAINS));
                            break;
                        case "maxplayers":
                            queryFilters.Add(new QueryFilter(QueryFilter.FieldOptions.MaxPlayers, filter.Value, QueryFilter.OpOptions.GE));
                            break;
                        case "availableslots":
                            queryFilters.Add(new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, filter.Value, QueryFilter.OpOptions.GE));
                            break;
                        case "haspassword":
                            queryFilters.Add(new QueryFilter(QueryFilter.FieldOptions.HasPassword, filter.Value, QueryFilter.OpOptions.EQ));
                            break;
                    }
                }

                var response = await LobbyService.Instance.QueryLobbiesAsync(new QueryLobbiesOptions() {
                    Count = maxRoomsToFind,
                    Filters = queryFilters
                });

                foreach(var lobby in response.Results) {
                    results.Add(new Lobby {
                        Name = lobby.Name,
                        IsValid = true,
                        LobbyId = lobby.Id,
                        LobbyCode = lobby.LobbyCode,
                        MaxPlayers = lobby.MaxPlayers,
                        Properties = GetLobbyProperties(lobby),
                        Members = GetLobbyUsers(lobby)
                    });
                }
            } catch(Exception ex) {
                Debug.LogException(ex);
            }

            return results;
        }

        public async Task SetLobbyDataAsync(string key, string value) {
            if(!IsUnityServiceAvailable || CurrentLobby == null) { return; }

            if(CurrentLobby.Data.TryGetValue(key, out DataObject dataObject)) {
                try {
                    CurrentLobby.Data[key] = new DataObject(dataObject.Visibility, value, dataObject.Index);

                    await LobbyService.Instance.UpdateLobbyAsync(CurrentLobby.Id, new UpdateLobbyOptions() {
                        Data = CurrentLobby.Data
                    });
                } catch(Exception ex) {
                    PurrLogger.LogError($"Failed to Update '{key}' Property: {ex.Message}");
                }
            } else {
                await SetLobbyDataNonIndexedAsync(key, value);
            }
        }

        /// <summary>
        /// Add/Update a Lobby property which will not be indexed for search query results.
        /// </summary>
        public async Task SetLobbyDataNonIndexedAsync(string key, string value, DataObject.VisibilityOptions visibility = DataObject.VisibilityOptions.Public) {
            if(!IsUnityServiceAvailable || CurrentLobby == null) { return; }

            try {
                CurrentLobby.Data[key] = new DataObject(visibility, value, 0);

                await LobbyService.Instance.UpdateLobbyAsync(CurrentLobby.Id, new UpdateLobbyOptions() {
                    Data = CurrentLobby.Data
                });
            } catch(Exception ex) {
                PurrLogger.LogError($"Failed to Add/Update '{key}' NonIndexed Property: {ex.Message}");
            }
        }

        public DataObject.IndexOptions GetNextAvailableStringIndex(string key = "", DataObject.IndexOptions desiredIndex = 0) {
            var usedIndices = new HashSet<DataObject.IndexOptions>();

            foreach(var obj in CurrentLobby.Data.Values) {
                if(obj.Index != 0) {
                    usedIndices.Add(obj.Index);
                }
            }

            if(desiredIndex != 0 && desiredIndex < DataObject.IndexOptions.N1 && !usedIndices.Contains(desiredIndex)) {
                return desiredIndex;
            } else if(desiredIndex != 0) {
                PurrLogger.LogWarning($"Desired String Index already exists or was invalid. '{key}' will instead use the next available string index.");
            }

            foreach(DataObject.IndexOptions index in Enum.GetValues(typeof(DataObject.IndexOptions))) {
                if(index != 0 && index < DataObject.IndexOptions.N1 && !usedIndices.Contains(index)) {
                    return index;
                }
            }

            PurrLogger.LogWarning($"Max String Indexed Properties reached. '{key}' will not be indexed for search queries.");
            return 0;
        }
        
        public DataObject.IndexOptions GetNextAvailableNumericIndex(string key = "", DataObject.IndexOptions desiredIndex = 0) {
            var usedIndices = new HashSet<DataObject.IndexOptions>();

            foreach(var obj in CurrentLobby.Data.Values) {
                if(obj.Index != 0) {
                    usedIndices.Add(obj.Index);
                }
            }

            if(desiredIndex != 0 && desiredIndex > DataObject.IndexOptions.S5 && !usedIndices.Contains(desiredIndex)) {
                return desiredIndex;
            } else if(desiredIndex != 0) {
                PurrLogger.LogWarning($"Desired Numeric Index already exists or was invalid. '{key}' will instead use the next available numeric index.");
            }

            foreach(DataObject.IndexOptions index in Enum.GetValues(typeof(DataObject.IndexOptions))) {
                if(index > DataObject.IndexOptions.S5 && !usedIndices.Contains(index)) {
                    return index;
                }
            }

            PurrLogger.LogWarning($"Max Numeric Indexed Properties reached. '{key}' will not be indexed for search queries.");
            return 0;
        }

        /// <summary>
        /// Add/Update a Lobby property which will be indexed as a string value for search query results. IndexOption must be unique, unset IndexOption to automatically populate with next available index. If the given Key already exists, the IndexOption will be unchanged.
        /// </summary>
        public async Task SetLobbyDataStringIndexedAsync(string key, string value, DataObject.VisibilityOptions visibility = DataObject.VisibilityOptions.Public, DataObject.IndexOptions index = 0) {
            if(!IsUnityServiceAvailable || CurrentLobby == null) { return; }

            try {
                if(CurrentLobby.Data.TryGetValue(key, out DataObject dataObject)) {
                    index = dataObject.Index;
                } else {
                    index = GetNextAvailableStringIndex(key, index);
                }

                CurrentLobby.Data[key] = new DataObject(visibility, value, index);

                await LobbyService.Instance.UpdateLobbyAsync(CurrentLobby.Id, new UpdateLobbyOptions() {
                    Data = CurrentLobby.Data
                });
            } catch(Exception ex) {
                PurrLogger.LogError($"Failed to Add/Update '{key}' StringIndexed Property: {ex.Message}");
            }
        }

        /// <summary>
        /// Add/Update a Lobby property which will be indexed as a numeric value for search query results. IndexOption must be unique, unset IndexOption to automatically populate with next available index. If the given Key already exists, the IndexOption will be unchanged.
        /// </summary>
        public async Task SetLobbyDataNumericIndexedAsync(string key, string value, DataObject.VisibilityOptions visibility = DataObject.VisibilityOptions.Public, DataObject.IndexOptions index = 0) {
            if(!IsUnityServiceAvailable || CurrentLobby == null) { return; }

            try {
                if(CurrentLobby.Data.TryGetValue(key, out DataObject dataObject)) {
                    index = dataObject.Index;
                } else {
                    index = GetNextAvailableNumericIndex(key, index);
                }

                CurrentLobby.Data[key] = new DataObject(visibility, value, index);

                await LobbyService.Instance.UpdateLobbyAsync(CurrentLobby.Id, new UpdateLobbyOptions() {
                    Data = CurrentLobby.Data
                });
            } catch(Exception ex) {
                PurrLogger.LogError($"Failed to Add/Update '{key}' NumericIndexed Property: {ex.Message}");
            }
        }

        /// <summary>
        /// Replace an existing indexed Lobby property
        /// </summary>
        public async Task ReplaceLobbyDataAsync(string oldKey, string key, string value, DataObject.VisibilityOptions visibility = DataObject.VisibilityOptions.Public) {
            if(!IsUnityServiceAvailable || CurrentLobby == null) { return; }

            try {
                if(CurrentLobby.Data.TryGetValue(oldKey, out DataObject dataObject) && dataObject.Index > 0) {
                    CurrentLobby.Data.Remove(oldKey);
                    CurrentLobby.Data[key] = new DataObject(visibility, value, dataObject.Index);

                    await LobbyService.Instance.UpdateLobbyAsync(CurrentLobby.Id, new UpdateLobbyOptions() {
                        Data = CurrentLobby.Data
                    });
                } else {
                    PurrLogger.LogError($"Failed to Replace Property '{oldKey}' because it either does not exist or is not an indexed property");
                }
            } catch(Exception ex) {
                PurrLogger.LogError($"Failed to Replace Property '{oldKey}' with '{key}': {ex.Message}");
            }
        }

        /// <summary>
        /// Remove an existing Lobby property
        /// </summary>
        public async Task RemoveLobbyDataAsync(string key) {
            if(!IsUnityServiceAvailable || CurrentLobby == null) { return; }

            try {
                if(CurrentLobby.Data.TryGetValue(key, out DataObject _)) {
                    CurrentLobby.Data.Remove(key);

                    await LobbyService.Instance.UpdateLobbyAsync(CurrentLobby.Id, new UpdateLobbyOptions() {
                        Data = CurrentLobby.Data
                    });
                }
            } catch (Exception ex) {
                PurrLogger.LogError($"Failed to Remove Property '{key}': {ex.Message}");
            }
        }

        /// <summary>
        /// Update Lobby properties if you have made direct changes to CurrentLobby.Data
        /// </summary>
        public async Task UpdateLobbyDataAsync() {
            if(!IsUnityServiceAvailable || CurrentLobby == null) { return; }

            try {
                await LobbyService.Instance.UpdateLobbyAsync(CurrentLobby.Id, new UpdateLobbyOptions() {
                    Data = CurrentLobby.Data
                });
            } catch(Exception ex) {
                PurrLogger.LogError($"Failed to Update LobbyData: {ex.Message}");
            }
        }

        /// <summary>
        /// Add/Update a Player property
        /// </summary>
        public async Task SetPlayerDataAsync(string key, string value, PlayerDataObject.VisibilityOptions visibility = PlayerDataObject.VisibilityOptions.Public) {
            if(!IsUnityServiceAvailable || CurrentLobby == null || LocalPlayer == null) { return; }

            try {
                LocalPlayer.Data[key] = new PlayerDataObject(visibility, value);

                await LobbyService.Instance.UpdatePlayerAsync(CurrentLobby.Id, LocalPlayerId, new UpdatePlayerOptions() {
                    Data = LocalPlayer.Data
                });
            } catch(Exception ex) {
                PurrLogger.LogError($"Failed to Add/Update '{key}' DataObject for LocalPlayer: {ex.Message}");
            }
        }

        /// <summary>
        /// Update Player properties if you have made direct changes to LocalPlayer.Data
        /// </summary>
        public async Task UpdatePlayerDataAsync() {
            if(!IsUnityServiceAvailable || CurrentLobby == null || LocalPlayer == null) { return; }

            try {
                await LobbyService.Instance.UpdatePlayerAsync(CurrentLobby.Id, LocalPlayerId, new UpdatePlayerOptions() {
                    Data = LocalPlayer.Data
                });
            } catch(Exception ex) {
                PurrLogger.LogError($"Failed to Update PlayerData for LocalPlayer: {ex.Message}");
            }
        }

        public async Task SetIsReadyAsync(string userId, bool isReady) {
            if(!IsUnityServiceAvailable || CurrentLobby == null || LocalPlayer == null) { return; }

            await SetPlayerDataAsync("IsReady", $"{isReady}");
        }

        public async Task SetAllReadyAsync() {
            if(!IsUnityServiceAvailable || CurrentLobby == null || !UseUnityRelayService) { return; }

            if(IsLocalPlayerHost) {
                await AllocateRelayServerAsync(CurrentLobby.MaxPlayers, RegionId);

                await SetLobbyDataAsync("JoinCode", RelayJoinCode);
            } else {
                RelayJoinCode = await WaitForJoinCodeAsync();
            }
        }

        public Task SetLobbyStartedAsync() {
            if(!IsUnityServiceAvailable || CurrentLobby == null || !UseUnityRelayService) { return Task.FromResult(Task.CompletedTask); }

            return Task.FromResult(Task.CompletedTask);
        }
        private async Task<string> WaitForJoinCodeAsync() {
            using(var cts = new CancellationTokenSource(RelayTimeout)) {
                try {
                    while(!cts.Token.IsCancellationRequested) {
                        if(CurrentLobby.Data.TryGetValue("JoinCode", out var joinCodeData) && !string.IsNullOrWhiteSpace(joinCodeData.Value)) {
                            return joinCodeData.Value;
                        }

                        await Task.Delay(100, cts.Token);
                    }
                } catch(OperationCanceledException) {
                    PurrLogger.LogError("Failed to join Relay due to timeout.");
                }
            }

            return null;
        }

        public void Shutdown() {
            _localPlayer = null;
            UnsubscribeLobbyEvents();
        }

        public Task<List<FriendUser>> GetFriendsAsync(LobbyManager.FriendFilter filter) {
            var friends = new List<FriendUser>();

            return Task.FromResult(friends);
        }

        public Task InviteFriendAsync(FriendUser user) {
            return Task.FromResult(Task.CompletedTask);
        }

        /// <summary>
        /// Allocates a Relay Server in a given Region. If no valid RegionId is provided, the most optimal Region will be automatically used instead.
        /// </summary>
        /// <param name="maxPlayers">The max number of players that may connect to this server.</param>
        /// <param name="regionId">The region to allocate the server in. May be null.</param>
        public async Task<bool> AllocateRelayServerAsync(int maxPlayers, string regionId) {
            if(!IsUnityServiceAvailable) { return false; }

            //Note: List of regions here https://docs.unity.com/ugs/manual/relay/manual/locations-and-regions
            if(!string.IsNullOrWhiteSpace(regionId)) {
                List<Region> listRegions = await RelayService.Instance.ListRegionsAsync();
                if(listRegions == null || listRegions.Count == 0) {
                    regionId = "";
                    PurrLogger.LogWarning($"Unable to retrieve the list of Relay regions, will use most optimal region instead.");
                } else if(listRegions.Find(x => x.Id == regionId) == null) {
                    regionId = "";
                    PurrLogger.LogWarning($"Invalid Relay Region ID, will use most optimal region instead.");
                }
            }

            RelayServerAllocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers, regionId);
            if(RelayServerAllocation == null) {
                PurrLogger.LogError($"Unable to allocate Relay Server.");
                return false;
            }

            RelayJoinCode = await RelayService.Instance.GetJoinCodeAsync(RelayServerAllocation.AllocationId);
            if(string.IsNullOrWhiteSpace(RelayJoinCode)) {
                RelayServerAllocation = null;
                PurrLogger.LogError($"Unable to allocate Relay Server, encountered an error retrieving the Join Code.");
                return false;
            }

            PurrLogger.Log($"Relay Server Allocated | Region: {RelayServerAllocation.Region} | Join Code: {RelayJoinCode}");

            return true;
        }
#endif
    }
}
