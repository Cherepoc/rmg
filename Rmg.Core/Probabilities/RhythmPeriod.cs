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

    public static double ToRhythmPeriodValue(this int primeIndex)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(primeIndex, MaxPrimeIndex);
        ArgumentOutOfRangeException.ThrowIfLessThan(primeIndex, -MaxPrimeIndex);

        return PrimePeriodValues[primeIndex + Primes.Length];
    }
}
