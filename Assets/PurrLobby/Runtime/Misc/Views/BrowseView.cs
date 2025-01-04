using UnityEngine;

namespace PurrLobby
{
    public class BrowseView : View
    {
        [SerializeField] private LobbyManager lobbyManager;
        [SerializeField] private LobbyList lobbyList;

        private bool _isActive;
        private float _lastSearchTime;
        
        public override void OnShow()
        {
            lobbyManager.SearchLobbies();
            _lastSearchTime = Time.time;
            _isActive = true;
        }

        public override void OnHide()
        {
            _isActive = false;
        }

        private void Update()
        {
            if(!_isActive)
                return;

            if (_lastSearchTime + 5f < Time.time)
            {
                _lastSearchTime = Time.time;
                lobbyManager.SearchLobbies();
            }
        }
    }
}
