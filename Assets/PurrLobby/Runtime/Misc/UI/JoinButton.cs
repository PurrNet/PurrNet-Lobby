using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace PurrLobby
{
    public class JoinButton : MonoBehaviour
    {
        [SerializeField] private TMP_InputField roomIdInput;
        [SerializeField] private LobbyManager lobbyManager;
        [SerializeField] private UnityEvent onStartJoin;
        
        public void JoinRoom()
        {
            if (string.IsNullOrEmpty(roomIdInput.text))
            {
                Debug.LogWarning($"Can't start join, room ID is empty.");
                return;
            }
            
            onStartJoin?.Invoke();
            lobbyManager.JoinLobby(roomIdInput.text);
        }
    }
}
