using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;

namespace Rmg.Tests.DrumKits;

public sealed class DoublingTest
{
    [Test]
    public async Task ADrumThatDoublesOrAccentsALead_PlaysOnlyBeatsTheLeadPlays_AnAccentFewerOfThem()
    {
        var checkedNotes = 0;
        int accentNotes = 0, accentedLeadNotes = 0;
        foreach (var song in TestCorpus.Range(100))
        foreach (var entry in song.Trace.Where(x => x.Point == TracePoints.Doubles))
        foreach (var (track, doubling) in ((ImmutableDictionary<int, Doubling>)entry.Value!).Where(x => x.Value.Binding != DrumBinding.Figure))
        foreach (var span in song.Map.Sections.Where(x => x.SectionId == entry.Section))
        {
            // the groove's notes, but in the last bar of a pattern, where a fill may take the lead's notes into its run, and
            // at a pattern's first beat, where a call may land with the band, pushed an 8th early now and then
            bool InFillBar(double position)
            {
                var inPattern = (position - span.Start) % Meter.PatternDuration;
                return inPattern >= Meter.PatternDuration - Meter.BarDuration || inPattern < 0.5;
            }
            // a drum the song never plays has no notes
            double[] Groove(int t) => [..song.Song.Notes!.GetValueOrDefault(t, EventTimeline.Create<Rmg.Core.Songs.RealizedNote>(0))
                .Where(x => x.Position >= span.Start && x.Position < span.End && !InFillBar(x.Position))
                .Select(x => Math.Round(x.Position, 6))];
            var lead = Groove(doubling.Lead).ToHashSet();
            var doubler = Groove(track);
            checkedNotes += doubler.Length;
            if (doubling.Binding == DrumBinding.Accent)
            {
                accentNotes += doubler.Length;
                accentedLeadNotes += lead.Count;
            }

            await Assert.That(doubler.All(lead.Contains)).IsTrue();
        }

        await Assert.That(checkedNotes).IsGreaterThan(0);
        // an accent plays about a share of the lead's beats up to its rank, fewer than all of the lead's notes
        await Assert.That(accentNotes).IsGreaterThan(0);
        await Assert.That((double)accentNotes).IsLessThan(accentedLeadNotes * DrumKitGenerator.AccentShare);
    }
}
