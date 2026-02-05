using PurrNet;
using PurrNet.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PurrLobby
{
    /// <summary>
    /// Handles scene transitions from lobby to game scene.
    /// 
    /// Can be used in two ways:
    /// 1. Automatic: Set subscribeToOnAllReady = true to auto-switch when all players ready
    /// 2. Manual: Call SwitchScene() from a button or script
    /// 
    /// For Unity Lobby with Relay: Ensures relay is allocated before scene switch
    /// For Steam/Other providers: Standard scene transition after lobby setup
    /// </summary>
    public class SceneSwitcher : MonoBehaviour
    {
        [SerializeField] private LobbyManager lobbyManager;
        [PurrScene, SerializeField] private string nextScene;
        [Tooltip("Automatically switch scene when OnAllReady event fires (recommended for Unity Relay)")]
        [SerializeField] private bool subscribeToOnAllReady = true;

        // Prevents duplicate scene transitions
        private static bool _hasAlreadySwitched = false;

        private void Start()
        {
            if (subscribeToOnAllReady && lobbyManager != null)
            {
                // Subscribe to OnAllReady event - fires after SetAllReadyAsync() completes
                // For Unity Relay: This ensures relay allocation is done before scene switch
                // For other providers: This ensures lobby setup is complete
                lobbyManager.OnAllReady.AddListener(SwitchScene);
            }
        }

        private void OnDestroy()
        {
            if (lobbyManager != null)
            {
                lobbyManager.OnAllReady.RemoveListener(SwitchScene);
            }
        }

        /// <summary>
        /// Switches to the game scene.
        /// Safe to call multiple times - will only switch once.
        /// </summary>
        public void SwitchScene()
        {
            // Prevent duplicate scene switches
            if (_hasAlreadySwitched)
            {
                PurrLogger.LogWarning("SwitchScene already called - ignoring duplicate", this);
                return;
            }
            
            _hasAlreadySwitched = true;
            
            if (string.IsNullOrEmpty(nextScene))
            {
                PurrLogger.LogError("Next scene name is not set!", this);
                return;
            }

            PurrLogger.Log($"Switching to scene: {nextScene}", this);
            
            // Mark lobby as started to prevent new players from joining
            if (lobbyManager != null)
            {
                lobbyManager.SetLobbyStarted();
            }
            
            // Load game scene - ConnectionStarter in new scene will handle network initialization
            SceneManager.LoadSceneAsync(nextScene);
        }
    }
}
