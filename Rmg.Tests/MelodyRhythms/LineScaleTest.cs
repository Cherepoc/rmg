using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.MelodyRhythms;

public sealed class LineScaleTest
{
    // C major's steps, counted from the chord's root, here G's
    private static readonly int[] Major = [0, 2, 4, 5, 7, 9, 11];

    private static ChordContext On(int rootStep, int[] scale) =>
        new(steps => scale[(rootStep + steps).Mod(7)] + 12 * (int)Math.Floor((rootStep + steps) / 7.0), rootStep);

    [Test]
    public async Task ALinesScale_TakesTheSectionsScale_ItsPentatonic_OrItsOwnOnTheChordsRoot()
    {
        var g = On(4, Major);

        await Assert.That(LineScales.Classes(LineScale.Section, g)).IsNull();
        // C major but F and B
        await Assert.That(LineScales.Classes(LineScale.Pentatonic, g)!.Order()).IsEquivalentTo(new[] { 0, 2, 4, 7, 9 }, CollectionOrdering.Matching);
        await Assert.That(LineScales.Classes(LineScale.WholeTone, g)!.Order()).IsEquivalentTo(new[] { 1, 3, 5, 7, 9, 11 }, CollectionOrdering.Matching);
        await Assert.That(LineScales.Classes(LineScale.Blues, g)!.Order()).IsEquivalentTo(new[] { 0, 1, 2, 5, 7, 10 }, CollectionOrdering.Matching);
        await Assert.That(LineScales.Classes(LineScale.Chromatic, g)!.Count).IsEqualTo(12);
        // the pentatonic union on the key's tonic, C's, both thirds, whatever chord it is over
        await Assert.That(LineScales.Classes(LineScale.Union, g)!.Order()).IsEquivalentTo(new[] { 0, 2, 3, 4, 5, 7, 9, 10 }, CollectionOrdering.Matching);
        // harmonic minor has two tritones, and its pentatonic line keeps all of it
        await Assert.That(LineScales.Classes(LineScale.Pentatonic, On(0, [0, 2, 3, 5, 7, 8, 11]))).IsNull();
    }

    [Test]
    public async Task ThePlainestLines_TakeTheSectionsScale_OrItsPentatonic_AndTheWildestMostlyAScaleOfTheirOwn()
    {
        var context = new GenerationContext(5);
        var plain = Enumerable.Range(0, 2000).Select(_ => LineScales.Draw(context, 0, Genre.None, riff: false)).ToHashSet();
        var wild = Enumerable.Range(0, 2000).Select(_ => LineScales.Draw(context, 1, Genre.None, riff: false)).ToArray();

        await Assert.That(plain.SetEquals([LineScale.Section, LineScale.Pentatonic])).IsTrue();
        await Assert.That(wild.Contains(LineScale.Pentatonic)).IsFalse();
        await Assert.That(wild.Count(x => x >= LineScale.MelodicMinor) / (double)wild.Length).IsGreaterThan(0.85);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        // every section's melody's line scale by its scale facet, and the share of its notes off the section's scale
        var sections = TestCorpus.Measure(256, song =>
        {
            var common = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap;
            var melody = song.Notes(song.Track(TrackRole.Melody));
            return song.Trace.Where(x => x.Point == TracePoints.LineScale && x.Track == song.Track(TrackRole.Melody)).Select(x =>
            {
                var span = song.Map.Sections.First(s => s.SectionId == x.Section);
                var facet = ((Unconventionality)song.Trace.First(t => t.Point == TracePoints.SectionUnconventionality && t.Section == x.Section).Value!)[Facet.Scale];
                var notes = melody.Where(n => n.Position >= span.Start && n.Position < span.End).ToArray();
                var off = notes.Count(n =>
                {
                    var key = common.GetStateTimeline(StateKinds.KeyOffset).GetEffectiveValueAt(n.Position);
                    var altered = common.GetStateTimeline(StateKinds.ScaleAlterations).GetEffectiveValueAt(n.Position);
                    var scale = common.GetStateTimeline(StateKinds.ScaleOffsets).GetEffectiveValueAt(n.Position);
                    var by = altered.IsSet ? altered.Value : StepAlterations.None;
                    return !scale.Select((o, i) => (o + key + by.By(i)).Mod(12)).Contains(n.Value.Offset.Mod(12));
                });
                return (Scale: (LineScale)x.Value!, Facet: facet, Notes: notes.Length, Off: off);
            }).ToArray();
        }).SelectMany(x => x).ToArray();

        foreach (var band in sections.GroupBy(x => Math.Clamp((int)(x.Facet * 5), 0, 4)).OrderBy(x => x.Key))
            Console.WriteLine($"  scale {band.Key}/5, {band.Count()} sections: {string.Join(", ", band.GroupBy(x => x.Scale).OrderBy(x => x.Key).Select(x => $"{x.Key} {x.Count() / (double)band.Count():P0}"))}");
        foreach (var scale in sections.GroupBy(x => x.Scale).OrderBy(x => x.Key))
            Console.WriteLine($"  {scale.Key}: {scale.Count()} sections, notes off the section's scale {scale.Sum(x => x.Off) / (double)Math.Max(1, scale.Sum(x => x.Notes)):P1}");
        await Task.CompletedTask;
    }

    [Test]
    public async Task AGenresRiffScale_IsTheRiffsAlone_TheMelodyKeepingTheLines()
    {
        var genre = new Genre(System.Collections.Immutable.ImmutableDictionary<string, System.Collections.Immutable.ImmutableDictionary<string, Rmg.Core.Probabilities.ByConvention>>.Empty
            .Add("lineScale", System.Collections.Immutable.ImmutableDictionary<string, Rmg.Core.Probabilities.ByConvention>.Empty.Add("section", new(1, 1, 1)))
            .Add("riffScale", System.Collections.Immutable.ImmutableDictionary<string, Rmg.Core.Probabilities.ByConvention>.Empty.Add("chromatic", new(1, 1, 1))));
        // the riff given, as a song draws one half the time
        var scales = TestCorpus.InParallel(Enumerable.Range(0, 8), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre,
                Parts: System.Collections.Immutable.ImmutableDictionary<TrackRole, bool>.Empty.Add(TrackRole.Riff, true))))
            .SelectMany(song => song.Trace.Where(x => x.Point == TracePoints.LineScale).Select(x => (Role: song.Song.TrackDefinitions[x.Track].Role, Scale: (LineScale)x.Value!))).ToArray();

        await Assert.That(scales.Where(x => x.Role == TrackRole.Riff).All(x => x.Scale == LineScale.Chromatic)).IsTrue();
        await Assert.That(scales.Where(x => x.Role == TrackRole.Melody).All(x => x.Scale == LineScale.Section)).IsTrue();
        await Assert.That(scales.Count(x => x.Role == TrackRole.Riff)).IsGreaterThan(0);
        await Assert.That(scales.Count(x => x.Role == TrackRole.Melody)).IsGreaterThan(0);
    }
}
