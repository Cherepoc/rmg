using System.Collections.Immutable;

namespace Rmg.Core.Probabilities;

public static class DyadicRankDistribution
{
    public const int MaxRank = 8;

    private static readonly ImmutableArray<ImmutableArray<Item>> ExactRankedDistributions;

    private static readonly ImmutableArray<ImmutableArray<Item>> CombinedRankedDistributions;

    private static readonly ImmutableArray<ImmutableArray<Item>> ExactRankedHalfDistributions;

    private static readonly ImmutableArray<ImmutableArray<Item>> CombinedRankedHalfDistributions;

    private static readonly ImmutableArray<ImmutableArray<Item>> ExactRankedMultiplierDistributions;

    private static readonly ImmutableArray<ImmutableArray<Item>> CombinedRankedMultiplierDistributions;

    static DyadicRankDistribution()
    {
        ExactRankedDistributions = BuildExactRankedDistributions();
        CombinedRankedDistributions = BuildCombinedRankedDistributions(ExactRankedDistributions);
        ExactRankedHalfDistributions = BuildExactRankedHalfDistributions(ExactRankedDistributions);
        CombinedRankedHalfDistributions = BuildCombinedRankedDistributions(ExactRankedHalfDistributions);
        ExactRankedMultiplierDistributions = BuildExactRankedMultiplierDistributions(ExactRankedDistributions);
        CombinedRankedMultiplierDistributions = BuildCombinedRankedDistributions(ExactRankedMultiplierDistributions);
    }

    private static ImmutableArray<ImmutableArray<Item>> BuildExactRankedDistributions()
    {
        var result = new ImmutableArray<Item>[MaxRank + 1];
        result[0] = [new Item(0, 0)];

        for (var rank = 1; rank < result.Length; rank++)
        {
            var itemCount = (int)Math.Pow(2, rank);
            var period = 2.0 / itemCount;
            var phase = -1 + period / 2;

            var items = new Item[itemCount];

            for (var i = 0; i < itemCount; i++)
                items[i] = new Item(phase + i * period, rank);

            result[rank] = [..items];
        }

        return [..result];
    }

    private static ImmutableArray<ImmutableArray<Item>> BuildCombinedRankedDistributions(
        ImmutableArray<ImmutableArray<Item>> exactRankedDistributions
    )
    {
        var result = new ImmutableArray<Item>[exactRankedDistributions.Length];
        result[0] = exactRankedDistributions[0];

        for (var rank = 1; rank < result.Length; rank++)
        {
            var previousItems = result[rank - 1];
            var newItems = exactRankedDistributions[rank];

            var items = new Item[previousItems.Length + newItems.Length];
            previousItems.CopyTo(items);
            newItems.CopyTo(items, previousItems.Length);

            Array.Sort(items);

            result[rank] = [..items];
        }

        return [..result];
    }

    private static ImmutableArray<ImmutableArray<Item>> BuildExactRankedHalfDistributions(ImmutableArray<ImmutableArray<Item>> exactFullDistributions)
    {
        var result = new ImmutableArray<Item>[exactFullDistributions.Length];
        result[0] = exactFullDistributions[0];

        for (var rank = 1; rank < result.Length; rank++)
        {
            var fullDistribution = exactFullDistributions[rank];
            var halfLength = fullDistribution.Length / 2;
            result[rank] = fullDistribution.Slice(halfLength, halfLength);
        }

        return [..result];
    }

    private static ImmutableArray<ImmutableArray<Item>> BuildExactRankedMultiplierDistributions(
        ImmutableArray<ImmutableArray<Item>> exactFullDistributions
    )
    {
        var result = new ImmutableArray<Item>[exactFullDistributions.Length];
        result[0] = [new Item(1, 0)];

        for (var rank = 1; rank < result.Length; rank++)
        {
            var fullDistribution = exactFullDistributions[rank];

            var items = new Item[fullDistribution.Length];
            for (var i = 0; i < fullDistribution.Length; i++)
            {
                var item = fullDistribution[i];
                var position = item.Position < 0
                    ? -item.Position
                    : 1 / item.Position;
                items[i] = item with { Position = position };
            }

            Array.Sort(items);

            result[rank] = [..items];
        }

        return [..result];
    }

    public static ImmutableArray<Item> GetCombinedDistributionByRank(int rank)
    {
        ValidateRank(rank);

        return CombinedRankedDistributions[rank];
    }

    public static ImmutableArray<Item> GetCombinedHalfDistributionByRank(int rank)
    {
        ValidateRank(rank);

        return CombinedRankedHalfDistributions[rank];
    }

    public static ImmutableArray<Item> GetMultiplierDistributionSlice(int rank, double minValue, double maxValue)
    {
        ValidateRank(rank);

        ArgumentOutOfRangeException.ThrowIfNegative(minValue);
        ArgumentOutOfRangeException.ThrowIfNegative(maxValue);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minValue, maxValue);

