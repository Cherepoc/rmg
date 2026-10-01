using Rmg.Core;
using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;

namespace Rmg.Tests.Forms;

public sealed class SectionTempoTest
{
    private static ImmutableDictionary<int, double> Tempos(CorpusSong song) =>
        (ImmutableDictionary<int, double>)song.Trace.Single(x => x.Point == TracePoints.SectionTempo).Value!;

    [Test]
    public async Task ASection_PlaysAtTheSongsTempoTimesItsOwn_EveryTimeItPlays_TheFirstAtTheSongs()
    {
        var changed = 0;
        foreach (var song in TestCorpus.Range(128))
        {
            var tempos = Tempos(song);
            var tempo = song.Rendered.TempoTimeline;
            var songTempo = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetStateTimeline(StateKinds.Tempo).GetEffectiveValueAt(0);
            await Assert.That(tempos[song.Map.Sections[0].SectionId]).IsEqualTo(1);
            // every section's start, the ending's slowing after them all
            foreach (var section in song.Map.Sections)
                await Assert.That(tempo.GetEffectiveValueAt(section.Start)).IsEqualTo(songTempo * tempos[section.SectionId]).Within(1e-9).Because($"seed {song.Seed}");
            changed += tempos.Values.Any(x => x != 1) ? 1 : 0;
        }

        await Assert.That(changed).IsGreaterThan(0);
    }

    [Test]
    public async Task AChangeOfTempo_IsEasedIntoHalfTheTime_OverTheBarBefore_ButAMetricOne()
    {
        var (changes, eased) = (0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Facets: ImmutableDictionary<Facet, double>.Empty.Add(Facet.Feel, 1)))))
        {
            var tempos = Tempos(song);
            var tempo = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetStateTimeline(StateKinds.Tempo);
            var sections = song.Map.Sections;
            for (var i = 1; i < sections.Length; i++)
            {
                var (before, next) = (tempos[sections[i - 1].SectionId], tempos[sections[i].SectionId]);
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

        await Assert.That(changes).IsGreaterThan(30);
        await Assert.That(eased / (double)changes).IsEqualTo(0.5).Within(0.15);
    }

    [Test]
    public async Task ThePlainestSongs_KeepTheirTempo()
    {
        foreach (var seed in Enumerable.Range(0, 8))
            await Assert.That(Tempos(TestCorpus.Get(seed, new SongOverrides(Base: 0))).Values.All(x => x == 1)).IsTrue();
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var sections = TestCorpus.Measure(1024, song =>
        {
            var structure = (SongStructure)song.Trace.Single(x => x.Point == TracePoints.SongForm).Value!;
            var facet = ((Unconventionality)song.Trace.Single(x => x.Point == TracePoints.SongUnconventionality).Value!)[Facet.Feel];
            return Tempos(song).Where(x => x.Key != structure.SectionIds[0])
                .Select(x => (Band: Math.Clamp((int)(facet * 5), 0, 4), Contrasts: structure.Roles[x.Key] is SectionRole.Chorus or SectionRole.Bridge, Factor: x.Value))
                .ToArray();
        }).SelectMany(x => x).ToArray();

        foreach (var band in sections.GroupBy(x => x.Band).OrderBy(x => x.Key))
            Console.WriteLine($"  feel {band.Key}/5: " + string.Join("; ", band.GroupBy(x => x.Contrasts).OrderBy(x => x.Key)
                .Select(x => $"{(x.Key ? "chorus or bridge" : "other")} {x.Count()}, own tempo {x.Count(y => y.Factor != 1) / (double)x.Count():P0}")));
        Console.WriteLine($"  factors {string.Join(", ", sections.Where(x => x.Factor != 1).GroupBy(x => Math.Round(x.Factor, 2)).OrderBy(x => x.Key).Select(x => $"{x.Key} {x.Count()}"))}");
        await Task.CompletedTask;
    }
}
