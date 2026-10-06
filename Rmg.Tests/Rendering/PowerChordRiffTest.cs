using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Rendering;

/// <summary>A riff in power chords: every note with its perfect fifth over it, whatever the scale, and no twin.</summary>
public sealed class PowerChordRiffTest
{
    [Test]
    public async Task ARiffInPowerChords_PlaysItsFifthOverEveryNote_AndNoTwin()
    {
        int notes = 0, fifths = 0, twinNotes = 0, sections = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("rock")))))
        {
            // a section that does not chug, whose riff plays in power chords throughout (Chugs for one that does)
            var chugs = song.Trace.Where(x => x.Point == TracePoints.Chugs && (bool)x.Value!).Select(x => x.Section).ToHashSet();
            var power = song.Trace.Where(x => x.Point == TracePoints.RiffPowerChords && (bool)x.Value! && !chugs.Contains(x.Section)).Select(x => x.Section).ToHashSet();
            foreach (var span in song.Map.Sections.Where(x => power.Contains(x.SectionId)))
            {
                sections++;
                var riff = song.Song.Notes!.GetValueOrDefault(SongTracks.RiffTrack)?.Where(x => x.Position >= span.Start && x.Position < span.End).ToArray() ?? [];
                foreach (var note in riff)
                {
                    notes++;
                    fifths += note.Value.Pitches.Length > 1 && note.Value.Pitches.Contains(note.Value.Pitches.Min() + 7) ? 1 : 0;
                }
                twinNotes += song.Song.Notes!.GetValueOrDefault(SongTracks.RiffTwinTrack)?.Count(x => x.Position >= span.Start && x.Position < span.End) ?? 0;
            }
        }

        await Assert.That(sections).IsGreaterThan(5);
        // but where a section's landing stacks the band's chord (LineChords.Riff), or the range has no room above the note:
        // 86% (October 2026)
        await Assert.That(fifths / (double)notes).IsGreaterThan(0.8);
        await Assert.That(twinNotes).IsEqualTo(0);
    }

    [Test]
    public async Task AChuggingRiff_SitsOnItsPedal_ItsAccentsFree_InChordsWhereItIsSlow_AndOnItsAccentsWhereItIsFast()
    {
        int pedal = 0, weak = 0, slowNotes = 0, slowChords = 0, fastOffAccents = 0, fastChordsOffAccents = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("djent")))))
        {
            var chugs = song.Trace.Where(x => x.Point == TracePoints.Chugs && (bool)x.Value!).Select(x => x.Section).ToHashSet();
            var common = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap;
            foreach (var span in song.Map.Sections.Where(x => chugs.Contains(x.SectionId) && !song.IsSolo(x)))
            {
                var riff = song.Song.Notes!.GetValueOrDefault(SongTracks.RiffTrack)?.Where(x => x.Position >= span.Start && x.Position < span.End).ToArray() ?? [];
                if (riff.Length == 0)
                    continue;
                var fast = riff.Length / span.Duration >= Chugs.FastNotes;
                foreach (var note in riff)
                {
                    var accent = note.Value.State.GetStateValue(CompositionStateKinds.BeatRank) == 0;
                    var chord = note.Value.Pitches.Length > 1;
                    if (!accent)
                    {
                        weak++;
                        var root = Realizer.GetChord(common.GetEffectiveStateMapAt(note.Position)).Chord.Root;
                        pedal += ((note.Value.Pitches.Min() - root) % 12 + 12) % 12 == 0 ? 1 : 0;
                    }
                    if (!fast)
                        (slowNotes, slowChords) = (slowNotes + 1, slowChords + (chord ? 1 : 0));
                    else if (!accent)
                        (fastOffAccents, fastChordsOffAccents) = (fastOffAccents + 1, fastChordsOffAccents + (chord ? 1 : 0));
                }
            }
        }

        Console.WriteLine($"off the accents on the pedal {pedal / (double)weak:P0}; slow notes in chords {slowChords / (double)Math.Max(1, slowNotes):P0}; fast notes off the accents in chords {fastChordsOffAccents / (double)Math.Max(1, fastOffAccents):P0}");
        // the pedal its share, the rest by its scale; a slow chug's every note a chord, but where a landing stacks the
        // band's or the range has no room; a fast one's single notes off its accents
        await Assert.That(pedal / (double)weak).IsBetween(0.5, 0.85);
        await Assert.That(slowChords / (double)Math.Max(1, slowNotes)).IsGreaterThan(0.75);
        await Assert.That(fastChordsOffAccents / (double)Math.Max(1, fastOffAccents)).IsLessThan(0.1);
    }
}
