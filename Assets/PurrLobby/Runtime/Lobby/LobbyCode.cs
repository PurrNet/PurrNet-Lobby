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
            if (encoder == null)
            {
                return value.ToString();
            }
            return encoder.Encode(value);
        }
        public static ulong Decode(string value)
        {
            if (encoder == null)
            {
                return ulong.Parse(value);
            }
            return encoder.Decode(value);
        }
        
        public static List<Type> GetEncoderTypes() =>
            AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => typeof(IBaseEncoder).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .ToList();

        public static void AssignEncoder(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }
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