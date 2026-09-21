using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Numerics;

namespace Rmg.Core.Events;

public static class StateKinds
{
    public static readonly StateKind<double> Velocity = CreateAdditive<double>("Velocity");
    public static readonly StateKind<double> QuarterNoteDurationPower = CreateAdditive<double>("QuarterNoteDurationPower");
    public static readonly StateKind<double> NextNoteDurationFactor = CreateAdditive<double>("NextNoteDurationFactor");
    public static readonly StateKind<ImmutableArray<double>> ArticulationOffset = CreateCollection<double>("ArticulationOffset");
    public static readonly StateKind<int> KeyOffset = CreateAdditive<int>("KeyOffset");
    public static readonly StateKind<int> OctaveOffset = CreateAdditive<int>("OctaveOffset");
    public static readonly StateKind<ImmutableArray<double>> ChordNoteInScaleOffsets = CreateCollection<double>("ChordNoteInScaleOffsets");
    public static readonly StateKind<ImmutableArray<double>> ChordRootNoteOffset = CreateCollection<double>("ChordRootOffset");
    public static readonly StateKind<ImmutableArray<double>> ChordNoteOffset = CreateCollection<double>("ChordNoteOffset");
    public static readonly StateKind<ImmutableArray<int>> ScaleOffsets = CreateCollection<int>("ScaleOffsets");
    public static readonly StateKind<double> Tempo = CreateMultiplicative<double>("Tempo");

    private static readonly FrozenDictionary<string, IStateKind> Values;
    private static readonly ImmutableArray<IStateKind> AllValues;

    static StateKinds()
    {
        AllValues =
        [
            Velocity,
            QuarterNoteDurationPower,
            NextNoteDurationFactor,
            ArticulationOffset,
            KeyOffset,
            OctaveOffset,
            ChordNoteInScaleOffsets,
            ChordRootNoteOffset,
            ChordNoteOffset,
            ScaleOffsets,
            Tempo
        ];
        Values = AllValues.ToFrozenDictionary(x => x.Name);
    }

    public static IStateKind GetByName(string name)
    {
        return Values[name];
    }

    public static ImmutableArray<IStateKind> GetAll()
    {
        return AllValues;
    }

    public static StateKind<bool> CreateBoolPessimistic(string name)
    {
        return new StateKind<bool>(
            name,
            true,
            (v1, v2) => v1 == v2,
            values => values.Aggregate(true, (a, b) => a && b)
        );
    }

    public static StateKind<bool> CreateBoolOptimistic(string name)
    {
        return new StateKind<bool>(
            name,
            false,
            (v1, v2) => v1 == v2,
            values => values.Aggregate(false, (a, b) => a || b)
        );
    }

    public static StateKind<T> CreateAdditive<T>(string name)
        where T : INumber<T>
    {
        return new StateKind<T>(
            name,
            T.Zero,
            (v1, v2) => v1 == v2,
            values => values.Aggregate(T.Zero, (a, b) => a + b)
        );
    }

    public static StateKind<T> CreateMultiplicative<T>(string name)
        where T : INumber<T>
    {
        return new StateKind<T>(
            name,
            T.One,
            (v1, v2) => v1 == v2,
            values => values.Aggregate(T.One, (a, b) => a * b)
        );
    }

    public static StateKind<ImmutableArray<T>> CreateCollection<T>(string name)
    {
        var isComparable = typeof(IComparable<T>).IsAssignableFrom(typeof(T));
        return new StateKind<ImmutableArray<T>>(
            name,
            [],
            (v1, v2) => v1.SequenceEqual(v2),
            values =>
            {
                var preprocessedValues = values
                    .SelectMany(x => x)
                    .Distinct();
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
            }
        );
    }
}
