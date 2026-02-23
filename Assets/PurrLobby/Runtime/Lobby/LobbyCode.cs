using System;
using System.Collections.Generic;
using System.Linq;

namespace PurrLobby
{
    public static class LobbyCode
    {
        private static IBaseEncoder encoder;
        public static string Encode(ulong value)
        {
            return encoder.Encode(value);
        }
        public static ulong Decode(string value)
        {
            return encoder.Decode(value);
        }
        
        public static List<Type> GetEncoderTypes() =>
            AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => typeof(IBaseEncoder).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .ToList();

        public static void AssignEncoder(string name)
        {
            var types = GetEncoderTypes();
            var type = types.FirstOrDefault(t => t.Name == name);
            if (type == null)
            {
                return;
            }
            encoder = (IBaseEncoder)Activator.CreateInstance(type);
        }
    }
}