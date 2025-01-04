using System;
using System.Collections;
using PurrNet;
using PurrNet.Transports;
using UnityEngine;
using TMPro;

namespace PurrLobby
{
    public class ConnectedText : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private TMP_Text connectedText;

        private void Awake()
        {
            networkManager.onClientConnectionState += OnConnectionState;
        }

        private void OnDestroy()
        {
            networkManager.onClientConnectionState -= OnConnectionState;
        }

        private void OnConnectionState(ConnectionState obj)
        {
            if (obj == ConnectionState.Connected)
                StartCoroutine(TypewriterEffect("Connected"));
            else if (obj == ConnectionState.Disconnected)
                StartCoroutine(TypewriterEffect("Not connected"));
        }

        private WaitForSeconds _wait = new(0.1f);
        
        private IEnumerator TypewriterEffect(string newText)
        {
            while (connectedText.text.Length > 0)
            {
                connectedText.text = connectedText.text.Substring(0, connectedText.text.Length - 1);
                yield return _wait;
            }

            foreach (char c in newText)
            {
                connectedText.text += c;
                yield return _wait;
            }
        }
    }
}