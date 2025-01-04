using System.Collections;
using PurrNet;
using PurrNet.Logging;
using PurrNet.Transports;
using UnityEngine;

namespace PurrLobby
{
    public class ConnectionStarter : MonoBehaviour
    {
        private PurrTransport _transport;
        private NetworkManager _networkManager;
        private LobbyDataHolder _lobbyDataHolder;
        
        private void Awake()
        {
            if(!TryGetComponent(out _transport))
                PurrLogger.LogError($"Failed to get {nameof(PurrTransport)} component.", this);
            
            if(!TryGetComponent(out _networkManager))
                PurrLogger.LogError($"Failed to get {nameof(NetworkManager)} component.", this);
            
            _lobbyDataHolder = FindFirstObjectByType<LobbyDataHolder>();
            if(!_lobbyDataHolder)
                PurrLogger.LogError($"Failed to get {nameof(LobbyDataHolder)} component.", this);
        }

        private void Start()
        {
            if (!_networkManager)
            {
                PurrLogger.LogError($"Failed to start connection. {nameof(NetworkManager)} is null!", this);
                return;
            }
            
            if (!_transport)
            {
                PurrLogger.LogError($"Failed to start connection. {nameof(PurrTransport)} is null!", this);
                return;
            }
            
            if (!_lobbyDataHolder)
            {
                PurrLogger.LogError($"Failed to start connection. {nameof(LobbyDataHolder)} is null!", this);
                return;
            }
            
            if (!_lobbyDataHolder.CurrentLobby.IsValid)
            {
                PurrLogger.LogError($"Failed to start connection. Lobby is invalid!", this);
                return;
            }
            
            _transport.roomName = _lobbyDataHolder.CurrentLobby.lobbyId;
            
            if(_lobbyDataHolder.CurrentLobby.IsOwner)
                _networkManager.StartServer();
            StartCoroutine(StartClient());
        }

        private IEnumerator StartClient()
        {
            yield return new WaitForSeconds(1f);
            _networkManager.StartClient();
        }
    }
}
