using UnityEngine;

namespace PurrLobby
{
    public interface IBaseEncoder
    {
        public string Encode(ulong value);
        public ulong Decode(string value);
    }
}
