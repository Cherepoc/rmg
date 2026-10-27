using System.Collections.Immutable;
using Rmg.Core;
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
        foreach (var (seed, with, substitutes, without) in TestCorpus.Streamed(Enumerable.Range(0, 32), seed =>
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

    /// <summary>
    ///     A secondary dominant substituted leads into the next chord: its root a fifth above the next chord's, its triad
    ///     major and its seventh flat, Mixolydian on its root, as a soul arranger leads into a chorus's chords.
    /// </summary>
    [Test]
    public async Task ASecondaryDominant_IsTheNextChordsDominant()
    {
        var genre = Rmg.Tests.Genres.GenrePresets.With("soul", GenreChoices.ReharmonisedChords, ("third", 0), ("applied", 1), ("borrowed", 0));
        // every song reharmonising
        genre = genre with { Choices = genre.Choices.SetItem(GenreChoices.HarmonyDevices.Name, ImmutableDictionary<string, Rmg.Core.Probabilities.ByConvention>.Empty.Add("reharmonised", new(1, 1, 1))) };
        var dominants = 0;
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre))))
        {
            if (song.Trace.Single(x => x.Point == TracePoints.Reharmonisation).Value is not (int, ImmutableArray<double> at))
                continue;
            var common = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap;
            var changes = common.GetStateTimeline(StateKinds.ChordChange).Select(x => x.Position).ToArray();
            foreach (var position in at)
            {
                var next = changes.FirstOrDefault(x => x > position + 1e-9, double.NaN);
                if (double.IsNaN(next))
                    continue;
                dominants++;
                var (_, _, chord) = Realizer.GetChord(common.GetEffectiveStateMapAt(position));
                var (_, _, target) = Realizer.GetChord(common.GetEffectiveStateMapAt(next));
                var because = $"seed {song.Seed} at {position}";
                await Assert.That((target.GetPitch(0) - chord.GetPitch(0)).Mod(12)).IsEqualTo(5).Because(because);
                await Assert.That((chord.GetPitch(2) - chord.GetPitch(0)).Mod(12)).IsEqualTo(4).Because(because);
                await Assert.That((chord.GetPitch(6) - chord.GetPitch(0)).Mod(12)).IsEqualTo(10).Because(because);
            }
        }

        Console.WriteLine($"{dominants} secondary dominants in 64 Soul songs asked for them");
        await Assert.That(dominants).IsGreaterThan(10);
    }

    /// <summary>A pattern's chord substituted is substituted alike at every pass of the pattern, a fade's passes after it too, as a band keeps the changes it learned.</summary>
    [Test]
    public async Task ASubstitute_IsTheSame_AtEveryPassOfThePattern()
    {
        var (passes, places, faded, ends, all) = (0, 0, 0, 0, 0);
        foreach (var (seed, with, substitutes, without) in TestCorpus.Streamed(Enumerable.Range(0, 64), seed =>
                 {
                     var (with, substitutes) = Generate(seed);
                     return (Seed: seed, With: with, Substitutes: substitutes, Without: substitutes is null ? null : Generate(seed, "Reharmonisation").Song);
                 }))
        {
            if (substitutes is not { } found || without is null)
                continue;
            int Root(Song song, double position) => song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetEffectiveStateMapAt(position).GetStateValue(StateKinds.ChordRoot);
            var span = with.Map!.Sections[found.Appearance];
            // a fade's passes after the appearance playing its changes on, and those in a phrase's last bar, where an arranger
            // reharmonises
            faded += found.At.Count(x => x >= span.End - 1e-9);
            ends += found.At.Count(x => (x - span.Start) % span.Meter.PhraseDuration >= span.Meter.PhraseDuration - span.Meter.BarDuration - 1e-9);
            all += found.At.Length;
            foreach (var place in found.At.GroupBy(x => Math.Round((x - span.Start) % span.Meter.PatternDuration, 6)))
            {
                places++;
                passes += place.Count() > 1 ? 1 : 0;
                await Assert.That(place.Select(x => Root(with, x) - Root(without, x)).Distinct().Count()).IsEqualTo(1).Because($"seed {seed} at {place.Key}");
            }
        }

        Console.WriteLine($"{passes} of {places} substituted places played at more than one pass, {faded} substitutes in a fade's passes, {ends} of {all} substitutes in a phrase's last bar");
        await Assert.That(passes).IsGreaterThan(0);
        await Assert.That(faded).IsGreaterThan(0);
    }

    /// <summary>The shapes a Soul song's substitutes play, its own kinds, and secondary dominants alone, by the chords' notes there.</summary>
    [Test]
    [Explicit]
    public async Task ShapesReport()
    {
        foreach (var (name, kinds) in new[] { ("soul", new[] { ("third", 1.0), ("applied", 0.4), ("borrowed", 0.2) }), ("dominants", [("third", 0.0), ("applied", 1.0), ("borrowed", 0.0)]) })
        {
            var genre = Rmg.Tests.Genres.GenrePresets.With("soul", GenreChoices.ReharmonisedChords, kinds);
            genre = genre with { Choices = genre.Choices.SetItem(GenreChoices.HarmonyDevices.Name, ImmutableDictionary<string, Rmg.Core.Probabilities.ByConvention>.Empty.Add("reharmonised", new(1, 1, 1))) };
            var shapes = new List<string>();
            var others = new List<string>();
            foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre))))
            {
                if (song.Trace.Single(x => x.Point == TracePoints.Reharmonisation).Value is not (int index, ImmutableArray<double> at))
                    continue;
                var chords = song.Song.TrackEventStateTimelineMap.TrackTimelineMap.GetValueOrDefault(song.Track(TrackRole.Chords))?.EventTimeline.ToArray() ?? [];
                string? ShapeAt(double position) => chords.Where(n => n.Position <= position + 1e-6).Select(n => n.Value).LastOrDefault() is { } state
                    ? Classes(state.GetStateValue(StateKinds.ChordNotePitchOffsets).Select(h => h * 12)) is var classes && classes.Count == 0 ? "none"
                    : ChordShapes.All.FirstOrDefault(x => Classes(x.Targets).SetEquals(classes))?.Name ?? "other" : null;
                shapes.AddRange(at.Select(ShapeAt).OfType<string>());
                var span = song.Map.Sections[index];
                others.AddRange(song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetStateTimeline(StateKinds.ChordChange).Select(x => x.Position)
                    .Where(x => span.IsWithin(x) && !at.Contains(x)).Select(ShapeAt).OfType<string>());
            }

            // a shape's notes by their place in the octave, as voicing leaves them
            static HashSet<double> Classes(IEnumerable<double> semitones) => [..semitones.Select(x => Math.Round((x % 12 + 12) % 12, 2))];
            string Of(List<string> all) => string.Join(", ", all.GroupBy(x => x).OrderByDescending(x => x.Count()).Select(x => $"{x.Key} {x.Count() / (double)all.Count:P0}"));
            Console.WriteLine($"{name}: {shapes.Count} substitutes: {Of(shapes)}");
            Console.WriteLine($"  {others.Count} other chords of the appearance: {Of(others)}");
        }

        await Task.CompletedTask;
    }
}
