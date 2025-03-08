#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace PurrLobby.Editor
{
    [CustomEditor(typeof(LobbyManager))]
    public class LobbyManagerEditor : UnityEditor.Editor
    {
        private bool showCreateRoomArgs = false;
        private bool showSearchRoomArgs = false;
        private bool showEvents = false;
        private bool showRoomStatus = true;
        private Dictionary<string, bool> memberFoldouts = new Dictionary<string, bool>();

        public override void OnInspectorGUI()
        {
            var lobbyManager = (LobbyManager)target;

            DrawProviderDropdown(lobbyManager);

            EditorGUILayout.Space();

            DrawCreateRoomArgs();
            
            EditorGUILayout.Space();

            DrawSearchRoomArgs();

            EditorGUILayout.Space();

            DrawRoomStatus(lobbyManager);

            EditorGUILayout.Space();

            DrawEventsFoldout();
        }

        private void DrawProviderDropdown(LobbyManager lobbyManager)
        {
            var providers = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None); 
            var providerOptions = new List<MonoBehaviour>();
            foreach (var provider in providers)
            {
                if (provider is ILobbyProvider)
                    providerOptions.Add(provider);
            }

            if (providerOptions.Count > 0)
            {
                var providerNames = providerOptions.ConvertAll(p => p.GetType().Name);
                var currentIndex = providerOptions.IndexOf((MonoBehaviour)lobbyManager.CurrentProvider);

                EditorGUILayout.LabelField("Lobby Provider", EditorStyles.boldLabel);
                var selectedIndex = EditorGUILayout.Popup(
                    currentIndex >= 0 ? currentIndex : 0,
                    providerNames.ToArray()
                );

                if (selectedIndex >= 0 && selectedIndex != currentIndex)
                {
                    Undo.RecordObject(lobbyManager, "Change Lobby Provider");
                    lobbyManager.GetType()
                        .GetField("currentProvider", BindingFlags.NonPublic | BindingFlags.Instance)
                        ?.SetValue(lobbyManager, providerOptions[selectedIndex]);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No provider found in scene.", MessageType.Warning);
            }
        }

        private void DrawCreateRoomArgs()
        {
            var serializedObject = new SerializedObject(target);
            var createRoomArgsProp = serializedObject.FindProperty("createRoomArgs");

            if (createRoomArgsProp != null)
            {
                showCreateRoomArgs = EditorGUILayout.Foldout(showCreateRoomArgs, "Create Room Arguments", true);
                if (showCreateRoomArgs)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(createRoomArgsProp.FindPropertyRelative("maxPlayers"));

                    var roomPropertiesProp = createRoomArgsProp.FindPropertyRelative("roomProperties");
                    if (roomPropertiesProp != null)
                    {
                        EditorGUILayout.LabelField("Room Properties");
                        DrawSerializableDictionary(roomPropertiesProp);
                    }

                    EditorGUI.indentLevel--;
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSearchRoomArgs()
        {
            var serializedObject = new SerializedObject(target);
            var searchRoomArgsProp = serializedObject.FindProperty("searchRoomArgs");

            if (searchRoomArgsProp != null)
            {
                showSearchRoomArgs = EditorGUILayout.Foldout(showSearchRoomArgs, "Search Room Arguments", true);
                if (showSearchRoomArgs)
                {
                    EditorGUI.indentLevel++;
                    DrawSerializableDictionary(searchRoomArgsProp);
                    EditorGUI.indentLevel--;
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSerializableDictionary(SerializedProperty dictionaryProperty)
        {
            var keys = dictionaryProperty.FindPropertyRelative("keys");
            var values = dictionaryProperty.FindPropertyRelative("values");

            if (keys.arraySize != values.arraySize)
            {
                EditorGUILayout.HelpBox("Key and value counts do not match!", MessageType.Error);
                return;
            }

            for (int i = 0; i < keys.arraySize; i++)
            {
                EditorGUILayout.BeginHorizontal();
                keys.GetArrayElementAtIndex(i).stringValue = EditorGUILayout.TextField(keys.GetArrayElementAtIndex(i).stringValue);
                values.GetArrayElementAtIndex(i).stringValue = EditorGUILayout.TextField(values.GetArrayElementAtIndex(i).stringValue);
                if (GUILayout.Button("-", GUILayout.Width(20)))
                {
                    keys.DeleteArrayElementAtIndex(i);
                    values.DeleteArrayElementAtIndex(i);
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Add Entry"))
            {
                keys.arraySize++;
                values.arraySize++;
            }
        }

        private void DrawRoomStatus(LobbyManager lobbyManager)
        {
            showRoomStatus = EditorGUILayout.Foldout(showRoomStatus, "Lobby Room Status", true);
            if (!showRoomStatus) return;

            EditorGUI.indentLevel++;

            if (!lobbyManager)
                return;
            
            var currentRoom = lobbyManager.CurrentLobby;

            if (currentRoom.IsValid)
            {
                EditorGUILayout.LabelField("Room ID:", currentRoom.LobbyId);
                if(!string.IsNullOrWhiteSpace(currentRoom.LobbyCode))
                {
                    EditorGUILayout.LabelField("Lobby Code:", currentRoom.LobbyCode);
                }
                EditorGUILayout.LabelField("Max Players:", currentRoom.MaxPlayers.ToString());
                EditorGUILayout.LabelField("Room Properties:");
                foreach (var property in currentRoom.Properties)
                {
                    EditorGUILayout.LabelField($"{property.Key}: {property.Value}");
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"Members ({currentRoom.Members.Count}):", EditorStyles.boldLabel);

                foreach (var member in currentRoom.Members)
                {
                    if (!memberFoldouts.ContainsKey(member.Id))
                        memberFoldouts[member.Id] = false;

                    memberFoldouts[member.Id] = EditorGUILayout.Foldout(memberFoldouts[member.Id], member.DisplayName);
                    if (memberFoldouts[member.Id])
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.LabelField("ID:", member.Id);
                        EditorGUILayout.LabelField("Display Name:", member.DisplayName);
                        EditorGUILayout.LabelField("Is Ready:", member.IsReady.ToString());
                        if (member.Avatar != null)
                        {
                            EditorGUILayout.LabelField("Avatar:");
                            var rect = EditorGUILayout.GetControlRect(false, 64);
                            float indentOffset = EditorGUI.indentLevel * 15f;
                            rect.x += indentOffset;
                            rect.width = 64;
                            EditorGUI.DrawPreviewTexture(rect, member.Avatar);
                        }
                        EditorGUI.indentLevel--;
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No active lobby room.", MessageType.Info);
            }

            EditorGUI.indentLevel--;
        }

        private void DrawEventsFoldout()
        {
            showEvents = EditorGUILayout.Foldout(showEvents, "Lobby Events", true);

            if (showEvents)
            {
                EditorGUI.indentLevel++;
                var serializedObject = new SerializedObject(target);
                var eventFields = typeof(LobbyManager).GetFields(BindingFlags.Public | BindingFlags.Instance);

                foreach (var field in eventFields)
                {
                    if (field.FieldType.IsSubclassOf(typeof(UnityEngine.Events.UnityEventBase)))
                    {
                        var eventProperty = serializedObject.FindProperty(field.Name);
                        if (eventProperty != null)
                        {
                            EditorGUILayout.PropertyField(eventProperty, true);
                        }
                    }
                }

                serializedObject.ApplyModifiedProperties();
                EditorGUI.indentLevel--;
            }
        }
    }
}

#endif