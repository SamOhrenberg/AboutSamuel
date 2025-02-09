using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PortfolioWebsite.Common;

public class Utility
{
    public static int TrueRandom(int min, int max)
    {
        var rng = new byte[4];
        using var random = RandomNumberGenerator.Create();
        random.GetBytes(rng);
        int value = BitConverter.ToInt32(rng, 0);
        return Math.Abs(value % (max - min + 1)) + min;
    }

        public static List<T> Shuffle<T>(List<T> list)
    {
        var rng = new byte[4];
        using (var random = RandomNumberGenerator.Create())
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                random.GetBytes(rng);
                int j = BitConverter.ToInt32(rng, 0) % (i + 1);
                if (j < 0) j = -j;

                // Swap elements
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }
        return list;
    }
}
