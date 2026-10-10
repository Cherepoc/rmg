using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.DrumKits;

/// <summary>A genre's drum strokes (<see cref="GenreChoices.DrumStrokes" />): a drum's steady sound leaned, apart from the sounds its notes play (<see cref="GenreChoices.DrumSounds" />).</summary>
public sealed class DrumStrokesTest
{
    private static Genre Leaning(GenreChoice choice, double amount) =>
        new(ImmutableDictionary<string, ImmutableDictionary<string, ByConvention>>.Empty.Add(choice.Name,
            ImmutableDictionary<string, ByConvention>.Empty.Add("openHiHat", new ByConvention(amount, amount, amount))));

    /// <summary>Every section's steady stroke of a drum, by its drum's track.</summary>
    private static ImmutableDictionary<int, int>[] Strokes(CorpusSong song) =>
        [..song.Trace.Where(x => x.Point == TracePoints.DrumStrokes).Select(x => (ImmutableDictionary<int, int>)x.Value!)];

    /// <summary>The share of the sections' hi-hat strokes that are its open sound.</summary>
    private static double Open(IEnumerable<CorpusSong> songs)
    {
        var hiHat = DrumGroups.GetTrackNumber(DrumDefinitions.HiHat);
        var strokes = songs.SelectMany(Strokes).Where(x => x.ContainsKey(hiHat)).Select(x => DrumDefinitions.HiHat.Sounds[x[hiHat]].Code).ToArray();
        return strokes.Count(x => x == DrumSounds.OpenHiHat) / (double)strokes.Length;
    }

    [Test]
    public async Task AGenresDrumStroke_LeansTheSteadyStroke_AndItsDrumSounds_LeaveItBe()
    {
        Genre[] genres = [Leaning(GenreChoices.DrumStrokes, 1), Leaning(GenreChoices.DrumStrokes, 0), Leaning(GenreChoices.DrumSounds, 1), Genre.None];
        var songs = genres.Select(genre => TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre))).ToArray()).ToArray();
        var (most, least) = (Open(songs[0]), Open(songs[1]));
        // against the mechanism breaking (measured: 29% to 35% leaning most, none least, 5% to 14% as the songs have it, at offsets 0 and a million)
        Console.WriteLine($"the hi-hat's steady stroke open: {most:P0} leaning most, {least:P0} least, {Open(songs[3]):P0} as the songs have it");
        await Assert.That(most).IsGreaterThan(least * 4);
        // the sounds leaned leave the strokes as the songs draw them
        foreach (var (sounds, own) in songs[2].Zip(songs[3]))
            await Assert.That(Strokes(sounds).Select(x => string.Join(",", x.OrderBy(k => k.Key)))).IsEquivalentTo(Strokes(own).Select(x => string.Join(",", x.OrderBy(k => k.Key))), CollectionOrdering.Matching)
                .Because($"seed {own.Seed}");
    }
}
