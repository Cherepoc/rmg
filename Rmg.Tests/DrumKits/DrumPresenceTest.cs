using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.DrumKits;

public sealed class DrumPresenceTest
{
    [Test]
    public async Task OnlyOptionalDrums_SitOut_AndNeverInTheFirstBarPattern()
    {
        var restingCount = 0;
        foreach (var song in TestCorpus.Range(40))
        foreach (var entry in song.Trace.Where(x => x.Point == TracePoints.DrumPresence))
        foreach (var (track, letter) in (ImmutableHashSet<(int Track, int Letter)>)entry.Value!)
        {
            restingCount++;
            await Assert.That(DrumGroups.GetGroup(track).IsAlwaysOn).IsFalse();
            await Assert.That(letter).IsGreaterThan(0);
        }

        await Assert.That(restingCount).IsGreaterThan(0);
    }

    [Test]
    public async Task ADrumSittingOut_PlaysNoNotes_InTheBarsOfItsLetter()
    {
        foreach (var song in TestCorpus.Range(40))
        {
            var schemes = song.Trace.Where(x => x.Point == TracePoints.BarPattern).GroupBy(x => x.Section).ToDictionary(x => x.Key, x => x.First().Phrase!.Replace("′", ""));
            foreach (var entry in song.Trace.Where(x => x.Point == TracePoints.DrumPresence))
            foreach (var (track, letter) in (ImmutableHashSet<(int Track, int Letter)>)entry.Value!)
            foreach (var span in song.Map.Sections.Where(x => x.SectionId == entry.Section))
            {
                // the bars of its letter, but the last of a pattern, where a fill may play it
                var bars = schemes[entry.Section].Select((x, bar) => (x, bar)).Where(x => x.x - 'A' == letter && x.bar < Meter.PatternBarCount - 1).Select(x => x.bar);
                foreach (var bar in bars)
                {
                    var start = span.Start + bar * Meter.BarDuration;
                    await Assert.That(song.Song.Notes![track].Count(x => x.Position >= start && x.Position < start + Meter.BarDuration)).IsEqualTo(0);
                }
            }
        }
    }

    [Test]
    public async Task APlainSection_KeepsItsDrums_MoreThanAWildOne()
    {
        var scheme = new PhraseScheme([0, 1, 0, 1], [false, false, false, false]);
        int[] tracks = [DrumGroups.GetTrackNumber(DrumDefinitions.HiHat), DrumGroups.GetTrackNumber(DrumDefinitions.Conga)];

        double Share(double unconventionality)
        {
            var context = new GenerationContext(1);
            return Enumerable.Range(0, 2_000)
                .Average(_ => DrumPresence.Draw(context, tracks, scheme, new RhythmicUnconventionality(unconventionality).Tilt, Tilt.None).Count / 2.0);
        }

        await Assert.That(Share(0)).IsLessThan(0.15);
        await Assert.That(Share(0.5)).IsEqualTo(DrumPresence.SitOutChance).Within(0.03);
        await Assert.That(Share(1)).IsGreaterThan(0.5);
    }
}
