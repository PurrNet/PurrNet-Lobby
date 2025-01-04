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
}