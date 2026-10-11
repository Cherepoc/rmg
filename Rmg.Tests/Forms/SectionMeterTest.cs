using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;

namespace Rmg.Tests.Forms;

public sealed class SectionMeterTest
{
    private static ImmutableDictionary<int, Meter> Meters(CorpusSong song) =>
        (ImmutableDictionary<int, Meter>)song.Trace.Single(x => x.Point == TracePoints.SectionMeter).Value!;

    [Test]
    public async Task ASection_PlaysWholePatternsOfItsMeter_TheFileSigningEveryChange()
    {
        var changing = 0;
        foreach (var song in TestCorpus.Range(128))
        {
            var meters = Meters(song);
            await Assert.That(meters[song.Map.Sections[0].SectionId]).IsEqualTo(song.Map.Meter);
            foreach (var span in song.Map.Sections)
            {
                await Assert.That(span.Meter).IsEqualTo(meters[span.SectionId]);
                // a short last bar's cut aside (ShortBars)
                var patterns = (span.Duration + span.Cut) / span.Meter.PatternDuration;
                await Assert.That(Math.Abs(patterns - Math.Round(patterns))).IsLessThan(1e-9);
            }

            // a time signature where the song starts, where a section's bar is not the bar before it, whatever its phrases,
            // and where a short last bar starts, the section after it signing its own again where it is not the short one
            Meter BarBefore(int i) => song.Map.Sections[i - 1] is var before && before.Cut > 0 ? ShortBars.Of(before.Meter) : before.Meter;
            var changes = song.Map.Sections.SelectMany((x, i) =>
                (i > 0 && !x.Meter.HasSameBar(BarBefore(i)) ? new[] { x.Start } : [])
                .Concat(x.Cut > 0 ? [x.End - x.Meter.BarDuration + x.Cut] : []));
            await Assert.That(song.Rendered.Meters.Select(x => x.Position).ToArray()).IsEquivalentTo(new[] { 0.0 }.Concat(changes).ToArray(), CollectionOrdering.Matching).Because($"seed {song.Seed}");
            changing += song.Rendered.Meters.Length > 1 ? 1 : 0;
        }

        await Assert.That(changing).IsGreaterThan(0);
    }

    [Test]
    public async Task ThePlainestSongs_KeepTheirMeter()
    {
        foreach (var seed in Enumerable.Range(0, 8))
            await Assert.That(Meters(TestCorpus.Get(seed, new SongOverrides(Base: 0))).Values.All(x => x.HasSameBar(Meter.FourFour))).IsTrue();
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var sections = TestCorpus.Measure(1024, song =>
        {
            var structure = (SongStructure)song.Trace.Single(x => x.Point == TracePoints.SongForm).Value!;
            var facet = song.Unconventionality[Facet.Feel];
            return Meters(song).Where(x => x.Key != structure.SectionIds[0])
                .Select(x => (Band: Math.Clamp((int)(facet * 5), 0, 4), Contrasts: structure.Roles[x.Key] is SectionRole.Chorus or SectionRole.Bridge, Own: !x.Value.Equals(song.Map.Meter), x.Value))
                .ToArray();
        }).SelectMany(x => x).ToArray();

        foreach (var band in sections.GroupBy(x => x.Band).OrderBy(x => x.Key))
            Console.WriteLine($"  feel {band.Key}/5: " + string.Join("; ", band.GroupBy(x => x.Contrasts).OrderBy(x => x.Key)
                .Select(x => $"{(x.Key ? "chorus or bridge" : "other")} {x.Count()}, own meter {x.Count(y => y.Own) / (double)x.Count():P0}")));
        Console.WriteLine($"  meters {string.Join(", ", sections.Where(x => x.Own).GroupBy(x => x.Value.ToString()).OrderByDescending(x => x.Count()).Select(x => $"{x.Key} {x.Count()}"))}");
        await Task.CompletedTask;
    }
}
