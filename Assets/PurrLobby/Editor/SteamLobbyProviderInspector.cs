#if UNITY_EDITOR
using PurrLobby.Providers;

#if STEAMWORKS_NET_PACKAGE
using Steamworks;
#endif
using UnityEditor;
using UnityEngine;

namespace PurrLobby.Editor
{
    [CustomEditor(typeof(SteamLobbyProvider))]
    public class SteamLobbyProviderInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var provider = (SteamLobbyProvider)target;

#if STEAMWORKS_NET_PACKAGE && !DISABLESTEAMWORKS
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Steam Lobby Status", EditorStyles.boldLabel);

            if (Application.isPlaying)
            {
                if (!provider)
                    return;
                
                var isInitialized = provider.IsSteamClientAvailable;

                if (isInitialized)
                {
                    EditorGUILayout.HelpBox("SteamLobbyProvider is initialized and running.", MessageType.Info);

                    var currentLobby = (CSteamID)provider.GetType()
                        .GetField("_currentLobby", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        ?.GetValue(provider)!;

                    if (currentLobby.m_SteamID != 0)
                    {
                        EditorGUILayout.LabelField("In Lobby:", "Yes");
                        EditorGUILayout.LabelField("Lobby ID:", currentLobby.m_SteamID.ToString());
                        EditorGUILayout.LabelField("Players in Lobby:", SteamMatchmaking.GetNumLobbyMembers(currentLobby).ToString());
                    }
                    else
                    {
                        EditorGUILayout.LabelField("In Lobby:", "No");
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("SteamLobbyProvider is not initialized.", MessageType.Warning);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Status will be available in Play Mode.", MessageType.Info);
            }
#else
    EditorGUILayout.HelpBox("Steamworks.NET is not properly set up. SteamLobbyProvider will not function.", MessageType.Error);
    if (GUILayout.Button("Add Steamworks.NET to Package Manager"))
    {
        UnityEditor.PackageManager.Client.Add("https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net#2024.8.0");
    }
#endif
        }
    }
}

#endif