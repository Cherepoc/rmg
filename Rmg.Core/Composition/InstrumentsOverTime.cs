using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>How a part's instrument plays its articulation in a section.</summary>
public enum ArticulationMode
{
    /// <summary>Its own sound throughout.</summary>
    None,

    /// <summary>A variant of it in some bars, such as a bar of muted guitar.</summary>
    Bars,

    /// <summary>A variant of it on its accented notes, such as a bass slapped on its accents.</summary>
    Accents
}

/// <summary>
///     A part's instrument over the song, which only its program number changes, the notes as they are: a section switches
///     a part to another instrument of its role now and then, as a chorus's strings take over from a verse's piano, and a
///     part plays a variant of its instrument, an articulation General MIDI has as an instrument of its own, in some bars or
///     on its accents, as a bass is slapped or a guitar muted. By the sound facet: the plainest songs play every part in
///     its own sound throughout, the wildest switch every part in every section and articulate every way alike.
/// </summary>
internal static class InstrumentsOverTime
{
    /// <summary>The program a note plays: one it switches to, kept as its General MIDI number + 1, or the part's own where none.</summary>
    public static int ProgramOf(StateMap state, int own) => state.GetStateValue(CompositionStateKinds.Program) is var stated and > 0 ? stated - 1 : own;

    /// <summary>The chance a section switches a part to another instrument of its role.</summary>
    public static ByConvention SwitchChance { get; } = new(0, 0.015, 1);

    /// <summary>
    ///     The chance a switch keeps to the instrument's family, the same player's other sound, rather than hand the part to
    ///     another of its role's: the plainer, the likelier, as a chorus's strings taking over from a verse's piano is the
    ///     wilder.
    /// </summary>
    public static ByConvention FamilyChance { get; } = new(1, 0.6, 0.2);

    /// <summary>
    ///     The instruments one player moves between, by General MIDI's groups of eight, split where a group holds other
    ///     players or sounds: the guitars acoustic, clean and driven, the basses acoustic, electric and synth, the organs
    ///     and the reed organs; an instrument in none is a family of its own.
    /// </summary>
    public static ImmutableArray<ImmutableHashSet<int>> Families { get; } =
    [
        [0, 1, 2, 3], [4, 5, 7], [8, 9, 10, 11, 12, 13, 14, 15], [16, 17, 18], [20, 21, 22, 23], [24, 25], [26, 27, 28], [29, 30, 31],
        [32, 43], [33, 34, 35, 36, 37], [38, 39], [40, 41, 42, 44, 45, 48, 49, 50, 51], [52, 53, 54], [56, 57, 58, 59, 60, 61, 62, 63],
        [64, 65, 66, 67], [68, 69, 70, 71], [72, 73, 74, 75, 76, 77, 78, 79], [80, 81, 82, 83, 84, 85, 86, 87], [88, 89, 90, 91, 92, 93, 94, 95],
        [96, 97, 98, 99, 100, 101, 102, 103], [104, 105, 106, 107, 108], [109, 111]
    ];

    /// <summary>The instruments of a program's family, itself alone where it has none.</summary>
    public static ImmutableHashSet<int> FamilyOf(int program) => Families.FirstOrDefault(x => x.Contains(program)) ?? [program];

    public static ImmutableArray<(ArticulationMode Mode, ByConvention Weight)> Modes { get; } =
    [
        (ArticulationMode.None, new ByConvention(1, 0.93, 1)),
        (ArticulationMode.Bars, new ByConvention(0, 0.04, 1)),
        (ArticulationMode.Accents, new ByConvention(0, 0.03, 1))
    ];

    /// <summary>The chance a bar plays the variant, where a section articulates by bars.</summary>
    public const double BarChance = 0.5;

    /// <summary>
    ///     The share of a part's notes in a section, the loudest, that count as its accents: those louder than the note at
    ///     that share, so that a part playing every note alike, a chug, has none.
    /// </summary>
    public const double AccentShare = 0.25;

    /// <summary>
    ///     The variants of an instrument, as General MIDI has them, each one the same player's on the same rig: a finger or
    ///     picked bass slapped or picked, a clean guitar muted and a muted one clean, strings in tremolo or pizzicato, a muted
    ///     trumpet; a driven guitar none, its pinch harmonics a held note's highlight (<see cref="Expression.Pinches" />)
    ///     and General MIDI's muted guitar a clean one's.
    /// </summary>
    public static ImmutableDictionary<int, ImmutableArray<int>> Variants { get; } = new Dictionary<int, ImmutableArray<int>>
    {
        [33] = [36, 37, 34],
        [34] = [33, 36],
        [26] = [28],
        [27] = [28],
        [28] = [27],
        [40] = [44, 45],
        [41] = [44, 45],
        [42] = [44, 45],
        [48] = [44, 45],
        [49] = [44, 45],
        [56] = [59]
    }.ToImmutableDictionary();

