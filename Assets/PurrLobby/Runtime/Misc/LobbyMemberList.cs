using System;
using UnityEngine;

namespace PurrLobby
{
    public class LobbyMemberList : MonoBehaviour
    {
        [SerializeField] private MemberEntry memberEntryPrefab;
        [SerializeField] private Transform content;

        public void LobbyDataUpdate(LobbyRoom room)
        {
            if(!room.IsValid)
                return;

            HandleExistingMembers(room);
            HandleNewMembers(room);
            HandleLeftMembers(room);
        }

        public void OnLobbyLeave()
        {
            foreach (Transform child in content)
                Destroy(child.gameObject);
        }

        private void HandleExistingMembers(LobbyRoom room)
        {
            foreach (Transform child in content)
            {
                if (!child.TryGetComponent(out MemberEntry member))
                    continue;
                
                if(room.Members.Exists(x => x.Id == member.MemberId))
                    member.SetReady(room.Members.Find(x => x.Id == member.MemberId).IsReady);
            }
        }

        private void HandleNewMembers(LobbyRoom room)
        {
            var existingMembers = content.GetComponentsInChildren<MemberEntry>();
            
            foreach (var member in room.Members)
            {
                if (Array.Exists(existingMembers, x => x.MemberId == member.Id))
                    continue;

                var entry = Instantiate(memberEntryPrefab, content);
                entry.Init(member);
            }
        }

        private void HandleLeftMembers(LobbyRoom room)
        {
            for (int i = 0; i < content.childCount; i++)
            {
                var child = content.GetChild(i);
                if (!child.TryGetComponent(out MemberEntry member))
                    continue;

                if (!room.Members.Exists(x => x.Id == member.MemberId))
                {
                    Destroy(child.gameObject);
                    i--;
                }
            }
        }
    }
}
