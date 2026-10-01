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

/// <summary>The strong effects, which move a part's sound about, a song playing one of them at most.</summary>
public enum StrongEffect
{
    None,
    Tremolo,
    Echo,
    Sweep
}

/// <summary>
///     How the parts are played, beyond their notes: a line's vibrato and bends, as pitch bend, where its instrument can
///     bend, a single line at a time; a held chord's swell and an instrument's tremolo, as its expression; a pad's sweep
///     from side to side; a lead's echo, its notes repeated quieter after it, as a delay pedal plays; and every part's
///     reverb and chorus. Decided once a song's notes are, by the song's sound facet: first which effects the song plays
///     at all, the gentle ones, a vibrato, a chorus, a swell and bends, each of its own, and of the strong ones, a tremolo,
///     an echo and a sweep, one at most, and the one part that plays it, the likelier one it suits, from a sequence of the
///     song's; then which of the gentle ones each part plays, together as they come, from a sequence of the part's; and a
///     note's bend from its section's, by the section's. The plainest songs play a reverb alone, and the wildest every
///     gentle effect on some of their parts, and one strong one on one.
/// </summary>
internal static class Expression
{
    /// <summary>Whether an instrument bends and sings a vibrato: guitars, strings, voices, brass, reeds, pipes, synth leads, a fretless or synth bass.</summary>
    public static bool Bends(int program) => program is >= 24 and <= 31 or >= 40 and <= 44 or >= 48 and <= 87 or 35 or 38 or 39;

    /// <summary>Whether an instrument sounds with a chorus the classic way: an electric piano, a clean guitar, a synth pad.</summary>
    private static bool ChorusesClassically(int program) => program is 4 or 5 or 27 or >= 88 and <= 95;

    /// <summary>Whether an instrument may take a chorus: pianos, guitars, strings, leads and pads.</summary>
    private static bool Choruses(int program) => program is >= 0 and <= 7 or >= 24 and <= 31 or >= 48 and <= 51 or >= 80 and <= 95;

    /// <summary>
    ///     Whether a strong effect suits a part, which plays it the likelier: an echo a lead's, a tremolo a guitar's, an
    ///     electric piano's or an organ's, a sweep a pad's, a synth's or an organ's.
    /// </summary>
    private static bool Suits(StrongEffect effect, TrackRole role, int program) => effect switch
    {
        StrongEffect.Echo => role == TrackRole.Melody,
        StrongEffect.Tremolo => program is 4 or 5 or >= 16 and <= 20 or >= 24 and <= 31,
        StrongEffect.Sweep => role == TrackRole.Pad || program is >= 16 and <= 20 or >= 80 and <= 95,
        _ => false
    };

    /// <summary>Whether a part plays lines, single notes one after another, rather than chords or drums.</summary>
    private static bool IsLineRole(TrackRole role) => !role.PlaysChords() && role is not (TrackRole.Pad or TrackRole.Drum);

    /// <summary>The chances a song plays each gentle effect at all, on whichever of its parts take it.</summary>
    public static ByConvention SongVibratoChance { get; } = new(0, 0.7, 1);

    public static ByConvention SongChorusChance { get; } = new(0, 0.5, 1);

    public static ByConvention SongSwellChance { get; } = new(0, 0.35, 1);

    public static ByConvention SongBendChance { get; } = new(0, 0.5, 1);

    /// <summary>The strong effect a song plays, if any, an echo the likeliest: none in a plain song, one in the wildest.</summary>
    public static ImmutableArray<(StrongEffect Effect, ByConvention Weight)> StrongEffects { get; } =
    [
        (StrongEffect.None, new ByConvention(1, 0.75, 0)),
        (StrongEffect.Echo, new ByConvention(0, 0.12, 1)),
        (StrongEffect.Tremolo, new ByConvention(0, 0.08, 0.8)),
        (StrongEffect.Sweep, new ByConvention(0, 0.05, 0.6))
    ];

