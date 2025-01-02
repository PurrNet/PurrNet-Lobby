using System.Collections.Generic;

namespace PurrLobby
{
    public struct LobbyRoom
    {
        public bool IsValid;
        public string RoomId;
        public int MaxPlayers;
        public int CurrentPlayers;
        public Dictionary<string, string> Properties;
    }
}