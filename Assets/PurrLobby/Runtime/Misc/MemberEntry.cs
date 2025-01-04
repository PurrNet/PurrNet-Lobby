using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PurrLobby
{
    public class MemberEntry : MonoBehaviour
    {
        [SerializeField] private TMP_Text userName;
        [SerializeField] private RawImage avatar;
        [SerializeField] private Color readyColor;

        private Color _defaultColor;
        private string _memberId;
        public string MemberId => _memberId;

        public void Init(LobbyUser user)
        {
            _defaultColor = userName.color;
            _memberId = user.Id;
            avatar.texture = user.Avatar;
            userName.text = user.DisplayName;
            SetReady(user.IsReady);
        }
        
        public void SetReady(bool isReady)
        {
            userName.color = isReady ? readyColor : _defaultColor;
        }
    }
}