    /// <summary>
    ///     How much likelier a part the song's strong effect suits is to be the one that plays it than any other that may:
    ///     an echo goes to a line, and a tremolo and a sweep to any pitched part.
    /// </summary>
    public const double SuitedStrongWeight = 4;

    /// <summary>The chance a part that can sings a vibrato where the song does, on its long notes.</summary>
    public static ByConvention VibratoChance { get; } = new(0, 0.6, 0.9);

    /// <summary>A held note's vibrato, in cents at its widest.</summary>
    public static ImmutableArray<(int Cents, ByConvention Weight)> Vibratos { get; } =
    [
        (15, new ByConvention(1, 0.7, 1)),
        (35, new ByConvention(0, 0.3, 1))
    ];

    /// <summary>The chance a line that can bends where the song does, and then a note's chance to, three times it in a solo.</summary>
    public static ByConvention PartBendChance { get; } = new(0, 0.6, 0.9);

    public static ByConvention BendChance { get; } = new(0, 0.03, 0.5);

    public static ImmutableArray<(BendKind Kind, ByConvention Weight)> BendKinds { get; } =
    [
        (BendKind.Scoop, new ByConvention(1, 0.4, 1)),
        (BendKind.FallOff, new ByConvention(1, 0.25, 1)),
        (BendKind.Slide, new ByConvention(1, 0.2, 1)),
        (BendKind.BendAndRelease, new ByConvention(1, 0.15, 1))
    ];

    /// <summary>The chance a part's held notes swell in where the song's do, a pad's likely, any other's now and then.</summary>
    public static ByConvention PadSwellChance { get; } = new(0, 0.5, 0.9);

    public static ByConvention SwellChance { get; } = new(0, 0.05, 0.3);

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

    /// <summary>The chance a part plays with a chorus where the song does, the classic ones the likelier.</summary>
    public static ByConvention ClassicChorusChance { get; } = new(0, 0.6, 0.9);

    public static ByConvention ChorusChance { get; } = new(0, 0.2, 0.5);

