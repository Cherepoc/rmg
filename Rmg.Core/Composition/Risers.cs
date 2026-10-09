using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>A riser into every drop, a stage of the song after it is put together (<see cref="SongStages" />).</summary>
internal static class Risers
{
    /// <summary>The General MIDI program a riser plays, the reverse cymbal, a cymbal's swell played backwards.</summary>
    public const int Program = 119;

    /// <summary>The note a riser plays, the reverse cymbal's own pitch.</summary>
    public const int Pitch = 60;

    /// <summary>Whether a note is a riser, which the render plays on a channel of its own.</summary>
    public static bool IsRiser(StateMap state) => state.GetStateValue(CompositionStateKinds.Program) == Program + 1;

    /// <summary>
    ///     A riser into every drop: the reverse cymbal swelling over the bar before it and ending on its first beat, as the
    ///     build's snare roll does, over a silent bar or a filtered one whose band plays on. The song's one part that
    ///     carries it, the pad, or the chords, or another pitched part, as the song has them, whose mix it follows on a
    ///     channel of its own (<see cref="Rendering.Render" />); as loud as the part's last note before the bar, or its first, and sent to
    ///     its reverb and chorus, none of the part's other expression. The last stage, so that no other plays on it.
    /// </summary>
    internal static SongDraft Apply(SongDecisions song, SongDraft draft)
    {
        if (song.Form.Drops.IsEmpty)
            return draft;
        var notes = SongStages.NotesOf(draft);
        // the pad, then the chords, then any other pitched part, as the song plays them
        TrackRole[] order = [TrackRole.Pad, TrackRole.Chords];
        var carrier = song.Playing.Where(x => x.Value != TrackRole.Drum && song.Tracks.Definitions[x.Key] is PitchInstrumentTrack && notes.TryGetValue(x.Key, out var played) && played.Count > 0)
            .OrderBy(x => Array.IndexOf(order, x.Value) is var i and >= 0 ? i : order.Length).ThenBy(x => x.Key).Select(x => (int?)x.Key).FirstOrDefault();
        if (carrier is not { } track)
            return draft;

        var risers = new List<TimelineItem<RealizedNote>>();
        foreach (var drop in song.Form.Drops)
        {
            var bar = drop - song.Form.Map.MeterAt(drop - 1e-6).BarDuration;
            // the part's last note before the bar, whose loudness and sends the riser takes, or its first where it comes in later
            var before = notes[track].LastOrDefault(n => n.Position < bar - 1e-9) is { Value: not null } last ? last : notes[track][0];
            var state = StateMap.Default.With(CompositionStateKinds.Program, Program + 1)
                .With(CompositionStateKinds.Reverb, before.Value.State.GetStateValue(CompositionStateKinds.Reverb))
                .With(CompositionStateKinds.Chorus, before.Value.State.GetStateValue(CompositionStateKinds.Chorus));
            risers.Add(new RealizedNote([Pitch], before.Value.Velocity, drop - bar, state).ToTimelineItem(bar));
        }

        return draft with { Notes = notes.SetItem(track, EventTimeline.Create(notes[track].Duration, notes[track].Concat(risers).OrderBy(x => x.Position))) };
    }
}
