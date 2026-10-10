using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Forms;

/// <summary>A heavy band's breakdown: a bridge in half time, the kick with the riff, the riff chugging, the lines and the pad out.</summary>
public sealed class BreakdownTest
{
    /// <summary>A preset with every section that may break down broken down.</summary>
    private static Genre Always(string preset)
    {
        var genre = GenrePresets.Genre(preset);
        return genre with
        {
            Choices = genre.Choices.SetItem(GenreChoices.Breakdown.Name, ImmutableDictionary<string, ByConvention>.Empty.Add("sections", new ByConvention(1, 1, 1)))
        };
    }

    private static bool[] Breakdowns(CorpusSong song) =>
        [..song.Map.Sections.Select(x => song.Trace.Any(t => t.Point == TracePoints.Breakdown && t.Section == x.SectionId && (bool)t.Value!))];

    /// <summary>
    ///     A breakdown is a bridge or a free form's section, never the first; its drums in half time, its kick with the riff
    ///     where the song has one, and no melody, counter-melody nor pad where it plays but in a solo.
    /// </summary>
    [Test]
    public async Task ABreakdown_IsABridgeInHalfTime_TheKickWithTheRiff_TheLinesAndThePadOut()
    {
        var (found, kicked, withRiff) = (0, 0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Always("djent")))))
        {
            var structure = (SongStructure)song.Trace.Single(t => t.Point == TracePoints.SongForm).Value!;
            var breakdowns = Breakdowns(song);
            foreach (var (span, index) in song.Map.Sections.Select((x, i) => (x, i)).Where(x => breakdowns[x.i]))
            {
                found++;
                var because = $"seed {song.Seed} section {index}";
                await Assert.That(structure.Roles[span.SectionId] is SectionRole.Bridge or SectionRole.Free).IsTrue().Because(because);
                await Assert.That(span.SectionId).IsNotEqualTo(song.Map.Sections[0].SectionId).Because(because);
                await Assert.That(song.Trace.Single(t => t.Point == TracePoints.TimeFeel && t.Section == span.SectionId).Value).IsEqualTo(1).Because(because);
                var figures = (ImmutableDictionary<FigurePlayer, string>)song.Trace.Single(t => t.Point == TracePoints.Figures && t.Section == span.SectionId).Value!;
                await Assert.That(figures.GetValueOrDefault(FigurePlayer.Ground)).IsEqualTo("withRiff").Because(because);
                if (song.Solos.ContainsKey(index))
                    continue;
                // its riff chugging, and its kick on the riff's notes in the bars the riff plays, but a phrase's last, where a fill
                // plays, and the landing on a phrase's first beat
                await Assert.That(song.Trace.Single(t => t.Point == TracePoints.Chugs && t.Section == span.SectionId).Value).IsEqualTo(true).Because(because);
                if (song.Song.Notes!.TryGetValue(song.Track(TrackRole.Riff), out var riff))
                    foreach (var (bar, barIndex) in Enumerable.Range(0, span.BarCount).Select(x => (span.Bar(x), x)).Where(x => (x.x + 1) % span.Meter.PhraseBars != 0))
                    {
                        var onsets = riff.Where(x => x.IsWithin(bar.Start, bar.End)).Select(x => Math.Round(x.Position, 6)).ToHashSet();
                        var kicks = DrumGroups.Kick.Tracks.Where(song.Song.Notes.ContainsKey).SelectMany(x => song.Song.Notes[x])
                            .Where(x => x.IsWithin(bar.Start, bar.End) && !(barIndex % span.Meter.PhraseBars == 0 && Math.Abs(x.Position - bar.Start) < 1e-6)).ToArray();
                        if (onsets.Count == 0)
                            continue;
                        kicked += kicks.Length;
                        withRiff += kicks.Count(x => onsets.Contains(Math.Round(x.Position, 6)));
                    }
                var lines = song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key].Role is TrackRole.Melody or TrackRole.CounterMelody or TrackRole.Pad)
                    // a riser into a drop after it rides a resting part's channel, no line of it
                    .SelectMany(x => x.Value).Where(x => x.Value.State.GetStateValue(CompositionStateKinds.Program) != Risers.Program + 1)
                    .Count(x => x.Position >= span.Start - 1e-9 && x.Position < span.End - 1e-9);
                await Assert.That(lines).IsEqualTo(0).Because(because);
            }
        }

        await Assert.That(found).IsGreaterThan(4);
        // a push's kick an eighth early and a lift's cue among the rest, against the mechanism breaking (measured: 99.7% to 99.9% at offsets 0, a million and three million)
        Console.WriteLine($"{withRiff} of {kicked} kicks on the riff's notes");
        await Assert.That(withRiff / (double)kicked).IsGreaterThan(0.8);
    }

    /// <summary>How many songs and sections break down in the heavy presets as they are, and how many could, every bridge broken down.</summary>
    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var name in new[] { "metal", "djent", "nuMetal", "rock", "blackMetal" })
        {
            var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(name))));
            var always = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Always(name))));
            Console.WriteLine($"{name}: songs with a breakdown {songs.Average(x => Breakdowns(x).Any(b => b) ? 1.0 : 0):P0}, of the song's time " +
                              $"{songs.Average(x => x.Map.Sections.Where((s, i) => Breakdowns(x)[i]).Sum(s => s.Duration) / x.Map.Sections.Sum(s => s.Duration)):P0}; " +
                              $"every bridge broken down {always.Average(x => Breakdowns(x).Any(b => b) ? 1.0 : 0):P0} of songs, " +
                              $"{always.Average(x => x.Map.Sections.Where((s, i) => Breakdowns(x)[i]).Sum(s => s.Duration) / x.Map.Sections.Sum(s => s.Duration)):P0} of their time");
        }

        await Task.CompletedTask;
    }
}