    /// <summary>
    ///     The song's notes with every part's expression on them (<see cref="CompositionStateKinds.Vibrato" /> and the
    ///     rest), each part in a section drawing from a sequence of its own, by the section, so that a section plays as it
    ///     did before.
    /// </summary>
    /// <param name="songSound">The song's sound facet, which the song's effects and a part's lean by.</param>
    /// <param name="songStream">The song's sequence, which the effects it plays at all are drawn from.</param>
    /// <param name="sound">Every section's sound facet, by its place among the song's sections.</param>
    /// <param name="partStreams">A part's sequence for the whole song, by its track.</param>
    /// <param name="streams">A part's sequence in a section, by the section and the part's track.</param>
    public static ImmutableSortedDictionary<int, EventTimeline<RealizedNote>> Apply(
        ImmutableSortedDictionary<int, EventTimeline<RealizedNote>> notes,
        ImmutableSortedDictionary<int, IInstrumentTrack> definitions,
        SongMap map,
        double songSound,
        IGenerationContext songStream,
        IReadOnlyList<double> sound,
        Func<int, IGenerationContext> partStreams,
        Func<int, int, IGenerationContext> streams
    )
    {
        // which effects the song plays at all
        var songVibrato = songStream.TestProbability(SongVibratoChance.At(songSound));
        var songChorus = songStream.TestProbability(SongChorusChance.At(songSound));
        var songSwell = songStream.TestProbability(SongSwellChance.At(songSound));
        var songBends = songStream.TestProbability(SongBendChance.At(songSound));
        var strong = songStream.Pick(ByConvention.Weigh(StrongEffects, songSound));

        // and the one part that plays the strong effect, the likelier one it suits, if any part may
        var mayCarry = notes
            .Where(x => x.Value.Count > 0 && definitions[x.Key] is PitchInstrumentTrack && (strong != StrongEffect.Echo || IsLineRole(definitions[x.Key].Role)))
            .Select(x => new Weighted<int>(Suits(strong, definitions[x.Key].Role, ((PitchInstrumentTrack)definitions[x.Key]).InstrumentCode) ? SuitedStrongWeight : 1, x.Key))
            .ToImmutableArray();
        var carrier = strong != StrongEffect.None && !mayCarry.IsEmpty ? songStream.Pick(mayCarry) : -1;

        var result = notes.ToBuilder();
        foreach (var (track, timeline) in notes)
        {
            var definition = definitions[track];
            var role = definition.Role;
            var program = definition is PitchInstrumentTrack pitched ? pitched.InstrumentCode : -1;
            var states = new StateMap?[timeline.Count];
            // the first note at or after a position
            int first(double position)
            {
                var (low, high) = (0, timeline.Count);
                while (low < high)
                {
                    var middle = (low + high) / 2;
                    if (timeline[middle].Position < position - 1e-9)
                        low = middle + 1;
                    else
                        high = middle;
                }

                return low;
            }

            // the part's sound for the whole song, of the effects the song plays: a part makes every draw whatever the song
            // plays, so that what one effect decides leaves the others' draws as they are
            var part = partStreams(track);
            var vibrates = part.TestProbability(VibratoChance.At(songSound)) & songVibrato;
            var vibrato = part.Pick(ByConvention.Weigh(Vibratos, songSound));
            var swells = part.TestProbability((role == TrackRole.Pad ? PadSwellChance : SwellChance).At(songSound)) & songSwell;
            var bendsHere = part.TestProbability(PartBendChance.At(songSound)) & songBends;
            var carries = track == carrier;
            var tremoloDepth = 30 + part.GenerateInt(0, 40);
            var sweepDepth = 40 + part.GenerateInt(0, 60);
            var tremolo = carries && strong == StrongEffect.Tremolo ? tremoloDepth : 0;
            var sweeps = carries && strong == StrongEffect.Sweep ? sweepDepth : 0;
            var echoes = carries && strong == StrongEffect.Echo;
            var delay = EchoDelays[part.GenerateInt(0, EchoDelays.Length)];
            var reverb = (int)Math.Round(Reverb(role) + ReverbSpread.At(songSound) * (part.GenerateDouble() * 127 - Reverb(role)));
            var choruses = part.TestProbability((ChorusesClassically(program) ? ClassicChorusChance : ChorusChance).At(songSound)) & songChorus;
            var chorus = 40 + part.GenerateInt(0, 60);
            foreach (var (span, index) in map.Sections.Select((x, i) => (x, i)))
            {
                var context = streams(span.SectionId, track);
                var u = sound[index];
                var bendChance = BendChance.At(u);
                var bendKinds = ByConvention.Weigh(BendKinds, u);

                // the notes in the section, in order, the timeline's being
                for (var i = first(span.Start); i < timeline.Count && timeline[i].Position < span.End - 1e-9; i++)
                {
                    var note = timeline[i];

                    var noteProgram = note.Value.State.GetStateValue(CompositionStateKinds.Program) is var stated and > 0 ? stated - 1 : program;
                    var isLine = note.Value.Pitches.Length == 1 && IsLineRole(role)
                                 || note.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 1;
                    var bends = bendsHere && isLine && Bends(noteProgram);
                    var isSolo = note.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 1;
                    // every note draws whether it bends, and how, so that the draws stay as they are
                    var bendsNow = context.TestProbability(Math.Min(1, bendChance * (isSolo ? 3 : 1)));
                    var kind = context.Pick(bendKinds);
                    var state = note.Value.State
                        .With(CompositionStateKinds.Reverb, reverb + 1)
                        .With(CompositionStateKinds.Chorus, choruses && Choruses(noteProgram) ? chorus : 0);
                    if (vibrates && isLine && Bends(noteProgram) && note.Value.Duration >= 1)
                        state = state.With(CompositionStateKinds.Vibrato, vibrato);
                    if (bends && bendsNow)
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
