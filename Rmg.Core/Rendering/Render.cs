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
    public static RenderedSong RenderSong(Song song)
    {
        var notes = song.Notes ?? Realizer.Realize(song.TrackDefinitions, song.TrackEventStateTimelineMap, song.Meter);
        var common = song.TrackEventStateTimelineMap.CommonStateTimelineMap.OfScope(StateScope.Render);
        var swing = GetSwing(song);
        var renderedTracks = new List<RenderedTrack>();
        var percussionTrackNotes = new List<IEnumerable<TimelineItem<RenderedNote>>>();
        foreach (var (trackNumber, trackNotes) in notes)
        {
            var renderedNotes = trackNotes.SelectMany(note =>
                {
                    var velocity = GetMidiVelocity(note.Value);
                    // swung, its end as its start, so that it still reaches the note it reached
                    var position = swing.Apply(note.Position);
                    var duration = swing.Apply(note.Position + note.Value.Duration) - position;
                    return note.Value.Pitches.Select(pitch => new RenderedNote(pitch, velocity, duration).ToTimelineItem(position));
                }
            );
            if (song.TrackDefinitions[trackNumber] is PitchInstrumentTrack pitchInstrumentTrack)
                renderedTracks.Add(
                    new RenderedTrack(false, pitchInstrumentTrack.InstrumentCode, EventTimeline.Create(trackNotes.Duration, renderedNotes), pitchInstrumentTrack.Pan)
                );
            else
                percussionTrackNotes.Add(renderedNotes);
        }

        var percussionEventTimeline = EventTimeline.Create(song.Duration, percussionTrackNotes.SelectMany(x => x));
        if (percussionEventTimeline.Count > 0)
        {
            // the drums share their channel, where the soundfont places each drum as a kit stands
            var percussionTrack = new RenderedTrack(true, 0, percussionEventTimeline, 0);
            renderedTracks.Add(percussionTrack);
        }

        return new RenderedSong(song.Duration, song.Meter, common.GetStateTimeline(StateKinds.Tempo), common.GetStateTimeline(StateKinds.Fade), [..renderedTracks]);
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
