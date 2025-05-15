using System.Collections.Immutable;
using Rmg.Core.Events;

namespace Rmg.Core.Probabilities;

public static class Generators
{
    public static Func<IGenerationContext, int> WeightedIndex<T>(ImmutableArray<Weighted<T>> items)
    {
        var probabilityThresholds = items.ToProbabilityThresholds();
        return context =>
        {
            var probability = context.GenerateDouble();
            return probabilityThresholds.FindIndexByProbability(probability);
        };
    }

    public static Func<IGenerationContext, T> WeightedValue<T>(ImmutableArray<Weighted<T>> items)
    {
        var probabilityThresholds = items.ToProbabilityThresholds();
        return context =>
        {
            var probability = context.GenerateDouble();
            return probabilityThresholds.FindValueByProbability(probability);
        };
    }

    public static Func<IGenerationContext, int> Rank(
        Func<int, double> weightFunc,
        int minRank,
        int maxRank
    )
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minRank, maxRank);

        if (minRank == maxRank)
            return _ => minRank;

        var absRanks = Enumerable
            .Range(minRank, maxRank - minRank + 1)
            .Select(Math.Abs)
            .Distinct()
            .Order()
            .ToImmutableArray();
        var rankedRanksBuilder = ImmutableArray.CreateBuilder<ImmutableArray<int>>(absRanks.Length);
        foreach (var absRank in absRanks)
        {
            var hasPositiveRank = absRank >= minRank && absRank <= maxRank;
            var hasNegativeRank = -absRank >= minRank && -absRank <= maxRank;
            var rankedItemsBuilder = ImmutableArray.CreateBuilder<int>(hasPositiveRank && hasNegativeRank ? 2 : 1);
            if (hasPositiveRank)
                rankedItemsBuilder.Add(absRank);
            if (hasNegativeRank)
                rankedItemsBuilder.Add(-absRank);
            rankedRanksBuilder.Add(rankedItemsBuilder.ToImmutable());
        }
        var rankedRanks = rankedRanksBuilder.ToImmutable();
        var generateWeightedAbsRankIndex = WeightedIndex(absRanks.WeightUsing(weightFunc));
        return context =>
        {
            var absRankIndex = generateWeightedAbsRankIndex(context);
            var rankedItems = rankedRanks[absRankIndex];
            if (rankedItems.Length == 1)
                return rankedItems[0];
            var itemIndex = context.GenerateInt(0, rankedItems.Length);
            return rankedItems[itemIndex];
        };
    }

    public static Func<IGenerationContext, T> ItemSelector<T>(IEnumerable<T> items)
    {
        var itemArray = items.AsImmutableArray();
        if (itemArray.Length == 0)
            throw new ArgumentException("Items must not be empty", nameof(items));

        return context =>
        {
            var index = context.GenerateInt(0, itemArray.Length);
            return itemArray[index];
        };
    }

    public static Func<IGenerationContext, double> RhythmPrimePeriod(
        Func<int, double> weightFunc,
        int maxPrimeIndex
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxPrimeIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxPrimeIndex, RhythmPeriod.MaxPrimeIndex);

        return Rank(weightFunc, -maxPrimeIndex, maxPrimeIndex)
            .Then(RhythmPeriod.ToRhythmPeriodValue);
    }

    /// <summary>
    /// Creates a generator that returns a dyadic multiplier.
    /// The multiplier is centered around 1.0, with a range depending on the maxRank.
    /// with maxRank = 1, the multiplier can be from 0.5 to 2.0
    /// with maxRank = 2, the multiplier can be from 0.25 to 4.0, etc.
    /// </summary>
    public static Func<IGenerationContext, double> DyadicMultiplier(
        Func<int, double> weightFunc,
        int maxRank,
        double minValue,
        double maxValue
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxRank);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxRank, DyadicRankDistribution.MaxRank);
        ArgumentOutOfRangeException.ThrowIfNegative(minValue);
        ArgumentOutOfRangeException.ThrowIfNegative(maxValue);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minValue, maxValue);

        if (maxRank == 0)
            return _ => 1.0;

        var probabilityThresholds = Enumerable.Range(0, maxRank + 1)
            .WeightUsing(weightFunc)
            .ToProbabilityThresholds();
        return context =>
        {
            var probability = context.GenerateDouble();
            var rank = probabilityThresholds.FindIndexByProbability(probability);
            var items = DyadicRankDistribution.GetMultiplierDistributionSlice(rank, minValue, maxValue);
            var itemIndex = items.Length > 1
                ? context.GenerateInt(0, items.Length)
                : 0;
            return items[itemIndex].Position;
        };
    }

    public static Func<IGenerationContext, double> HalfDyadicOffset(
        Func<int, double> weightFunc,
        int maxRank,
        double minValue,
        double maxValue
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxRank);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxRank, DyadicRankDistribution.MaxRank);
        ArgumentOutOfRangeException.ThrowIfNegative(minValue);
        ArgumentOutOfRangeException.ThrowIfNegative(maxValue);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minValue, maxValue);

        if (maxRank == 0)
            return _ => 1.0;

        var probabilityThresholds = Enumerable.Range(0, maxRank + 1)
            .WeightUsing(weightFunc)
            .ToProbabilityThresholds();
        return context =>
        {
            var probability = context.GenerateDouble();
            var rank = probabilityThresholds.FindIndexByProbability(probability);
            var items = DyadicRankDistribution.GetExactHalfDistributionSlice(rank, minValue, maxValue);
            var itemIndex = items.Length > 1
                ? context.GenerateInt(0, items.Length)
                : 0;
            return items[itemIndex].Position;
        };
    }

    public static Func<IGenerationContext, EventTimeline<T>> EventTimelineFromRankTimeline<T>(
        Func<int, double> weightFunc,
        Func<int, T> itemFunc,
        EventTimeline<int> rankTimeline
    )
        where T : notnull
    {
        if (rankTimeline.Count == 0)
            return _ => EventTimeline.Empty<T>();

        return context =>
        {
            var items = new List<TimelineItem<T>>(rankTimeline.Count);
            foreach (var rankItem in rankTimeline)
            {
                var probabilityThreshold = weightFunc(rankItem.Value);
                var generateItem = context.TestProbability(probabilityThreshold);
                if (!generateItem)
                    continue;

                var value = itemFunc(rankItem.Value);
                var item = new TimelineItem<T>(rankItem.Position, value);
                items.Add(item);
            }

            return EventTimeline.Create(rankTimeline.Duration, items);
        };
    }

    public static Func<IGenerationContext, EventTimeline<T>> EventTimelineFromSequentialDurationItems<T>(
        Func<IGenerationContext, WithDuration<T>> itemGenerator,
        double duration
    )
        where T : notnull
    {
        ArgumentOutOfRangeException.ThrowIfNegative(duration);

        if (duration == 0)
            return _ => EventTimeline.Empty<T>();

        return context =>
        {
            var totalDuration = 0.0;
            var items = new List<TimelineItem<T>>();
            while (totalDuration < duration)
            {
                var (itemDuration, itemValue) = itemGenerator(context);
                if (itemDuration <= 0)
                    throw new InvalidOperationException("Item duration must be positive");

                var item = new TimelineItem<T>(totalDuration, itemValue);
                items.Add(item);

                totalDuration += itemDuration;
            }

            return EventTimeline.Create(duration, items);
        };
    }

    public static Func<IGenerationContext, ImmutableArray<T>> Sequence<T>(
        Func<IGenerationContext, T> itemGenerator,
        int count
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (count == 0)
            return _ => [];

        return context =>
        {
            var items = new T[count];
            for (var i = 0; i < count; i++)
                items[i] = itemGenerator(context);

            return [..items];
        };
    }

    public static Func<IGenerationContext, ImmutableArray<T>> SubSequence<T>(IEnumerable<T> sequence, int count)
        where T : notnull
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (count == 0)
            return _ => [];

        var itemArray = sequence.AsImmutableArray();
        if (itemArray.Length == 0)
            throw new ArgumentException("Items must not be empty", nameof(sequence));
        if (count > itemArray.Length)
            throw new ArgumentOutOfRangeException(
                nameof(count),
                "Count must be less than or equal to the sequence length"
            );

        if (itemArray.Length == 1)
            return _ => [itemArray[0]];

        var indexes = new int[itemArray.Length];
        for (var i = 0; i < itemArray.Length; i++)
            indexes[i] = i;

        return context =>
        {
            var items = new T[count];
            var remainingIndexes = new List<int>(indexes);

            for (var i = 0; i < count; i++)
            {
                if (remainingIndexes.Count > 1)
                {
                    var remainingIndex = context.GenerateInt(0, remainingIndexes.Count);
                    var itemIndex = remainingIndexes[remainingIndex];
                    items[i] = itemArray[itemIndex];
                    remainingIndexes.RemoveAt(remainingIndex);
                }
                else
                {
                    var itemIndex = remainingIndexes[0];
                    items[i] = itemArray[itemIndex];
                }
            }

            return [..items];
        };
    }

    public static Func<IGenerationContext, ImmutableArray<T>> SubSequenceFromWeighted<T>(
        ImmutableArray<Weighted<T>> sequence,
        int count
    )
        where T : notnull
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (count == 0)
            return _ => [];

        if (sequence.Length == 0)
            throw new ArgumentException("Items must not be empty", nameof(sequence));
        if (count > sequence.Length)
            throw new ArgumentOutOfRangeException(
                nameof(count),
                "Count must be less than or equal to the sequence length"
            );

        if (sequence.Length == 1)
            return _ => [sequence[0].Value];

        var probabilityThresholds = sequence.ToProbabilityThresholds();
        return context =>
        {
            var items = new T[count];
            var remainingProbabilityThresholds = probabilityThresholds;

            for (var i = 0; i < count; i++)
            {
                var probability = context.GenerateDouble();
                var itemIndex = remainingProbabilityThresholds.FindIndexByProbability(probability);
                var item = remainingProbabilityThresholds[itemIndex];
                items[i] = item.Value;
                remainingProbabilityThresholds = remainingProbabilityThresholds.RemoveThresholdAtIndex(itemIndex);
            }

            return [..items];
        };
    }

    public static Func<IGenerationContext, int> Int(int min, int max)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(min, max);

        if (min == max - 1)
            return _ => min;

        return context => context.GenerateInt(min, max);
    }

    public static Func<IGenerationContext, int> Int()
    {
        return context => context.GenerateInt();
    }

    public static Func<IGenerationContext, double> Double(double min, double max)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(min, max);

        if (min.IsEqualToByEpsilon(max))
            return _ => min;

        var scale = max - min;
        return context => context.GenerateDouble() * scale + min;
    }

    public static Func<IGenerationContext, double> SplineValue(double c = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(c);

        var circlePower = c <= 1 ? 1 : 1 / c;

        return context =>
        {
            var value = context.GenerateDouble() * 2 - 1;
            var circleValue = Math.Pow(Math.Sqrt(1 - Math.Pow(value, 2)), circlePower) - 1;
            if (value > 0)
                circleValue = -circleValue;

            return c <= 1
                ? circleValue + (1 - c) * value
                : circleValue;
        };
    }

    public static Func<IGenerationContext, double> AbsSplineValue(double c = 1)
    {
        return SplineValue(c).Then(Math.Abs);
    }
}