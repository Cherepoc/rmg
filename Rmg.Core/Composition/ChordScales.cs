using System.Collections.Immutable;
using Rmg.Core.Events;
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
    Dominant,

    /// <summary>A mode on the chord's own root that keeps its triad, such as Lydian over a major chord or melodic minor over a minor one.</summary>
    Own,

    /// <summary>A minor chord a third from the home turned major, Mixolydian on its root, moving on as it likes: a film's or a pop song's III or VI.</summary>
    Mediant,

    /// <summary>A major chord a third from the home turned minor, Aeolian on its root: a black metal band's ♭vi or ♭iii.</summary>
    MinorMediant
}

/// <summary>
///     The scale a chord sounds in: the section's, with its steps raised or lowered a semitone, which every note over the
///     chord takes, the chord's own, the bass's and the lines' (<see cref="Realizer.GetChord" />).
/// </summary>
/// <param name="Steps">The scale's steps, from its first note, raised or lowered.</param>
public sealed record ChordScale(ChordScaleKind Kind, StepAlterations Steps)
{
    public static ChordScale Section { get; } = new(ChordScaleKind.Section, StepAlterations.None);
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

    /// <summary>
    ///     The chance a chord before a root a fifth below it is that root's dominant: its shape as drawn, in Mixolydian on
    ///     its root, so that a triad is major, a seventh a dominant seventh and a suspended one a suspended dominant, and only
    ///     a wild song's stranger shapes, wild already, lose its pull.
    /// </summary>
    public static ByConvention Applied { get; } = new(0, 0.35, 0.35);

    /// <summary>The chance a chord but the home is in the section's borrowed mode.</summary>
    public static ByConvention Borrowed { get; } = new(0, 0.12, 0.12);

    /// <summary>
    ///     The chance a section plays every chord but its home as its own dominant, the blues' I7, IV7 and V7, the home's
    ///     its scale's, I7 in Mixolydian and i7 in Dorian: a genre's alone, which the songs never play.
    /// </summary>
    public static ByConvention Dominant { get; } = new(0, 0, 0);

    /// <summary>
    ///     The chance a minor chord a third from the home, iii or vi, turns major, a chromatic mediant, whatever follows it,
    ///     as an applied dominant does only before a root a fifth below: never at the plain end, a convention by the middle.
    /// </summary>
    public static ByConvention Mediant { get; } = new(0, 0.1, 0.1);

    /// <summary>
    ///     The chance a major chord a third from the home, ♭III or ♭VI of a minor key, turns minor, its third lowered, as a
    ///     black metal band's chords move in minor triads: a genre's that the songs never play, drawn only where it gives a chance.
    /// </summary>
    public static ByConvention MinorMediant { get; } = new(0, 0, 0);

    /// <summary>The minor modes a minor mediant takes, the fewest steps moved first: melodic minor, Dorian, Aeolian.</summary>
    private static readonly Scale[] MinorModes = [Scales.MelodicMinor, Scales.Dorian, Scales.NaturalMinor];

    /// <summary>The chance a chord but the home sounds in a mode of its own on its root, by the progression facet: a wild song's alone.</summary>
    public static ByConvention Own { get; } = new(0, 0, 0.3);

    /// <summary>
    ///     The modes a chord may take on its own root, and how likely each is by the progression facet: Lydian, Mixolydian
    ///     and Dorian, a note from a major or a minor chord's plainest scale, at either end, and melodic minor's and
    ///     Phrygian's stranger colours toward the wild end alone, where the five are alike.
    /// </summary>
    public static ImmutableArray<(Scale Mode, ByConvention Weight)> OwnModes { get; } =
    [
        (Scales.Lydian, new ByConvention(1, 0.3, 0.2)),
        (Scales.Mixolydian, new ByConvention(1, 0.3, 0.2)),
        (Scales.Dorian, new ByConvention(1, 0.3, 0.2)),
        (Scales.MelodicMinor, new ByConvention(0, 0.07, 0.2)),
        (Scales.Phrygian, new ByConvention(0, 0.03, 0.2))
    ];

    /// <summary>
    ///     A mode among those given, by the facet and a genre's pool (<see cref="GenreChoices.OwnMode" />), from one draw
    ///     whichever it is, so that the draws are as many; none where none is given or the pool keeps none of them.
    /// </summary>
    private static Scale? PickMode(IGenerationContext context, IReadOnlySet<Scale> modes, double unconventionality, Genre genre)
    {
        var pool = genre.Pool<Scale>(GenreChoices.OwnMode, x => GenreChoices.Name(x.Name));
        (Scale, ByConvention)[] options = [..OwnModes.Where(x => modes.Contains(x.Mode) && (pool is null || pool(x.Mode) is not null))];
        if (options.Length == 0)
        {
            context.GenerateDouble();
            return null;
        }

        return context.Pick(pool is null ? ByConvention.WeighFilled(options, unconventionality) : ByConvention.Weigh(options, unconventionality, pool));
    }

    /// <summary>How many of its notes the most a mode borrowed differs from the section's scale on its home.</summary>
    public const int BorrowedDistance = 3;

