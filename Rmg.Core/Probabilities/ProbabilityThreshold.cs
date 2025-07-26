using System.Collections.Immutable;

namespace Rmg.Core.Probabilities;

public readonly struct ProbabilityThreshold<T> : IComparable<ProbabilityThreshold<T>>, IComparable
{
    public T Value { get; }
    public double Threshold { get; }

    public bool Test(double probability)
    {
        if (probability is < 0 or > 1)
            throw new ArgumentOutOfRangeException(
                nameof(probability),
                probability,
                "Probability must be greater than or equal to 0 and lesser or equal to 1."
            );
        return Threshold >= probability;
    }

    public ProbabilityThreshold(double threshold, T value)
    {
        ProbabilityThreshold.ValidateProbabilityThreshold(threshold);

        Threshold = threshold;
        Value = value;
    }

    public int CompareTo(ProbabilityThreshold<T> other)
    {
        return Threshold.CompareTo(other.Threshold);
    }

    public int CompareTo(object? obj)
    {
        if (obj is ProbabilityThreshold<T> item)
            return CompareTo(item);

        throw new ArgumentException($"Object must be of type {nameof(ProbabilityThreshold<T>)}", nameof(obj));
    }
}

public static class ProbabilityThreshold
{
    public static void ValidateProbabilityThreshold(double value)
    {
        if (value is <= 0 or > 1)
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Value must be greater than 0 and lesser or equal to 1."
            );
    }

    public static int FindIndexByProbability<T>(
        this ImmutableArray<ProbabilityThreshold<T>> probabilityThresholds,
        double probability
    )
    {
        ValidateProbabilityThreshold(probability);

        if (probabilityThresholds.Length == 0)
            throw new ArgumentException("Probability thresholds must not be empty", nameof(probabilityThresholds));

        return probabilityThresholds.BinarySearchCeiling(new ProbabilityThreshold<T>(probability, default!));
    }

    public static T FindValueByProbability<T>(
        this ImmutableArray<ProbabilityThreshold<T>> probabilityThresholds,
        double probability
    )
    {
        ValidateProbabilityThreshold(probability);

        if (probabilityThresholds.Length == 0)
            throw new ArgumentException("Probability thresholds must not be empty", nameof(probabilityThresholds));

        var index = probabilityThresholds.BinarySearchCeiling(new ProbabilityThreshold<T>(probability, default!));
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(probability), "Probability is out of range");

        return probabilityThresholds[index].Value;
    }

    public static ImmutableArray<ProbabilityThreshold<T>> ToProbabilityThresholds<T>(this ImmutableArray<Weighted<T>> weights)
    {
        if (weights.Length == 0)
            return [];

        var result = new ProbabilityThreshold<T>[weights.Length];
        var weightSum = weights.Sum(x => x.Weight);

        double lastThreshold = 0;
        for (var i = 0; i < weights.Length; i++)
        {
            var weightItem = weights[i];
            lastThreshold = (lastThreshold + weightItem.Weight / weightSum).RoundByEpsilon(1);
            result[i] = new ProbabilityThreshold<T>(lastThreshold, weightItem.Value);
        }

        return [..result];
    }

    public static ImmutableArray<ProbabilityThreshold<T>> RemoveThresholdAtIndex<T>(
        this ImmutableArray<ProbabilityThreshold<T>> probabilityThresholds,
        int index
    )
    {
        if (probabilityThresholds.Length == 0)
            throw new ArgumentException("Probability thresholds must not be empty", nameof(probabilityThresholds));

        if (index < 0 || index >= probabilityThresholds.Length)
            throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range");

        if (probabilityThresholds.Length == 1)
            return [];

        var thresholdAtIndex = probabilityThresholds[index].Threshold;
        var previousThreshold = index > 0 ? probabilityThresholds[index - 1].Threshold : 0;
        var thresholdDiff = thresholdAtIndex - previousThreshold;
        var thresholdMultiplier = 1 / (1 - thresholdDiff);

        var result = new ProbabilityThreshold<T>[probabilityThresholds.Length - 1];
        for (var i = 0; i < result.Length; i++)
        {
            var sourceIndex = i < index ? i : i + 1;
            var item = probabilityThresholds[sourceIndex];
            result[i] = new ProbabilityThreshold<T>(item.Threshold * thresholdMultiplier, item.Value);
        }

        return [..result];
    }
}
