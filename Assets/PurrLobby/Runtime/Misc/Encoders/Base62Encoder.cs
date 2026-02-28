using System;

namespace PurrLobby
{
    public class Base62Encoder : IBaseEncoder
    {
        private const int MaxBase62Length = 11; // ceil(log62(ulong.MaxValue))
        private const string Chars = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

        public string Encode(ulong value)
        {
            if (value == 0)
            {
                return "0";
            }

            var result = "";
            while (value > 0)
            {
                result = Chars[(int)(value % 62)] + result;
                value /= 62;
            }
            return result;
        }

        public ulong Decode(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("Value cannot be null or empty.");
            }

            if (value.Length > MaxBase62Length)
            {
                throw new FormatException($"Input exceeds maximum Base62 length ({MaxBase62Length}).");
            }
            
            ulong result = 0;
            foreach (var c in value)
            {
                var digit = Chars.IndexOf(c);
                if (digit < 0)
                {
                    throw new FormatException($"Invalid Base62 character: {c}");
                }
                result = result * 62 + (ulong)digit;
            }
            return result;
        }
    }
}