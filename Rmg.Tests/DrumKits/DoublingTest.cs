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
                var inPattern = (position - span.Start) % Meter.FourFour.PatternDuration;
                return inPattern >= Meter.FourFour.PatternDuration - Meter.FourFour.BarDuration || inPattern < 0.5;
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

            await Assert.That(doubler.All(lead.Contains)).IsTrue()
                .Because($"seed {song.Seed}, section {entry.Section} at {span.Start}, {DrumGroups.GetDrum(track).Name} on {DrumGroups.GetDrum(doubling.Lead).Name}: {string.Join(", ", doubler.Where(x => !lead.Contains(x)).Take(5))}");
        }

        await Assert.That(checkedNotes).IsGreaterThan(0);
        // an accent plays about a share of the lead's beats up to its rank, fewer than all of the lead's notes
        await Assert.That(accentNotes).IsGreaterThan(0);
        await Assert.That((double)accentNotes).IsLessThan(accentedLeadNotes * DrumKitGenerator.AccentShare);
    }

    [Test]
    public async Task ADrumOnItsLeadsFeel_PlaysItsTuplet_EveryBar_TheBoundAlways_MostColouringOnes()
    {
        int bars = 0, colours = 0, following = 0;
        foreach (var song in TestCorpus.Range(100))
        {
            var patterns = song.Trace.Where(x => x.Point == TracePoints.BarPattern)
                .GroupBy(x => (x.Section, x.Track, x.Bar))
                .ToDictionary(x => x.Key, x => ResolvedRhythm.Of(x.First().StateMap));
            foreach (var entry in song.Trace.Where(x => x.Point == TracePoints.FeelLeads))
            {
                var feelLeads = (ImmutableDictionary<int, int>)entry.Value!;
                var kit = (SectionKit)song.Trace.Single(x => x.Point == TracePoints.Kit && x.Section == entry.Section).Value!;
                foreach (var drum in kit.Doubles.Keys)
                    await Assert.That(feelLeads[DrumGroups.GetTrackNumber(drum)]).IsEqualTo(DrumGroups.GetTrackNumber(kit.Doubles[drum]));
                var colouring = kit.Drums.Where(x => !kit.Leads.Contains(x) && !kit.Doubles.ContainsKey(x) && kit.Leads.Any(l => l.MainRole == x.MainRole)).ToArray();
                colours += colouring.Length;
                following += colouring.Count(x => feelLeads.ContainsKey(DrumGroups.GetTrackNumber(x)));

                foreach (var (track, lead) in feelLeads)
                for (var bar = 0; bar < Rmg.Core.Composition.Progressions.BarCount; bar++)
                {
                    bars++;
                    await Assert.That(patterns[(entry.Section, track, bar)].PrimeIndex).IsEqualTo(patterns[(entry.Section, lead, bar)].PrimeIndex);
                }
            }
        }

        await Assert.That(bars).IsGreaterThan(0);
        await Assert.That(following / (double)colours).IsBetween(0.7, 0.95);
    }
}
