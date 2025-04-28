using System.Numerics;

namespace Rmg.Core;

public static class MathExtensions
{
    public const double Epsilon = 1e-3;
    
    public static T Mod<T>(this T divident, T divisor)
        where T : INumber<T>
    {
        var remainder = divident % divisor;
        return remainder < T.Zero ? remainder + divisor : remainder;
    }
    
    public static T TruncateMin<T>(this T value, T min)
        where T : INumber<T>
    {
        return value < min ? min : value;
    }

    public static double RoundByEpsilon(this double value, double reference)
    {
        return Math.Abs(value - reference) >= Epsilon
            ? value
            : reference;
    }

    public static bool IsEqualToByEpsilon(this double value, double reference)
    {
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        return value.RoundByEpsilon(reference) == reference;
    }

    public static int BounceInBounds(this int value, int min, int max)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(min, max);
        
        if (value >= min && value <= max)
            return value;

        if (min == max)
            return min;
        
        var range = max - min + 1;
        var normalizedValue = value - min;
        var valueInPeriod = normalizedValue.Mod(range);
        var periodIndex = (int)Math.Floor((double)normalizedValue / range);
        var isInvertedPeriod = periodIndex % 2 != 0;
        return isInvertedPeriod
            ? max - valueInPeriod
            : min + valueInPeriod;
    }
    
    public static double BounceInBounds(this double value, double min, double max)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(min, max);
        
        if (value >= min && value <= max)
            return value;

        if (min.IsEqualToByEpsilon(max))
            return min;
        
        var range = max - min;
        var normalizedValue = value - min;
        var valueInPeriod = normalizedValue.Mod(range);
        var periodIndex = (int)Math.Floor(normalizedValue / range);
        var isInvertedPeriod = periodIndex % 2 != 0;
        return isInvertedPeriod
            ? max - valueInPeriod
            : min + valueInPeriod;
    }
    
    public static double Pow2(this double value)
    {
        return Math.Pow(2, value);
    }
    
    public static double WeightedAverage(this double value, double weight, double otherValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(weight);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(weight, 1);

        return value * weight + otherValue * (1 - weight);
    }
}