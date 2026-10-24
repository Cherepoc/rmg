using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Melodies;

/// <summary>The lines' ornaments: quick neighbours opening a note, never in the plainest songs.</summary>
public sealed class OrnamentsTest
{
    private static readonly Genre Ornamenting = new(ImmutableDictionary<string, ImmutableDictionary<string, ByConvention>>.Empty
        .Add("ornaments", Ornaments.All.ToImmutableDictionary(x => Ornaments.NameOf(x.Ornament), _ => new ByConvention(1, 1, 1))));

    private static ImmutableArray<Ornament> Played(CorpusSong song) => (ImmutableArray<Ornament>)song.Trace.Single(x => x.Point == TracePoints.Ornaments).Value!;

    private static ImmutableArray<int> OrnamentOf(RealizedNote note) => note.State.GetStateValue(CompositionStateKinds.Ornament) is var x && !x.IsDefault ? x : [];

    [Test]
    public async Task ThePlainestSongs_PlayNoOrnaments()
    {
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Base: 0))))
            await Assert.That(Played(song).IsEmpty).IsTrue().Because($"seed {song.Seed}");
    }

    [Test]
    public async Task AnOrnamentsNotes_AreTheNotesNeighbours_ATrillsAStepOfTheKey_WithinItsLength_OnTheLinesAlone_InOctavesToo()
    {
        var (ornaments, octaves) = (0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Ornamenting))))
        {
            await Assert.That(Played(song).Length).IsEqualTo(Ornaments.All.Length);
            // a pitched part's, a drum's flams and drags its own (DrumOrnamentsTest)
            foreach (var (track, notes) in song.Song.Notes!.Where(x => !song.IsDrum(x.Key)))
            foreach (var note in notes.Where(x => OrnamentOf(x.Value).Length > 0))
            {
                var ornament = OrnamentOf(note.Value);
                var because = $"seed {song.Seed} track {track} at {note.Position}";
                await Assert.That(song.Song.TrackDefinitions[track].Role is TrackRole.Melody or TrackRole.CounterMelody).IsTrue().Because(because);
                await Assert.That(ornament.All(x => Math.Abs(x) <= 4) && ornament.Any(x => x != 0)).IsTrue().Because(because);
                await Assert.That(ornament.Length * note.Value.State.GetStateValue(CompositionStateKinds.OrnamentStep)).IsLessThan(note.Value.Duration).Because(because);
                // a note of one pitch, or a line's in octaves, never a block of voices
                await Assert.That(note.Value.Pitches.Length == 1 || note.Value.State.GetStateValue(CompositionStateKinds.LineVoices) <= 1).IsTrue().Because(because);
                // a trill's or a turn's neighbours the next steps of the section's scale, never a pentatonic's third
                if (ornament.Length > 1)
                {
                    var (_, _, chord) = Realizer.GetChord(note.Value.State);
                    var scale = Enumerable.Range(0, 7).Select(x => chord.GetPitch(x).Mod(12)).ToHashSet();
                    var pitch = note.Value.Pitches[0];
                    foreach (var x in ornament.Where(x => x != 0))
                        await Assert.That(scale.Contains((pitch + x).Mod(12)) && Enumerable.Range(1, Math.Abs(x) - 1).All(d => !scale.Contains((pitch + Math.Sign(x) * d).Mod(12))))
                            .IsTrue().Because($"{because}, its neighbour {x}");
                }
                ornaments++;
                octaves += note.Value.Pitches.Length > 1 ? 1 : 0;
            }
        }

        await Assert.That(ornaments).IsGreaterThan(0);
        await Assert.That(octaves).IsGreaterThan(0);
    }

    /// <summary>How often the songs ornament their lines, each ornament's share of the songs and the share of the lines' notes ornamented, by genre.</summary>
    [Test]
    [Arguments("none")]
    [Arguments("medieval")]
    [Arguments("folk")]
    [Arguments("country")]
    [Arguments("orchestral")]
    [Arguments("rock")]
    [Arguments("techno")]
    [Explicit]
    public async Task Report(string id)
    {
        const int count = 64;
        var genre = id == "none" ? Genre.None : GenrePresets.Genre(id);
        var songs = TestCorpus.InParallel(Enumerable.Range(0, count), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre)));
        var lines = songs.SelectMany(s => s.Song.Notes!.Where(x => s.Song.TrackDefinitions[x.Key].Role is TrackRole.Melody or TrackRole.CounterMelody).SelectMany(x => x.Value)).ToArray();
        var shares = string.Join(", ", Ornaments.All.Select(o => $"{o.Ornament} {songs.Count(s => Played(s).Contains(o.Ornament)) / (double)count:P0}"));
        Console.WriteLine($"{id}: songs {shares}; the lines' notes ornamented {lines.Count(x => OrnamentOf(x.Value).Length > 0) / (double)lines.Length:P1}");
        await Task.CompletedTask;
    }
}
