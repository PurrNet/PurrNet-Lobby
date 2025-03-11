#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

using PurrLobby.Providers;

namespace PurrLobby.Editor {
    [CustomEditor(typeof(UnityLobbyProvider))]
    public class UnityLobbyProviderInspector : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            base.OnInspectorGUI();

            var provider = (UnityLobbyProvider)target;

#if UTP_AUTH && UTP_LOBBYRELAY
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Unity Lobby Status", EditorStyles.boldLabel);

            if(Application.isPlaying) {
                if(!provider)
                    return;

                EditorGUILayout.HelpBox("UnityLobbyProvider is initialized and running.", MessageType.Info);

                var currentLobby = (Unity.Services.Lobbies.Models.Lobby)provider.GetType()
                    .GetField("CurrentLobby", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.GetValue(provider)!;

                if(currentLobby != null && currentLobby.Id != "") {
                    EditorGUILayout.LabelField("In Lobby:", "Yes");
                    EditorGUILayout.LabelField("Lobby ID:", currentLobby.Id);
                    EditorGUILayout.LabelField("Lobby Code:", currentLobby.LobbyCode);
                    currentLobby.Data.TryGetValue("JoinCode", out var joinCodeData);
                    EditorGUILayout.LabelField("Relay Code:", joinCodeData?.Value ?? "");
                    EditorGUILayout.LabelField($"Players in Lobby:", $"{currentLobby.Players.Count}");
                } else {
                    EditorGUILayout.LabelField("In Lobby:", "No");
                }
            } else {
                EditorGUILayout.HelpBox("Status will be available in Play Mode.", MessageType.Info);
            }
#else
            EditorGUILayout.HelpBox("UnityServices are not installed. UnityLobbyProvider will not function.", MessageType.Error);
            if(GUILayout.Button("Add UnityServices to Package Manager")) {
                UnityEditor.PackageManager.Client.Add("com.unity.services.multiplayer");

                //These auto install as dependencies of com.unity.services.multiplayer
                //UnityEditor.PackageManager.Client.Add("com.unity.services.core");
                //UnityEditor.PackageManager.Client.Add("com.unity.services.authentication");
            }
#endif
        }
    }
}

#endif