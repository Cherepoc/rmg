using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Core.Rendering;

public static class Render
{
    // a note's velocity, the sum its layers drew, plays on a fixed scale, the same for every song, so that a quiet section
    // or an evenly played bass is heard as such: the sum at the centre plays at the middle velocity, and the further
    // from it, the less it moves, towards the quietest and the loudest velocities
    internal const double VelocityCentre = 0.2;
    internal const double VelocityScale = 1.03;
    private const double MiddleVelocity = 0.6;
    private const double VelocityReach = 0.4;

    /// <summary>
    ///     The song as MIDI plays it: its notes, as its generation decided them, or decided here for a song that comes
    ///     without them; a chord's pitches as notes together, the drums on the percussion channel, the velocities
    ///     on a fixed scale, and every note swung as the song swings.
    /// </summary>
    public static RenderedSong RenderSong(Song song) => RenderSong(song, SongMix.None);

    /// <summary>
    ///     The song as MIDI plays it, heard as the mix asks (<see cref="SongMix" />): a drum group quieter or left out,
    ///     every part's instrument, volume and pan, and the parts left out not written. A track with no notes, such as a
    ///     part the song leaves out, is not written either.
    /// </summary>
    public static RenderedSong RenderSong(Song song, SongMix mix)
    {
        var notes = song.Notes ?? Realizer.Realize(song.TrackDefinitions, song.TrackEventStateTimelineMap, song.MeterAt);
        var common = song.TrackEventStateTimelineMap.CommonStateTimelineMap.OfScope(StateScope.Render);
        var swing = GetSwing(song);
        var renderedTracks = new List<RenderedTrack>();
        var percussionTrackNotes = new List<IEnumerable<TimelineItem<RenderedNote>>>();
        ImmutableArray<(double Position, int Controller, int Value)> percussionSends = [];
        foreach (var (trackNumber, trackNotes) in notes)
        {
            // a drum plays as its group is asked to
            var group = song.TrackDefinitions[trackNumber] is PercussionInstrumentTrack && !mix.DrumGroups.IsEmpty
                ? mix.DrumGroups.GetValueOrDefault(DrumGroups.GroupOf(DrumGroups.GetDrum(trackNumber)).Name)
                : null;
            if (group is { IsOn: false } || trackNotes.Count == 0)
                continue;

            var renderedNotes = trackNotes.SelectMany(note =>
                {
                    var velocity = GetMidiVelocity(note.Value) * (group?.Volume ?? 1);
                    // swung, its end as its start, so that it still reaches the note it reached
                    var position = swing.Apply(note.Position);
                    var duration = swing.Apply(note.Position + note.Value.Duration) - position;
                    return note.Value.Pitches.Select(pitch => new RenderedNote(pitch, velocity, duration).ToTimelineItem(position));
                }
            );
            if (song.TrackDefinitions[trackNumber] is PitchInstrumentTrack pitchInstrumentTrack)
            {
                // how the part is played beyond its notes, its echo among its notes
                var expression = ExpressionRender.Render(trackNotes, swing, common.GetStateTimeline(StateKinds.Tempo), pitchInstrumentTrack.Pan, GetMidiVelocity);
                renderedTracks.Add(
                    new RenderedTrack(false, pitchInstrumentTrack.Role, pitchInstrumentTrack.InstrumentCode, EventTimeline.Create(trackNotes.Duration, renderedNotes), pitchInstrumentTrack.Pan)
                    {
                        ProgramChanges = ProgramChanges(trackNotes, pitchInstrumentTrack.InstrumentCode, swing),
                        PitchBends = expression.Bends,
                        Controllers = expression.Controllers,
                        Expression = expression.Expression,
                        Echoes = expression.Echoes
                    }
                );
            }
            else
            {
                percussionTrackNotes.Add(renderedNotes);
                // the drums' reverb and chorus, as their first note has them
                if (percussionSends.IsEmpty && trackNotes.Count > 0)
                    percussionSends = ExpressionRender.Render(EventTimeline.Create(trackNotes.Duration, [trackNotes[0]]), swing, common.GetStateTimeline(StateKinds.Tempo), 0, GetMidiVelocity).Controllers;
            }
        }

        var percussionEventTimeline = EventTimeline.Create(song.Duration, percussionTrackNotes.SelectMany(x => x));
        if (percussionEventTimeline.Count > 0)
        {
            // the drums share their channel, where the soundfont places each drum as a kit stands
            var percussionTrack = new RenderedTrack(true, TrackRole.Drum, 0, percussionEventTimeline, 0) { Controllers = [..percussionSends.Select(x => (0.0, x.Controller, x.Value))] };
            renderedTracks.Add(percussionTrack);
        }

        // every part as it is asked to sound, over the song's volume, and those left out not written; an instrument given
        // holds throughout, its changes let go of
        var mixed = renderedTracks
            .Select(track => (Track: track, Mix: mix.Parts.GetValueOrDefault(track.Role)))
            .Where(x => x.Mix is not { IsOn: false })
            .Select(x => new RenderedTrack(
                x.Track.IsPercussionInstrument,
                x.Track.Role,
                x.Mix?.Instrument ?? x.Track.PitchInstrumentCode,
                x.Track.NoteTimeline,
                x.Mix?.Pan ?? x.Track.Pan,
                mix.Volume * (x.Mix?.Volume ?? 1)
            )
            {
                ProgramChanges = x.Mix?.Instrument is null ? x.Track.ProgramChanges : [],
                PitchBends = x.Track.PitchBends,
                // a pan given holds throughout, its sweep let go of
                Controllers = [..x.Track.Controllers.Where(c => c.Controller != 10 || x.Mix?.Pan is null)],
                Expression = x.Track.Expression,
                Echoes = x.Track.Echoes
            });
        return new RenderedSong(song.Duration, song.Meter, common.GetStateTimeline(StateKinds.Tempo), common.GetStateTimeline(StateKinds.Fade), [..mixed]);
    }

    /// <summary>Where a track's instrument changes, a note playing another than the one before it, swung as the note is.</summary>
    private static ImmutableArray<(double Position, int Program)> ProgramChanges(EventTimeline<RealizedNote> notes, int own, Swing swing)
    {
        var changes = ImmutableArray.CreateBuilder<(double, int)>();
        var playing = own;
        foreach (var note in notes)
        {
            var program = note.Value.State.GetStateValue(CompositionStateKinds.Program) is var stated and > 0 ? stated - 1 : own;
            if (program == playing)
                continue;

            changes.Add((swing.Apply(note.Position), program));
            playing = program;
        }

        return changes.ToImmutable();
    }

    /// <summary>How the song swings, which moves where its every note plays.</summary>
    internal static Swing GetSwing(Song song)
    {
        var common = song.TrackEventStateTimelineMap.CommonStateTimelineMap;
        return new Swing(
            common.GetStateTimeline(StateKinds.SwingDelay).GetEffectiveValueAt(0),
            common.GetStateTimeline(StateKinds.SwingPeriod).GetEffectiveValueAt(0)
        );
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
        return MiddleVelocity + VelocityReach * Math.Tanh((sum - VelocityCentre) / VelocityScale);
    }
}
