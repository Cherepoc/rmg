using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Rendering;

/// <summary>A trance gate (<see cref="StrongEffect.Gate" />): a held chord chopped in 16ths.</summary>
public sealed class GateTest
{
    private static readonly Genre Gating = new(ImmutableDictionary<string, ImmutableDictionary<string, ByConvention>>.Empty
        .Add(GenreChoices.StrongEffect.Name, ImmutableDictionary<string, ByConvention>.Empty.Add(GenreChoices.Name(nameof(StrongEffect.Gate)), new ByConvention(1, 1, 1))));

    [Test]
    public async Task AGatedPart_HoldsChords_AndSoundsAndFallsSilentOnlyOnTheGrid()
    {
        var gated = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Gating))))
        {
            var chopped = song.Rendered.Tracks.Where(x => x.Expression.Any(e => e.Value == 0)).ToArray();
            await Assert.That(chopped.Length).IsLessThanOrEqualTo(1);
            if (chopped.Length == 0)
                continue;
            gated++;
            await Assert.That(chopped[0].Role is TrackRole.Pad or TrackRole.Chords or TrackRole.Rhythm).IsTrue();
            // silent from 0.6 of a 16th, or from its start where it lets none through
            foreach (var (position, _) in chopped[0].Expression.Where(x => x.Value == 0))
                await Assert.That(new[] { 0, 0.15 }.Any(x => Math.Abs((position - x) / 0.25 - Math.Round((position - x) / 0.25)) < 1e-6)).IsTrue().Because($"seed {song.Seed} at {position}");
        }

        await Assert.That(gated).IsGreaterThan(10);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var genre in new[] { "trance", "electronic", "synthwave" })
        {
            var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre))));
            var gated = songs.Where(x => x.Song.Notes!.Values.Any(n => n.Any(y => y.Value.State.GetStateValue(CompositionStateKinds.Gate) != 0))).ToArray();
            Console.WriteLine($"{genre}: gated {gated.Length / (double)songs.Length:P0}");
        }

        await Task.CompletedTask;
    }
}
