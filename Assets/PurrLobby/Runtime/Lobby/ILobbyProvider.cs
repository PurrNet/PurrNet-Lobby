using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.Events;

namespace PurrLobby {
    public interface ILobbyProvider {
        // Initialization
        Task InitializeAsync();
        void Shutdown();

        // Friend List
        Task<List<FriendUser>> GetFriendsAsync(LobbyManager.FriendFilter filter);

        // Invitations
        Task InviteFriendAsync(FriendUser user);

        // Lobby Management
        Task<Lobby> CreateLobbyAsync(int maxPlayers, Dictionary<string, string> lobbyProperties = null);
        Task LeaveLobbyAsync();
        Task LeaveLobbyAsync(string lobbyId);
        Task<Lobby> JoinLobbyAsync(string lobbyId);
        Task<List<Lobby>> SearchLobbiesAsync(int maxRoomsToFind = 10, Dictionary<string, string> filters = null);
        Task SetIsReadyAsync(string userId, bool isReady);
        Task SetLobbyDataAsync(string key, string value);
        Task<string> GetLobbyDataAsync(string key);
        Task<List<LobbyUser>> GetLobbyMembersAsync();
        Task<string> GetLocalUserIdAsync();
        Task SetAllReadyAsync();
        Task SetLobbyStartedAsync();

        // Events
        event UnityAction<string> OnLobbyJoinFailed;
        event UnityAction OnLobbyLeft;
        event UnityAction<Lobby> OnLobbyUpdated;
        event UnityAction<List<LobbyUser>> OnLobbyPlayerListUpdated;
        event UnityAction<List<FriendUser>> OnFriendListPulled;

        // Error Handling
        event UnityAction<string> OnError;
    }
}