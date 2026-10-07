using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Drums;

/// <summary>The kick with the riff (<see cref="KickLock" />): in a section that locks them, the kick striking where the riff's notes start.</summary>
public sealed class KickLockTest
{
    [Test]
    public async Task InALockedSection_TheKickStrikesOnlyWhereTheRiffsNotesStart_ButInAFill()
    {
        var kicks = DrumGroups.Kick.Drums.Select(DrumGroups.GetTrackNumber).ToHashSet();
        var (locked, hits) = (0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("nuMetal")))))
        {
            var locks = song.Trace.Where(t => t.Point == TracePoints.KickLock && (bool)t.Value!).Select(t => t.Section).ToHashSet();
            var leader = song.Song.TrackDefinitions.FirstOrDefault(x => x.Value.Role == TrackRole.Riff && song.Song.Notes!.ContainsKey(x.Key) && song.Song.Notes[x.Key].Count > 0).Key is var riff and > 0
                ? riff
                : song.Song.TrackDefinitions.First(x => x.Value.Role == TrackRole.Bass).Key;
            foreach (var span in song.Map.Sections.Where(x => locks.Contains(x.SectionId)))
            {
                locked++;
                // the riff's notes as its pattern places them, before a held note takes the next into it
                var onsets = song.Song.TrackEventStateTimelineMap.TrackTimelineMap[leader].EventTimeline.Where(x => x.Position >= span.Start - 1e-9 && x.Position < span.End - 1e-9).Select(x => x.Position).ToArray();
                // but a phrase's last bar, where a fill may take the drums, the section's first beat, where the fill into it
                // lands, and the bars the riff rests in
                foreach (var hit in song.Song.Notes.Where(x => kicks.Contains(x.Key)).SelectMany(x => x.Value).Where(x => x.Position > span.Start + 1e-9 && x.Position < span.End - 1e-9))
                {
                    var bar = span.Start + Math.Floor((hit.Position - span.Start) / span.Meter.BarDuration + 1e-9) * span.Meter.BarDuration;
                    var isPhraseEnd = Math.Abs(Math.IEEERemainder(bar + span.Meter.BarDuration - span.Start, span.Meter.PatternDuration)) < 1e-6;
                    if (isPhraseEnd || !onsets.Any(x => x >= bar - 1e-9 && x < bar + span.Meter.BarDuration - 1e-9))
                        continue;
                    hits++;
                    // as the notes are placed, an onset a pocket's offset or a push away at most
                    await Assert.That(onsets.Any(x => Math.Abs(x - hit.Position) < 0.13)).IsTrue().Because($"seed {song.Seed} at {hit.Position}");
                }
            }
        }

        await Assert.That(locked).IsGreaterThan(5);
        await Assert.That(hits).IsGreaterThan(50);
    }
}
