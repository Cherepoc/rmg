using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Drums;

/// <summary>The kick taken out before a line, the bass leaving with it, the low end out together into the downbeat.</summary>
public sealed class KickOutTest
{
    [Test]
    public async Task AKickOut_TakesTheBassOutWithTheKick_UntilItsLine()
    {
        var spans = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.With("techno", GenreChoices.KickOut, ("lines", 1))))))
        {
            var kickOuts = (ImmutableArray<(double From, double To)>)song.Trace.Single(x => x.Point == TracePoints.KickOuts).Value!;
            // but a riser into a drop, which a song without a pad or chords plays on the bass's track
            var bass = song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key].Role == TrackRole.Bass).SelectMany(x => x.Value).Where(x => !Risers.IsRiser(x.Value.State)).ToArray();
            foreach (var (from, to) in kickOuts)
            {
                spans++;
                // nothing of the bass struck in it, nor sounding into it
                await Assert.That(bass.Any(x => x.Position < to - 1e-9 && x.Position + x.Value.Duration > from + 1e-9)).IsFalse().Because($"seed {song.Seed} at {from}-{to}");
            }
        }

        Console.WriteLine($"{spans} kick-outs");
        await Assert.That(spans).IsGreaterThan(20);
    }
}
