using System;
using System.Collections.Generic;
using UnityEngine;

namespace PurrLobby
{
    public class FriendsList : MonoBehaviour
    {
        [SerializeField] private LobbyManager lobbyManager;
        [SerializeField] private FriendEntry friendEntry;
        [SerializeField] private Transform content;

        private float _lastUpdateTime;

        public void Populate(List<FriendUser> friends)
        {
            foreach (Transform child in content)
                Destroy(child.gameObject);

            foreach (var friend in friends)
            {
                var entry = Instantiate(friendEntry, content);
                entry.Init(friend, lobbyManager);
            }
        }

        private void Update()
        {
            if(_lastUpdateTime + 5f < Time.time)
            {
                _lastUpdateTime = Time.time;
                lobbyManager.PullFriends();
            }
        }
    }
}
