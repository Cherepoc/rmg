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
        foreach (var song in TestCorpus.Range(32))
        {
            var leads = song.Trace.Where(x => x.Point == TracePoints.Kit)
                .ToDictionary(x => x.Section, x => ((SectionKit)x.Value!).Leads.Values.Select(DrumGroups.GetTrackNumber).ToHashSet());
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
        foreach (var song in TestCorpus.Range(32).Where(x => x.PlaysDrums))
        {
            var schemes = song.Trace.Where(x => x.Point == TracePoints.PhraseScheme).ToDictionary(x => x.Section, x => ((PhraseScheme)x.Value!).Letters);
            // a drum solo plays its own segments in place of the groove (DrumSolos)
            var solos = song.Trace.Where(x => x.Point == TracePoints.DrumSolo).Select(x => x.Section).ToHashSet();
            // and a pushing section's crash, which strikes the next bar's change an eighth early (Pushes)
            var pushing = song.Trace.Where(x => x.Point == TracePoints.Push && (bool)x.Value!).Select(x => x.Section).ToHashSet();
            foreach (var entry in song.Trace.Where(x => x.Point == TracePoints.DrumPresence && !solos.Contains(x.Section)))
            foreach (var ((track, letter), stroke) in ((BarDrums)entry.Value!).Strokes)
            foreach (var span in song.Map.Sections.Where(x => x.SectionId == entry.Section))
            {
                var code = DrumGroups.GetDrum(track).Sounds[stroke].Code;
                // every phrase's bars of its letter, as every phrase plays its scheme
                var letters = schemes[entry.Section];
                foreach (var (_, start, barEnd) in span.Bars.Where(x => letters[x.Index % letters.Length] == letter))
                {
                    // but a fill's notes, which name their sounds, and an accent's, a note's own stroke
                    var notes = song.Song.Notes![track]
                        .Where(x => x.Position >= start && x.Position < barEnd &&
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
        var checkedBars = 0;
        foreach (var song in TestCorpus.Range(32).Where(x => x.PlaysDrums))
        {
            var schemes = song.Trace.Where(x => x.Point == TracePoints.PhraseScheme).ToDictionary(x => x.Section, x => ((PhraseScheme)x.Value!).Letters);
            // a drum solo plays its own segments in place of the groove (DrumSolos)
            var solos = song.Trace.Where(x => x.Point == TracePoints.DrumSolo).Select(x => x.Section).ToHashSet();
            // and a pushing section's crash, which strikes the next bar's change an eighth early (Pushes)
            var pushing = song.Trace.Where(x => x.Point == TracePoints.Push && (bool)x.Value!).Select(x => x.Section).ToHashSet();
            foreach (var entry in song.Trace.Where(x => x.Point == TracePoints.DrumPresence && !solos.Contains(x.Section)))
            foreach (var (track, letter) in ((BarDrums)entry.Value!).Resting)
            foreach (var span in song.Map.Sections.Where(x => x.SectionId == entry.Section))
            {
                // a drum the song never plays has no notes to count
                if (!song.Song.Notes!.TryGetValue(track, out var notes))
                    continue;

                // every phrase's bars of its letter, but each phrase's last, where a fill may play it
                var letters = schemes[entry.Section];
                foreach (var (_, start, end) in span.Bars.Where(x => letters[x.Index % letters.Length] == letter && x.Index % letters.Length < letters.Length - 1))
                {
                    var pushed = pushing.Contains(entry.Section) ? end - Pushes.Early : end;
                    await Assert.That(notes.Count(x => x.Position >= start && x.Position < pushed - 1e-9)).IsEqualTo(0).Because($"seed {song.Seed}, track {track} at {start}");
                    checkedBars++;
                }
            }
        }

        await Assert.That(checkedBars).IsGreaterThan(0);
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
                .Average(_ => DrumPresence.Draw(_ => context, tracks, scheme, new HashSet<int>(), _ => 0, unconventionality, Tilt.None, DrumSounds.OddsOf(Genre.None, GenreChoices.DrumStrokes)).Resting.Count / 2.0);
        }

        // an unconventional thing, never at the plain end and every time at the wild
        await Assert.That(Share(0)).IsEqualTo(0);
        await Assert.That(Share(0.5)).IsEqualTo(DrumPresence.SitOutChance).Within(0.03);
        await Assert.That(Share(1)).IsEqualTo(1);
    }
}
