using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PurrLobby
{
    public class FriendsList : MonoBehaviour
    {
        [SerializeField] private LobbyManager lobbyManager;
        [SerializeField] private FriendEntry friendEntry;
        [SerializeField] private Transform content;
        [SerializeField] private LobbyManager.FriendFilter filter;

        private float _lastUpdateTime;
        private Dictionary<string, FriendEntry> _currentFriends = new Dictionary<string, FriendEntry>();

        public void Populate(List<FriendUser> friends)
        {
            var newFriendIds = new HashSet<string>(friends.Select(f => f.Id));
            var existingFriendIds = new HashSet<string>(_currentFriends.Keys);

            foreach (var id in existingFriendIds.Except(newFriendIds))
            {
                Destroy(_currentFriends[id].gameObject);
                _currentFriends.Remove(id);
            }

            foreach (var friend in friends)
            {
                if (!_currentFriends.TryGetValue(friend.Id, out var existingEntry))
                {
                    var newEntry = Instantiate(friendEntry, content);
                    newEntry.Init(friend, lobbyManager);
                    _currentFriends[friend.Id] = newEntry;
                }
            }
        }

        private void Update()
        {
            if(_lastUpdateTime + 3f < Time.time)
            {
                _lastUpdateTime = Time.time;
                lobbyManager.PullFriends(filter);
            }
        }
    }
}
