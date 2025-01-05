using System;
using PurrNet.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PurrLobby
{
    public class FriendEntry : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private RawImage avatarImage;
        
        private FriendUser? _friend;
        private LobbyManager _lobbyManager;
        private float _inviteTime = -999;
        private Button _button;
        
        public void Init(FriendUser friend, LobbyManager lobbyManager)
        {
            if(!TryGetComponent(out _button))
                PurrLogger.LogError($"{nameof(FriendEntry)}: No button found.", this);
            
            nameText.text = friend.DisplayName;
            avatarImage.texture = friend.Avatar;
            _friend = friend;
            _lobbyManager = lobbyManager;
        }

        private void Update()
        {
            if (_button.interactable == false && _inviteTime + 3 < Time.time)
                _button.interactable = true;
        }

        public void Invite()
        {
            if (!_friend.HasValue)
            {
                PurrLogger.LogError($"{nameof(FriendEntry)}: No friend to invite.", this);
                return;
            }
            _inviteTime = Time.time;
            _lobbyManager.InviteFriend(_friend.Value);
            _button.interactable = false;
        }
    }
}
