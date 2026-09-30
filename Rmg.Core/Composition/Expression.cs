using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>How a line's note bends, as a guitar's string or a voice does.</summary>
public enum BendKind
{
    None,

    /// <summary>From a step below up into the note.</summary>
    Scoop,

    /// <summary>Up a step and back.</summary>
    BendAndRelease,

    /// <summary>From the note before, sliding into it.</summary>
    Slide,

    /// <summary>Falling away at its end.</summary>
    FallOff
}

/// <summary>
///     How the parts are played, beyond their notes: a line's vibrato and bends, as pitch bend, where its instrument can
///     bend, a single line at a time; a held chord's swell and an instrument's tremolo, as its expression; a pad's sweep
///     from side to side; a lead's echo, its notes repeated quieter after it, as a delay pedal plays; and every part's
///     reverb and chorus. Decided once a song's notes are: a part's sound for the whole song, its vibrato, swell, tremolo,
///     sweep, echo, reverb and chorus, from a sequence of the part's own by the song's sound facet, and a note's bend from
///     its section's, by the section's: the plainest songs a vibrato on the long notes, a swell on the pad, and a moderate
///     reverb, and the wildest every effect everywhere.
/// </summary>
internal static class Expression
{
    /// <summary>Whether an instrument bends and sings a vibrato: guitars, strings, voices, brass, reeds, pipes, synth leads, a fretless or synth bass.</summary>
    public static bool Bends(int program) => program is >= 24 and <= 31 or >= 40 and <= 44 or >= 48 and <= 87 or 35 or 38 or 39;

    /// <summary>Whether an instrument sounds with a chorus the classic way: an electric piano, a clean guitar, a synth pad.</summary>
    private static bool ChorusesClassically(int program) => program is 4 or 5 or 27 or >= 88 and <= 95;

    /// <summary>Whether an instrument may take a chorus: pianos, guitars, strings, leads and pads.</summary>
    private static bool Choruses(int program) => program is >= 0 and <= 7 or >= 24 and <= 31 or >= 48 and <= 51 or >= 80 and <= 95;

    /// <summary>A held note's vibrato, in cents at its widest.</summary>
    public static ImmutableArray<(int Cents, ByConvention Weight)> Vibratos { get; } =
    [
        (0, new ByConvention(0, 0.3, 1)),
        (15, new ByConvention(1, 0.5, 1)),
        (35, new ByConvention(0, 0.2, 1))
    ];

    /// <summary>The chance a line's note bends, three times it in a solo.</summary>
    public static ByConvention BendChance { get; } = new(0, 0.03, 0.5);

    public static ImmutableArray<(BendKind Kind, ByConvention Weight)> BendKinds { get; } =
    [
        (BendKind.Scoop, new ByConvention(1, 0.4, 1)),
        (BendKind.FallOff, new ByConvention(1, 0.25, 1)),
        (BendKind.Slide, new ByConvention(1, 0.2, 1)),
        (BendKind.BendAndRelease, new ByConvention(1, 0.15, 1))
    ];

    /// <summary>The chance a part's held notes swell in, a pad's plainly, any other's now and then.</summary>
    public static ByConvention PadSwellChance { get; } = new(0.3, 0.2, 1);

    public static ByConvention SwellChance { get; } = new(0, 0.03, 1);

    /// <summary>The chance a part plays with a tremolo, a guitar's, an electric piano's or an organ's the likelier.</summary>
    public static ByConvention TremoloChance { get; } = new(0, 0.04, 1);

    /// <summary>The chance a part sweeps from side to side, a pad's, a synth's or an organ's the likelier.</summary>
    public static ByConvention AutoPanChance { get; } = new(0, 0.05, 1);

    /// <summary>The chance a line echoes, a lead's the likelier.</summary>
    public static ByConvention EchoChance { get; } = new(0, 0.1, 1);

    /// <summary>The delays an echo repeats after, in beats: a dotted 8th, a quarter, an 8th.</summary>
    public static ImmutableArray<double> EchoDelays { get; } = [0.75, 1, 0.5];

    /// <summary>A part's reverb, from 0 to 127, as a mix sets it by role, and how far a section strays from it, by the sound facet.</summary>
    public static int Reverb(TrackRole role) => role switch
    {
        TrackRole.Drum => 40,
        TrackRole.Bass => 25,
        TrackRole.Pad => 80,
        TrackRole.Chords => 55,
        TrackRole.Riff or TrackRole.RiffTwin => 45,
        TrackRole.Rhythm => 50,
        _ => 60
    };

    public static ByConvention ReverbSpread { get; } = new(0, 0.1, 1);

    /// <summary>The chance a part plays with a chorus, the classic ones the likelier.</summary>
    public static ByConvention ClassicChorusChance { get; } = new(0.5, 0.4, 1);

