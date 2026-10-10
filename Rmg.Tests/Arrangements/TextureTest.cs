using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Tests.Arrangements;

public sealed class TextureTest
{
    [Test]
    public async Task NoAppearanceOfASection_FallsSilent_ItsPartAloneOneWithNotesThere()
    {
        var silent = TestCorpus.Measure(512, song => song.Map.Sections
            // a note sounding past its first beat, not a landing on its downbeat out of the section before
            .Where(span => !song.Song.Notes!.Values.Any(notes => notes.Any(n => n.Position < span.End - 1e-9 && n.Position + n.Value.Duration > Math.Min(span.End, span.Start + 1) + 1e-9)))
            .Select(span => $"seed {song.Seed} at {span.Start}")
            .ToArray()).SelectMany(x => x).ToArray();

        await Assert.That(silent).IsEmpty().Because(string.Join("; ", silent.Take(5)));
    }

    [Test]
    public async Task DrumsMarkingTheForm_NeverPlayASectionAlone_APitchedPartPlayingWithThem()
    {
        var alone = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Genres.GenrePresets.Genre("medieval")))))
        {
            if ((DrumJob)song.Trace.Single(t => t.Point == TracePoints.DrumJob).Value! != DrumJob.Marks)
                continue;
            // but a drum solo's, which is the drums alone
            foreach (var (span, _) in song.Map.Sections.Select((x, i) => (x, i)).Where(x => song.Texture(x.x).Kind == TextureKind.Alone && !(song.Solos.TryGetValue(x.i, out var solo) && solo.IsDrumSolo)))
            {
                alone++;
                var pitched = song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key] is PitchInstrumentTrack)
                    .Sum(x => x.Value.Count(n => n.Position >= span.Start - 1e-9 && n.Position < span.End - 1e-9));
                await Assert.That(pitched).IsGreaterThan(0).Because($"seed {song.Seed} at {span.Start}");
            }
        }

        await Assert.That(alone).IsGreaterThan(10);
    }

    [Test]
    public async Task TheTexturesOfFewerParts_LeanToTheQuieterSections()
    {
        var context = new GenerationContext(3);
        int Fewer(double energy) => Enumerable.Range(0, 4000).Count(_ => Texture.Draw(context, 1, 2, Band, Genre.None, SectionEnergy.Tilt(energy, 1), false).Kind is TextureKind.Alone or TextureKind.Thin);

        await Assert.That(Fewer(0.5)).IsLessThan(Fewer(0)).And.IsLessThan(Fewer(-0.5));
        await Assert.That(Fewer(0)).IsLessThan(Fewer(-0.5));
    }

    private static readonly TrackRole[] Band = [TrackRole.Melody, TrackRole.Chords, TrackRole.Bass, TrackRole.Pad, TrackRole.Drum];

    [Test]
    public async Task ThePlainestSections_PlaySteady_AndTheWildest_EveryTextureAlike()
    {
        var context = new GenerationContext(1);
        var plain = Enumerable.Range(0, 2000).Select(_ => Texture.Draw(context, 0, 2, Band, Genre.None, default, false).Kind).ToArray();
        var wild = Enumerable.Range(0, 4000).Select(_ => Texture.Draw(context, 1, 2, Band, Genre.None, default, false).Kind).ToArray();

        await Assert.That(plain.All(x => x == TextureKind.Steady)).IsTrue();
        foreach (var kind in Enum.GetValues<TextureKind>())
            await Assert.That(wild.Count(x => x == kind) / (double)wild.Length).IsEqualTo(0.25).Within(0.03);
    }

    [Test]
    public async Task ABuild_BringsItsPartsIn_PhraseByPhrase_ItsDrumsNowAndThenLate_AndAThinTakesItsPitchedOnesOut()
    {
        var context = new GenerationContext(2);
        var draws = Enumerable.Range(0, 400).Select(_ => Texture.Draw(context, 1, 4, Band, Genre.None, default, false)).ToArray();
        foreach (var (kind, resting, silent) in draws.Where(x => x.Kind is TextureKind.Build or TextureKind.Thin))
        {
            var counts = silent.Select(x => x.Count).ToArray();
            var order = kind == TextureKind.Build ? counts : [..counts.Reverse()];

            await Assert.That(resting).IsEmpty();
            await Assert.That(order.Zip(order.Skip(1)).All(x => x.First >= x.Second)).IsTrue();
            await Assert.That(order[^1]).IsEqualTo(0);
            await Assert.That(order[0]).IsLessThan(4);
            // a build's drums come in as a part, once and for good; a thinning's play throughout
            if (kind == TextureKind.Thin)
                await Assert.That(silent.All(x => !x.Contains(TrackRole.Drum))).IsTrue();
            else
                await Assert.That(silent.SkipWhile(x => x.Contains(TrackRole.Drum)).All(x => !x.Contains(TrackRole.Drum))).IsTrue();
        }

        await Assert.That(draws.Any(x => x.Kind == TextureKind.Build && x.PhraseSilent.Any(p => p.Contains(TrackRole.Drum)))).IsTrue();

        foreach (var (_, resting, _) in draws.Where(x => x.Kind == TextureKind.Alone))
            await Assert.That(resting.Count).IsEqualTo(Band.Length - 1);
    }

    [Test]
    public async Task DrumsABuildBringsInLate_PlayNothingBefore_ButTheFillTheyComeInOn()
    {
        var late = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 128), seed => TestCorpus.Get(seed, new SongOverrides(Facets: ImmutableDictionary<Facet, double>.Empty.Add(Facet.Form, 1)))))
        foreach (var (span, index) in song.Map.Sections.Select((x, i) => (x, i)))
        {
            var from = song.DrumsFrom(index);
            if (from == 0 || !song.HasDrums(span))
                continue;

            // the drums' notes before the phrase they come in at, but in its last bar before, the fill's, and on its first
            // beat, where the section before's fill lands before they drop out
            var entry = span.Start + from * span.Meter.PatternDuration;
            var before = song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key].Role == TrackRole.Drum).SelectMany(x => x.Value)
                .Count(x => x.Position > span.Start + 1e-9 && x.Position < entry - span.Meter.BarDuration - 1e-9);
            await Assert.That(before).IsEqualTo(0).Because($"seed {song.Seed} at {span.Start}");
            late++;
        }

        await Assert.That(late).IsGreaterThan(5);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var songs = TestCorpus.Range(256);
        var rows = songs.SelectMany(song => song.Map.Sections.Select(span => (Form: song.Trace.Where(x => x.Point == TracePoints.SectionUnconventionality && x.Section == span.SectionId).Select(x => ((Unconventionality)x.Value!)[Facet.Form]).First(), Texture: song.Texture(span), Resting: song.Resting(span), Song: song))).ToArray();
        Console.WriteLine("textures by the section's form facet, in fifths:");
        foreach (var band in rows.GroupBy(x => Math.Clamp((int)(x.Form * 5), 0, 4)).OrderBy(x => x.Key))
            Console.WriteLine($"  {band.Key}/5, {band.Count()} appearances: " + string.Join(", ", Enum.GetValues<TextureKind>().Select(k => $"{k} {band.Count(x => x.Texture.Kind == k) / (double)band.Count():P0}")));
        var alone = rows.Where(x => x.Texture.Kind == TextureKind.Alone).Select(x => x.Song.Song.TrackDefinitions.Values.Select(d => d.Role).Distinct().Single(r => !x.Resting.Contains(r))).GroupBy(x => x);
        Console.WriteLine($"alone: {string.Join(", ", alone.Select(x => $"{x.Key} {x.Count()}"))}");
        await Task.CompletedTask;
    }

    /// <summary>A section's appearances of fewer parts, one alone or parts dropping out, by its energy, and its drums alone where they mark the form.</summary>
    [Test]
    [Explicit]
    public async Task ReportByEnergy()
    {
        foreach (var name in new[] { "medieval", "orchestral", "dungeon", "ambient", "" })
        {
            var genre = name == "" ? null : Genres.GenrePresets.Genre(name);
            var songs = TestCorpus.InParallel(Enumerable.Range(0, 128), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre)));
            var (quiet, loud, quietFewer, loudFewer, choruses, chorusesAlone, marking, drumsAlone) = (0, 0, 0, 0, 0, 0, 0, 0);
            foreach (var song in songs)
            {
                var energies = song.Trace.Where(t => t.Point == TracePoints.SectionEnergy).GroupBy(t => t.Section).ToDictionary(g => g.Key, g => ((SectionEnergyTrace)g.First().Value!).Energy);
                var mean = song.Map.Sections.Average(x => energies.GetValueOrDefault(x.SectionId));
                var structure = (SongStructure)song.Trace.Single(t => t.Point == TracePoints.SongForm).Value!;
                var marks = (DrumJob)song.Trace.Single(t => t.Point == TracePoints.DrumJob).Value! == DrumJob.Marks;
                foreach (var span in song.Map.Sections)
                {
                    var kind = song.Texture(span).Kind;
                    var fewer = kind is TextureKind.Alone or TextureKind.Thin;
                    if (energies.GetValueOrDefault(span.SectionId) > mean + 1e-9)
                        (loud, loudFewer) = (loud + 1, loudFewer + (fewer ? 1 : 0));
                    else
                        (quiet, quietFewer) = (quiet + 1, quietFewer + (fewer ? 1 : 0));
                    if (structure.Roles.GetValueOrDefault(span.SectionId) == SectionRole.Chorus)
                        (choruses, chorusesAlone) = (choruses + 1, chorusesAlone + (kind == TextureKind.Alone ? 1 : 0));
                    if (marks && kind == TextureKind.Alone)
                    {
                        marking++;
                        if (!song.Resting(span).Contains(TrackRole.Drum))
                            drumsAlone++;
                    }
                }
            }

            Console.WriteLine($"{(name == "" ? "no genre" : name)}: one part alone or parts dropping out in {quietFewer / (double)Math.Max(1, quiet):P0} of the quieter appearances, " +
                              $"{loudFewer / (double)Math.Max(1, loud):P0} of the louder; choruses alone {chorusesAlone / (double)Math.Max(1, choruses):P0}; " +
                              $"of the appearances alone where the drums mark the form, the drums {drumsAlone} of {marking}");
        }

        await Task.CompletedTask;
    }

    /// <summary>Which part plays a section alone, by genre, where its texture leaves one.</summary>
    [Test]
    [Explicit]
    public async Task ReportAlone()
    {
        foreach (var name in new[] { "metal", "nuMetal", "djent", "blackMetal", "ambient", "" })
        {
            var genre = name == "" ? null : Genres.GenrePresets.Genre(name);
            var songs = TestCorpus.InParallel(Enumerable.Range(0, 128), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre)));
            var alone = songs.SelectMany(song => song.Map.Sections.Select((span, i) => (span, i))
                    .Where(x => song.Texture(x.span).Kind == TextureKind.Alone && !(song.Solos.TryGetValue(x.i, out var solo) && solo.IsDrumSolo))
                    // the part playing the most of it, past a fill's landing and the drums' fill into the next
                    .Select(x => song.Song.Notes!.Where(n => song.Song.TrackDefinitions[n.Key].Role != TrackRole.RiffTwin)
                        .MaxBy(n => n.Value.Count(v => v.Position >= x.span.Start - 1e-9 && v.Position < x.span.End - 1e-9)))
                    .Select(n => song.Song.TrackDefinitions[n.Key].Role))
                .ToArray();
            Console.WriteLine($"{(name == "" ? "no genre" : name)}: {alone.Length} appearances alone, {string.Join(", ", alone.GroupBy(x => x).OrderByDescending(x => x.Count()).Select(x => $"{x.Key} {x.Count() / (double)alone.Length:P0}"))}");
        }

        await Task.CompletedTask;
    }
}
