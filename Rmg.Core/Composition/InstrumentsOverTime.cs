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
    /// <summary>The chance a section switches a part to another instrument of its role.</summary>
    public static ByConvention SwitchChance { get; } = new(0, 0.015, 1);

    public static ImmutableArray<(ArticulationMode Mode, ByConvention Weight)> Modes { get; } =
    [
        (ArticulationMode.None, new ByConvention(1, 0.93, 1)),
        (ArticulationMode.Bars, new ByConvention(0, 0.04, 1)),
        (ArticulationMode.Accents, new ByConvention(0, 0.03, 1))
    ];

    /// <summary>The chance a bar plays the variant, where a section articulates by bars.</summary>
    public const double BarChance = 0.5;

    /// <summary>The share of a part's notes in a section, the loudest, that count as its accents.</summary>
    public const double AccentShare = 0.25;

    /// <summary>
    ///     The variants of an instrument, as General MIDI has them: a finger or picked bass slapped or picked, a guitar
    ///     muted or played in harmonics, strings in tremolo or pizzicato, a muted trumpet.
    /// </summary>
    public static ImmutableDictionary<int, ImmutableArray<int>> Variants { get; } = new Dictionary<int, ImmutableArray<int>>
    {
        [33] = [36, 37, 34],
        [34] = [33, 36],
        [26] = [28],
        [27] = [28, 31],
        [29] = [28, 31],
        [30] = [28, 31],
        [40] = [44, 45],
        [41] = [44, 45],
        [42] = [44, 45],
        [48] = [44, 45],
        [49] = [44, 45],
        [56] = [59]
    }.ToImmutableDictionary();

    /// <summary>The instruments of a role, which a section switches a part among.</summary>
    private static InstrumentRole? RoleOf(TrackRole role) => role switch
    {
        TrackRole.Chords => InstrumentRoles.Chords,
        TrackRole.Melody => InstrumentRoles.Melody,
        TrackRole.Bass => InstrumentRoles.Bass,
        TrackRole.Pad => InstrumentRoles.Pad,
        TrackRole.CounterMelody => InstrumentRoles.CounterMelody,
        TrackRole.Riff => InstrumentRoles.Riff,
        _ => null
    };

    /// <summary>
    ///     The song's notes with every pitched part's instrument over the song on them (<see cref="CompositionStateKinds.Program" />),
    ///     each section and part drawing from a sequence of its own.
    /// </summary>
    /// <param name="sound">Every section's sound facet, by its place among the song's sections.</param>
    /// <param name="streams">A part's sequence in a section, by the section and the part's track.</param>
    public static ImmutableSortedDictionary<int, EventTimeline<RealizedNote>> Apply(
        ImmutableSortedDictionary<int, EventTimeline<RealizedNote>> notes,
        ImmutableSortedDictionary<int, IInstrumentTrack> definitions,
        SongMap map,
        IReadOnlyList<double> sound,
        Func<int, int, IGenerationContext> streams
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
                var switches = context.TestProbability(SwitchChance.At(sound[index]));
                var role = RoleOf(pitched.Role);
                var switched = role is null ? pitched.InstrumentCode : role.Pick(context, pitched.InstrumentCode).Program;
                var program = switches ? switched : pitched.InstrumentCode;
                var mode = context.Pick(ByConvention.Weigh(Modes, sound[index]));
                var variants = Variants.GetValueOrDefault(program, []);
                var variant = variants.IsEmpty ? program : variants[context.GenerateInt(0, variants.Length)];
                var bars = Enumerable.Range(0, (int)Math.Round(span.Duration / map.Meter.BarDuration)).Select(_ => context.TestProbability(BarChance)).ToArray();

                var inSpan = notes[track].Select((x, i) => (Note: x, Index: i)).Where(x => x.Note.Position >= span.Start - 1e-9 && x.Note.Position < span.End - 1e-9).ToArray();
                var accent = inSpan.Length == 0 ? double.MaxValue : inSpan.Select(x => x.Note.Value.Velocity).OrderDescending().ElementAt((int)(inSpan.Length * AccentShare));
                if (switches || mode != ArticulationMode.None && !variants.IsEmpty)
                    StateTrace.Record(TracePoints.InstrumentOverTime, track, span.SectionId, 0, StateMap.Default, 0, $"{program} {mode} {variant}", (index, pitched.Role, program, variants.IsEmpty ? ArticulationMode.None : mode, variant));
                foreach (var (note, i) in inSpan)
                {
                    var bar = Math.Min(bars.Length - 1, (int)Math.Floor((note.Position - span.Start) / map.Meter.BarDuration + 1e-9));
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
                notes[track].Select((x, i) => programs.TryGetValue(i, out var program) && program != pitched.InstrumentCode
                    ? (x.Value with { State = x.Value.State.With(CompositionStateKinds.Program, program + 1) }).ToTimelineItem(x.Position)
                    : x)
            );
        }

        return result.ToImmutable();
    }
}
