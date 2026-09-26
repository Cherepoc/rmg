using System.Collections.Immutable;
using System.Numerics;

namespace Rmg.Core.Events;

public static class StateKinds
{
    // what Render reads; the key, the scale and the tempo are the same for every track
    public static readonly StateKind<double> Velocity = CreateAdditive<double>("Velocity", StateScope.Render);
    public static readonly StateKind<double> QuarterNoteDurationPower = CreateAdditive<double>("QuarterNoteDurationPower", StateScope.Render);
    public static readonly StateKind<double> NextNoteDurationFactor = CreateAdditive<double>("NextNoteDurationFactor", StateScope.Render);
    public static readonly StateKind<ImmutableArray<double>> ArticulationOffset = CreateCollection<double>("ArticulationOffset", StateScope.Render);
    public static readonly StateKind<int> KeyOffset = CreateAdditive<int>("KeyOffset", StateScope.Render, isShared: true);
    public static readonly StateKind<int> OctaveOffset = CreateAdditive<int>("OctaveOffset", StateScope.Render);
    public static readonly StateKind<ImmutableArray<double>> ChordNotePitchOffsets = CreateCollection<double>("ChordNotePitchOffsets", StateScope.Render);
    public static readonly StateKind<ImmutableArray<double>> ChordRootNoteOffset = CreateCollection<double>("ChordRootOffset", StateScope.Render);
    public static readonly StateKind<ImmutableArray<double>> ChordNoteOffset = CreateCollection<double>("ChordNoteOffset", StateScope.Render);
    public static readonly StateKind<ImmutableArray<int>> ScaleOffsets = CreateCollection<int>("ScaleOffsets", StateScope.Render, isShared: true);
    public static readonly StateKind<double> Tempo = CreateMultiplicative<double>("Tempo", StateScope.Render, isShared: true);

    public static StateKind<T> CreateAdditive<T>(
        string name,
        StateScope scope = StateScope.Composition,
        bool isShared = false
    )
        where T : INumber<T>
    {
        return new StateKind<T>(
            name,
            T.Zero,
            (v1, v2) => v1 == v2,
            values => values.Aggregate(T.Zero, (a, b) => a + b),
            scope: scope,
            isShared: isShared
        );
    }

    public static StateKind<T> CreateMultiplicative<T>(
        string name,
        StateScope scope = StateScope.Composition,
        bool isShared = false
    )
        where T : INumber<T>
    {
        return new StateKind<T>(
            name,
            T.One,
            (v1, v2) => v1 == v2,
            values => values.Aggregate(T.One, (a, b) => a * b),
            scope: scope,
            isShared: isShared
        );
    }

    private static bool IsOrdered<T>(ImmutableArray<T> values)
    {
        var comparer = Comparer<T>.Default;
        for (var i = 1; i < values.Length; i++)
            if (comparer.Compare(values[i - 1], values[i]) > 0)
                return false;
        return true;
    }

    public static StateKind<ImmutableArray<T>> CreateCollection<T>(
        string name,
        StateScope scope = StateScope.Composition,
        bool isShared = false
    )
    {
        var isComparable = typeof(IComparable<T>).IsAssignableFrom(typeof(T));
        return new StateKind<ImmutableArray<T>>(
            name,
            [],
            (v1, v2) => v1.SequenceEqual(v2),
            values =>
            {
                // duplicates are kept: each layer contributes its own values, such as offsets that are summed later
                var preprocessedValues = values.SelectMany(x => x);
                if (isComparable)
                    preprocessedValues = preprocessedValues.Order();
                return [..preprocessedValues];
            },
            values =>
            {
                var hashCode = new HashCode();
                if (!values.IsDefault)
                    foreach (var value in values)
                        hashCode.Add(value);
                return hashCode.ToHashCode();
            },
            // aggregating a single collection only puts its values in order
            values => !isComparable || values.IsDefault || IsOrdered(values),
            scope,
            isShared
        );
    }
}
