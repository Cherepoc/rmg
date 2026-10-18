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

        // the same songs, Hip-hop's pocket against the songs' own
        var laidBack = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("hipHop"))));
        var own = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Without("hipHop", GenreChoices.Pocket.Name))));
        await Assert.That(LaidBackShifts(songs)).IsNotEmpty();
        Console.WriteLine($"the drums laid back by {LaidBackShifts(laidBack).Average():F4} beats in Hip-hop, {LaidBackShifts(own).DefaultIfEmpty(0).Average():F4} with the songs' pocket");
        // measured: 0.038 beats against 0.014, at offsets 0 and a million
        await Assert.That(LaidBackShifts(laidBack).Average()).IsGreaterThan(LaidBackShifts(own).DefaultIfEmpty(0).Average() * 1.5);
    }

    /// <summary>How far ahead of its place the bass's notes are, the most a song plays it, in beats, by its seed.</summary>
    private static Dictionary<int, double> BassAhead(IEnumerable<CorpusSong> songs) => songs.ToDictionary(song => song.Seed,
        song => song.Rendered.Tracks.Where(x => x.Role == TrackRole.Bass).SelectMany(x => x.NoteTimeline).Select(x => -x.Value.Shift).DefaultIfEmpty(0).Max());

    [Test]
    public async Task ALaidBackGenre_LaysItsBackbeatBack_AndNeverRushesItsBass()
    {
        foreach (var genre in new[] { "reggae", "hipHop" })
        {
            var laidBack = TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre))));
            var own = TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Without(genre, GenreChoices.Pocket.Name))));
            var (ahead, ownAhead) = (BassAhead(laidBack), BassAhead(own));
            Console.WriteLine($"{genre}: the bass ahead by {ahead.Values.Average():F4} beats at the most, {ownAhead.Values.Average():F4} with the songs' pocket; " +
                              $"the drums laid back by {LaidBackShifts(laidBack).DefaultIfEmpty(0).Average():F4}, {LaidBackShifts(own).DefaultIfEmpty(0).Average():F4}");
            // the same songs, their pocket alone differing
            foreach (var (seed, beats) in ahead)
                await Assert.That(beats).IsLessThanOrEqualTo(ownAhead[seed] + 1e-9).Because($"{genre}, seed {seed}");
            await Assert.That(LaidBackShifts(laidBack).Average()).IsGreaterThan(LaidBackShifts(own).DefaultIfEmpty(0).Average());
        }
    }
}
