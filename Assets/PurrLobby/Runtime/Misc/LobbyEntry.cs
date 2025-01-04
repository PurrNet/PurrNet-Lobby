using TMPro;
using UnityEngine;

namespace PurrLobby
{
    public class LobbyEntry : MonoBehaviour
    {
        [SerializeField] private TMP_Text lobbyNameText;
        [SerializeField] private TMP_Text playersText;

        private LobbyRoom _room;
        private LobbyManager _lobbyManager;
        
        public void Init(LobbyRoom room, LobbyManager lobbyManager)
        {
            lobbyNameText.text = room.Name.Length > 0 ? room.Name : room.RoomId;
            playersText.text = $"{room.Members.Count}/{room.MaxPlayers}";
            _room = room;
            _lobbyManager = lobbyManager;
        }

        public void OnClick()
        {
            _lobbyManager.JoinRoom(_room.RoomId);
        }
    }
}
