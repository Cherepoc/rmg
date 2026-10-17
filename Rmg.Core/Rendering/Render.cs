using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Core.Rendering;

public static class Render
{
    // a note's velocity, the sum its layers drew, plays on a fixed scale, the same for every song, so that a quiet section
    // or an evenly played bass is heard as such: the sum at the centre plays at the middle velocity, and the further
    // from it, the less it moves, towards the quietest and the loudest velocities; slower above the middle, as a player
    // strikes again and again at about 111 at the most and harder only now and then, as an accent
    internal const double VelocityCentre = 0.2;

    /// <summary>How much of its length a palm-muted note sounds, and the least, in beats, and how loud against its own.</summary>
    internal const double MutedShare = 0.35;

    internal const double MutedFloor = 0.12;

    internal const double MutedVelocity = 0.9;

    /// <summary>How loud a slurred note plays against a struck one.</summary>
    internal const double SlurredVelocity = 0.85;

    /// <summary>How loud an ornament's quick note plays against its note.</summary>
    internal const double OrnamentVelocity = 0.8;

    /// <summary>How loud a drum's grace stroke plays against its note, a flam's: well under it.</summary>
    internal const double GraceStrokeVelocity = 0.6;

    /// <summary>How loud an up stroke plays against a down stroke.</summary>
    internal const double UpStrokeVelocity = 0.9;

    /// <summary>How many strings an up stroke's chord has at least for its lowest to be missed.</summary>
    internal const int UpStrokeFullest = 4;
    internal const double VelocityScale = 1.03;
    private const double LoudVelocityScale = 1.4;
    private const double MiddleVelocity = 0.6;
    private const double VelocityReach = 0.4;

    /// <summary>
    ///     The song as MIDI plays it: its notes, as its generation decided them, or decided here for a song that comes
    ///     without them; a chord's pitches as notes together, the drums on the percussion channel, the velocities
    ///     on a fixed scale, and every note swung as the song swings.
    /// </summary>
    public static RenderedSong RenderSong(Song song) => RenderSong(song, SongMix.None);

    /// <summary>The orchestra kit's program, whose notes from 41 to 53 are timpani, a semitone apart (<see cref="GenreChoices.Kits" />).</summary>
    private const int OrchestraKit = 48;

    /// <summary>The controllers that name a non-registered parameter, its high byte and its low, and set its value.</summary>
    public const int NrpnHigh = 99, NrpnLow = 98, DataEntry = 6;

    /// <summary>A Roland GS drum's delay send, by its parameter's high byte, the drum's note its low, and a send in full.</summary>
    public const int DrumDelayNrpn = 31, FullSend = 127;

    /// <summary>The orchestra kit's hi-hats, closed, pedalled and open, by the standard kit's notes for them.</summary>
    private static readonly IReadOnlyDictionary<int, int> OrchestraHiHats = new Dictionary<int, int> { [42] = 27, [44] = 28, [46] = 29 };

    /// <summary>
    ///     A drum's note as the orchestra kit plays it: its crash and its china on the kit's concert cymbal, the ride and its
    ///     bell on the kit's ride, beside its hi-hats; its hi-hats the kit's own; and any other in the timpani's range a timpani on the chord's root
    ///     or fifth there, the nearest to the note, as a timpanist tunes to the key rather than to a drum's fixed note.
    /// </summary>
    private static int InOrchestra(int code, StateMap state)
    {
        const int lowest = 41, highest = 53, crash = 57, ride = 30;
        if (code is 49 or 52)
            return crash;
        if (code is 51 or 53)
            return ride;
        // the hi-hat the orchestra set's own, closed, pedalled and open, a timekeeper rather than a timpani
        if (code is 42 or 44 or 46)
            return OrchestraHiHats[code];
        if (code is < lowest or > highest)
            return code;
        var chord = Composition.Realizer.GetChord(state).Chord;
        var tuned = new[] { chord.Root, chord.GetPitch(4) }.Select(x => x.Mod(12)).ToHashSet();
        return Enumerable.Range(lowest, highest - lowest + 1).Where(x => tuned.Contains(x.Mod(12))).MinBy(x => (Math.Abs(x - code), x));
    }

