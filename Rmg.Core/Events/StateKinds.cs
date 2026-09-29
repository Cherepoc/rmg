using System.Collections.Immutable;
using System.Numerics;

namespace Rmg.Core.Events;

public static class StateKinds
{
    // what Render reads; the key, the scale and the tempo are the same for every track
    public static readonly StateKind<double> Velocity = CreateAdditive<double>("Velocity", StateScope.Render);
    public static readonly StateKind<double> QuarterNoteDurationPower = CreateAdditive<double>("QuarterNoteDurationPower", StateScope.Render);
    public static readonly StateKind<double> NextNoteDurationFactor = CreateAdditive<double>("NextNoteDurationFactor", StateScope.Render);
    // which of a drum's sounds its walk picks, as every layer's step of it, a fraction of the sounds the drum has, which
    // Render rounds to whole sounds each and adds, so that a layer moves every note under it alike (README)
    public static readonly StateKind<ImmutableArray<double>> ArticulationOffset = CreateCollection<double>("ArticulationOffset", StateScope.Render);
    public static readonly StateKind<int> KeyOffset = CreateAdditive<int>("KeyOffset", StateScope.Render, isShared: true);
    public static readonly StateKind<int> OctaveOffset = CreateAdditive<int>("OctaveOffset", StateScope.Render);
    // a chord's shape, the heights of its notes above its root: one value, set by one layer, whose kind would join two
    // layers' shapes into one (SingleValuedListsTest); a layer that replaces it, as an ending's home chord does, takes
    // the note's out first, and should one ever lean on a deeper layer's, it would be a kind the lowest layer sets
    public static readonly StateKind<ImmutableArray<double>> ChordNotePitchOffsets = CreateCollection<double>("ChordNotePitchOffsets", StateScope.Render);
    // 1 for a chord whose layout is what it is, which Render moves only by whole octaves
    public static readonly StateKind<int> ChordVoicingFixed = CreateAdditive<int>("ChordVoicingFixed", StateScope.Render);
    // how smoothly a track's chords move, from 0, the chord's shape sliding with the root, to 1, every note moving as
    // little as it can
    public static readonly StateKind<double> VoiceLeading = CreateAdditive<double>("VoiceLeading", StateScope.Render);
    // a bar whose first chord plays as drawn, in its own register, and not led from the chord before; each such bar
    // has its own number, so that bars in a row are told apart
    public static readonly StateKind<int> ChordVoicingReset = CreateAdditive<int>("ChordVoicingReset", StateScope.Render);
    // how a bar's last bass note leads into the next chord (a ChordApproach), and what its first note of a new chord
    // plays (a ChordArrival)
    public static readonly StateKind<int> ChordApproach = CreateAdditive<int>("ChordApproach", StateScope.Render);
    public static readonly StateKind<int> ChordArrival = CreateAdditive<int>("ChordArrival", StateScope.Render);
    // a note's scale step above its chord's root, set outright, as the melody's notes are placed where they are made
    public static readonly StateKind<int> ScaleStep = CreateAdditive<int>("ScaleStep", StateScope.Render);
    // which of a drum's sounds a note plays, counted from 1, such as a fill's high tom or a landing's crash, over the
    // walk of its articulation; 0 for the walk's
    public static readonly StateKind<int> ArticulationIndex = CreateAdditive<int>("ArticulationIndex", StateScope.Render);
    // the sound a drum that strikes plays steadily, as its index among the drum's sounds, set by the lowest layer, such
    // as a bar's over its section's and its song's
    public static readonly StateKind<LayerValue<int>> DrumStroke = CreateLowestLayerWins<int>("DrumStroke", StateScope.Render);
    // a note's length set outright, in beats, over the gap to the next note, such as a phrase's last note; 0 for none
    public static readonly StateKind<double> HeldDuration = CreateAdditive<double>("HeldDuration", StateScope.Render);
    // the chord's root, and which of its notes the bass plays, as every layer's step of them, such as the progression's
    // and the walk's: fractions of the scale's steps and of the chord's notes, which Render rounds to whole steps each
    // and adds, so that a layer moves every note under it alike, where a sum rounded once would move one note and not
    // the next (README); none is a step of 0. How a track plays its chord is its role's, not an offset's
    public static readonly StateKind<ImmutableArray<double>> ChordRootNoteOffset = CreateCollection<double>("ChordRootOffset", StateScope.Render);
    public static readonly StateKind<ImmutableArray<double>> ChordNoteOffset = CreateCollection<double>("ChordNoteOffset", StateScope.Render);
    // the scale, as its notes' semitones above its root: one value, set by one layer, a section's, whose kind would join
    // two layers' scales into one of fourteen notes (SingleValuedListsTest)
    public static readonly StateKind<ImmutableArray<int>> ScaleOffsets = CreateCollection<int>("ScaleOffsets", StateScope.Render, isShared: true);
    // scale steps raised a semitone each time they are listed, such as the seventh on a cadence
    public static readonly StateKind<ImmutableArray<int>> RaisedScaleSteps = CreateCollection<int>("RaisedScaleSteps", StateScope.Render, isShared: true);
    public static readonly StateKind<double> Tempo = CreateMultiplicative<double>("Tempo", StateScope.Render, isShared: true);

    // how far the whole band is faded in, from 1, as loud as it plays, to 0, silent, which the song's MIDI plays as every
    // channel's expression
    public static readonly StateKind<double> Fade = CreateMultiplicative<double>("Fade", StateScope.Render, isShared: true);

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

    /// <summary>
    ///     A kind that the lowest layer that sets it decides, such as a note's over its bar pattern's, its section's and its
    ///     song's: every value carries the depth of the layer that set it (<see cref="StateDepths" />), and the deepest
    ///     wins, whatever order the layers merge in. Two layers of the same depth that set different values are an error.
    /// </summary>
    public static StateKind<LayerValue<T>> CreateLowestLayerWins<T>(
        string name,
        StateScope scope = StateScope.Composition,
        bool isShared = false
    )
        where T : notnull
    {
        return new StateKind<LayerValue<T>>(
            name,
            LayerValue<T>.Unset,
            (v1, v2) => v1.Equals(v2),
            values =>
            {
                var set = values.Where(x => x.IsSet).ToArray();
                if (set.Length == 0)
                    return LayerValue<T>.Unset;

                var deepest = set.MaxBy(x => x.Depth);
                if (set.Any(x => x.Depth == deepest.Depth && !x.Equals(deepest)))
                    throw new InvalidOperationException($"{name} is set to different values by two layers of depth {deepest.Depth}.");
                return deepest;
            },
            scope: scope,
            isShared: isShared
        );
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

/// <summary>The depths of the layers, from the song's down to a note's, by which a kind the lowest layer sets goes.</summary>
public static class StateDepths
{
    public const int Song = 0;
    public const int Section = 1;
    public const int BarPattern = 2;
    public const int Note = 3;
}

/// <summary>A value as a layer sets it, with the layer's depth (<see cref="StateDepths" />); unset below 0.</summary>
public readonly record struct LayerValue<T>(int Depth, T Value)
    where T : notnull
{
    public static LayerValue<T> Unset { get; } = new(-1, default!);

    public bool IsSet => Depth >= 0;
}
