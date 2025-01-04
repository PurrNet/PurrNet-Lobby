using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.Events;

namespace PurrLobby
{
    public interface ILobbyProvider
    {
        // Initialization
        Task InitializeAsync();
        void Shutdown();

        // Friend List
        Task<List<FriendUser>> GetFriendsAsync();
        
        // Invitations
        Task InviteFriendAsync(FriendUser user);
        Task AcceptInviteAsync(string inviteId);
        Task DeclineInviteAsync(string inviteId);

        // Room Management
        Task<LobbyRoom> CreateRoomAsync(int maxPlayers, Dictionary<string, string> roomProperties = null);
        Task LeaveRoomAsync();
        Task<LobbyRoom> JoinRoomAsync(string roomId);
        Task<List<LobbyRoom>> SearchRoomsAsync(int maxRoomsToFind = 10, Dictionary<string, string> filters = null);
        Task SetIsReadyAsync(string userId, bool isReady);
        Task<List<LobbyUser>> GetLobbyMembersAsync();
        Task<string> GetLocalUserIdAsync();

        // Events
        event UnityAction<LobbyUser> OnInviteReceived;
        event UnityAction<string> OnInviteAccepted;
        event UnityAction<string> OnInviteDeclined;
        event UnityAction<string> OnRoomJoinFailed;
        event UnityAction OnRoomLeft;
        event UnityAction<LobbyRoom> OnRoomUpdated;
        event UnityAction<List<LobbyUser>> OnLobbyPlayerListUpdated;
        event UnityAction<List<FriendUser>> OnFriendListPulled;

        // Error Handling
        event UnityAction<string> OnError;
    }
}