    /// <summary>
    ///     The song as MIDI plays it, heard as the mix asks (<see cref="SongMix" />): a drum group quieter or left out,
    ///     every part's instrument, volume and pan, and the parts left out not written. A track with no notes, such as a
    ///     part the song leaves out, is not written either.
    /// </summary>
    public static RenderedSong RenderSong(Song song, SongMix mix) => RenderSong(song, mix, CancellationToken.None);

    /// <summary>
    ///     The song as <see cref="RenderSong(Song, SongMix)" /> renders it, given up at the next track once nobody
    ///     waits for it any more.
    /// </summary>
    public static RenderedSong RenderSong(Song song, SongMix mix, CancellationToken cancellation)
    {
        var notes = song.Notes ?? Realizer.Realize(song.TrackDefinitions, song.TrackEventStateTimelineMap, song.PlaceOf);
        var common = song.TrackEventStateTimelineMap.CommonStateTimelineMap.OfScope(StateScope.Render);
        var swing = GetSwing(song);
        // how closed the whole mix is, wherever it changes, every part's brightness under it
        var mixClosed = ExpressionRender.MixSteps(common.GetStateTimeline(CompositionStateKinds.MixClosed), swing);
        var renderedTracks = new List<RenderedTrack>();
        var percussionTrackNotes = new List<IEnumerable<TimelineItem<RenderedNote>>>();
        ImmutableArray<(double Position, int Controller, int Value)> percussionSends = [];
        var echoingDrums = new HashSet<int>();
        foreach (var (trackNumber, allNotes) in notes)
        {
            cancellation.ThrowIfCancellationRequested();
            // a riser on a channel of its own, its part's mix over it, so that it swells where the part plays on
            var risers = allNotes.Where(x => Risers.IsRiser(x.Value.State)).ToArray();
            var trackNotes = risers.Length == 0 ? allNotes : EventTimeline.Create(allNotes.Duration, allNotes.Where(x => !Risers.IsRiser(x.Value.State)));
            var isPercussion = song.TrackDefinitions[trackNumber] is PercussionInstrumentTrack;
            // a drum plays as its group is asked to
            var group = song.TrackDefinitions[trackNumber] is PercussionInstrumentTrack && !mix.DrumGroups.IsEmpty
                ? mix.DrumGroups.GetValueOrDefault(DrumGroups.GroupOf(DrumGroups.GetDrum(trackNumber)).Name)
                : null;
            // a drum's group the mix leaves out not written, as a part left out is not
            if (trackNotes.Count == 0 || group?.Volume == 0)
                continue;
            // as the kit that plays is, a kit the mix gives in place of the drawn
            if (isPercussion && (mix.Of(TrackRole.Drum)?.Instrument ?? song.Draws?.Kit) == OrchestraKit)
                trackNotes = EventTimeline.Create(trackNotes.Duration, trackNotes.Select(x =>
                    (x.Value with { Pitches = [..x.Value.Pitches.Select(p => InOrchestra(p, song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetEffectiveStateMapAt(x.Position)))] }).ToTimelineItem(x.Position)));

            var renderedNotes = trackNotes.SelectMany(note =>
                {
                    var velocity = GetMidiVelocity(note.Value) * (group?.Volume ?? 1);
                    // swung, its end as its start, so that it still reaches the note it reached
                    var position = swing.Apply(note.Position);
                    var duration = swing.Apply(note.Position + note.Value.Duration) - position;
                    // a palm-muted note as General MIDI can play it, which has no mute for a driven guitar: short and a touch
                    // softer; the one place a plugin's output would play it as its keyswitch or controller instead
                    if (note.Value.State.GetStateValue(CompositionStateKinds.PalmMute) > 0)
                        (duration, velocity) = (Math.Max(MutedFloor, duration * MutedShare), velocity * MutedVelocity);
                    // a slurred note as General MIDI can play it, which has no legato: struck softer, the note before reaching it,
                    // but a soloist's, whose margin over the band was set before its notes were slurred
                    if (note.Value.State.GetStateValue(CompositionStateKinds.Legato) > 0 && note.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 0)
                        velocity *= SlurredVelocity;
                    // a strummed chord's strings struck one after another, down from the lowest or up from the highest, the up
                    // stroke a touch softer and thinner, a full chord's lowest string missed as the hand comes up
                    var shift = note.Value.State.GetStateValue(CompositionStateKinds.TimingOffset);
                    var strum = note.Value.State.GetStateValue(CompositionStateKinds.Strum);
                    var order = note.Value.Pitches.Order().Skip(strum < 0 && note.Value.Pitches.Length >= UpStrokeFullest ? 1 : 0).ToArray();
                    // an ornament's quick notes, lighter, from the note's start, and the note after them; a drum's grace
                    // strokes, softer, before its note, on its sound, as a flam's are
                    var ornament = note.Value.State.GetStateValue(CompositionStateKinds.Ornament);
                    if (!ornament.IsDefaultOrEmpty && isPercussion)
                    {
                        var stroke = note.Value.State.GetStateValue(CompositionStateKinds.OrnamentStep);
                        return ornament.Select((x, i) => new RenderedNote(note.Value.Pitches[0] + x, velocity * GraceStrokeVelocity, stroke, shift)
                                .ToTimelineItem(swing.Apply(note.Position - (ornament.Length - i) * stroke)))
                            .Append(new RenderedNote(note.Value.Pitches[0], velocity, duration, shift).ToTimelineItem(position));
                    }

                    if (!ornament.IsDefaultOrEmpty)
                    {
                        var step = note.Value.State.GetStateValue(CompositionStateKinds.OrnamentStep);
                        double At(int i) => swing.Apply(note.Position + i * step);
                        var start = At(ornament.Length);
                        // on every pitch of the note, a line doubled in octaves ornamented in both
                        return note.Value.Pitches.SelectMany(pitch =>
                            ornament.Select((x, i) => new RenderedNote(pitch + x, velocity * OrnamentVelocity, At(i + 1) - At(i), shift).ToTimelineItem(At(i)))
                                .Append(new RenderedNote(pitch, velocity, position + duration - start, shift).ToTimelineItem(start)));
                    }

                    return order.Select(pitch =>
                    {
                        var rank = strum > 0 ? Array.IndexOf(order, pitch) : order.Length - 1 - Array.IndexOf(order, pitch);
                        var spread = order.Length > 1 ? Math.Abs(strum) * rank / (order.Length - 1) : 0;
                        return new RenderedNote(pitch, strum < 0 ? velocity * UpStrokeVelocity : velocity, duration, shift + spread).ToTimelineItem(position);
                    });
                }
            );
            if (song.TrackDefinitions[trackNumber] is PitchInstrumentTrack pitchInstrumentTrack)
            {
                // a driven guitar played twice, by the instrument it sounds, the drawn or the one given, or one a section switches
                // it to, all the way to the side it drew, its second take on the other
                var programChanges = ProgramChanges(trackNotes, pitchInstrumentTrack.InstrumentCode, swing);
                var doubles = mix.Of(pitchInstrumentTrack.Role)?.Instrument is { } givenProgram
                    ? DoubleTracking.Doubles(pitchInstrumentTrack.Role, givenProgram)
                    : programChanges.Select(x => x.Program).Prepend(pitchInstrumentTrack.InstrumentCode).Any(x => DoubleTracking.Doubles(pitchInstrumentTrack.Role, x));
                var pan = doubles ? Math.Sign(pitchInstrumentTrack.Pan) is 0 ? 1.0 : Math.Sign(pitchInstrumentTrack.Pan) : pitchInstrumentTrack.Pan;
                // how the part is played beyond its notes, its send to the delay that echoes it among them
                var expression = ExpressionRender.Render(trackNotes, song.Map, swing, common.GetStateTimeline(StateKinds.Tempo), pan, mixClosed);
                var played = EventTimeline.Create(trackNotes.Duration, renderedNotes);
                // a doubled part plays no sweep, its takes standing apart on either side
                var controllers = doubles ? [..expression.Controllers.Where(x => x.Controller != 10)] : expression.Controllers;
                renderedTracks.Add(
                    new RenderedTrack(false, pitchInstrumentTrack.Role, pitchInstrumentTrack.InstrumentCode, played, pan, doubles ? DoubleTracking.Level : 1)
                    {
                        ProgramChanges = programChanges,
                        PitchBends = expression.Bends,
                        Controllers = controllers,
                        Expression = expression.Expression
                    }
                );
                // its riser swung as its notes are, unfiltered by the mix's closing, sent to the part's reverb and chorus alone
                if (risers.Length > 0)
                    renderedTracks.Add(new RenderedTrack(false, pitchInstrumentTrack.Role, Risers.Program, EventTimeline.Create(allNotes.Duration, risers.Select(x =>
                        new RenderedNote(Risers.Pitch, GetMidiVelocity(x.Value), swing.Apply(x.Position + x.Value.Duration) - swing.Apply(x.Position), 0).ToTimelineItem(swing.Apply(x.Position)))), pitchInstrumentTrack.Pan)
                    {
                        IsRiser = true,
                        Controllers = [..ExpressionRender.Render(EventTimeline.Create(allNotes.Duration, risers.Take(1)), song.Map, swing, common.GetStateTimeline(StateKinds.Tempo), pitchInstrumentTrack.Pan, [])
                            .Controllers.Where(x => x.Controller is ExpressionRender.ReverbController or ExpressionRender.ChorusController)]
                    });
                // and its second take, on the other side and the other driven guitar, a little late, where its part plays driven:
                // an acoustic or clean form of it plays once, as a second take of the same sound 8 ms late would only phase
                if (doubles)
                {
                    var tempo = common.GetStateTimeline(StateKinds.Tempo);
                    TimelineItem<RenderedNote> Late(TimelineItem<RenderedNote> note) =>
                        (note.Value with { Shift = note.Value.Shift + DoubleTracking.Delay * Meter.BeatsPerSecond(tempo.GetEffectiveValueAt(note.Position)) }).ToTimelineItem(note.Position);
                    var given = mix.Of(pitchInstrumentTrack.Role)?.Instrument;
                    // a driven guitar's pinch, played on the harmonics, doubled with the notes around it
                    bool Driven(double position) => given is not null
                        || programChanges.LastOrDefault(x => x.Position <= position + 1e-9, (0, pitchInstrumentTrack.InstrumentCode)).Item2 is var program
                        && (InstrumentTraits.Has(program, InstrumentTrait.Driven) || program == Composition.Expression.GuitarHarmonics);
                    renderedTracks.Add(
                        new RenderedTrack(false, pitchInstrumentTrack.Role, DoubleTracking.SecondTake(pitchInstrumentTrack.InstrumentCode),
                            EventTimeline.Create(played.Duration, played.Where(x => Driven(x.Position)).Select(Late)), -pan, DoubleTracking.Level)
                        {
                            IsDouble = true,
                            ProgramChanges = [..programChanges.Select(x => (x.Position, DoubleTracking.SecondTake(x.Program)))],
                            PitchBends = expression.Bends,
                            Controllers = controllers,
                            Expression = expression.Expression
                        }
                    );
                }
            }
            else
            {
                percussionTrackNotes.Add(renderedNotes);
                // a drum that echoes, a dub's snare, sent to the delay by its key, every stroke of it
                if (trackNotes.Any(x => x.Value.State.GetStateValue(CompositionStateKinds.Echo) > 0))
                    echoingDrums.UnionWith(renderedNotes.Select(x => x.Value.Offset));
                // the drums' reverb and chorus, as their first note sent to them has them, an intro's carrying none
                if (percussionSends.IsEmpty && trackNotes.FirstOrDefault(x => x.Value.State.GetStateValue(CompositionStateKinds.Reverb) > 0) is { Value: not null } sent)
                    percussionSends = [..ExpressionRender.Render(EventTimeline.Create(trackNotes.Duration, [sent]), song.Map, swing, common.GetStateTimeline(StateKinds.Tempo), 0, [])
                        .Controllers.Where(x => x.Controller is not (ExpressionRender.DelayController or ExpressionRender.BrightnessController))];
            }
        }

        var percussionEventTimeline = EventTimeline.Create(song.Duration, percussionTrackNotes.SelectMany(x => x));
        if (percussionEventTimeline.Count > 0)
        {
            // the drums share their channel, where the soundfont places each drum as a kit stands, in the kit's sound
            // and to the delay, the channel's send and every echoing drum's of its own, as a Roland GS drum's (NRPN 31)
            (int, int)[] delaySends = echoingDrums.Count == 0
                ? []
                : [(ExpressionRender.DelayController, ExpressionRender.DelaySend), ..echoingDrums.Order().SelectMany(key => new[] { (NrpnHigh, DrumDelayNrpn), (NrpnLow, key), (DataEntry, FullSend) })];
            var percussionTrack = new RenderedTrack(true, TrackRole.Drum, song.Draws?.Kit ?? 0, percussionEventTimeline, 0)
                { Controllers = [..percussionSends.Select(x => (0.0, x.Controller, x.Value)), ..delaySends.Select(x => (0.0, x.Item1, x.Item2)), ..ExpressionRender.MixBrightness(mixClosed)] };
            renderedTracks.Add(percussionTrack);
        }

        // every part as it is asked to sound, over the song's volume; an instrument given
        // holds throughout, its changes let go of
        var mixed = renderedTracks
            .Select(track => (Track: track, Mix: mix.Of(track.Role)))
            .Select(x => new RenderedTrack(
                x.Track.IsPercussionInstrument,
                x.Track.Role,
                x.Mix?.Instrument is { } given && !x.Track.IsRiser ? x.Track.IsDouble ? DoubleTracking.SecondTake(given) : given : x.Track.PitchInstrumentCode,
                x.Track.NoteTimeline,
                // a second take on the other side of the pan given
                x.Mix?.Pan is { } pan ? x.Track.IsDouble ? -pan : pan : x.Track.Pan,
                x.Track.Volume * mix.Volume * (x.Mix?.Volume ?? 1)
            )
            {
                IsDouble = x.Track.IsDouble,
                IsRiser = x.Track.IsRiser,
                ProgramChanges = x.Mix?.Instrument is null ? x.Track.ProgramChanges : [],
                PitchBends = x.Track.PitchBends,
                // a pan given holds throughout, its sweep let go of
                Controllers = [..x.Track.Controllers.Where(c => c.Controller != 10 || x.Mix?.Pan is null)],
                Expression = x.Track.Expression
            });
        // the meter the song starts in, and every section's that is another than the one before, a short last bar's its own
        var meters = new List<(double Position, Meter Meter)> { (0, song.Meter) };
        foreach (var section in song.Map?.Sections ?? [])
        foreach (var (position, meter) in section.Cut > 0 ? [(section.Start, section.Meter), (section.LastBar, ShortBars.Of(section.Meter))] : new[] { (section.Start, section.Meter) })
            if (!meter.HasSameBar(meters[^1].Meter))
                meters.Add((position, meter));
        return new RenderedSong(song.Duration, [..meters], common.GetStateTimeline(StateKinds.Tempo), common.GetStateTimeline(StateKinds.Fade), [..mixed])
        {
            Room = song.Draws?.Room,
            Insertion = song.Draws?.Insertion,
            // the echo's length, the carrier's, every echoing part's and drum's alike
            Echo = notes.Values.SelectMany(x => x).Select(x => x.Value.State.GetStateValue(CompositionStateKinds.Echo)).FirstOrDefault(x => x > 0) is > 0 and var echo ? echo / 4.0 : null,
            EchoOnce = notes.Values.SelectMany(x => x).Any(x => x.Value.State.GetStateValue(CompositionStateKinds.EchoOnce) > 0),
            Harmony = SongHarmony.Of(song, notes)
        };
    }

