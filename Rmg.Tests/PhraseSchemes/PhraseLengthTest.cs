using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Tests.Genres;

namespace Rmg.Tests.PhraseSchemes;

/// <summary>A section's phrase of three bars, of four, or of four and two, its last two again (<see cref="PhraseLengths" />).</summary>
public sealed class PhraseLengthTest
{
    [Test]
    public async Task ThePlainestSongs_PhraseInFours()
    {
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Base: 0))))
            await Assert.That(song.Map.Sections.All(x => x.Meter.PhraseBars == Meter.DefaultPhraseBars)).IsTrue().Because($"seed {song.Seed}");
    }

    /// <summary>
    ///     Every section's scheme as long as its phrase; a phrase of other than four only in a pattern of one phrase; and a
    ///     phrase of four and two playing its third and fourth bars' chords again, but where the chords move otherwise.
    /// </summary>
    [Test]
    public async Task ASectionsScheme_IsAsLongAsItsPhrase_AndFourAndTwo_PlaysItsCadencesChordsAgain()
    {
        var lengths = new Dictionary<int, int>();
        var extended = 0;
        foreach (var song in TestCorpus.Range(128))
        foreach (var id in song.Map.Sections.Select(x => x.SectionId).Distinct())
        {
            var meter = song.MeterOf(id);
            var because = $"seed {song.Seed} section {id}";
            lengths[meter.PhraseBars] = lengths.GetValueOrDefault(meter.PhraseBars) + 1;
            T Traced<T>(string point) => (T)song.Trace.Single(x => x.Point == point && x.Section == id).Value!;
            await Assert.That(Traced<PhraseScheme>(TracePoints.PhraseScheme).Letters.Length).IsEqualTo(meter.PhraseBars).Because(because);
            if (meter.PhraseBars != Meter.DefaultPhraseBars)
                await Assert.That(meter.Phrases).IsEqualTo(1).Because(because);

            var rhythm = Traced<HarmonicRhythm>(TracePoints.HarmonicRhythm);
            if (meter.PhraseBars <= Meter.DefaultPhraseBars || !rhythm.Passing.IsEmpty || Traced<(bool Turns, bool)>(TracePoints.Turnaround).Turns || song.Trace.Single(x => x.Point == TracePoints.Planing && x.Section == id).Value is not null)
                continue;
            var progression = Traced<ImmutableArray<int>>(TracePoints.Progression);
            var again = (meter.PhraseBars - Meter.DefaultPhraseBars) * meter.BarDuration;
            foreach (var change in rhythm.Changes.Where(x => x >= Meter.DefaultPhraseBars * meter.BarDuration - 1e-9))
                await Assert.That(progression[rhythm.IndexAt(change)]).IsEqualTo(progression[rhythm.IndexAt(change - again)]).Because(because);
            extended++;
        }

        Console.WriteLine($"of 128 songs' sections, by their phrase's bars: {string.Join(", ", lengths.OrderBy(x => x.Key).Select(x => $"{x.Key} {x.Value}"))}; {extended} of four and two checked");
        await Assert.That(lengths.GetValueOrDefault(3)).IsGreaterThan(0);
        await Assert.That(extended).IsGreaterThan(0);
    }

    /// <summary>The sections' phrases by their bars, by fifths of the form facet and by genre.</summary>
    [Test]
    [Arguments("none")]
    [Arguments("folk")]
    [Arguments("orchestral")]
    [Arguments("techno")]
    [Explicit]
    public async Task Report(string id)
    {
        var genre = id == "none" ? Genre.None : GenrePresets.Genre(id);
        var songs = TestCorpus.InParallel(Enumerable.Range(0, 128), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre)));
        var sections = songs.SelectMany(s => s.Map.Sections.DistinctBy(x => x.SectionId).Select(x => (Fifth: Math.Clamp((int)(s.Unconventionality[Facet.Form] * 5), 0, 4), x.Meter.PhraseBars))).ToArray();
        foreach (var fifth in sections.GroupBy(x => x.Fifth).OrderBy(x => x.Key))
            Console.WriteLine($"{id}, form fifth {fifth.Key}: {fifth.Count()} sections, " +
                              string.Join(", ", fifth.GroupBy(x => x.PhraseBars).OrderBy(x => x.Key).Select(x => $"{x.Key} bars {x.Count() / (double)fifth.Count():P0}")));
        await Task.CompletedTask;
    }
}
