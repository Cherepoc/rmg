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
            // notes' stop time, pushes, inner lines, hat rolls, ornaments, articulation and timing are edits of them, made once they are decided
            var song = SongGenerator.GenerateSong((ulong)seed, ProgressionSettings.Default, SongOverrides.None, ImmutableHashSet.Create("Stop time", "Pushes", "Inner lines", "Hat rolls", "Ornaments", "Articulation", "Timing"), (_, _) => { }, CancellationToken.None);
            if (song.Map!.Ending.Kind == EndingKind.Stop)
                continue;

            var withoutNotes = new Song(song.Duration, song.Meter, song.TrackDefinitions, song.TrackEventStateTimelineMap, song.Map);

            await Assert.That(song.Notes).IsNotNull();
            await Assert.That(Midi(song)).IsEquivalentTo(Midi(withoutNotes));
        }
    }

    [Test]
    public async Task Notes_AreSingleForTheMelodyAndTheBass_ButInOctaves_AndMayBeChordsForTheChords()
    {
        foreach (var corpusSong in TestCorpus.Range(8))
        {
            var notes = corpusSong.Song.Notes!;

            // but the melody's in octaves, its own note first and the note an octave from it, and its stacked notes, a double
            // stop or a solo's chord, its own note on top
            await Assert.That(notes[SongTracks.MelodyTrack].All(x => x.Value.State.GetStateValue(CompositionStateKinds.LineVoices) > 1
                ? x.Value.Pitches.Skip(1).All(p => p < x.Value.Pitches[0])
                : x.Value.State.GetStateValue(CompositionStateKinds.Octave) > 0
                    ? x.Value.Pitches.Length == 2 && Math.Abs(x.Value.Pitches[1] - x.Value.Pitches[0]) == 12 || x.Value.Pitches.Length == 1
                    : x.Value.Pitches.Length == 1)).IsTrue();
            await Assert.That(notes[SongTracks.BassTrack].All(x => x.Value.Pitches.Length == 1)).IsTrue();
            await Assert.That(notes[SongTracks.ChordsTrack].Any(x => x.Value.Pitches.Length > 1)).IsTrue();
            // every note keeps the state it was decided from, and a drum's plays as loud as its sound is over the drum
            double SoundLoudness(int track, int pitch) => corpusSong.Song.TrackDefinitions[track] is PercussionInstrumentTrack drum
                ? VelocityLayers.SoundLevel * drum.Sounds.Single(x => x.Code == pitch).Loudness
                : 0;
            await Assert.That(notes.All(track => track.Value.All(x =>
                    (x.Value.State.GetStateValue(StateKinds.Velocity) + SoundLoudness(track.Key, x.Value.Pitches[0])).IsEqualToByEpsilon(x.Value.Velocity)
                )))
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
        var edited = new Song(song.Duration, song.Meter, song.TrackDefinitions, song.TrackEventStateTimelineMap, song.Map, song.Notes.SetItem(SongTracks.MelodyTrack, changed));

        var program = ((PitchInstrumentTrack)song.TrackDefinitions[SongTracks.MelodyTrack]).InstrumentCode;
        var rendered = Render.RenderSong(edited).Tracks.Single(x => !x.IsPercussionInstrument && x.PitchInstrumentCode == program);

        await Assert.That(rendered.NoteTimeline.OrderBy(x => x.Position).Last().Value.Offset).IsEqualTo(last.Value.Pitches[0] + 12);
    }
}