    /// <summary>The instruments of a role, which a section switches a part among.</summary>
    internal static InstrumentRole? RoleOf(TrackRole role) => role switch
    {
        TrackRole.Chords => InstrumentRoles.Chords,
        TrackRole.Melody => InstrumentRoles.Melody,
        TrackRole.Bass => InstrumentRoles.Bass,
        TrackRole.Pad => InstrumentRoles.Pad,
        TrackRole.CounterMelody => InstrumentRoles.CounterMelody,
        TrackRole.Riff or TrackRole.RiffTwin => InstrumentRoles.Riff,
        TrackRole.Rhythm => InstrumentRoles.Rhythm,
        TrackRole.Solo => InstrumentRoles.Solo,
        _ => null
    };

    /// <summary>
    ///     An instrument's weight in a genre's pool of a part's instruments at the sound facet, drawn alike at every
    ///     conventionality as the songs draw them (<see cref="ByConvention.Within{T}" />); none where the genre names none.
    /// </summary>
    public static Func<RoleInstrument, double>? PoolOf(TrackRole part, double sound, Genre genre) =>
        genre.Pool<RoleInstrument>(GenreChoices.Instruments[part], x => GenreChoices.Name(x.Name)) is { } pool && RoleOf(part) is { } role
            ? ByConvention.Within(role.Everything, sound, pool)
            : null;

