using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Core.Rendering;

/// <summary>
///     A part's expression written as MIDI plays it (<see cref="Composition.Expression" />): its notes' vibrato and bends
///     as pitch bend, a whole-channel control that a single line alone can take; a swell and a tremolo as a curve of its
///     expression; a sweep as a curve of its pan; its reverb and chorus wherever they change; and an echo as its notes
///     repeated quieter after them, as a delay pedal plays.
/// </summary>
internal static class ExpressionRender
{
    /// <summary>The semitones a pitch bend reaches either way, set on every channel that bends.</summary>
    public const int BendRange = 12;

    /// <summary>A vibrato's speed, in cycles a second, as a singer's or a string player's.</summary>
    private const double VibratoRate = 5.5;

    /// <summary>How finely a curve is written, in beats: a 32nd.</summary>
    private const double Step = 0.125;

    /// <summary>How many times an echo repeats a note, and how much quieter each time.</summary>
    private const int EchoRepeats = 3;

    private const double EchoDecay = 0.55;

    public const int ReverbController = 91;
    public const int ChorusController = 93;

    /// <summary>The track's expression, from its notes, as they are swung, at the song's tempo.</summary>
    public static (ImmutableArray<(double Position, int Value)> Bends, ImmutableArray<(double Position, int Controller, int Value)> Controllers,
        ImmutableArray<(double Position, double Value)> Expression, ImmutableArray<TimelineItem<RenderedNote>> Echoes) Render(
            EventTimeline<RealizedNote> notes,
            Swing swing,
            StateTimeline<double> tempo,
            double pan,
            Func<RealizedNote, double> velocity
        )
    {
        var bends = ImmutableArray.CreateBuilder<(double, int)>();
        var controllers = ImmutableArray.CreateBuilder<(double, int, int)>();
        var expression = ImmutableArray.CreateBuilder<(double, double)>();
        var echoes = ImmutableArray.CreateBuilder<TimelineItem<RenderedNote>>();
        int? reverb = null, chorus = null;
        int? previousPitch = null;

        foreach (var note in notes)
        {
            var state = note.Value.State;
            var start = swing.Apply(note.Position);
            var duration = swing.Apply(note.Position + note.Value.Duration) - start;
            var end = start + duration;

            // the sends, where they change
            var sentReverb = Math.Clamp(state.GetStateValue(CompositionStateKinds.Reverb) - 1, -1, 127);
            if (sentReverb >= 0 && sentReverb != reverb)
            {
                reverb = sentReverb;
                controllers.Add((start, ReverbController, sentReverb));
            }

            var sentChorus = Math.Clamp(state.GetStateValue(CompositionStateKinds.Chorus), 0, 127);
            if (sentReverb >= 0 && sentChorus != chorus)
            {
                chorus = sentChorus;
                controllers.Add((start, ChorusController, sentChorus));
            }

            // a line's bend and vibrato, from its start to its end, and the pitch back where it was
            var pitch = note.Value.Pitches[0];
            var bend = (BendKind)state.GetStateValue(CompositionStateKinds.Bend);
            var vibrato = state.GetStateValue(CompositionStateKinds.Vibrato);
            if (bend != BendKind.None || vibrato > 0)
            {
                var cyclesPerBeat = VibratoRate * 60 / (tempo.GetEffectiveValueAt(note.Position) * Meter.BaseTempo);
                var slideFrom = Math.Clamp((previousPitch ?? pitch) - pitch, -BendRange, BendRange);
                for (var t = 0.0; t < duration - 1e-9; t += Step / 4)
                {
                    var share = t / duration;
                    var semitones = bend switch
                    {
                        BendKind.Scoop => Math.Min(0, -2 + 2 * t / Math.Min(0.25, duration / 3)),
                        BendKind.Slide => slideFrom * Math.Max(0, 1 - t / Math.Min(0.25, duration / 3)),
                        BendKind.BendAndRelease => 2 * Math.Sin(Math.PI * share),
                        BendKind.FallOff => share < 0.75 ? 0 : -3 * (share - 0.75) / 0.25,
                        _ => 0
                    };
                    // a vibrato grows in after the attack, from a third of the note to its middle
                    var growth = Math.Clamp((share - 0.3) / 0.3, 0, 1);
                    semitones += vibrato / 100.0 * growth * Math.Sin(2 * Math.PI * cyclesPerBeat * t);
                    bends.Add((start + t, ToBend(semitones)));
                }

                bends.Add((end, ToBend(0)));
            }

            previousPitch = pitch;

            // a swell and a tremolo, as the note's expression, back to full where it ends
            var swells = state.GetStateValue(CompositionStateKinds.Swell) == 1;
            var tremolo = state.GetStateValue(CompositionStateKinds.Tremolo);
            if (swells || tremolo > 0)
            {
                for (var t = 0.0; t < duration - 1e-9; t += Step / 2)
                {
                    var swell = swells ? Math.Min(1, 0.25 + 0.75 * t / Math.Min(1, duration / 2)) : 1;
                    var shake = 1 - tremolo / 100.0 * (0.5 + 0.5 * Math.Sin(2 * Math.PI * t / 0.25));
                    expression.Add((start + t, swell * shake));
                }

                expression.Add((end, 1));
            }

            // a sweep from side to side over two bars of four, while the part plays
            var sweep = state.GetStateValue(CompositionStateKinds.AutoPan);
            if (sweep > 0)
                for (var t = start; t < end - 1e-9; t += Step * 4)
                    controllers.Add((t, 10, (int)Math.Round(64 + 63 * Math.Clamp(pan + sweep / 100.0 * Math.Sin(2 * Math.PI * t / 8), -1, 1))));

            // an echo, the note repeated quieter after it
            var echo = state.GetStateValue(CompositionStateKinds.Echo) / 4.0;
            if (echo > 0)
                for (var repeat = 1; repeat <= EchoRepeats; repeat++)
                    echoes.AddRange(note.Value.Pitches.Select(x => new RenderedNote(x, velocity(note.Value) * Math.Pow(EchoDecay, repeat), Math.Min(duration, echo)).ToTimelineItem(start + repeat * echo)));
        }

        return (bends.ToImmutable(), controllers.ToImmutable(), expression.ToImmutable(), echoes.ToImmutable());
    }

    /// <summary>A bend of the semitones given, as pitch bend's 14 bits, 8192 in the middle.</summary>
    private static int ToBend(double semitones) => Math.Clamp((int)Math.Round(8192 + semitones / BendRange * 8191), 0, 16383);
}
