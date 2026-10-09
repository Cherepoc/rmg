using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Tests.Arrangements;

public sealed class RhythmPartTest
{
    private static bool Plays(CorpusSong song) =>
        !((System.Collections.Immutable.ImmutableHashSet<TrackRole>)song.Trace.Single(x => x.Point == TracePoints.SongParts).Value!).Contains(TrackRole.Rhythm);

    [Test]
    public async Task TheRhythmPart_PlaysTheChordsInASoundOfItsOwn_AsTheChordsPlayThem()
    {
        var songs = TestCorpus.Range(64).Where(Plays).ToArray();
        var checkedChords = 0;
        foreach (var song in songs)
        {
            var rhythm = (PitchInstrumentTrack)song.Song.TrackDefinitions[song.Song.Track(TrackRole.Rhythm)];
            var chords = (PitchInstrumentTrack)song.Song.TrackDefinitions[song.Song.Track(TrackRole.Chords)];
            await Assert.That(rhythm.InstrumentCode).IsNotEqualTo(chords.InstrumentCode);

            // whole chords, but where broken or soloing
            var whole = song.Song.Notes![song.Song.Track(TrackRole.Rhythm)]
                .Where(x => x.Value.State.GetStateValue(CompositionStateKinds.Arpeggio) == 0 && x.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 0)
                .Select(x => x.Value.Pitches.Distinct().Count())
                .ToArray();
            await Assert.That(whole.All(x => x >= 2)).IsTrue().Because($"seed {song.Seed}");
            checkedChords += whole.Length;
        }

        await Assert.That(songs.Length).IsGreaterThan(10);
        await Assert.That(checkedChords).IsGreaterThan(100);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        // how many songs have it, and in them how many notes a bar it and the chords strike where both play
        var songs = TestCorpus.Range(256);
        var with = songs.Where(Plays).ToArray();
        double PerBar(CorpusSong song, int track) => song.Notes(track).Select(x => x.Position).Distinct().Count() / (song.Map.Sections[^1].End / song.Map.Meter.BarDuration);
        var rhythm = with.Average(x => PerBar(x, x.Track(TrackRole.Rhythm)));
        var chords = with.Average(x => PerBar(x, x.Track(TrackRole.Chords)));
        var programs = with.GroupBy(x => ((PitchInstrumentTrack)x.Song.TrackDefinitions[x.Song.Track(TrackRole.Rhythm)]).InstrumentCode).OrderByDescending(x => x.Count()).Select(x => $"{x.Key} {x.Count()}");
        Console.WriteLine($"  {with.Length} of {songs.Length} songs have a rhythm part; strikes a bar {rhythm:F2} against the chords' {chords:F2}; programs {string.Join(", ", programs)}");
        await Task.CompletedTask;
    }
}
