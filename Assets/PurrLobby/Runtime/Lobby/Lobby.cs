using System.Collections.Generic;

namespace PurrLobby {
    [System.Serializable]
    public struct Lobby {
        public string Name;
        public bool IsValid;
        public string LobbyId;
        public string LobbyCode;
        public int MaxPlayers;
        public Dictionary<string, string> Properties;
        public bool IsOwner;
        public List<LobbyUser> Members;
        public object ServerObject;

        public bool HasChanged(Lobby @new) {
            if(!IsValid || Name != @new.Name || LobbyId != @new.LobbyId || LobbyCode != @new.LobbyCode || Members.Count != @new.Members.Count || Properties.Count != @new.Properties.Count || ServerObject != @new.ServerObject)
                return true;

            for(int i = 0; i < @new.Members.Count; i++) {
                var newMember = @new.Members[i];
                var oldMember = Members[i];

                if(newMember.Id != oldMember.Id || newMember.IsReady != oldMember.IsReady || newMember.DisplayName != oldMember.DisplayName || newMember.Avatar != oldMember.Avatar)
                    return true;
            }

            foreach(var oldProp in Properties) {
                if(!@new.Properties.TryGetValue(oldProp.Key, out var newVal) || oldProp.Value != newVal)
                    return true;
            }

            return false;
        }
    }

    public static class LobbyFactory {
        public static Lobby Create(string name, string lobbyId, int maxPlayers, bool isOwner, List<LobbyUser> members, Dictionary<string, string> properties) {
            return new Lobby {
                Name = name,
                IsValid = true,
                LobbyId = lobbyId,
                MaxPlayers = maxPlayers,
                Properties = properties ?? new Dictionary<string, string>(),
                IsOwner = isOwner,
                Members = members
            };
        }

        public static Lobby Create(string name, string lobbyId, string lobbyCode, int maxPlayers, bool isOwner, List<LobbyUser> members, Dictionary<string, string> properties, object serverObject = null) {
            return new Lobby {
                Name = name,
                IsValid = true,
                LobbyId = lobbyId,
                LobbyCode = lobbyCode,
                MaxPlayers = maxPlayers,
                Properties = properties ?? new Dictionary<string, string>(),
                IsOwner = isOwner,
                Members = members,
                ServerObject = serverObject
            };
        }
    }
}