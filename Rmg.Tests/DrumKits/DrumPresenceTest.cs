using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.DrumKits;

public sealed class DrumPresenceTest
{
    [Test]
    public async Task OnlyDrumsThatLeadNoRole_SitOut_AndTheLeads_ChangeTheirStroke_NeverInTheFirstBarPattern()
    {
        int resting = 0, strokes = 0;
        foreach (var song in TestCorpus.Range(40))
        {
            var leads = song.Trace.Where(x => x.Point == TracePoints.Kit)
                .ToDictionary(x => x.Section, x => ((SectionKit)x.Value!).Leads.Select(DrumGroups.GetTrackNumber).ToHashSet());
            foreach (var entry in song.Trace.Where(x => x.Point == TracePoints.DrumPresence))
            {
                var bars = (BarDrums)entry.Value!;
                foreach (var (track, letter) in bars.Resting)
                {
                    resting++;
                    await Assert.That(leads[entry.Section].Contains(track)).IsFalse();
                    await Assert.That(letter).IsGreaterThan(0);
                }

                foreach (var ((track, letter), _) in bars.Strokes)
                {
                    strokes++;
                    await Assert.That(DrumGroups.GetDrum(track).HasStrokes && leads[entry.Section].Contains(track)).IsTrue();
                    await Assert.That(letter).IsGreaterThan(0);
                }
            }
        }

        await Assert.That(resting).IsGreaterThan(0);
        await Assert.That(strokes).IsGreaterThan(0);
    }

    [Test]
    public async Task AChangedStroke_PlaysInTheBarsOfItsLetter()
    {
        var checkedNotes = 0;
        foreach (var song in TestCorpus.Range(40))
        {
            var schemes = song.Trace.Where(x => x.Point == TracePoints.BarPattern).GroupBy(x => x.Section).ToDictionary(x => x.Key, x => x.First().Phrase!.Replace("′", ""));
            foreach (var entry in song.Trace.Where(x => x.Point == TracePoints.DrumPresence))
            foreach (var ((track, letter), stroke) in ((BarDrums)entry.Value!).Strokes)
            foreach (var span in song.Map.Sections.Where(x => x.SectionId == entry.Section))
            {
                var code = DrumGroups.GetDrum(track).Sounds[stroke].Code;
                foreach (var bar in schemes[entry.Section].Select((x, bar) => (x, bar)).Where(x => x.x - 'A' == letter).Select(x => x.bar))
                {
                    var start = span.Start + bar * Meter.BarDuration;
                    // but a fill's notes, which name their sounds, and an accent's, a note's own stroke
                    var notes = song.Song.Notes![track]
                        .Where(x => x.Position >= start && x.Position < start + Meter.BarDuration &&
                                    x.Value.State.GetStateValue(Rmg.Core.Events.StateKinds.ArticulationIndex) == 0 &&
                                    x.Value.State.GetStateValue(Rmg.Core.Events.StateKinds.DrumStroke).Depth < Rmg.Core.Events.StateDepths.Note)
                        .ToArray();
                    checkedNotes += notes.Length;
                    await Assert.That(notes.All(x => x.Value.Pitches[0] == code)).IsTrue();
                }
            }
        }

        await Assert.That(checkedNotes).IsGreaterThan(0);
    }

    [Test]
    public async Task ADrumSittingOut_PlaysNoNotes_InTheBarsOfItsLetter()
    {
        foreach (var song in TestCorpus.Range(40))
        {
            var schemes = song.Trace.Where(x => x.Point == TracePoints.BarPattern).GroupBy(x => x.Section).ToDictionary(x => x.Key, x => x.First().Phrase!.Replace("′", ""));
            foreach (var entry in song.Trace.Where(x => x.Point == TracePoints.DrumPresence))
            foreach (var (track, letter) in ((BarDrums)entry.Value!).Resting)
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
        int[] tracks = [DrumGroups.GetTrackNumber(DrumDefinitions.Tom), DrumGroups.GetTrackNumber(DrumDefinitions.Conga)];

        double Share(double unconventionality)
        {
            var context = new GenerationContext(1);
            return Enumerable.Range(0, 2_000)
                .Average(_ => DrumPresence.Draw(context, tracks, scheme, new HashSet<int>(), _ => 0, new RhythmicUnconventionality(unconventionality).Tilt, Tilt.None).Resting.Count / 2.0);
        }

        await Assert.That(Share(0)).IsLessThan(0.15);
        await Assert.That(Share(0.5)).IsEqualTo(DrumPresence.SitOutChance).Within(0.03);
        await Assert.That(Share(1)).IsGreaterThan(0.5);
    }
}
