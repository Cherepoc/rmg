using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;

namespace Rmg.Tests.DrumKits;

public sealed class DoublingTest
{
    /// <summary>The bars before a line a fill may play in, in beats: a fill's four beats at the most, in as many bars as they take.</summary>
    private static double FillBars(Meter meter) => Math.Ceiling(4 / meter.BarDuration - 1e-9) * meter.BarDuration;

    [Test]
    public async Task ADrumThatDoublesOrAccentsALead_PlaysOnlyBeatsTheLeadPlays_AnAccentFewerOfThem()
    {
        var checkedNotes = 0;
        int accentNotes = 0, accentedLeadNotes = 0;
        foreach (var song in TestCorpus.Range(128))
        foreach (var entry in song.Trace.Where(x => x.Point == TracePoints.Doubles))
        foreach (var (track, doubling) in ((ImmutableDictionary<int, Doubling>)entry.Value!).Where(x => x.Value.Binding != DrumBinding.Figure))
        foreach (var span in song.Map.Sections.Where(x => x.SectionId == entry.Section))
        {
            // the groove's notes, but in the last bar of a phrase, where a fill may take the lead's notes into its run, as an
            // intro's window ends on a phrase, and at a phrase's first beat, where a call may land with the band, pushed an
            // 8th early now and then
            bool InFillBar(double position)
            {
                var phrase = span.Meter.PhraseDuration;
                var inPhrase = (position - span.Start) % phrase;
                return inPhrase >= phrase - FillBars(span.Meter) || inPhrase < 0.5;
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
    public async Task ADrumThatAccentsALead_PlaysWhereTheLeadPlaysItsStrongBeats_HoweverPlainTheSection()
    {
        var accents = 0;
        foreach (var song in TestCorpus.Range(128))
        foreach (var entry in song.Trace.Where(x => x.Point == TracePoints.Doubles))
        foreach (var (track, doubling) in ((ImmutableDictionary<int, Doubling>)entry.Value!).Where(x => x.Value.Binding == DrumBinding.Accent))
        foreach (var span in song.Map.Sections.Where(x => x.SectionId == entry.Section))
        {
            var notes = song.Song.Notes!;
            // the bars the drum sits out, by their letter in the section's scheme (DrumPresence)
            var resting = song.Trace.Where(x => x.Point == TracePoints.DrumPresence && x.Section == entry.Section).Select(x => ((BarDrums)x.Value!).Resting).FirstOrDefault() ?? [];
            var letters = ((PhraseScheme)song.Trace.First(x => x.Point == TracePoints.PhraseScheme && x.Section == entry.Section).Value!).Letters;
            // the lead's strong beats in its groove, not in a pattern's last bar, where a fill plays, nor at its first beat,
            // where a landing does, nor in a bar the drum sits out
            bool InGroove(double position)
            {
                var inPattern = (position - span.Start) % span.Meter.PatternDuration;
                var letter = letters[(int)Math.Floor(inPattern / span.Meter.BarDuration + 1e-9) % letters.Length];
                return inPattern < span.Meter.PatternDuration - FillBars(span.Meter) && inPattern >= 0.5 && !resting.Contains((track, letter));
            }
            var strong = notes.GetValueOrDefault(doubling.Lead, EventTimeline.Create<Rmg.Core.Songs.RealizedNote>(0))
                .Any(x => x.Position >= span.Start && x.Position < span.End && InGroove(x.Position) && x.Value.State.GetStateValue(CompositionStateKinds.BeatRank) <= doubling.MaxRank);
            if (!strong)
                continue;

            accents++;
            // a bar that plays over and over in a plain section, its draw picking none of the lead's beats, still has the last
            await Assert.That(notes.GetValueOrDefault(track, EventTimeline.Create<Rmg.Core.Songs.RealizedNote>(0)).Any(x => x.Position >= span.Start && x.Position < span.End))
                .IsTrue().Because($"seed {song.Seed}, section {entry.Section} at {span.Start}, {DrumGroups.GetDrum(track).Name} on {DrumGroups.GetDrum(doubling.Lead).Name}");
        }

        await Assert.That(accents).IsGreaterThan(0);
    }

    [Test]
    public async Task ADrumOnItsLeadsFeel_PlaysItsTuplet_EveryBar_TheBoundAlways_MostColouringOnes()
    {
        int bars = 0, colours = 0, following = 0;
        foreach (var song in TestCorpus.Range(128))
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
                var colouring = kit.Drums.Where(x => !kit.Leads.ContainsValue(x) && !kit.Doubles.ContainsKey(x) && kit.Leads.ContainsKey(x.MainRole)).ToArray();
                colours += colouring.Length;
                following += colouring.Count(x => feelLeads.ContainsKey(DrumGroups.GetTrackNumber(x)));

                foreach (var (track, lead) in feelLeads)
                for (var bar = 0; bar < Meter.DefaultPhraseBars; bar++)
                {
                    bars++;
                    await Assert.That(patterns[(entry.Section, track, bar)].Feel).IsEqualTo(patterns[(entry.Section, lead, bar)].Feel);
                }
            }
        }

        await Assert.That(bars).IsGreaterThan(0);
        // following the lead is the conventional choice, which the wildest sections never take
        await Assert.That(following / (double)colours).IsBetween(0.55, 0.95);
    }
}
