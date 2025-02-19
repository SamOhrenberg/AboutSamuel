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

    public static float LevenshteinDifference(string s, string t)
    {
        int levenshteinDistance = ComputeLevenshtein(s, t);
        int maxLength = Math.Max(s.Length, t.Length);
        return ((float)levenshteinDistance / maxLength) * 100;
    }

    public static int ComputeLevenshtein(string s, string t)
    {
        if (string.IsNullOrEmpty(s))
        {
            return string.IsNullOrEmpty(t) ? 0 : t.Length;
        }

        if (string.IsNullOrEmpty(t))
        {
            return s.Length;
        }

        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];

        for (int i = 0; i <= n; i++)
        {
            d[i, 0] = i;
        }

        for (int j = 0; j <= m; j++)
        {
            d[0, j] = j;
        }

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }

}
