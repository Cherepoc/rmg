using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>A riser into every drop, a stage of the song after it is put together (<see cref="SongStages" />).</summary>
internal static class Risers
{
    /// <summary>The General MIDI program a riser plays, the reverse cymbal, a cymbal's swell played backwards.</summary>
    public const int Program = 119;

    /// <summary>The note a riser plays, the reverse cymbal's own pitch.</summary>
    public const int Pitch = 60;

    /// <summary>
    ///     A riser into every drop: the reverse cymbal swelling over the silent bar before it and ending on its first beat, as
    ///     the build's snare roll does; played by the pad, or the chords, or another pitched part, the first that has played
    ///     before the silence, whose channel the silence leaves free, from its last note before it, which it takes its state
    ///     and loudness from; the part's own instrument back on its next note.
    /// </summary>
    internal static SongDraft Apply(SongDecisions song, SongDraft draft)
    {
        if (song.Form.Drops.IsEmpty)
            return draft;
        var notes = SongStages.NotesOf(draft);
        // the pad, then the chords, then any other pitched part, as the song plays them
        TrackRole[] order = [TrackRole.Pad, TrackRole.Chords];
        var parts = song.Playing.Where(x => x.Value != TrackRole.Drum && song.Tracks.Definitions[x.Key] is PitchInstrumentTrack && notes.ContainsKey(x.Key))
            .OrderBy(x => Array.IndexOf(order, x.Value) is var i and >= 0 ? i : order.Length).ThenBy(x => x.Key).Select(x => x.Key).ToArray();
        var result = notes.ToBuilder();
        // into a silent drop alone, a filtered one's band playing on its channel
        foreach (var drop in song.Form.Drops.Except(song.Form.FilteredDrops))
        {
            var gap = drop - song.Form.Map.MeterAt(drop - 1e-6).BarDuration;
            // the first that has played before the silence, whose last note there the riser takes its state and loudness from
            var (track, before) = parts.Select(x => (Track: x, Before: result[x].LastOrDefault(n => n.Position < gap - 1e-9))).FirstOrDefault(x => x.Before.Value is not null);
            if (before.Value is null)
                continue;
            var risen = result[track].Where(x => x.Position < gap - 1e-9 || x.Position >= drop - 1e-9).Append((before.Value with
            {
                Pitches = [Pitch],
                Duration = drop - gap,
                State = before.Value.State.With(CompositionStateKinds.Program, Program + 1)
            }).ToTimelineItem(gap));
            result[track] = EventTimeline.Create(result[track].Duration, risen.OrderBy(x => x.Position));
        }

        return draft with { Notes = result.ToImmutable() };
    }
}
