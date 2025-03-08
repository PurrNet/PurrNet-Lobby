using TMPro;
using UnityEngine;

namespace PurrLobby
{
    public class LobbyEntry : MonoBehaviour
    {
        [SerializeField] private TMP_Text lobbyNameText;
        [SerializeField] private TMP_Text playersText;

        private Lobby _room;
        private LobbyManager _lobbyManager;
        
        public void Init(Lobby room, LobbyManager lobbyManager)
        {
            lobbyNameText.text = room.Name.Length > 0 ? room.Name : room.LobbyId;
            playersText.text = $"{room.Members.Count}/{room.MaxPlayers}";
            _room = room;
            _lobbyManager = lobbyManager;
        }

        public void OnClick()
        {
            _lobbyManager.JoinLobby(_room.LobbyId);
        }
    }
}