    /// <summary>
    ///     Every chord's scale, in the progression's order: the cadence's raised seventh where it has one, and the others'
    ///     drawn by the progression facet, the borrowed mode drawn once for the section among the scales on its home
    ///     close to its own, as likely as a song is in them, or the genre's where it names its scales, the closer the likelier; a section of dominants every chord but
    ///     the home Mixolydian, where its scale lets it; the chances a genre's where it names them.
    /// </summary>
    /// <param name="dominants">Whether the section plays every chord but its home as its own dominant (<see cref="Dominant" />).</param>
    /// <param name="progression">The chords' roots, in steps above the home.</param>
    /// <param name="cadenceRaised">The step the cadence raises, none for a cadence that keeps the scale.</param>
    /// <param name="ownContext">The sequence a chord's own scale is drawn from, apart from the rest, so that a song that plays none draws as it did.</param>
    /// <param name="mediantContext">The sequence a chromatic mediant is drawn from, apart from the rest.</param>
    internal static ImmutableArray<ChordScale> Draw(IGenerationContext context, IGenerationContext ownContext, IGenerationContext mediantContext,
        IGenerationContext minorMediantContext, Scale scale, int home,
        ImmutableArray<int> progression, int? cadenceRaised, bool dominants, double unconventionality, Genre genre)
    {
        var mediant = Mediant.At(unconventionality, genre.Chance(GenreChoices.ChordScales, "mediant"));
        var minorMediant = MinorMediant.At(unconventionality, genre.Chance(GenreChoices.ChordScales, "minorMediant"));
        var own = Own.At(unconventionality, genre.Chance(GenreChoices.ChordScales, "own"));
        var applied = Applied.At(unconventionality, genre.Chance(GenreChoices.ChordScales, "applied"));
        var borrowing = Borrowed.At(unconventionality, genre.Chance(GenreChoices.ChordScales, "borrowed"));
        var onHome = Rotate(scale.Offsets, home);
        // the parallels a genre's scales allow, as it weighs them where it names them, the songs' weights otherwise
        var pool = genre.Pool<Scale>(GenreChoices.Scale, x => GenreChoices.Name(x.Name));
        int Distance(Scale x) => x.Offsets.Zip(onHome).Count(y => y.First != y.Second);
        var near = Scales.All.Where(x => Distance(x) is > 0 and <= BorrowedDistance && (pool is null || pool(x) is not null)).Select(x => (x, x.Weight)).ToArray();
        var parallels = (pool is null || near.Length == 0 ? [..near.Select(x => new Weighted<Scale>(x.Weight.At(unconventionality), x.x))] : ByConvention.Weigh(near, unconventionality, pool))
            .Select(x => x with { Weight = x.Weight * Math.Pow(Scales.Closeness, Distance(x.Value) - 1) })
            .Where(x => x.Weight > 0)
            .ToArray();
        var borrowed = parallels.Length > 0 ? context.Pick([..parallels]) : null;

        // a mode on the chord's root that changes something but keeps its triad, the lines over it taking its colour
        ChordScale? OwnScale(int root)
        {
            var modes = OwnModes.Select(x => (x.Mode, Scale: Toward(scale.Offsets, home + root, x.Mode.Offsets, ChordScaleKind.Own)))
                .Where(x => x.Scale is not null && !new[] { 0, 2, 4 }.Any(step => x.Scale.Steps.Moves(Mod(home + root + step))))
                .ToDictionary(x => x.Mode, x => x.Scale!);
            return PickMode(ownContext, modes.Keys.ToHashSet(), unconventionality, genre) is { } mode ? modes[mode] : null;
        }

        // whether the chord before was made this one's dominant, which a borrowed scale lowering this one's root would leave
        // resolving a semitone off
        var resolvesHere = false;
        return
        [
            ..progression.Select((root, chord) =>
            {
                var resolving = resolvesHere;
                resolvesHere = false;
                // drawn for every chord, so that its draws are as many
                var ownsScale = ownContext.TestProbability(own);
                var ownScale = OwnScale(root);
                var mediates = mediantContext.TestProbability(mediant);
                // from a sequence of its own, drawn for every chord, so that its chance at 0 moves no other draw
                var mediatesMinor = minorMediantContext.TestProbability(minorMediant);
                var applies = context.TestProbability(applied);
                var borrows = context.TestProbability(borrowing);
                if (Mod(root) == 0)
                    return ChordScale.Section;
                // but a minor home's chord a fourth above, a minor blues' iv7, which a dominant would make IV7
                if (dominants && !(onHome[2] == 3 && Mod(root) == 3)
                    && Heard(Toward(scale.Offsets, home + root, Scales.Mixolydian.Offsets, ChordScaleKind.Dominant), home + root) is { } dominant)
                    return dominant;
                if (chord == progression.Length - 1 && cadenceRaised is { } raised)
                    return new ChordScale(ChordScaleKind.Cadence, StepAlterations.Of([raised], []));
                // the next chord's dominant, where it has a fifth to resolve to
                if (chord < progression.Length - 1 && Mod(progression[chord + 1] - root) == 3 && !IsDiminished(scale, home, progression[chord + 1], ChordScale.Section)
                    && applies
                    && Heard(Toward(scale.Offsets, home + root, Scales.Mixolydian.Offsets, ChordScaleKind.Applied), home + root) is { } dominantOfNext)
                {
                    resolvesHere = true;
                    return dominantOfNext;
                }
                // a minor chord a third from the home turned major, its third raised
                if (mediates && Mod(root) is 2 or 5
                    && Toward(scale.Offsets, home + root, Scales.Mixolydian.Offsets, ChordScaleKind.Mediant) is { } major && major.Steps.Moves(Mod(home + root + 2)))
                    return major;
                // a major chord a third from the home turned minor, its third lowered, by the minor mode that moves the fewest
                // steps, as a minor key's ♭VI would lower its tonic, which the key keeps
                if (mediatesMinor && Mod(root) is 2 or 5
                    && MinorModes.Select(x => Toward(scale.Offsets, home + root, x.Offsets, ChordScaleKind.MinorMediant)).FirstOrDefault(x => x is not null && x.Steps.Moves(Mod(home + root + 2))) is { } minor)
                    return minor;
                if (borrowed is not null && borrows
                    && Heard(Toward(scale.Offsets, home, borrowed.Offsets, ChordScaleKind.Borrowed), home + root) is { } lent
                    && !(resolving && lent.Steps.Moves(Mod(home + root))))
                    return lent;
                return ownsScale && ownScale is not null ? ownScale : ChordScale.Section;
            })
        ];
    }

