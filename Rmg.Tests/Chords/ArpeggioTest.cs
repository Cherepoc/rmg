using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;
using System.Collections.Immutable;

namespace Rmg.Tests.Chords;

public sealed class ArpeggioTest
{
    private static readonly System.Collections.Immutable.ImmutableArray<int> Chord = [60, 64, 67, 72];

    private static int[] Play(ArpeggioPattern pattern, int notes, System.Collections.Immutable.ImmutableArray<int>? chord = null) =>
        [..Enumerable.Range(0, notes).Select(place => Arpeggios.Pick(pattern, chord ?? Chord, place, place * 0.5, place % 2 == 0, place / 2, 60, 0, place * 0.5 % 4))];

    [Test]
    public async Task APattern_PlaysTheChordsNotes_InItsOrder()
    {
        await Assert.That(Play(ArpeggioPattern.Up, 6)).IsEquivalentTo(new[] { 60, 64, 67, 72, 60, 64 }, CollectionOrdering.Matching);
        await Assert.That(Play(ArpeggioPattern.Down, 5)).IsEquivalentTo(new[] { 72, 67, 64, 60, 72 }, CollectionOrdering.Matching);
        await Assert.That(Play(ArpeggioPattern.UpDown, 8)).IsEquivalentTo(new[] { 60, 64, 67, 72, 67, 64, 60, 64 }, CollectionOrdering.Matching);
        await Assert.That(Play(ArpeggioPattern.Alberti, 4)).IsEquivalentTo(new[] { 60, 72, 67, 72 }, CollectionOrdering.Matching);
        await Assert.That(Play(ArpeggioPattern.RootFifth, 4)).IsEquivalentTo(new[] { 60, 67, 60, 67 }, CollectionOrdering.Matching);
        await Assert.That(Play(ArpeggioPattern.UpTwoOctaves, 8)).IsEquivalentTo(new[] { 60, 64, 67, 72, 72, 76, 79, 84 }, CollectionOrdering.Matching);
        // the root and its fifth by their notes, a triad in root position and inverted alike, and the pedal on the root
        await Assert.That(Play(ArpeggioPattern.RootFifth, 4, [60, 64, 67])).IsEquivalentTo(new[] { 60, 67, 60, 67 }, CollectionOrdering.Matching);
        await Assert.That(Play(ArpeggioPattern.RootFifth, 4, [55, 60, 64, 67])).IsEquivalentTo(new[] { 60, 67, 60, 67 }, CollectionOrdering.Matching);
        await Assert.That(Play(ArpeggioPattern.Pedal, 6, [55, 60, 64])).IsEquivalentTo(new[] { 60, 55, 60, 64, 60, 55 }, CollectionOrdering.Matching);
        // a roll, the lowest under the two highest in 8ths of three, three and two from the bar's start, a bar of 8ths and the next
        await Assert.That(Play(ArpeggioPattern.Roll, 10)).IsEquivalentTo(new[] { 60, 67, 72, 60, 67, 72, 60, 67, 60, 67 }, CollectionOrdering.Matching);
    }

    [Test]
    public async Task ThePlainestSections_BreakTheirChordsTheCommonWays_OrNot()
    {
        var context = new GenerationContext(3);
        var plain = Enumerable.Range(0, 2000).Select(_ => Arpeggios.Draw(context, 0, Genre.None, TrackRole.Chords)).ToHashSet();

        await Assert.That(plain.SetEquals([ArpeggioPattern.None, ArpeggioPattern.Up, ArpeggioPattern.UpDown, ArpeggioPattern.Alberti])).IsTrue();
        // every pattern at the wild end, but the roll, a genre's alone
        await Assert.That(Enumerable.Range(0, 4000).Select(_ => Arpeggios.Draw(context, 1, Genre.None, TrackRole.Chords)).Distinct().Count()).IsEqualTo(Enum.GetValues<ArpeggioPattern>().Length - 1);
    }

