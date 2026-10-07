using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Drums;

/// <summary>A genre's figures (<see cref="Figures" />): a player's rhythm in a section given by its figure, the kick with the riff, a double kick, a clave.</summary>
public sealed class FiguresTest
{
    /// <summary>The sections, by their ids, whose player plays the figure named.</summary>
    private static ImmutableHashSet<int> Playing(CorpusSong song, FigurePlayer player, string figure) =>
    [
        ..song.Trace.Where(t => t.Point == TracePoints.Figures && ((ImmutableDictionary<FigurePlayer, string>)t.Value!).GetValueOrDefault(player) == figure).Select(t => t.Section)
    ];

    /// <summary>
    ///     Every hit of the tracks given in the sections given, but a phrase's last bar, where a fill may take the drums, and
    ///     the section's first beat, where the fill into it lands, with its section and its bar's start.
    /// </summary>
    private static IEnumerable<(SectionSpan Span, double Bar, double Position)> Hits(CorpusSong song, IReadOnlySet<int> tracks, IReadOnlySet<int> sections) =>
        song.Map.Sections.Where(x => sections.Contains(x.SectionId)).SelectMany(span => song.Song.Notes!.Where(x => tracks.Contains(x.Key)).SelectMany(x => x.Value)
            .Where(x => x.Position > span.Start + 1e-9 && x.Position < span.End - 1e-9)
            .Select(x => (span, Bar: span.Start + Math.Floor((x.Position - span.Start) / span.Meter.BarDuration + 1e-9) * span.Meter.BarDuration, x.Position))
            .Where(x => Math.Abs(Math.IEEERemainder(x.Bar + span.Meter.BarDuration - span.Start, span.Meter.PatternDuration)) > 1e-6));

    /// <summary>
    ///     A calibration: the kick follows the riff's pattern before the lines are placed, which leave a few of the riff's
    ///     notes out after it, so that nearly all its hits are on the riff's notes as they end up.
    /// </summary>
    [Test]
    public async Task AKickWithTheRiff_StrikesNearlyOnlyWhereTheRiffsNotesStart()
    {
        var kicks = DrumGroups.Kick.Drums.Select(DrumGroups.GetTrackNumber).ToHashSet();
        var (hits, on) = (0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("nuMetal")))))
        {
            var leader = song.Song.TrackDefinitions.FirstOrDefault(x => x.Value.Role == TrackRole.Riff && song.Song.Notes!.ContainsKey(x.Key) && song.Song.Notes[x.Key].Count > 0).Key is var riff and > 0
                ? riff
                : song.Song.TrackDefinitions.First(x => x.Value.Role == TrackRole.Bass).Key;
            // the riff's notes as its pattern places them, before a held note takes the next into it
            var onsets = song.Song.TrackEventStateTimelineMap.TrackTimelineMap[leader].EventTimeline.Select(x => x.Position).ToArray();
            foreach (var (span, bar, position) in Hits(song, kicks, Playing(song, FigurePlayer.Ground, "withRiff")))
            {
                // but in a bar the riff rests in, where the kick plays its own
                if (!onsets.Any(x => x >= bar - 1e-9 && x < bar + span.Meter.BarDuration - 1e-9))
                    continue;
                hits++;
                // as the notes are placed, an onset a pocket's offset away at most
                if (onsets.Any(x => Math.Abs(x - position) < 0.13))
                    on++;
            }
        }

        Console.WriteLine($"of {hits} hits of a kick with the riff, {on / (double)hits:P1} on the riff's notes");
        await Assert.That(hits).IsGreaterThan(50);
        await Assert.That(on / (double)hits).IsGreaterThan(0.8);
    }

    [Test]
    public async Task ADoubleKick_StrikesOnlyOnTheSixteenths()
    {
        var kicks = DrumGroups.Kick.Drums.Select(DrumGroups.GetTrackNumber).ToHashSet();
        var hits = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("metal")))))
        foreach (var (_, bar, position) in Hits(song, kicks, Playing(song, FigurePlayer.Ground, "doubleKick")))
        {
            hits++;
            var sixteenth = (position - bar) / 0.25;
            await Assert.That(Math.Abs(sixteenth - Math.Round(sixteenth))).IsLessThan(0.1).Because($"seed {song.Seed} at {position}");
        }

        await Assert.That(hits).IsGreaterThan(50);
    }

    [Test]
    public async Task AClave_StrikesOnlyOnItsThreeAndTwo()
    {
        var claves = new HashSet<int> { DrumGroups.GetTrackNumber(DrumDefinitions.Claves) };
        var hits = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("latin")))))
        foreach (var name in new[] { "clave32", "clave23" })
        {
            var figure = Figures.All.Single(x => x.Name == name);
            foreach (var (span, bar, position) in Hits(song, claves, Playing(song, FigurePlayer.Claves, name)))
            {
                hits++;
                // its 16th in the figure's two bars, counted from the section's start
                var twoBars = span.Start + Math.Floor((bar - span.Start) / (2 * span.Meter.BarDuration) + 1e-9) * 2 * span.Meter.BarDuration;
                var sixteenth = (position - twoBars) / 0.25;
                await Assert.That(figure.Onsets.Any(x => Math.Abs(x - sixteenth) < 0.1)).IsTrue().Because($"seed {song.Seed} at {position}, {name}");
            }
        }

        await Assert.That(hits).IsGreaterThan(30);
    }
}
