using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Rendering;

/// <summary>A tape's wow (<see cref="Expression.TapeChance" />): a song's pitched parts drifting together, some held a few cents off.</summary>
public sealed class TapeTest
{
    private static int[] Wows(CorpusSong song) =>
        [..song.Song.Notes!.SelectMany(x => x.Value).Select(x => x.Value.State.GetStateValue(CompositionStateKinds.Wow)).Where(x => x > 0).Distinct()];

    [Test]
    public async Task ATapesSong_DriftsEveryPitchedPartAlike_AndNoDrum_ALineADetuneAFewCents()
    {
        var taped = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("idm")))))
        {
            if (Wows(song).Length == 0)
                continue;
            taped++;
            await Assert.That(Wows(song).Length).IsEqualTo(1);
            foreach (var (track, notes) in song.Song.Notes!.Where(x => x.Value.Count > 0))
            {
                var isDrum = song.Song.TrackDefinitions[track].Role == TrackRole.Drum;
                // its notes in the sections, before an ending, which plays no effect
                var played = notes.Where(x => x.Position >= song.Map.Sections[0].Start - 1e-9 && x.Position < song.Map.Sections[^1].End - 1e-9).ToArray();
                foreach (var note in played)
                {
                    await Assert.That(note.Value.State.GetStateValue(CompositionStateKinds.Wow) > 0).IsEqualTo(!isDrum).Because($"seed {song.Seed}, track {track}");
                    await Assert.That(Math.Abs(note.Value.State.GetStateValue(CompositionStateKinds.Detune))).IsLessThanOrEqualTo(8);
                }
                // a part holding chords in tune with the band, its wow alone
                var role = song.Song.TrackDefinitions[track].Role;
                if (role == TrackRole.Pad || role.PlaysChords())
                    await Assert.That(played.All(x => x.Value.State.GetStateValue(CompositionStateKinds.Detune) == 0)).IsTrue().Because($"seed {song.Seed}, track {track}");
                // a part's detune the same throughout
                await Assert.That(played.Select(x => x.Value.State.GetStateValue(CompositionStateKinds.Detune)).Distinct().Count()).IsLessThanOrEqualTo(1);
            }
        }

        await Assert.That(taped).IsGreaterThan(5);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var (name, given) in new (string?, double?)[] { (null, 0.1), (null, null), (null, 0.9), ("idm", null), ("hipHop", null) })
        {
            var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Base: given, Genre: name is null ? null : GenrePresets.Genre(name))));
            var taped = songs.Where(x => Wows(x).Length > 0).ToArray();
            var parts = taped.SelectMany(x => x.Song.Notes!.Where(n => n.Value.Count > 0 && x.Song.TrackDefinitions[n.Key].Role != TrackRole.Drum)
                .Select(n => n.Value[0].Value.State.GetStateValue(CompositionStateKinds.Detune))).ToArray();
            var bends = taped.SelectMany(x => x.Rendered.Tracks.Where(t => !t.IsPercussionInstrument)).Sum(t => t.PitchBends.Length);
            Console.WriteLine($"{name ?? "the songs"}, base {given?.ToString() ?? "drawn"}: taped {taped.Length / (double)songs.Length:P0}, wow {taped.Select(x => Wows(x)[0]).DefaultIfEmpty().Average():F1} cents, " +
                              $"parts detuned {parts.Count(x => x != 0) / (double)Math.Max(1, parts.Length):P0}, {parts.Where(x => x != 0).Select(Math.Abs).DefaultIfEmpty().Average():F1} cents; bends a taped song {bends / (double)Math.Max(1, taped.Length):F0}");
        }

        await Task.CompletedTask;
    }
}
