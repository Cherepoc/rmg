using Rmg.Core.Composition;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Rendering;

/// <summary>The whole mix filtered (<see cref="MixFilter" />): a song opening up, a section dipping before the next, a song closing.</summary>
public sealed class MixFilterTest
{
    private static MixFilterPlan Plan(CorpusSong song) => (MixFilterPlan)song.Trace.Single(x => x.Point == TracePoints.MixFilter).Value!;

    /// <summary>A track's brightness at a time, swung, as its controllers last set it, its own where none has.</summary>
    private static int BrightnessAt(RenderedTrack track, double at) =>
        track.Controllers.Where(c => c.Controller == ExpressionRender.BrightnessController && c.Position <= at + 1e-9).Select(c => c.Value).DefaultIfEmpty(64).Last();

    [Test]
    public async Task AnOpeningSong_IsClosed_EveryPartAndTheDrums_OverItsOpeningsFirstHalf()
    {
        var opening = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("trance")))))
        {
            if (!Plan(song).Opening)
                continue;
            opening++;
            var first = song.Map.Sections[0];
            var (start, opened) = (song.Map.Intro.Duration > 0 ? 0 : song.Swing.Apply(first.Start), song.Swing.Apply(song.Map.Intro.Duration > 0 ? first.Start : first.Start + 4 * first.Meter.BarDuration));
            var half = (start + opened) / 2;
            foreach (var track in song.Rendered.Tracks.Where(x => x.NoteTimeline.Any(n => n.Position >= start - 1e-9 && n.Position < half - 1e-9)))
            {
                // closed at its first note there, at least half as far as the opening's depth
                var played = track.NoteTimeline.First(n => n.Position >= start - 1e-9).Position;
                await Assert.That(BrightnessAt(track, played)).IsLessThan(60).Because($"seed {song.Seed}, {track.Role} at {played}");
            }
        }

        await Assert.That(opening).IsGreaterThan(5);
    }

    [Test]
    public async Task ADip_ClosesTheMixBeforeASection_AndOpensItAsTheSectionStarts()
    {
        var dips = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("trance")))))
        {
            var drums = song.Rendered.Tracks.FirstOrDefault(x => x.IsPercussionInstrument);
            if (drums is null)
                continue;
            foreach (var at in Plan(song).Dips)
            {
                dips++;
                var start = song.Swing.Apply(at);
                await Assert.That(BrightnessAt(drums, start - 0.5)).IsLessThan(58).Because($"seed {song.Seed} at {at}");
                await Assert.That(BrightnessAt(drums, start)).IsEqualTo(64).Because($"seed {song.Seed} at {at}");
            }
        }

        await Assert.That(dips).IsGreaterThan(5);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var name in new string?[] { null, "trance", "techno", "electronic", "djent", "reggae", "dub", "ambient", "idm" })
        {
            var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: name is null ? null : GenrePresets.Genre(name))));
            var plans = songs.Select(Plan).ToArray();
            // the closing by the ending it closes into, one on a hit muffled by it
            string Closing(bool onHit) => songs.Where(x => FormLayers.HitEndings.Contains(x.Map.Ending.Kind) == onHit).ToArray() is var some
                ? $"{some.Count(x => Plan(x).Closing)} of {some.Length}" : "";
            Console.WriteLine($"{name ?? "the songs"}: opening {plans.Count(x => x.Opening) / (double)plans.Length:P0}, closing {plans.Count(x => x.Closing) / (double)plans.Length:P0} " +
                              $"(into a hit {Closing(true)}, into another ending {Closing(false)}), " +
                              $"songs dipping {plans.Count(x => !x.Dips.IsEmpty) / (double)plans.Length:P0}, {plans.Sum(x => x.Dips.Length) / (double)plans.Length:F2} dips a song");
        }

        await Task.CompletedTask;
    }
}
