using TMPro;
using UnityEngine;

namespace PurrLobby
{
    public class LobbyEntry : MonoBehaviour
    {
        [SerializeField] private TMP_Text lobbyNameText;
        [SerializeField] private TMP_Text playersText;
        
        public void Init(LobbyRoom room)
        {
            lobbyNameText.text = room.Name;
            playersText.text = $"{room.Members.Count}/{room.MaxPlayers}";
        }
    }
}
