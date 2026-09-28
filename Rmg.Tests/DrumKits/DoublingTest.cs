using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;

namespace Rmg.Tests.DrumKits;

public sealed class DoublingTest
{
    [Test]
    public async Task ADrumThatDoublesALead_PlaysOnlyBeatsTheLeadPlays()
    {
        var checkedNotes = 0;
        foreach (var song in TestCorpus.Range(60))
        foreach (var entry in song.Trace.Where(x => x.Point == TracePoints.Doubles))
        foreach (var (track, doubling) in (ImmutableDictionary<int, Doubling>)entry.Value!)
        foreach (var span in song.Map.Sections.Where(x => x.SectionId == entry.Section))
        {
            // the groove's notes, but in the last bar of a pattern, where a fill may take the lead's notes into its run
            bool InFillBar(double position) => (position - span.Start) % Meter.PatternDuration >= Meter.PatternDuration - Meter.BarDuration;
            double[] Groove(int t) => [..song.Song.Notes![t]
                .Where(x => x.Position >= span.Start && x.Position < span.End && !InFillBar(x.Position))
                .Select(x => Math.Round(x.Position, 6))];
            var lead = Groove(doubling.Lead).ToHashSet();
            var doubler = Groove(track);
            checkedNotes += doubler.Length;

            await Assert.That(doubler.All(lead.Contains)).IsTrue();
        }

        await Assert.That(checkedNotes).IsGreaterThan(0);
    }
}
