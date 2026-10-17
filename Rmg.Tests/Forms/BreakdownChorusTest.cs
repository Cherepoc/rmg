using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Forms;

/// <summary>A last chorus played twice whose first pass is a breakdown, built up from the voice alone (<see cref="SongForms.BreakdownChorusChance" />).</summary>
public sealed class BreakdownChorusTest
{
    /// <summary>Its first pass builds up, the melody alone in its first phrase, the drums out of it; never a solo.</summary>
    [Test]
    public async Task ABreakdownChorus_BuildsUpFromTheVoiceAlone()
    {
        var pop = GenrePresets.Genre("pop");
        var genre = pop with
        {
            Choices = pop.Choices
                .SetItem(GenreChoices.LastRepeat.Name, ImmutableDictionary<string, ByConvention>.Empty.Add("songs", new(1, 1, 1)).Add("breakdown", new(1, 1, 1)))
                // an ending that adds no appearance, so that the first pass is the second last
                .SetItem(GenreChoices.Ending.Name, ImmutableDictionary<string, ByConvention>.Empty.Add("ringOut", new(1, 1, 1)))
        };
        var (breakdowns, built) = (0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre))))
        {
            if (!(bool)song.Trace.Single(x => x.Point == TracePoints.BreakdownChorus).Value!)
                continue;
            breakdowns++;
            var index = song.Map.Sections.Length - 2;
            await Assert.That(song.Solos.ContainsKey(index)).IsFalse().Because($"seed {song.Seed}");
            var (_, kind, silent) = song.Trace.Where(x => x.Point == TracePoints.Texture).Select(x => ((int Index, TextureKind Kind, ImmutableArray<ImmutableHashSet<TrackRole>> Silent))x.Value!).Single(x => x.Index == index);
            if (kind != TextureKind.Build)
                continue;
            built++;
            await Assert.That(silent[0]).DoesNotContain(TrackRole.Melody).Because($"seed {song.Seed}");
            // the drums, where they play in it, out of its first phrase
            var span = song.Map.Sections[index];
            if (song.Song.Notes!.Any(x => song.IsDrum(x.Key) && x.Value.Any(n => n.IsWithin(span.Start, span.End))))
                await Assert.That(silent[0]).Contains(TrackRole.Drum).Because($"seed {song.Seed}");
        }

        Console.WriteLine($"{breakdowns} breakdown choruses, {built} built up");
        await Assert.That(built).IsGreaterThan(5);
    }
}
