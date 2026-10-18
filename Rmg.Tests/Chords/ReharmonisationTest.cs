using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Tests.Chords;

/// <summary>A later appearance reharmonised: some of its chords a third away from the chords they replace, the rest of the song as it was.</summary>
public sealed class ReharmonisationTest
{
    private static (Song Song, (int Appearance, ImmutableArray<double> At)? Substitutes) Generate(int seed, params string[] skipped)
    {
        using var trace = StateTrace.Start(explains: false);
        var overrides = new SongOverrides(Facets: TestCorpus.FacetsOf((Facet.Progression, 0.5)));
        var song = SongGenerator.GenerateSong(TestCorpus.SongSeed(seed), overrides, skipped.ToImmutableHashSet(), (_, _) => { }, CancellationToken.None);
        return (song, ((int, ImmutableArray<double>)?)trace.Entries.SingleOrDefault(x => x.Point == TracePoints.Reharmonisation)?.Value);
    }

    [Test]
    public async Task ASubstitute_IsAThirdFromTheChordItReplaces_InTheLaterAppearanceAlone()
    {
        var reharmonised = 0;
        // each song, side by side, and where it reharmonises, again without
        var songs = TestCorpus.InParallel(Enumerable.Range(0, 32), seed =>
        {
            var (with, substitutes) = Generate(seed);
            return (Seed: seed, With: with, Substitutes: substitutes, Without: substitutes is null ? null : Generate(seed, "Reharmonisation").Song);
        });
        foreach (var (seed, with, substitutes, without) in songs)
        {
            if (substitutes is not { } found || without is null)
                continue;
            reharmonised++;
            int Root(Song song, double position) => song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetEffectiveStateMapAt(position).GetStateValue(StateKinds.ChordRoot);
            // a chord's changes, but an inner line's steps inside a chord (InnerLines), which move with the chord they are in
            var common = with.TrackEventStateTimelineMap.CommonStateTimelineMap;
            var changes = common.GetStateTimeline(StateKinds.ChordChange).Select(x => x.Position)
                .Where(x => common.GetEffectiveStateMapAt(x).GetStateValue(CompositionStateKinds.InnerLine) == 0).ToArray();
            foreach (var change in changes)
            {
                var moved = Root(with, change) - Root(without, change);
                await Assert.That(found.At.Contains(change) ? Math.Abs(moved) == 2 : moved == 0).IsTrue().Because($"seed {seed} at {change}");
            }

            var span = with.Map!.Sections[found.Appearance];
            await Assert.That(found.At.All(x => x >= span.Start && x < span.End)).IsTrue();
            await Assert.That(with.Map.Sections.Take(found.Appearance).Any(x => x.SectionId == span.SectionId)).IsTrue();
        }

        await Assert.That(reharmonised).IsGreaterThan(3);
    }

    /// <summary>A pattern's chord substituted is substituted alike at every pass of the pattern, as a band keeps the changes it learned.</summary>
    [Test]
    public async Task ASubstitute_IsTheSame_AtEveryPassOfThePattern()
    {
        var (passes, places) = (0, 0);
        foreach (var (seed, with, substitutes, without) in TestCorpus.InParallel(Enumerable.Range(0, 64), seed =>
                 {
                     var (with, substitutes) = Generate(seed);
                     return (Seed: seed, With: with, Substitutes: substitutes, Without: substitutes is null ? null : Generate(seed, "Reharmonisation").Song);
                 }))
        {
            if (substitutes is not { } found || without is null)
                continue;
            int Root(Song song, double position) => song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetEffectiveStateMapAt(position).GetStateValue(StateKinds.ChordRoot);
            var span = with.Map!.Sections[found.Appearance];
            foreach (var place in found.At.GroupBy(x => Math.Round((x - span.Start) % span.Meter.PatternDuration, 6)))
            {
                places++;
                passes += place.Count() > 1 ? 1 : 0;
                await Assert.That(place.Select(x => Root(with, x) - Root(without, x)).Distinct().Count()).IsEqualTo(1).Because($"seed {seed} at {place.Key}");
            }
        }

        Console.WriteLine($"{passes} of {places} substituted places played at more than one pass");
        await Assert.That(passes).IsGreaterThan(0);
    }
}