    /// <summary>Where a track's instrument changes, a note playing another than the one before it, swung as the note is.</summary>
    private static ImmutableArray<(double Position, int Program)> ProgramChanges(EventTimeline<RealizedNote> notes, int own, Swing swing)
    {
        var changes = ImmutableArray.CreateBuilder<(double, int)>();
        var playing = own;
        foreach (var note in notes)
        {
            var program = InstrumentsOverTime.ProgramOf(note.Value.State, own);
            if (program == playing)
                continue;

            changes.Add((swing.Apply(note.Position), program));
            playing = program;
        }

        return changes.ToImmutable();
    }

    /// <summary>How the song swings, which moves where its every note plays, its pairs counted from each section's start in the meter it has.</summary>
    internal static Swing GetSwing(Song song)
    {
        var common = song.TrackEventStateTimelineMap.CommonStateTimelineMap;
        var swing = new Swing(
            common.GetStateTimeline(StateKinds.SwingDelay).GetEffectiveValueAt(0),
            common.GetStateTimeline(StateKinds.SwingPeriod).GetEffectiveValueAt(0)
        );
        return song.Map is not { } map ? swing : swing with
        {
            Spans = [..map.Sections.Select(x => (x.Start, x.Meter.Pairs(swing.Period))), (map.Ending.Start, (map.Sections.IsEmpty ? map.Meter : map.Sections[^1].Meter).Pairs(swing.Period))]
        };
    }

