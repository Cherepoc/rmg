using System;
using System.Collections.Generic;
using System.Linq;

namespace RMG.Core.Utils
{
    public static class PrimeNumbers
    {
        private static readonly List<int> Numbers = new List<int>
        {
            1,
            2,
            3
        };

        private static int _max = 3;

        public static IEnumerable<int> Get(int min, int max)
        {
            lock (Numbers)
            {
                if (max > _max)
                {
                    AddNumbers(max);
                }

                return Numbers
                    .SkipWhile(x => x < min)
                    .TakeWhile(x => x <= max);
            }
        }

        private static void AddNumbers(int max)
        {
            var oddMax = max / 2 + 1;
            for (var number = _max + 2; number <= oddMax; number++)
            {
                if (CheckPrime(number))
                {
                    Numbers.Add(number);
                }
            }

            _max = oddMax;
        }

        private static bool CheckPrime(int number)
        {
            var sqrt = (int) Math.Sqrt(number);
            return Numbers
                .Skip(1)
                .TakeWhile(x => x <= sqrt)
                .All(prime => number % prime != 0);
        }
    }
}
