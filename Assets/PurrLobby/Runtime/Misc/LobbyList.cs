using System.Collections.Generic;
using UnityEngine;

namespace PurrLobby
{
    public class LobbyList : MonoBehaviour
    {
        [SerializeField] private LobbyEntry lobbyEntryPrefab;
        [SerializeField] private Transform content;

        public void Populate(List<LobbyRoom> rooms)
        {
            foreach (Transform child in content)
                Destroy(child.gameObject);
            
            foreach (var room in rooms)
            {
                var entry = Instantiate(lobbyEntryPrefab, content);
                entry.Init(room);
            }
        }
    }
}
