using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Core.Rendering;

/// <summary>
///     A part's expression written as MIDI plays it (<see cref="Composition.Expression" />): its notes' vibrato and bends
///     as pitch bend, a whole-channel control that a single line alone can take; a swell and a tremolo as a curve of its
///     expression; a sweep as a curve of its pan; a filter's movement as curves of its brightness and its resonance, which
///     a player that knows them filters by; its reverb and chorus wherever they change; and an echo as its notes
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

    /// <summary>The two cycles a tape's wow drifts in, in beats from the song's start, apart so that it never repeats as a vibrato does.</summary>
    private const double WowCycle = 2, SlowWowCycle = 3.3;

    public const int ReverbController = 91;

    /// <summary>A part's send to the synthesizer's delay, which plays its echo (<see cref="Midi" />).</summary>
    public const int DelayController = 94;

    /// <summary>How much of an echoing part the delay takes.</summary>
    public const int DelaySend = 100;
    public const int ChorusController = 93;
    public const int BrightnessController = 74;
    public const int ResonanceController = 71;

    /// <summary>
    ///     How far a filter closes at its deepest, in the brightness controller's steps below the middle, where it is the
    ///     sound's own: to 40, where both soundfonts served have most instruments at half their brightness or less and
    ///     a few dB quieter, the pads the most, and below which many fall nearly silent (October 2026). Above the middle
    ///     only the synths brighten, the rest open already.
    /// </summary>
    private const int FilterReach = 24;

    private const double Sixteenth = 0.25;

    /// <summary>How much of a 16th a gate lets a note sound for.</summary>
    private const double GateShare = 0.6;

    /// <summary>How long a squelch takes to close most of the way, in beats.</summary>
    private const double SquelchDecay = 0.25;

    /// <summary>The section a note plays in, which a gate's bars and a filter's opening count from; a note outside every section is a mistake.</summary>
    private static SectionSpan SectionAt(SongMap? map, double position) =>
        map?.Sections.LastOrDefault(x => x.Start <= position + 1e-9) ?? throw new InvalidOperationException($"No section of the song plays at {position}, which a gate or a filter counts from.");

    /// <summary>Where the whole mix's closing changes, swung, and how closed it is from there, in the brightness controller's steps (<see cref="CompositionStateKinds.MixClosed" />).</summary>
    public static ImmutableArray<(double At, double Steps)> MixSteps(StateTimeline<double> mix, Swing swing) => [..mix.Select(x => (swing.Apply(x.Position), x.Value * FilterReach))];

    /// <summary>How far the mix is closed at a time, swung, in the brightness controller's steps.</summary>
    private static double MixAt(ImmutableArray<(double At, double Steps)> mix, double at) => mix.LastOrDefault(x => x.At <= at + 1e-9).Steps;

    /// <summary>The drums' brightness wherever the mix's closing changes, the drums sharing a channel and no tone of their own.</summary>
    public static IEnumerable<(double Position, int Controller, int Value)> MixBrightness(ImmutableArray<(double At, double Steps)> mix) =>
        mix.Select(x => (x.At, BrightnessController, Math.Max(64 - FilterReach, (int)Math.Round(64 - x.Steps))));

    /// <summary>The track's expression, from its notes, as they are swung, at the song's tempo, a gate's and a filter's by the sections of the map, under the whole mix's closing.</summary>
    public static (ImmutableArray<(double Position, int Value)> Bends, ImmutableArray<(double Position, int Controller, int Value)> Controllers,
        ImmutableArray<(double Position, double Value)> Expression) Render(
            EventTimeline<RealizedNote> notes,
            SongMap? map,
            Swing swing,
            StateTimeline<double> tempo,
            double pan,
            ImmutableArray<(double At, double Steps)> mix
        )
    {
        var bends = ImmutableArray.CreateBuilder<(double, int)>();
        var controllers = ImmutableArray.CreateBuilder<(double, int, int)>();
        var expression = ImmutableArray.CreateBuilder<(double, double)>();
        int? reverb = null, chorus = null, delay = null, brightness = null, pitch = null;

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
            var bend = (BendKind)state.GetStateValue(CompositionStateKinds.Bend);
            var depth = state.GetStateValue(CompositionStateKinds.BendDepth);
            var vibrato = state.GetStateValue(CompositionStateKinds.Vibrato);
            var wide = state.GetStateValue(CompositionStateKinds.WideVibrato);
            // and the tape's drift and the part's detune under them, in semitones, the drift by the time since the song's start
            var wow = state.GetStateValue(CompositionStateKinds.Wow);
            var detune = state.GetStateValue(CompositionStateKinds.Detune);
            double Offset(double at) => (detune + wow * (0.6 * Math.Sin(2 * Math.PI * at / WowCycle) + 0.4 * Math.Sin(2 * Math.PI * at / SlowWowCycle))) / 100.0;
            var shaped = bend != BendKind.None || vibrato > 0 || wide > 0;
            if (shaped || wow > 0)
            {
                var cyclesPerBeat = VibratoRate * 60 / (tempo.GetEffectiveValueAt(note.Position) * Meter.BaseTempo);
                for (var t = 0.0; t < duration - 1e-9; t += shaped ? Step / 4 : Step)
                {
                    var share = t / duration;
                    var semitones = bend switch
                    {
                        // from a scale note below or above, or the note before, onto the note
                        BendKind.Scoop or BendKind.PrebendRelease or BendKind.Slide => depth * Math.Max(0, 1 - t / Math.Min(0.25, duration / 3)),
                        // to a scale note above or below, and back
                        BendKind.BendAndRelease or BendKind.ReleaseAndBend => depth * Math.Sin(Math.PI * share),
                        BendKind.FallOff => share < 0.75 ? 0 : depth * (share - 0.75) / 0.25,
                        _ => 0
                    };
                    // a vibrato grows in after the attack, from a third of the note to its middle
                    var growth = Math.Clamp((share - 0.3) / 0.3, 0, 1);
                    semitones += vibrato / 100.0 * growth * Math.Sin(2 * Math.PI * cyclesPerBeat * t);
                    // a wide one bends up from the note to its top and back, as a guitar's does, never below it
                    semitones += wide * growth * (0.5 - 0.5 * Math.Cos(2 * Math.PI * cyclesPerBeat * t));
                    bends.Add((start + t, ToBend(semitones + Offset(start + t))));
                }

                pitch = ToBend(Offset(end));
                bends.Add((end, pitch.Value));
            }
            else if (ToBend(Offset(start)) != (pitch ?? ToBend(0)))
            {
                pitch = ToBend(Offset(start));
                bends.Add((start, pitch.Value));
            }


            // a swell, a tremolo, a pump and a gate, as the note's expression, the one curve of them all multiplied, back to
            // full where it ends: a gate's 16ths sounding in the first part of each it lets through and silent in the rest,
            // and in those it does not, counted from every bar's first and swung as the notes are
            var swells = state.GetStateValue(CompositionStateKinds.Swell) == 1;
            var tremolo = state.GetStateValue(CompositionStateKinds.Tremolo);
            var pump = state.GetStateValue(CompositionStateKinds.Pump);
            var gate = state.GetStateValue(CompositionStateKinds.Gate);
            var gateEdges = new List<(double At, double Open)>();
            if (gate != 0)
            {
                var section = SectionAt(map, note.Position);
                var (from, bar) = (section.Start, section.Meter.Sixteenths);
                for (var at = Math.Floor(note.Position / Sixteenth + 1e-9) * Sixteenth; swing.Apply(at) < end - 1e-9; at += Sixteenth)
                {
                    var sounds = (gate >> ((int)Math.Round((at - from) / Sixteenth)).Mod(bar).Mod(16) & 1) == 1;
                    gateEdges.Add((Math.Max(swing.Apply(at), start), sounds ? 1 : 0));
                    var closes = swing.Apply(at + GateShare * Sixteenth);
                    if (sounds && closes < end - 1e-9)
                        gateEdges.Add((closes, 0));
                }
            }

            var moves = swells || tremolo > 0 || pump > 0;
            if (moves || gateEdges.Count > 0)
            {
                double Moved(double at)
                {
                    var t = at - start;
                    var swell = swells ? Math.Min(1, 0.25 + 0.75 * t / Math.Min(1, duration / 2)) : 1;
                    var shake = 1 - tremolo / 100.0 * (0.5 + 0.5 * Math.Sin(2 * Math.PI * t / 0.25));
                    // ducked on the beat and swelling back over it
                    var duck = 1 - pump / 100.0 * Math.Pow(1 - at % 1, 2);
                    return swell * shake * duck;
                }

                double Gated(double at)
                {
                    var open = 1.0;
                    foreach (var edge in gateEdges)
                        if (edge.At <= at + 1e-9)
                            open = edge.Open;
                    return open;
                }

                var times = (moves ? Enumerable.Range(0, (int)Math.Ceiling((duration - 1e-9) / (Step / 2))).Select(i => start + i * Step / 2) : [])
                    .Concat(gateEdges.Select(x => x.At)).Where(x => x < end - 1e-9).Distinct().Order();
                foreach (var at in times)
                    expression.Add((at, Moved(at) * Gated(at)));
                expression.Add((end, 1));
            }

            // a sweep from side to side over two bars of four, while the part plays
            var sweep = state.GetStateValue(CompositionStateKinds.AutoPan);
            if (sweep > 0)
                for (var t = start; t < end - 1e-9; t += Step * 4)
                    controllers.Add((t, 10, (int)Math.Round(64 + 63 * Math.Clamp(pan + sweep / 100.0 * Math.Sin(2 * Math.PI * t / 8), -1, 1))));

            // the part's brightness, one curve: its tone, below the sound's own, a filter closing further from it and opening
            // back, ringing as it does: a wobble in time, a squelch on every note, an opening over a section's first phrase;
            // and the whole mix's closing; never below the filter's reach
            var tone = state.GetStateValue(CompositionStateKinds.Tone);
            var filter = (StrongEffect)state.GetStateValue(CompositionStateKinds.Filter);
            var filterCloses = state.GetStateValue(CompositionStateKinds.FilterDepth) / 100.0;
            var cycle = state.GetStateValue(CompositionStateKinds.FilterCycle);
            var filterFrom = filter != StrongEffect.None ? swing.Apply(SectionAt(map, note.Position).Start) : 0;
            double Filtered(double t) => filter switch
            {
                StrongEffect.None => 0,
                StrongEffect.Wobble => 0.5 - 0.5 * Math.Cos(2 * Math.PI * (start + t - filterFrom) / cycle),
                StrongEffect.Squelch => 1 - Math.Exp(-t / SquelchDecay),
                _ => 1 - Math.Clamp((start + t - filterFrom) / cycle, 0, 1)
            };
            if (filter != StrongEffect.None)
                controllers.Add((start, ResonanceController, filter == StrongEffect.Squelch ? 104 : 84));
            var step = filter == StrongEffect.Wobble ? cycle / 8 : Step / 2;
            // a filter's steps, from the note's start, and wherever the mix's closing changes while it sounds
            var brightnessTimes = (filter != StrongEffect.None ? Enumerable.Range(0, (int)Math.Ceiling((duration - 1e-9) / step)).Select(i => i * step) : [0.0])
                .Concat(mix.Where(x => x.At > start + 1e-9 && x.At < end - 1e-9).Select(x => x.At - start)).Order();
            // a filter's curve written whole on every note, the rest where the brightness changes
            int? last = filter != StrongEffect.None ? null : brightness ?? 64;
            foreach (var t in brightnessTimes)
            {
                var value = Math.Max(64 - FilterReach, (int)Math.Round(64 - tone - filterCloses * FilterReach * Filtered(t) - MixAt(mix, start + t)));
                if (value != last)
                    controllers.Add((start + t, BrightnessController, value));
                last = brightness = value;
            }

            // an echo, the note sent to the delay, and none sent where the notes stop echoing
            var sentDelay = state.GetStateValue(CompositionStateKinds.Echo) > 0 ? DelaySend : 0;
            if (sentDelay != (delay ?? 0))
            {
                delay = sentDelay;
                controllers.Add((start, DelayController, sentDelay));
            }
        }

        return (bends.ToImmutable(), controllers.ToImmutable(), expression.ToImmutable());
    }

    /// <summary>A bend of the semitones given, as pitch bend's 14 bits, 8192 in the middle.</summary>
    private static int ToBend(double semitones) => Math.Clamp((int)Math.Round(8192 + semitones / BendRange * 8191), 0, 16383);
}