    /// <summary>
    ///     How loud a note's every pitch plays: its velocity on the fixed scale, and a chord's notes softer, so that its
    ///     n notes together sound about as loud as one: n notes sound 10·log10(n) dB louder than one, and a note's
    ///     loudness goes with 40·log10 of its velocity, so each plays at n to the power of -1/4.
    /// </summary>
    internal static double GetMidiVelocity(RealizedNote note)
    {
        return ToMidiVelocity(note.Velocity) * Math.Pow(Math.Max(1, note.Pitches.Length), ChordVelocityPower);
    }

    private const double ChordVelocityPower = -0.25;

    /// <summary>A velocity sum on the fixed scale, from 0.2 to 1.</summary>
    internal static double ToMidiVelocity(double sum)
    {
        return MiddleVelocity + VelocityReach * Math.Tanh((sum - VelocityCentre) / (sum > VelocityCentre ? LoudVelocityScale : VelocityScale));
    }

    /// <summary>
    ///     A velocity sum made a share as loud as heard: its velocity on the fixed scale times the factor, and the sum it is
    ///     rendered from, as a roll growing from 60% of a note or a stroke struck again at 80% of it; a sum scaled by the
    ///     factor itself would barely change near the scale's middle and grow louder where it is negative. A share is kept
    ///     down to the scale's floor of 0.2 alone, under which every note plays at the floor, as a roll's first notes from a
    ///     quiet drum and a ghost under a quiet snare do.
    /// </summary>
    internal static double ScaleLoudness(double sum, double factor) => FromMidiVelocity(ToMidiVelocity(sum) * factor);

    /// <summary>The velocity sum a velocity on the fixed scale is rendered from (<see cref="ToMidiVelocity" />), the scale's ends kept just inside.</summary>
    internal static double FromMidiVelocity(double velocity)
    {
        var reach = Math.Clamp((velocity - MiddleVelocity) / VelocityReach, -0.999, 0.999);
        return VelocityCentre + (reach > 0 ? LoudVelocityScale : VelocityScale) * Math.Atanh(reach);
    }
}
