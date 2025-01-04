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
        
        public void Init(FriendUser friend, LobbyManager lobbyManager)
        {
            nameText.text = friend.DisplayName;
            avatarImage.texture = friend.Avatar;
            _friend = friend;
            _lobbyManager = lobbyManager;
        }

        public void Invite()
        {
            if (!_friend.HasValue)
            {
                PurrLogger.LogError($"{nameof(FriendEntry)}: No friend to invite.", this);
                return;
            }
            
            _lobbyManager.InviteFriend(_friend.Value);
        }
    }
}
