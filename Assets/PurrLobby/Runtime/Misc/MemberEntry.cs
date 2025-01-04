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

        public void Init(string id, string username, Texture2D texture)
        {
            _defaultColor = userName.color;
            _memberId = id;
            avatar.texture = texture;
            userName.text = username;
        }
        
        public void SetReady(bool isReady)
        {
            userName.color = isReady ? readyColor : _defaultColor;
        }
    }
}