    /// <summary>
    ///     The song's notes with every pitched part's instrument over the song on them (<see cref="CompositionStateKinds.Program" />),
    ///     each section and part drawing from a sequence of its own.
    /// </summary>
    /// <param name="sound">Every section's sound facet, by its place among the song's sections.</param>
    /// <param name="streams">A part's sequence in a section, by the section and the part's track, a riff's twin's its riff's.</param>
    /// <param name="families">A part's draw of whether a switch keeps to its family, by the section and the part's track.</param>
    /// <param name="genre">
    ///     The genre whose pools of the roles' instruments a switch keeps to, and an articulation too, a variant, where the
    ///     pool leaves it out; one the role never lists is the instrument's own.
    /// </param>
    public static ImmutableSortedDictionary<int, EventTimeline<RealizedNote>> Apply(
        ImmutableSortedDictionary<int, EventTimeline<RealizedNote>> notes,
        ImmutableSortedDictionary<int, IInstrumentTrack> definitions,
        SongMap map,
        IReadOnlyList<double> sound,
        Func<int, int, IGenerationContext> streams,
        Func<int, int, IGenerationContext> families,
        Genre genre
    )
    {
        var result = notes.ToBuilder();
        foreach (var (track, definition) in definitions.Where(x => x.Value is PitchInstrumentTrack && notes.ContainsKey(x.Key)))
        {
            var pitched = (PitchInstrumentTrack)definition;
            var programs = new Dictionary<int, int>();
            foreach (var (span, index) in map.Sections.Select((x, i) => (x, i)))
            {
                // a section plays as it did the time before, so that a chorus comes back in the instrument it switched to
                var context = streams(span.SectionId, track);
                var switches = context.TestProbability(SwitchChance.At(sound[index], genre.Chance(GenreChoices.InstrumentSwitches, "sections")));
                var role = RoleOf(pitched.Role);
                var pool = PoolOf(pitched.Role.Source(), sound[index], genre);
                var weight = pool ?? role?.Own(sound[index]);
                double Weight(RoleInstrument instrument) => weight!(instrument);
                // within the family where it keeps to it and the role has another of it, or to any other of the role's
                var keeps = families(span.SectionId, track).TestProbability(FamilyChance.At(sound[index]));
                var family = FamilyOf(pitched.InstrumentCode);
                var kin = role?.Of(pool).Where(x => x.Program != pitched.InstrumentCode && family.Contains(x.Program) && Weight(x) > 0).ToArray() ?? [];
                var switched = role is null ? pitched.InstrumentCode
                    : keeps && kin.Length > 0 ? context.Pick([..kin.Select(x => new Weighted<RoleInstrument>(Weight(x), x))]).Program
                    : role.Pick(context, sound[index], pool, pitched.InstrumentCode).Program;
                var program = switches ? switched : pitched.InstrumentCode;
                var mode = context.Pick(ByConvention.Weigh(Modes, sound[index]));
                // a variant the genre's pool of the part leaves out, a slapped bass in a metal band's, never plays, drawn all the
                // same; one the role never lists, a guitar's muted strings or a violin's pizzicato, is the player's own
                var drawnVariants = Variants.GetValueOrDefault(program, []);
                var drawnVariant = drawnVariants.IsEmpty ? program : drawnVariants[context.GenerateInt(0, drawnVariants.Length)];
                var leftOut = pool is not null && role?.Instruments.FirstOrDefault(x => x.Program == drawnVariant) is { } listed && !(Weight(listed) > 0);
                var (variants, variant) = leftOut ? (ImmutableArray<int>.Empty, program) : (drawnVariants, drawnVariant);
                var bars = Enumerable.Range(0, span.BarCount).Select(_ => context.TestProbability(BarChance)).ToArray();

                // the notes it plays, an intro made of it and the ending after the last among them, so that the final chord is
                // played on the instrument the song ended in
                var inSpan = notes[track].Select((x, i) => (Note: x, Index: i)).Where(x => map.PlayAt(x.Note.Position)?.Index == index).ToArray();
                var accent = inSpan.Length == 0 ? double.MaxValue : inSpan.Select(x => x.Note.Value.Velocity).OrderDescending().ElementAt((int)(inSpan.Length * AccentShare));
                if (switches || mode != ArticulationMode.None && !variants.IsEmpty)
                    StateTrace.RecordSection(TracePoints.InstrumentOverTime, track, span.SectionId, $"{program} {mode} {variant}", (index, pitched.Role, program, variants.IsEmpty ? ArticulationMode.None : mode, variant));
                foreach (var (note, i) in inSpan)
                {
                    var bar = Math.Clamp(span.BarOf(note.Position + map.PlayAt(note.Position)!.Value.Offset), 0, bars.Length - 1);
                    var articulated = !variants.IsEmpty && mode switch
                    {
                        ArticulationMode.Bars => bars[bar],
                        ArticulationMode.Accents => note.Value.Velocity > accent,
                        _ => false
                    };
                    programs[i] = articulated ? variant : program;
                }
            }

            if (programs.All(x => x.Value == pitched.InstrumentCode))
                continue;

            result[track] = EventTimeline.Create(
                notes[track].Duration,
                // but a note in a section's form, calm or heavy (SectionForm), which keeps it, and a soloist's, which plays its
                // solo on the instrument its line was planned for, its own
                notes[track].Select((x, i) => programs.TryGetValue(i, out var program) && program != pitched.InstrumentCode && x.Value.State.GetStateValue(CompositionStateKinds.Program) == 0
                                              && x.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 0
                    ? (x.Value with { State = x.Value.State.With(CompositionStateKinds.Program, program + 1) }).ToTimelineItem(x.Position)
                    : x)
            );
        }

        // a note in another part's sound, as twin leads are, in the program that part plays there, its own notes' or its own
        var playing = definitions.Where(x => x.Value is PitchInstrumentTrack).ToDictionary(x => x.Value.Role, x => x.Key);
        foreach (var (track, timeline) in result.ToArray())
        {
            if (!timeline.Any(x => x.Value.State.GetStateValue(CompositionStateKinds.SoundsAs) > 0))
                continue;

            result[track] = EventTimeline.Create(timeline.Duration, timeline.Select(x =>
            {
                var role = (TrackRole)(x.Value.State.GetStateValue(CompositionStateKinds.SoundsAs) - 1);
                if (role < 0 || !playing.TryGetValue(role, out var other) || !result.TryGetValue(other, out var notes))
                    return x;

                var near = notes.LastOrDefault(n => n.Position <= x.Position + 1e-9, notes.FirstOrDefault());
                var stated = near.Value?.State.GetStateValue(CompositionStateKinds.Program) ?? 0;
                var program = stated > 0 ? stated - 1 : ((PitchInstrumentTrack)definitions[other]).InstrumentCode;
                return (x.Value with { State = x.Value.State.With(CompositionStateKinds.Program, program + 1) }).ToTimelineItem(x.Position);
            }));
        }

        return result.ToImmutable();
    }
}
