using System;

namespace PurrLobby
{
    public class Base36Encoder : IBaseEncoder
    {
        private const string Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        public string Encode(ulong value)
        {
            if (value == 0)
            {
                return "0";
            }

            var result = "";
            while (value > 0)
            {
                result = Chars[(int)(value % 36)] + result;
                value /= 36;
            }
            return result;
        }

        public ulong Decode(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("Value cannot be null or empty.");
            }

            value = value.ToUpper().Trim();
            ulong result = 0;
            foreach (var c in value)
            {
                var digit = Chars.IndexOf(c);
                if (digit < 0)
                {
                    throw new FormatException($"Invalid Base36 character: {c}");
                }
                result = result * 36 + (ulong)digit;
            }
            return result;
        }
    }
}