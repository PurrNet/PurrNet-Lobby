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
        Task<IEnumerable<LobbyUser>> GetFriendsAsync();
        
        // Invitations
        Task InviteFriendAsync(LobbyUser user);
        Task AcceptInviteAsync(string inviteId);
        Task DeclineInviteAsync(string inviteId);

        // Room Management
        Task<LobbyRoom> CreateRoomAsync(int maxPlayers, Dictionary<string, string> roomProperties = null);
        Task LeaveRoomAsync();
        Task<LobbyRoom> JoinRoomAsync(string roomId);
        Task<IEnumerable<LobbyRoom>> SearchRoomsAsync(Dictionary<string, string> filters = null);
        Task SetIsReadyAsync(string userId, bool isReady);
        Task<IEnumerable<LobbyUser>> GetLobbyMembersAsync();

        // Events
        event UnityAction<LobbyUser> OnInviteReceived;
        event UnityAction<string> OnInviteAccepted;
        event UnityAction<string> OnInviteDeclined;
        event UnityAction<string> OnRoomJoinFailed;
        event UnityAction OnRoomLeft;
        event UnityAction<LobbyRoom> OnRoomUpdated;
        event UnityAction<IEnumerable<LobbyUser>> OnPlayerListUpdated;

        // Error Handling
        event UnityAction<string> OnError;
    }
}