    public static ByConvention ChorusChance { get; } = new(0, 0.15, 1);

    /// <summary>
    ///     The song's notes with every part's expression on them (<see cref="CompositionStateKinds.Vibrato" /> and the
    ///     rest), each part in a section drawing from a sequence of its own, by the section, so that a section plays as it
    ///     did before.
    /// </summary>
    /// <param name="songSound">The song's sound facet, which a part's sound for the whole song leans by.</param>
    /// <param name="sound">Every section's sound facet, by its place among the song's sections.</param>
    /// <param name="partStreams">A part's sequence for the whole song, by its track.</param>
    /// <param name="streams">A part's sequence in a section, by the section and the part's track.</param>
    public static ImmutableSortedDictionary<int, EventTimeline<RealizedNote>> Apply(
        ImmutableSortedDictionary<int, EventTimeline<RealizedNote>> notes,
        ImmutableSortedDictionary<int, IInstrumentTrack> definitions,
        SongMap map,
        double songSound,
        IReadOnlyList<double> sound,
        Func<int, IGenerationContext> partStreams,
        Func<int, int, IGenerationContext> streams
    )
    {
        var result = notes.ToBuilder();
        foreach (var (track, timeline) in notes)
        {
            var definition = definitions[track];
            var role = definition.Role;
            var program = definition is PitchInstrumentTrack pitched ? pitched.InstrumentCode : -1;
            var states = new StateMap?[timeline.Count];
            // the part's sound for the whole song
            var part = partStreams(track);
            var vibrato = part.Pick(ByConvention.Weigh(Vibratos, songSound));
            var swells = part.TestProbability((role == TrackRole.Pad ? PadSwellChance : SwellChance).At(songSound));
            var tremolo = part.TestProbability(TremoloChance.At(songSound)) ? 30 + part.GenerateInt(0, 40) : 0;
            var sweeps = part.TestProbability(AutoPanChance.At(songSound)) ? 40 + part.GenerateInt(0, 60) : 0;
            var echoes = part.TestProbability(EchoChance.At(songSound));
            var delay = EchoDelays[part.GenerateInt(0, EchoDelays.Length)];
            var reverb = (int)Math.Round(Reverb(role) + ReverbSpread.At(songSound) * (part.GenerateDouble() * 127 - Reverb(role)));
            var choruses = part.TestProbability((ChorusesClassically(program) ? ClassicChorusChance : ChorusChance).At(songSound));
            var chorus = 40 + part.GenerateInt(0, 60);
            foreach (var (span, index) in map.Sections.Select((x, i) => (x, i)))
            {
                var context = streams(span.SectionId, track);
                var u = sound[index];
                var bendChance = BendChance.At(u);

                for (var i = 0; i < timeline.Count; i++)
                {
                    var note = timeline[i];
                    if (note.Position < span.Start - 1e-9 || note.Position >= span.End - 1e-9)
                        continue;

                    var noteProgram = note.Value.State.GetStateValue(CompositionStateKinds.Program) is var stated and > 0 ? stated - 1 : program;
                    var isLine = note.Value.Pitches.Length == 1 && !role.PlaysChords() && role is not (TrackRole.Pad or TrackRole.Drum)
                                 || note.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 1;
                    var bends = isLine && Bends(noteProgram);
                    var isSolo = note.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 1;
                    // every note draws whether it bends, and how, so that the draws stay as they are
                    var bendsHere = context.TestProbability(Math.Min(1, bendChance * (isSolo ? 3 : 1)));
                    var kind = context.Pick(ByConvention.Weigh(BendKinds, u));
                    var state = note.Value.State
                        .With(CompositionStateKinds.Reverb, reverb + 1)
                        .With(CompositionStateKinds.Chorus, choruses && Choruses(noteProgram) ? chorus : 0);
                    if (bends && note.Value.Duration >= 1)
                        state = state.With(CompositionStateKinds.Vibrato, vibrato);
                    if (bends && bendsHere)
                        state = state.With(CompositionStateKinds.Bend, (int)kind);
                    if (swells && note.Value.Duration >= 1)
                        state = state.With(CompositionStateKinds.Swell, 1);
                    if (role != TrackRole.Drum)
                        state = state.With(CompositionStateKinds.Tremolo, tremolo).With(CompositionStateKinds.AutoPan, sweeps);
                    if (echoes && isLine)
                        state = state.With(CompositionStateKinds.Echo, (int)Math.Round(delay * 4));
                    states[i] = state;
                }
            }

            result[track] = EventTimeline.Create(
                timeline.Duration,
                timeline.Select((x, i) => states[i] is { } state ? (x.Value with { State = state }).ToTimelineItem(x.Position) : x)
            );
        }

        return result.ToImmutable();
    }
}
