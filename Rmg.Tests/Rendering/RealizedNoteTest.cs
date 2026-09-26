using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Tests.Rendering;

public sealed class RealizedNoteTest
{
    private static byte[] Midi(Song song)
    {
        using var stream = new MemoryStream();
        Render.RenderSong(song).Write(stream);
        return stream.ToArray();
    }

    [Test]
    public async Task GeneratedSongs_ComeWithTheirNotes_AsRenderWouldDecideThem()
    {
        for (var seed = 0; seed < 5; seed++)
        {
            var song = TestCorpus.Get(seed).Song;
            var withoutNotes = new Song(song.Duration, song.TrackDefinitions, song.TrackEventStateTimelineMap, song.Map);

            await Assert.That(song.Notes).IsNotNull();
            await Assert.That(Midi(song)).IsEquivalentTo(Midi(withoutNotes));
        }
    }

    [Test]
    public async Task Notes_AreSingleForTheMelodyAndTheBass_AndMayBeChordsForTheChords()
    {
        foreach (var corpusSong in TestCorpus.Range(10))
        {
            var notes = corpusSong.Song.Notes!;

            await Assert.That(notes[SongTracks.MelodyTrack].All(x => x.Value.Pitches.Length == 1)).IsTrue();
            await Assert.That(notes[SongTracks.BassTrack].All(x => x.Value.Pitches.Length == 1)).IsTrue();
            await Assert.That(notes[SongTracks.ChordsTrack].Any(x => x.Value.Pitches.Length > 1)).IsTrue();
            // every note keeps the state it was decided from
            await Assert.That(notes.Values.SelectMany(x => x).All(x => x.Value.State.GetStateValue(StateKinds.Velocity) == x.Value.Velocity))
                .IsTrue();
        }
    }

    [Test]
    public async Task Render_PlaysTheNotes_AsTheyAreAfterAChange()
    {
        var song = TestCorpus.Get(0).Song;
        var melody = song.Notes![SongTracks.MelodyTrack];
        var last = melody[^1];
        // a change made after the notes are decided, such as a stage that sets the final note outright
        var changed = EventTimeline.Create(
            melody.Duration,
            melody.Take(melody.Count - 1).Append((last.Value with { Pitches = [last.Value.Pitches[0] + 12] }).ToTimelineItem(last.Position))
        );
        var edited = new Song(song.Duration, song.TrackDefinitions, song.TrackEventStateTimelineMap, song.Map, song.Notes.SetItem(SongTracks.MelodyTrack, changed));

        var program = ((PitchInstrumentTrack)song.TrackDefinitions[SongTracks.MelodyTrack]).InstrumentCode;
        var rendered = Render.RenderSong(edited).Tracks.Single(x => !x.IsPercussionInstrument && x.PitchInstrumentCode == program);

        await Assert.That(rendered.NoteTimeline.OrderBy(x => x.Position).Last().Value.Offset).IsEqualTo(last.Value.Pitches[0] + 12);
    }
}
