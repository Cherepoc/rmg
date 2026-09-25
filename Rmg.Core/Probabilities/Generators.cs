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

    /// <summary>
    ///     Creates a generator of dyadic multipliers between <paramref name="minValue" /> and
    ///     <paramref name="maxValue" />. Rank 0 is 1 alone, and rank r adds k/2^r and its inverse for every odd k, so
    ///     each rank lies between the multipliers of the ranks before it: rank 1 is 1/2 and 2, rank 2 is 3/4 and 4/3,
    ///     1/4 and 4. A rank is chosen by its weight among the ranks that have a multiplier in range, then one of its
    ///     multipliers in range, each as likely.
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
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minValue, maxValue);

        var rankedMultipliers = Enumerable.Range(0, maxRank + 1)
            .Select(rank => DyadicRankDistribution.GetMultiplierDistributionSlice(rank, minValue, maxValue))
            .ToImmutableArray();
        var ranks = Enumerable.Range(0, maxRank + 1)
            .Where(rank => !rankedMultipliers[rank].IsEmpty)
            .ToImmutableArray();
        if (ranks.IsEmpty)
            throw new ArgumentException("No dyadic multiplier up to the rank is in the range.", nameof(maxRank));

        var generateRankIndex = WeightedIndex(ranks.WeightUsing(weightFunc));
        return context =>
        {
            var multipliers = rankedMultipliers[ranks[generateRankIndex(context)]];
            var index = multipliers.Length > 1
                ? context.GenerateInt(0, multipliers.Length)
                : 0;
            return multipliers[index].Position;
        };
    }

    public static Func<IGenerationContext, ImmutableArray<T>> Sequence<T>(Func<IGenerationContext, T> itemGenerator, int count)
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

    public static Func<IGenerationContext, EventTimeline<T>> SequentialTimeline<T>(
        Func<IGenerationContext, T> itemGenerator,
        double itemDuration,
        int count
    )
        where T : notnull
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (count == 0)
            return _ => EventTimeline.Create<T>(0);

        return context =>
        {
            var items = new TimelineItem<T>[count];
            for (var i = 0; i < count; i++)
                items[i] = itemGenerator(context).ToTimelineItem(i * itemDuration);

            return EventTimeline<T>.Create(count * itemDuration, items);
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

    /// <summary>
    ///     Creates a generator of values around 0, symmetric unless skewed. <paramref name="c" /> sets the spread: 1 is a
    ///     quarter circle on either side, more gathers the values closer to 0 and less spreads them further out, beyond
    ///     ±1. <paramref name="skew" /> is the power the random draw is raised to first: 1 leaves the values symmetric,
    ///     less tips them towards the positive side, so that a value is positive with a chance of 1 - 0.5^(1/skew).
    /// </summary>
    public static Func<IGenerationContext, double> SplineValue(double c = 1, double skew = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(c);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(skew);

        var circlePower = c <= 1 ? 1 : 1 / c;

        return context =>
        {
            var draw = context.GenerateDouble();
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            var value = (skew == 1 ? draw : Math.Pow(draw, skew)) * 2 - 1;
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
