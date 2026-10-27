using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Arrangements;

/// <summary>A pitched part left alone in a phrase played at a leading part's level (<see cref="VelocityLayers.LoneLift" />).</summary>
public sealed class LonePartTest
{
    /// <summary>A part under the melody's level lifted to it, the pad's bed the most, and a leading part not at all.</summary>
    [Test]
    public async Task ALonePart_IsLiftedToTheMelodysLevel()
    {
        foreach (var role in new[] { TrackRole.Pad, TrackRole.Chords, TrackRole.CounterMelody, TrackRole.Riff, TrackRole.Rhythm })
            await Assert.That(VelocityLayers.GetLevel(role) + VelocityLayers.LoneLift(role)).IsEqualTo(VelocityLayers.GetLevel(TrackRole.Melody)).Within(1e-9).Because($"{role}");
        await Assert.That(VelocityLayers.LoneLift(TrackRole.Melody)).IsEqualTo(0);
        await Assert.That(VelocityLayers.LoneLift(TrackRole.Bass)).IsEqualTo(0);
    }

    /// <summary>How loud the parts left alone in a phrase play, by role, against the song's melody, in the genres that build their sections up.</summary>
    [Test]
    [Explicit]
    [Arguments("trance")]
    [Arguments("postRock")]
    [Arguments("ambient")]
    public async Task Report(string genre)
    {
        var lone = new Dictionary<TrackRole, List<double>>();
        var melody = new List<double>();
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre)))))
        {
            var roles = song.Song.TrackDefinitions.Values.Select(x => x.Role).ToHashSet();
            if (roles.Contains(TrackRole.Melody))
                melody.AddRange(song.Notes(song.Track(TrackRole.Melody)).Select(x => x.Value.Velocity));
            foreach (var span in song.PlayedSpans)
            {
                var (_, _, silent) = song.Texture(span);
                var phrases = span.Phrases.ToArray();
                for (var phrase = 0; phrase < phrases.Length; phrase++)
                    if (roles.Except(song.Resting(span)).Except(phrase < silent.Length ? silent[phrase] : []).ToArray() is [var alone] && alone != TrackRole.Drum)
                        foreach (var (track, _) in song.Song.TrackDefinitions.Where(x => x.Value.Role == alone))
                            (lone.TryGetValue(alone, out var list) ? list : lone[alone] = []).AddRange(song.Notes(track)
                                .Where(x => x.Position >= phrases[phrase].Start - 1e-9 && x.Position < phrases[phrase].End - 1e-9).Select(x => x.Value.Velocity));
            }
        }

        static string Median(List<double> values) => values.Count == 0 ? "-" : $"{values.Order().ElementAt(values.Count / 2) * 127:F0}";
        Console.WriteLine($"{genre}: the melody's median velocity {Median(melody)}; a lone part's: {string.Join(", ", lone.OrderByDescending(x => x.Value.Count).Select(x => $"{x.Key} {Median(x.Value)} ({x.Value.Count})"))}");
        await Task.CompletedTask;
    }
}