    /// <summary>
    ///     A calibration: Bluegrass's banjo rolls where its rhythm part's own pool names the roll and its guitar's chords are
    ///     whole: a rolled section's notes on its bar's 1st, 4th and 7th 8ths the thumb's under the two above them, the lowest
    ///     of their cells, but where a fill, a walk or a change of chord takes the part (measured over 64 songs: 92%).
    /// </summary>
    [Test]
    public async Task ABluegrassBanjo_Rolls_ItsThumbOnTheCellsFirst8ths()
    {
        var (cells, low, rolled, whole) = (0, 0, 0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Rmg.Tests.Genres.GenrePresets.Genre("bluegrass")))))
        {
            var traced = song.Trace.Where(x => x.Point == TracePoints.Arpeggio).ToArray();
            whole += traced.Count(x => x.Track == song.Track(TrackRole.Chords) && (ArpeggioPattern)x.Value! == ArpeggioPattern.None);
            var rolls = traced.Where(x => x.Track == song.Track(TrackRole.Rhythm) && (ArpeggioPattern)x.Value! == ArpeggioPattern.Roll).Select(x => x.Section).ToHashSet();
            rolled += rolls.Count;
            var notes = song.Song.Notes!.GetValueOrDefault(song.Track(TrackRole.Rhythm))?.Where(x => song.Map.PlayingAt(x.Position) is { } at && rolls.Contains(at.Span.SectionId)).ToArray() ?? [];
            // each note's 8th in its bar, by the map's bars, and the cell it falls in
            var placed = notes.Select(x => (Note: x, Place: song.Map.PlaceOf(x.Position), Play: song.Map.PlayingAt(x.Position)!.Value.Index))
                .Select(x => (x.Note, x.Play, x.Place.Bar, Eighth: x.Place.Beat * 2)).ToArray();
            foreach (var cell in placed.GroupBy(x => (x.Play, x.Bar, Cell: (int)Math.Floor(x.Eighth + 1e-6) is var e ? (e < 6 ? e / 3 : 2) : 0)).Where(x => x.Count() >= 2))
            {
                // a cell that sounds its first 8th, the thumb's
                var start = cell.Key.Cell * 3;
                var opening = cell.Where(x => Math.Abs(x.Eighth - start) < 1e-6).ToArray();
                if (opening.Length == 0)
                    continue;
                var first = opening[0].Note;
                cells++;
                low += cell.All(x => x.Note.Value.Pitches.Min() >= first.Value.Pitches.Min()) ? 1 : 0;
            }
        }

        Console.WriteLine($"{rolled} sections' banjos rolling, {whole} sections' chords whole; of {cells} cells, {low / (double)Math.Max(1, cells):P0} their thumb the lowest");
        await Assert.That(rolled).IsGreaterThan(20);
        await Assert.That(low / (double)cells).IsGreaterThan(0.6);
    }

    /// <summary>A genre that keeps its chords whole never breaks them, a calm section's breaker too.</summary>
    [Test]
    public async Task AGenreOfWholeChords_NeverBreaksThem()
    {
        // and a quieter section always calm, its breaker breaking the chords where the genre lets it
        var genre = new Genre(TestCorpus.Always(GenreChoices.Arpeggio, GenreChoices.Name(nameof(ArpeggioPattern.None))).Choices
            .AddRange(TestCorpus.Always(GenreChoices.LouderForms, "quieter").Choices));
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre))))
            await Assert.That(song.Trace.Where(x => x.Point == TracePoints.Arpeggio).All(x => (ArpeggioPattern)x.Value! == ArpeggioPattern.None)).IsTrue().Because($"seed {song.Seed}");
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var sections = TestCorpus.Range(256).SelectMany(song => song.Trace.Where(x => x.Point == TracePoints.Arpeggio)
            .Select(x => (Pattern: (ArpeggioPattern)x.Value!, Chords: song.Trace.First(t => t.Point == TracePoints.SectionUnconventionality && t.Section == x.Section).Value is Unconventionality u ? u[Facet.Chords] : 0))).ToArray();
        foreach (var band in sections.GroupBy(x => Math.Clamp((int)(x.Chords * 5), 0, 4)).OrderBy(x => x.Key))
            Console.WriteLine($"  chords {band.Key}/5, {band.Count()} sections: broken {band.Count(x => x.Pattern != ArpeggioPattern.None) / (double)band.Count():P0}; {string.Join(", ", band.Where(x => x.Pattern != ArpeggioPattern.None).GroupBy(x => x.Pattern).OrderByDescending(x => x.Count()).Select(x => $"{x.Key} {x.Count()}"))}");
        await Task.CompletedTask;
    }
}
