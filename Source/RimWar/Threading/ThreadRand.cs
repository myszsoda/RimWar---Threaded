using System;
using System.Collections.Generic;

namespace RimWar.Threading
{
    /// <summary>
    /// Random helpers for job Compute() code. Verse.Rand is a single global stream owned by the game
    /// thread, so workers use the job's seeded System.Random instead.
    /// </summary>
    public static class ThreadRand
    {
        public static float Value(this Random rng)
        {
            return (float)rng.NextDouble();
        }

        public static bool Chance(this Random rng, float chance)
        {
            if (chance <= 0f)
            {
                return false;
            }
            if (chance >= 1f)
            {
                return true;
            }
            return rng.NextDouble() < chance;
        }

        /// <summary>Same contract as Verse.Rand.Range(float, float): inclusive on both ends.</summary>
        public static float Range(this Random rng, float minInclusive, float maxInclusive)
        {
            return minInclusive + (float)rng.NextDouble() * (maxInclusive - minInclusive);
        }

        /// <summary>Same contract as Verse.Rand.Range(int, int): max is exclusive.</summary>
        public static int Range(this Random rng, int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                return minInclusive;
            }
            return rng.Next(minInclusive, maxExclusive);
        }

        public static T RandomElement<T>(this Random rng, IList<T> list)
        {
            if (list == null || list.Count == 0)
            {
                return default(T);
            }
            return list[rng.Next(list.Count)];
        }

        /// <summary>In-place Fisher-Yates shuffle.</summary>
        public static void Shuffle<T>(this Random rng, IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }
}
