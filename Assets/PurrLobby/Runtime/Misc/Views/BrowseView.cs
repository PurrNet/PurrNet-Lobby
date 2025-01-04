using UnityEngine;

namespace PurrLobby
{
    public class BrowseView : View
    {
        [SerializeField] private LobbyManager lobbyManager;
        [SerializeField] private LobbyList lobbyList;

        public override void OnShow()
        {
            lobbyManager.SearchRooms();
        }
    }
}