    /// <summary>
    ///     Every planed chord's mode on the home, which its move by semitones carries to its root, drawn among the own modes
    ///     the section's scale is or reaches a semitone a step at most (<see cref="OwnModes" />), so that its quality is the
    ///     mode's, major in Lydian or Mixolydian, minor in Dorian, melodic minor or Phrygian; the first chord's the section's,
    ///     the home, and the last's where the section leads home.
    /// </summary>
    internal static ImmutableArray<ChordScale> Planed(IGenerationContext context, Scale scale, int home, int count, bool endsHome, double unconventionality, Genre genre)
    {
        var onHome = Rotate(scale.Offsets, home);
        var modes = OwnModes.Select(x => (x.Mode, Scale: x.Mode.Offsets.SequenceEqual(onHome) ? ChordScale.Section : Toward(scale.Offsets, home, x.Mode.Offsets, ChordScaleKind.Own)))
            .Where(x => x.Scale is not null).ToDictionary(x => x.Mode, x => x.Scale!);
        return
        [
            ..Enumerable.Range(0, count).Select(chord =>
            {
                // drawn for every chord, so that its draws are as many
                var mode = PickMode(context, modes.Keys.ToHashSet(), unconventionality, genre);
                return chord == 0 || endsHome && chord == count - 1 || mode is null ? ChordScale.Section : modes[mode];
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
        var steps = StepAlterations.Of(raised, lowered);
        return Realizer.TryAlterScaleSteps(scaleOffsets, steps) is null ? null : new ChordScale(kind, steps);
    }

    /// <summary>
    ///     A chord's scale where it moves one of the chord's own notes, its root, third, fifth or seventh, as the IV7's of a
    ///     blues; none where it moves only the notes between them, which the chord would not sound.
    /// </summary>
    /// <param name="root">The chord's root, in steps of the scale from its first note.</param>
    private static ChordScale? Heard(ChordScale? scale, int root)
    {
        int[] tones = [..new[] { 0, 2, 4, 6 }.Select(x => Mod(root + x))];
        return scale is not null && tones.Any(scale.Steps.Moves) ? scale : null;
    }

    /// <summary>The scale from one of its steps, as the semitones of its notes above it.</summary>
    private static ImmutableArray<int> Rotate(ImmutableArray<int> offsets, int step) =>
        [..Enumerable.Range(0, offsets.Length).Select(i => Mod(offsets[Mod(step + i, offsets.Length)] - offsets[Mod(step, offsets.Length)], Realizer.OctaveNoteCount))];

    private static int Mod(int value, int by = Scales.StepCount) => value.Mod(by);

    /// <summary>A step's pitch above the scale's tonic, in semitones, in its chord's scale, a step above the octave an octave higher.</summary>
    internal static int PitchOf(Scale scale, int step, ChordScale chordScale) =>
        scale.Offsets[Mod(step)] + chordScale.Steps.By(Mod(step)) + 12 * (int)Math.Floor(step / (double)Scales.StepCount);

    /// <summary>
    ///     Whether the chord on a step above the home is diminished in its scale, its fifth a tritone, which no dominant leads
    ///     into, as it has no fifth to resolve to.
    /// </summary>
    internal static bool IsDiminished(Scale scale, int home, int root, ChordScale chordScale) =>
        (PitchOf(scale, home + root + 4, chordScale) - PitchOf(scale, home + root, chordScale)).Mod(12) == 6;
}
