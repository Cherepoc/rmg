using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Drums;

/// <summary>A heavy figure's backbeat (<see cref="Figures" />): the snare under a double kick, a blast or a breakdown's kick struck on its head, a phrase at most without it.</summary>
public sealed class HeavyBackbeatTest
{
    [Test]
    [Arguments("metal")]
    [Arguments("blackMetal")]
    [Arguments("djent")]
    public async Task UnderAHeavyFigure_TheSnareStrikesItsHead_APhraseAtMostWithoutIt(string genre)
    {
        var (bars, bare, crossStick, snareNotes) = (0, 0, 0, 0);
        var snares = DrumGroups.Snare.Tracks.ToHashSet();
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre)))))
        {
            var heavy = song.Trace.Where(t => t.Point == TracePoints.Figures && ((ImmutableDictionary<FigurePlayer, string>)t.Value!).TryGetValue(FigurePlayer.Ground, out var f)
                                              && f is "doubleKick" or "blast" or "withRiff").Select(t => t.Section).ToHashSet();
            foreach (var span in song.Map.Sections.Where(x => heavy.Contains(x.SectionId)))
            for (var bar = 0; bar < span.BarCount; bar++)
            {
                var (start, end) = span.Bar(bar);
                var notes = song.Song.Notes!.Where(x => snares.Contains(x.Key)).SelectMany(x => x.Value).Where(n => n.IsWithin(start, end)).ToArray();
                (snareNotes, crossStick) = (snareNotes + notes.Length, crossStick + notes.Count(n => n.Value.Pitches[0] == DrumSounds.CrossStick));
                // a bar the other drums play, not one where the drums rest, as while a section builds
                if (bar < span.Meter.PhraseBars || !song.Song.Notes!.Any(x => song.IsDrum(x.Key) && !snares.Contains(x.Key) && x.Value.Any(n => n.IsWithin(start, end))))
                    continue;
                bars++;
                bare += notes.Length == 0 ? 1 : 0;
            }
        }

        // a fill's own sounds, a drop's silent bar and a stop among the rest, against the mechanism breaking (measured: under
        // 0.5% of the snare's notes the cross-stick, 0.5% to 2.1% of the bars the drums play after a section's first phrase without a snare)
        Console.WriteLine($"{genre}: {bare} of {bars} bars after a section's first phrase without a snare, {crossStick} of {snareNotes} snare notes the cross-stick");
        await Assert.That(bars).IsGreaterThan(100);
        await Assert.That(bare / (double)bars).IsLessThan(0.03);
        await Assert.That(crossStick / (double)snareNotes).IsLessThan(0.02);
    }
}