        var distribution = ExactRankedMultiplierDistributions[rank];
        return SliceItemsByPositions(distribution, minValue, maxValue);
    }

    public static ImmutableArray<Item> GetExactHalfDistributionSlice(int rank, double minValue, double maxValue)
    {
        ValidateRank(rank);

        ArgumentOutOfRangeException.ThrowIfNegative(minValue);
        ArgumentOutOfRangeException.ThrowIfNegative(maxValue);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minValue, maxValue);

        var distribution = ExactRankedHalfDistributions[rank];
        return SliceItemsByPositions(distribution, minValue, maxValue);
    }

    public static double RoundNearestDyadicMultiplier(this double value, int maxRank)
    {
        return RoundNearestDyadicDistribution(CombinedRankedMultiplierDistributions, value, maxRank);
    }

    public static double RoundNearestHalfDyadicOffset(this double value, int maxRank)
    {
        return RoundNearestDyadicDistribution(CombinedRankedHalfDistributions, value, maxRank);
    }

    public static double GetMultiplier(int rank, double rankedOffset)
    {
        ValidateRank(rank);
        ArgumentOutOfRangeException.ThrowIfLessThan(rankedOffset, -1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(rankedOffset, 1);

        var distribution = ExactRankedMultiplierDistributions[rank];
        if (rank == 0)
            return distribution[0].Position;

        var index = (int)Math.Floor((rankedOffset + 1) * distribution.Length);
        return index >= distribution.Length
            ? distribution[^1].Position
            : distribution[index].Position;
    }

    public static double GetHalfOffset(int rank, double rankedOffset)
    {
        ValidateRank(rank);
        ArgumentOutOfRangeException.ThrowIfLessThan(rankedOffset, -1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(rankedOffset, 1);

        var distribution = ExactRankedHalfDistributions[rank];
        if (rank == 0)
            return distribution[0].Position;

        var index = (int)Math.Floor((rankedOffset + 1) * distribution.Length);
        return index >= distribution.Length
            ? distribution[^1].Position
            : distribution[index].Position;
    }

    private static double RoundNearestDyadicDistribution(
        ImmutableArray<ImmutableArray<Item>> rankedDistributions,
        double value,
        int maxRank
    )
    {
        ValidateRank(maxRank);

        var distribution = rankedDistributions[maxRank];
        var searchIndex = distribution.BinarySearch(new Item(value, 0));
        if (searchIndex >= 0)
            return value;

        var greaterItemIndex = ~searchIndex;
        if (greaterItemIndex == 0)
            return distribution[0].Position;
        if (greaterItemIndex == distribution.Length)
            return distribution[^1].Position;

        var greaterPosition = distribution[greaterItemIndex].Position;
        var lesserPosition = distribution[greaterItemIndex - 1].Position;
        return greaterPosition - value > value - lesserPosition
            ? lesserPosition
            : greaterPosition;
    }

    private static void ValidateRank(int rank)
    {
        if (rank is < 0 or > MaxRank)
            throw new ArgumentOutOfRangeException(nameof(rank), rank, $"Rank must be between 0 and {MaxRank}");
    }

    private static int GetItemFloorIndex(ImmutableArray<Item> array, double position)
    {
        var searchItem = new Item(position, 0);
        var index = array.BinarySearch(searchItem);
        if (index < 0)
            index = ~index - 1;
        return index;
    }

    private static int GetItemCeilingIndex(ImmutableArray<Item> array, double position)
    {
        var searchItem = new Item(position, 0);
        var index = array.BinarySearch(searchItem);
        if (index < 0)
            index = ~index;
        return index < array.Length ? index : -1;
    }

    private static ImmutableArray<Item> SliceItemsByPositions(
        ImmutableArray<Item> array,
        double minPosition,
        double maxPosition
    )
    {
        if (array.Length == 0)
            throw new ArgumentException("Items must not be empty", nameof(array));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minPosition, maxPosition);

        var minIndex = GetItemCeilingIndex(array, minPosition);
        if (minIndex == -1)
            return ImmutableArray<Item>.Empty;

        var maxIndex = GetItemFloorIndex(array, maxPosition);
        if (minIndex > maxIndex)
            return ImmutableArray<Item>.Empty;

        return array.Slice(minIndex, maxIndex - minIndex + 1);
    }

    public readonly record struct Item(double Position, int Rank) : IComparable<Item>, IComparable
    {
        public int CompareTo(object? obj)
        {
            if (obj is Item item)
                return CompareTo(item);

            throw new ArgumentException($"Object must be of type {nameof(Item)}", nameof(obj));
        }

        public int CompareTo(Item other)
        {
            return Position.CompareTo(other.Position);
        }
    }
}
