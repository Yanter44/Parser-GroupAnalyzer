using System.Text;
using MpParserAPI.Interfaces;

namespace MpParserAPI.Services
{
    public  class Generator : IGenerator
    {
        private  readonly Random _rand = new Random();
        private  readonly char[] Symbols = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
        private  readonly char[] Numbers = "123456789".ToCharArray();

        public  string GenerateRandomPassword()
        {
            var passwordBuilder = new StringBuilder();

            for (int i = 0; i < 4; i++)
            {
                passwordBuilder.Append(Symbols[_rand.Next(Symbols.Length)]);
                passwordBuilder.Append(Numbers[_rand.Next(Numbers.Length)]);
            }

            return ShuffleString(passwordBuilder.ToString());
        }

        private  string ShuffleString(string str)
        {
            char[] array = str.ToCharArray();
            int n = array.Length;
            while (n > 1)
            {
                int k = _rand.Next(n--);
                (array[n], array[k]) = (array[k], array[n]);
            }
            return new string(array);
        }
    }

}
