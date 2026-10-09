using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Rendering;

/// <summary>The parts' pocket: each note played off its place as its role has it.</summary>
public sealed class TimingTest
{
    /// <summary>How far behind its place every drum note played behind is, over the drums' rendered notes of the songs given.</summary>
    private static double[] LaidBackShifts(IEnumerable<CorpusSong> songs) =>
    [
        ..songs.SelectMany(song => song.Rendered.Tracks.Where(x => x.IsPercussionInstrument).SelectMany(x => x.NoteTimeline))
            .Select(x => x.Value.Shift).Where(x => x > 0)
    ];

    [Test]
    public async Task APartsNotes_CarryTheirRolesShift_ALaidBackGenresDrumsMore()
    {
        var songs = TestCorpus.Range(16).ToArray();
        foreach (var song in songs)
        foreach (var track in song.Rendered.Tracks)
            // the shift is the role's own, never its sign turned: a bass ahead, a lead behind
            await Assert.That(track.NoteTimeline.All(x => track.Role switch
            {
                TrackRole.Bass => x.Value.Shift <= 0,
                TrackRole.Melody or TrackRole.Solo or TrackRole.CounterMelody => x.Value.Shift >= 0,
                _ => true
            })).IsTrue();

        var laidBack = TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("hipHop"))));
        var tight = TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("electronic"))));
        await Assert.That(LaidBackShifts(songs)).IsNotEmpty();
        await Assert.That(LaidBackShifts(laidBack).Average()).IsGreaterThan(LaidBackShifts(tight).DefaultIfEmpty(0).Average() * 2);
    }
}
