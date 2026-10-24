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

    /// <summary>
    ///     A reharmonised appearance's chords changed around its melody, as an arranger keeps the tune: the melody's every
    ///     note the same as the song's without the reharmonisation, its substitutes kept only where the tune fits them, but
    ///     a double stop's note under it, the band's chord's.
    /// </summary>
    [Test]
    public async Task AReharmonisedAppearance_KeepsItsMelody_EveryNote()
    {
        var reharmonised = 0;
        foreach (var (seed, with, substitutes, without) in TestCorpus.InParallel(Enumerable.Range(0, 32), seed =>
                 {
                     var (with, substitutes) = Generate(seed);
                     return (Seed: seed, With: with, Substitutes: substitutes, Without: substitutes is null ? null : Generate(seed, "Reharmonisation").Song);
                 }))
        {
            if (substitutes is null || without is null)
                continue;
            reharmonised++;
            // each note as its place and its own pitch, a double stop's under it the band's chord's, which changes with it
            string[] Melody(Song song) => [..song.Notes!.GetValueOrDefault(song.Track(TrackRole.Melody))?.Select(x => $"{x.Position:F3} {x.Value.Pitches[0]}") ?? []];
            await Assert.That(Melody(with)).IsEquivalentTo(Melody(without), CollectionOrdering.Matching).Because($"seed {seed}");
        }

        await Assert.That(reharmonised).IsGreaterThan(3);
    }

    [Test]
    public async Task ASubstitute_IsAThirdFromTheChordItReplaces_InTheLaterAppearanceAndItsPassesAlone()
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

            // in the appearance, or a pass of its section straight after it, a fade's
            var span = with.Map!.Sections[found.Appearance];
            var end = with.Map.Sections.Skip(found.Appearance).TakeWhile(x => x.SectionId == span.SectionId).Last().End;
            await Assert.That(found.At.All(x => x >= span.Start && x < end)).IsTrue();
            await Assert.That(with.Map.Sections.Take(found.Appearance).Any(x => x.SectionId == span.SectionId)).IsTrue();
        }

        await Assert.That(reharmonised).IsGreaterThan(3);
    }

    /// <summary>A pattern's chord substituted is substituted alike at every pass of the pattern, a fade's passes after it too, as a band keeps the changes it learned.</summary>
    [Test]
    public async Task ASubstitute_IsTheSame_AtEveryPassOfThePattern()
    {
        var (passes, places, faded) = (0, 0, 0);
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
            // a fade's passes after the appearance playing its changes on
            faded += found.At.Count(x => x >= span.End - 1e-9);
            foreach (var place in found.At.GroupBy(x => Math.Round((x - span.Start) % span.Meter.PatternDuration, 6)))
            {
                places++;
                passes += place.Count() > 1 ? 1 : 0;
                await Assert.That(place.Select(x => Root(with, x) - Root(without, x)).Distinct().Count()).IsEqualTo(1).Because($"seed {seed} at {place.Key}");
            }
        }

        Console.WriteLine($"{passes} of {places} substituted places played at more than one pass, {faded} substitutes in a fade's passes");
        await Assert.That(passes).IsGreaterThan(0);
        await Assert.That(faded).IsGreaterThan(0);
    }
}
