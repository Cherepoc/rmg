using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>Where a chord's own scale comes from.</summary>
public enum ChordScaleKind
{
    /// <summary>The section's scale, as it is.</summary>
    Section,

    /// <summary>The cadence's seventh raised, for a major chord on the fifth (<see cref="Progressions.GetCadenceRaisedStep" />).</summary>
    Cadence,

    /// <summary>Mixolydian on the chord's own root, which makes it the dominant of the next, a fifth below.</summary>
    Applied,

    /// <summary>A parallel mode on the section's home, such as Aeolian in a major song, which gives ♭VI, ♭VII and iv.</summary>
    Borrowed,

    /// <summary>Mixolydian on the chord's own root, as every chord but the home of a blues section is.</summary>
    Dominant
}

/// <summary>
///     The scale a chord sounds in: the section's, with the steps listed raised or lowered a semitone each, which every
///     note over the chord takes, the chord's own, the bass's and the lines' (<see cref="Realizer.GetChord" />).
/// </summary>
/// <param name="Raised">The scale's steps, from its first note, raised a semitone.</param>
/// <param name="Lowered">The scale's steps, from its first note, lowered a semitone.</param>
public sealed record ChordScale(ChordScaleKind Kind, ImmutableArray<int> Raised, ImmutableArray<int> Lowered)
{
    public static ChordScale Section { get; } = new(ChordScaleKind.Section, [], []);
}

/// <summary>
///     The chords of a section that leave its scale: a chord before a root a fifth below it now and then its dominant,
///     applied, and another chord but the home now and then in a mode borrowed from the home's parallels, the section's
///     scale turned toward it a semitone a step at most, a chord the turn cannot make keeping the section's. Both
///     conventions of every genre but the plainest songs, neither aptest at the wild end, whose sections change their
///     scales outright: never at the plain end, and as often at the wild end as in the middle.
/// </summary>
public static class ChordScales
{

    /// <summary>The chance a chord before a root a fifth below it is that root's dominant.</summary>
    public static ByConvention Applied { get; } = new(0, 0.35, 0.35);

    /// <summary>The chance a chord but the home is in the section's borrowed mode.</summary>
    public static ByConvention Borrowed { get; } = new(0, 0.12, 0.12);

    /// <summary>
    ///     The chance a section plays every chord but its home as its own dominant, the blues' I7, IV7 and V7, the home's
    ///     its scale's, I7 in Mixolydian and i7 in Dorian: a genre's alone, which the songs never play.
    /// </summary>
    public static ByConvention Dominant { get; } = new(0, 0, 0);

    /// <summary>How many of its notes the most a mode borrowed differs from the section's scale on its home.</summary>
    public const int BorrowedDistance = 3;

    /// <summary>
    ///     Every chord's scale, in the progression's order: the cadence's raised seventh where it has one, and the others'
    ///     drawn by the progression facet, the borrowed mode drawn once for the section among the scales on its home
    ///     close to its own, as likely as a song is in them, the closer the likelier; a section of dominants every chord but
    ///     the home Mixolydian, where its scale lets it; the chances a genre's where it names them.
    /// </summary>
    /// <param name="dominants">Whether the section plays every chord but its home as its own dominant (<see cref="Dominant" />).</param>
    /// <param name="progression">The chords' roots, in steps above the home.</param>
    /// <param name="cadenceRaised">The step the cadence raises, none for a cadence that keeps the scale.</param>
    internal static ImmutableArray<ChordScale> Draw(IGenerationContext context, Scale scale, int home, ImmutableArray<int> progression, int? cadenceRaised, bool dominants,
        double unconventionality, Genre genre)
    {
        var applied = Applied.At(unconventionality, genre.Chance(GenreChoices.ChordScales, "applied"));
        var borrowing = Borrowed.At(unconventionality, genre.Chance(GenreChoices.ChordScales, "borrowed"));
        var onHome = Rotate(scale.Offsets, home);
        var parallels = Scales.All
            .Select(x => (Scale: x, Distance: x.Offsets.Zip(onHome).Count(y => y.First != y.Second)))
            .Where(x => x.Distance is > 0 and <= BorrowedDistance)
            .Select(x => (x.Scale, Weight: x.Scale.Weight.At(unconventionality) * Math.Pow(Scales.Closeness, x.Distance - 1)))
            .Where(x => x.Weight > 0)
            .ToArray();
        var borrowed = parallels.Length > 0 ? context.Pick([..parallels.Select(x => new Weighted<Scale>(x.Weight, x.Scale))]) : null;

        return
        [
            ..progression.Select((root, chord) =>
            {
                if (Mod(root) == 0)
                    return ChordScale.Section;
                if (dominants && Toward(scale.Offsets, home + root, Scales.Mixolydian.Offsets, ChordScaleKind.Dominant) is { } dominant)
                    return dominant;
                if (chord == progression.Length - 1 && cadenceRaised is { } raised)
                    return new ChordScale(ChordScaleKind.Cadence, [raised], []);
                if (chord < progression.Length - 1 && Mod(progression[chord + 1] - root) == 3
                    && context.TestProbability(applied)
                    && Toward(scale.Offsets, home + root, Scales.Mixolydian.Offsets, ChordScaleKind.Applied) is { } dominantOfNext)
                    return dominantOfNext;
                if (borrowed is not null && context.TestProbability(borrowing)
                    && Toward(scale.Offsets, home, borrowed.Offsets, ChordScaleKind.Borrowed) is { } lent)
                    return lent;
                return ChordScale.Section;
            })
        ];
    }

    /// <summary>
    ///     The steps of the scale to raise and lower for the mode given to sound on one of its steps, none where that
    ///     takes more than a semitone a step, puts the scale out of order or changes nothing.
    /// </summary>
    internal static ChordScale? Toward(ImmutableArray<int> scaleOffsets, int step, ImmutableArray<int> mode, ChordScaleKind kind)
    {
        var length = scaleOffsets.Length;
        var on = scaleOffsets[Mod(step, length)];
        var raised = ImmutableArray.CreateBuilder<int>();
        var lowered = ImmutableArray.CreateBuilder<int>();
        for (var i = 0; i < length; i++)
        {
            var index = Mod(step + i, length);
            var change = Mod(on + mode[i] - scaleOffsets[index], Realizer.OctaveNoteCount);
            if (change == 1) raised.Add(index);
            else if (change == Realizer.OctaveNoteCount - 1) lowered.Add(index);
            else if (change != 0) return null;
        }

        if (raised.Count + lowered.Count == 0)
            return null;
        var altered = Realizer.TryAlterScaleSteps(scaleOffsets, raised.ToImmutable(), lowered.ToImmutable());
        return altered is null ? null : new ChordScale(kind, raised.ToImmutable(), lowered.ToImmutable());
    }

    /// <summary>The scale from one of its steps, as the semitones of its notes above it.</summary>
    private static ImmutableArray<int> Rotate(ImmutableArray<int> offsets, int step) =>
        [..Enumerable.Range(0, offsets.Length).Select(i => Mod(offsets[Mod(step + i, offsets.Length)] - offsets[Mod(step, offsets.Length)], Realizer.OctaveNoteCount))];

    private static int Mod(int value, int by = Scales.StepCount) => (value % by + by) % by;
}
