using System;
using System.Collections.Generic;
using PurrNet.Logging;
using UnityEngine;

namespace PurrLobby
{
    public class LobbyMemberList : MonoBehaviour
    {
        [SerializeField] private MemberEntry memberEntryPrefab;
        [SerializeField] private Transform content;

        public void LobbyDataUpdate(Lobby room)
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

        private void HandleExistingMembers(Lobby room)
        {
            foreach (Transform child in content)
            {
                if (!child.TryGetComponent(out MemberEntry member))
                    continue;

                var matchingMember = room.Members.Find(x => x.Id == member.MemberId);
                if (!string.IsNullOrEmpty(matchingMember.Id))
                {
                    member.SetReady(matchingMember.IsReady);
                }
            }
        }

        private void HandleNewMembers(Lobby room)
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

        private void HandleLeftMembers(Lobby room)
        {
            var childrenToRemove = new List<Transform>();

            for (int i = 0; i < content.childCount; i++)
            {
                var child = content.GetChild(i);
                if (!child.TryGetComponent(out MemberEntry member))
                    continue;

                if (!room.Members.Exists(x => x.Id == member.MemberId))
                {
                    childrenToRemove.Add(child);
                }
            }

            foreach (var child in childrenToRemove)
            {
                Destroy(child.gameObject);
            }
        }

    }
}
