using System.Collections.Generic;

namespace PurrLobby
{
    [System.Serializable]
    public struct Lobby
    {
        public string Name;
        public bool IsValid;
        public string lobbyId;
        public int MaxPlayers;
        public Dictionary<string, string> Properties;
        public bool IsOwner;
        public List<LobbyUser> Members;
    }
    
    public static class LobbyFactory
    {
        public static Lobby Create(string name, string lobbyId, int maxPlayers, bool isOwner, List<LobbyUser> members, Dictionary<string, string> properties)
        {
            return new Lobby
            {
                Name = name,
                IsValid = true,
                lobbyId = lobbyId,
                MaxPlayers = maxPlayers,
                Properties = properties ?? new Dictionary<string, string>(),
                IsOwner = isOwner,
                Members = members
            };
        }
    }
}