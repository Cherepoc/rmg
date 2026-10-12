using Rmg.Core;
using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;

namespace Rmg.Tests.Forms;

public sealed class SectionTempoTest
{
    /// <summary>Every appearance's tempo as a multiple of the song's, by its place.</summary>
    private static ImmutableArray<double> Tempos(CorpusSong song) =>
        (ImmutableArray<double>)song.Trace.Single(x => x.Point == TracePoints.SectionTempo).Value!;

    [Test]
    public async Task AnAppearance_PlaysAtTheSongsTempoTimesItsOwn_TheFirstSectionAtTheSongs()
    {
        var changed = 0;
        foreach (var song in TestCorpus.Range(128))
        {
            var tempos = Tempos(song);
            var tempo = song.Rendered.TempoTimeline;
            var songTempo = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetStateTimeline(StateKinds.Tempo).GetEffectiveValueAt(0);
            await Assert.That(tempos[0]).IsEqualTo(1);
            // every section's start, the ending's slowing after them all
            foreach (var (section, index) in song.Map.Sections.Select((x, i) => (x, i)))
                await Assert.That(tempo.GetEffectiveValueAt(section.Start)).IsEqualTo(songTempo * tempos[index]).Within(1e-9).Because($"seed {song.Seed}");
            changed += tempos.Any(x => x != 1) ? 1 : 0;
        }

        await Assert.That(changed).IsGreaterThan(0);
    }

    [Test]
    public async Task AChangeOfTempo_IsEasedIntoHalfTheTime_OverTheBarBefore_ButAMetricOne()
    {
        var (changes, eased) = (0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Facets: TestCorpus.FacetsOf((Facet.Feel, 1))))))
        {
            var tempos = Tempos(song);
            var tempo = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetStateTimeline(StateKinds.Tempo);
            var sections = song.Map.Sections;
            for (var i = 1; i < sections.Length; i++)
            {
                var (before, next) = (tempos[i - 1], tempos[i]);
                if (Math.Abs(next / before - 1) < 1e-9)
                    continue;

                // the tempo a moment before the section, the one before's where it changes at once
                var at = tempo.GetEffectiveValueAt(sections[i].Start - 1e-6) / tempo.GetEffectiveValueAt(sections[i].Start);
                var isMetric = SectionTempo.MetricRatios.Any(x => Math.Abs(x - next / before) < 1e-9);
                if (isMetric)
                    await Assert.That(Math.Abs(at - before / next)).IsLessThan(1e-9).Because($"seed {song.Seed}, a metric change at {sections[i].Start}");
                else
                {
                    changes++;
                    if (Math.Abs(at - before / next) > 1e-9)
                        eased++;
                }
            }
        }

        Console.WriteLine($"{eased} of {changes} changes of tempo eased");
        // measured 51% to 53% of 259 to 303 changes, at offsets 0, a million and 725 million
        await Assert.That(changes).IsGreaterThan(30);
        await Assert.That(eased / (double)changes).IsEqualTo(0.5).Within(0.15);
    }

    [Test]
    public async Task ThePlainestSongs_KeepTheirTempo()
    {
        foreach (var seed in Enumerable.Range(0, 8))
            await Assert.That(Tempos(TestCorpus.Get(seed, new SongOverrides(Base: 0))).All(x => x == 1)).IsTrue();
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var sections = TestCorpus.Measure(1024, song =>
        {
            var structure = (SongStructure)song.Trace.Single(x => x.Point == TracePoints.SongForm).Value!;
            var facet = song.Unconventionality[Facet.Feel];
            var tempos = Tempos(song);
            return song.Map.Sections.Select((x, i) => (x.SectionId, Index: i)).Where(x => x.SectionId != structure.SectionIds[0])
                .Select(x => (Band: Math.Clamp((int)(facet * 5), 0, 4), Contrasts: structure.Roles[x.SectionId] is SectionRole.Chorus or SectionRole.Bridge, Factor: tempos[x.Index],
                    Metric: x.Index > 0 && SectionTempo.MetricRatios.Any(r => Math.Abs(tempos[x.Index] / tempos[x.Index - 1] - r) < 1e-9)))
                .ToArray();
        }).SelectMany(x => x).ToArray();

        foreach (var band in sections.GroupBy(x => x.Band).OrderBy(x => x.Key))
            Console.WriteLine($"  feel {band.Key}/5: " + string.Join("; ", band.GroupBy(x => x.Contrasts).OrderBy(x => x.Key)
                .Select(x => $"{(x.Key ? "chorus or bridge" : "other")} {x.Count()}, own tempo {x.Count(y => y.Factor != 1) / (double)x.Count():P0}")));
        Console.WriteLine($"  factors {string.Join(", ", sections.Where(x => x.Factor != 1).GroupBy(x => Math.Round(x.Factor, 2)).OrderBy(x => x.Key).Select(x => $"{x.Key} {x.Count()}"))}");
        foreach (var band in sections.GroupBy(x => x.Band).OrderBy(x => x.Key))
            Console.WriteLine($"  feel {band.Key}/5: metric changes from the tempo before {band.Count(x => x.Metric) / (double)band.Count():P1}");
        await Task.CompletedTask;
    }
}
