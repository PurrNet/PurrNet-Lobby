using System.Collections.Generic;

namespace PurrLobby
{
    public struct LobbyRoom
    {
        public string Name;
        public bool IsValid;
        public string RoomId;
        public int MaxPlayers;
        public Dictionary<string, string> Properties;
        public List<LobbyUser> Members;
    }
    
    public static class LobbyRoomFactory
    {
        public static LobbyRoom Create(string name, string roomId, int maxPlayers, List<LobbyUser> members, Dictionary<string, string> properties)
        {
            return new LobbyRoom
            {
                Name = name,
                IsValid = true,
                RoomId = roomId,
                MaxPlayers = maxPlayers,
                Properties = properties ?? new Dictionary<string, string>(),
                Members = members
            };
        }
    }
}