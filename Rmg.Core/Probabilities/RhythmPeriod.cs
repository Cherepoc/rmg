using System.Collections.Immutable;

namespace Rmg.Core.Probabilities;

public static class RhythmPeriod
{
    private static readonly ImmutableArray<int> Primes = [3, 5, 7, 11, 13];

    private static readonly ImmutableArray<double> PrimePeriodValues = BuildPeriodValues();

    public static int MaxPrimeIndex => Primes.Length;

    private static ImmutableArray<double> BuildPeriodValues()
    {
        var result = new double[Primes.Length * 2 + 1];
        result[Primes.Length] = 1;

        for (var i = 0; i < Primes.Length; i++)
        {
            var prime = Primes[i];
            var nearestPower = Math.Round(Math.Log2(prime));
            var nearestDivider = Math.Pow(2, nearestPower);
            var divideResult = prime / nearestDivider;
            var (lesser, greater) = divideResult < 1
                ? (divideResult, 1 / divideResult)
                : (1 / divideResult, divideResult);
            var nextIndex = i + 1;
            result[Primes.Length - nextIndex] = lesser;
            result[Primes.Length + nextIndex] = greater;
        }

        return [..result];
    }

    /// <summary>
    ///     The tuplet a period's notes fall on: the prime, where the period divides the beat by it, such as 4/3 for
    ///     triplets; 1 where they stay on the straight grid, as a straight period does, or one the prime lengthens, such as
    ///     3/4 for dotted 8ths, whose notes group in threes but fall on 16ths.
    /// </summary>
    public static int ToTuplet(this int primeIndex)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(primeIndex, MaxPrimeIndex);
        ArgumentOutOfRangeException.ThrowIfLessThan(primeIndex, -MaxPrimeIndex);

        if (primeIndex == 0)
            return 1;

        // the prime divides the period where the period times the prime is a power of two
        var prime = Primes[Math.Abs(primeIndex) - 1];
        var power = Math.Log2(primeIndex.ToRhythmPeriodValue() * prime);
        return Math.Abs(power - Math.Round(power)) < 1e-9 ? prime : 1;
    }

    public static double ToRhythmPeriodValue(this int primeIndex)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(primeIndex, MaxPrimeIndex);
        ArgumentOutOfRangeException.ThrowIfLessThan(primeIndex, -MaxPrimeIndex);

        return PrimePeriodValues[primeIndex + Primes.Length];
    }
}
