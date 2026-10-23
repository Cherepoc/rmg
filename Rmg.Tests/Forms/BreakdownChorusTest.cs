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
        var (breakdowns, built, voiced) = (0, 0, 0);
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
            // the voice alone in its first phrase: no other part strikes a note in it, but a fill's last bar into the next
            // phrase, the drums', and a held note's last beat, as a texture's rule allows
            var span = song.Map.Sections[index];
            var phrase = span.Phrases.First();
            if (!song.Song.Notes!.Any(x => song.Song.TrackDefinitions[x.Key].Role == TrackRole.Melody && x.Value.Any(n => n.IsWithin(phrase.Start, phrase.End))))
                continue;
            voiced++;
            foreach (var (track, notes) in song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key].Role != TrackRole.Melody))
            {
                var allowance = song.Song.TrackDefinitions[track].Role == TrackRole.Drum ? span.Meter.BarDuration : 1;
                await Assert.That(notes.Any(n => n.Position > phrase.Start + 1e-6 && n.Position < phrase.End - allowance - 1e-9)).IsFalse()
                    .Because($"seed {song.Seed}, {song.Song.TrackDefinitions[track].Role}");
            }
        }

        Console.WriteLine($"{breakdowns} breakdown choruses, {built} built up, {voiced} from the voice");
        await Assert.That(built).IsGreaterThan(5);
        await Assert.That(voiced).IsGreaterThan(5);
    }
}
