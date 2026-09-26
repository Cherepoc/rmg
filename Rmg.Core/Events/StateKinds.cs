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
    // 1 for a chord whose layout is what it is, which Render moves only by whole octaves
    public static readonly StateKind<int> ChordVoicingFixed = CreateAdditive<int>("ChordVoicingFixed", StateScope.Render);
    // how smoothly a track's chords move, from 0, the chord's shape sliding with the root, to 1, every note moving as
    // little as it can
    public static readonly StateKind<double> VoiceLeading = CreateAdditive<double>("VoiceLeading", StateScope.Render);
    // a bar whose first chord plays as drawn, in its own register, and not led from the chord before; each such bar
    // has its own number, so that bars in a row are told apart
    public static readonly StateKind<int> ChordVoicingReset = CreateAdditive<int>("ChordVoicingReset", StateScope.Render);
    // 1 for a track that plays the chord roots, the bass, whose line leads into the chords and lands on them
    public static readonly StateKind<int> FollowsChordRoots = CreateAdditive<int>("FollowsChordRoots", StateScope.Render);
    // how a bar's last bass note leads into the next chord (a ChordApproach), and what its first note of a new chord
    // plays (a ChordArrival)
    public static readonly StateKind<int> ChordApproach = CreateAdditive<int>("ChordApproach", StateScope.Render);
    public static readonly StateKind<int> ChordArrival = CreateAdditive<int>("ChordArrival", StateScope.Render);
    public static readonly StateKind<ImmutableArray<double>> ChordRootNoteOffset = CreateCollection<double>("ChordRootOffset", StateScope.Render);
    public static readonly StateKind<ImmutableArray<double>> ChordNoteOffset = CreateCollection<double>("ChordNoteOffset", StateScope.Render);
    public static readonly StateKind<ImmutableArray<int>> ScaleOffsets = CreateCollection<int>("ScaleOffsets", StateScope.Render, isShared: true);
    // scale steps raised a semitone each time they are listed, such as the seventh on a cadence
    public static readonly StateKind<ImmutableArray<int>> RaisedScaleSteps = CreateCollection<int>("RaisedScaleSteps", StateScope.Render, isShared: true);
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
