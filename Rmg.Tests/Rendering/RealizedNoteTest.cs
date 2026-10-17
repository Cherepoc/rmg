using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Tests.Rendering;

public sealed class RealizedNoteTest
{
    // the song as written, but for its changes of instrument, which the song puts on its notes once they are decided
    private static byte[] Midi(Song song)
    {
        using var stream = new MemoryStream();
        var rendered = Render.RenderSong(song);
        new RenderedSong(rendered.Duration, rendered.Meters, rendered.TempoTimeline, rendered.FadeTimeline,
            [..rendered.Tracks.Select(x => new RenderedTrack(x.IsPercussionInstrument, x.Role, x.PitchInstrumentCode, x.NoteTimeline, x.Pan, x.Volume))]).Write(stream, null);
        return stream.ToArray();
    }

    [Test]
    public async Task GeneratedSongs_ComeWithTheirNotes_AsRenderWouldDecideThem()
    {
        for (var seed = 0; seed < 16; seed++)
        {
            // a stopped ending cuts the notes that sound into its stop, which only the song put together knows; and the
            // notes' stop time, pushes, inner lines, hat rolls, ornaments, a drum's too, articulation, timing, a hook intro's quote
            // and the instrument switches, which a second take plays only where its part is driven, are edits of them, made
            // once they are decided
            var song = SongGenerator.GenerateSong(TestCorpus.SongSeed(seed), SongOverrides.None, ImmutableHashSet.Create("Stop time", "Pushes", "Inner lines", "Hat rolls", "Ornaments", "Drum ornaments", "Articulation", "Timing", "Hook intro", "Instruments"), (_, _) => { }, CancellationToken.None);
            if (song.Map!.Ending.Kind == EndingKind.Stop)
                continue;

            var withoutNotes = new Song(song.Duration, song.Meter, song.TrackDefinitions, song.TrackEventStateTimelineMap, song.Map, null, null);

            await Assert.That(song.Notes).IsNotNull();
            await Assert.That(Midi(song)).IsEquivalentTo(Midi(withoutNotes), CollectionOrdering.Matching);
        }
    }

    [Test]
    public async Task Notes_AreSingleForTheMelodyAndTheBass_ButInOctaves_AndMayBeChordsForTheChords()
    {
        var stacked = 0;
        foreach (var corpusSong in TestCorpus.Range(8))
        {
            var notes = corpusSong.Song.Notes!;

            // but the melody's in octaves, its own note first and the note an octave from it, and its stacked notes, a double
            // stop or a solo's chord, its own note on top
            await Assert.That(notes[corpusSong.Track(TrackRole.Melody)].All(x => x.Value.State.GetStateValue(CompositionStateKinds.LineVoices) > 1
                ? x.Value.Pitches.Skip(1).All(p => p < x.Value.Pitches[0])
                : x.Value.State.GetStateValue(CompositionStateKinds.Octave) > 0
                    ? x.Value.Pitches.Length == 2 && Math.Abs(x.Value.Pitches[1] - x.Value.Pitches[0]) == 12 || x.Value.Pitches.Length == 1
                    : x.Value.Pitches.Length == 1)).IsTrue();
            // and the bass's single, but in octaves where it solos so, its own note first and the octave above it
            foreach (var note in notes[corpusSong.Track(TrackRole.Bass)].Where(x => x.Value.Pitches.Length != 1))
                await Assert.That(note.Value.State.GetStateValue(CompositionStateKinds.Octave) > 0 && note.Value.Pitches.Length == 2 && Math.Abs(note.Value.Pitches[1] - note.Value.Pitches[0]) == 12)
                    .IsTrue().Because($"seed {corpusSong.Seed}, the bass at {note.Position}");
            // the chords stacked, in the songs together, as a song's chords may be broken throughout
            stacked += notes.GetValueOrDefault(corpusSong.Track(TrackRole.Chords))?.Count(x => x.Value.Pitches.Length > 1) ?? 0;
            // every note keeps the state it was decided from, a drum's struck as hard whatever its sound
            foreach (var (track, note) in notes.SelectMany(t => t.Value.Where(x => !x.Value.State.GetStateValue(StateKinds.Velocity).IsEqualToByEpsilon(x.Value.Velocity)).Select(x => (t.Key, x))))
                await Assert.That(note.Value.Velocity).IsEqualTo(note.Value.State.GetStateValue(StateKinds.Velocity))
                    .Because($"seed {corpusSong.Seed}, track {track} ({corpusSong.Song.TrackDefinitions[track].Role}) at {note.Position}");
        }

        await Assert.That(stacked).IsGreaterThan(0);
    }

    [Test]
    public async Task Render_PlaysTheNotes_AsTheyAreAfterAChange()
    {
        var song = TestCorpus.Get(0).Song;
        var melody = song.Notes![song.Track(TrackRole.Melody)];
        var last = melody[^1];
        // a change made after the notes are decided, such as a stage that sets the final note outright
        var changed = EventTimeline.Create(
            melody.Duration,
            melody.Take(melody.Count - 1).Append((last.Value with { Pitches = [last.Value.Pitches[0] + 12] }).ToTimelineItem(last.Position))
        );
        var edited = new Song(song.Duration, song.Meter, song.TrackDefinitions, song.TrackEventStateTimelineMap, song.Map, song.Notes.SetItem(song.Track(TrackRole.Melody), changed), song.Draws);

        // the melody's track by its role, as another part may play its instrument
        var rendered = Render.RenderSong(edited).Tracks.Single(x => !x.IsPercussionInstrument && x.Role == TrackRole.Melody && !x.IsDouble && !x.IsRiser);

        await Assert.That(rendered.NoteTimeline.OrderBy(x => x.Position).Last().Value.Offset).IsEqualTo(last.Value.Pitches[0] + 12);
    }
}
