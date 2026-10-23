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
    ///     A reharmonised appearance's melody sings the tune its section's appearance before sang, as an arranger keeps the
    ///     tune and changes the chords under it: its notes over the substitutes the same as heard before, where they fit the
    ///     new chord, but where it lands on a chord or leads into the next (measured over 64 songs: 59-68% at offsets 0 and a
    ///     million, from 15-25% placed anew over them).
    /// </summary>
    [Test]
    public async Task AReharmonisedAppearance_SingsItsTuneAsHeardBefore()
    {
        var (notes, kept) = (0, 0);
        foreach (var (song, substitutes) in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => Generate(seed)))
        {
            if (substitutes is not { } found)
                continue;
            var span = song.Map!.Sections[found.Appearance];
            var source = song.Map.Sections.Take(found.Appearance).Last(x => x.SectionId == span.SectionId);
            var melody = song.Notes!.GetValueOrDefault(song.Track(TrackRole.Melody))?.ToArray() ?? [];
            var sung = melody.GroupBy(x => Math.Round(x.Position, 6)).ToDictionary(x => x.Key, x => x.First().Value.Pitches[0]);
            // the notes over a substitute, from its change to the next
            var common = song.TrackEventStateTimelineMap.CommonStateTimelineMap;
            var changes = common.GetStateTimeline(StateKinds.ChordChange).Select(x => x.Position).Append(span.End).ToArray();
            bool OverSubstitute(double at) => found.At.Any(x => at >= x - 1e-9 && at < changes.First(c => c > x + 1e-9) - 1e-9);
            foreach (var note in melody.Where(x => span.IsWithin(x.Position) && OverSubstitute(x.Position)))
                if (sung.TryGetValue(Math.Round(note.Position - span.Start + source.Start, 6), out var before))
                {
                    notes++;
                    kept += note.Value.Pitches[0] == before ? 1 : 0;
                }
        }

        Console.WriteLine($"  {kept} of {notes} melody notes over a substitute as sung before ({kept / (double)Math.Max(1, notes):P0})");
        await Assert.That(notes).IsGreaterThan(20);
        await Assert.That(kept / (double)notes).IsGreaterThan(0.4);
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
