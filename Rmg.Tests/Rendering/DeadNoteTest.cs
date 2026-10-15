using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Rendering;

/// <summary>Dead notes (<see cref="Articulations.DeadNoteChance" />): a funk bass's or guitar's 16ths between the 8ths, in a chain, muted short and soft.</summary>
public sealed class DeadNoteTest
{
    /// <summary>
    ///     Funk's songs asked to play them mute only a chain's 16ths between the 8ths, its bass's, riff's or rhythm part's,
    ///     a note a 16th before or after; the songs' own play none, but a chug's.
    /// </summary>
    [Test]
    public async Task ADeadNote_IsAChainsSixteenthBetweenTheEighths()
    {
        var genre = GenrePresets.With("funk", GenreChoices.Articulation, ("deadNotes", 1), ("legato", 0), ("stabs", 0), ("roll", 0));
        var dead = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre))))
        foreach (var role in new[] { TrackRole.Bass, TrackRole.Riff, TrackRole.Rhythm })
        {
            if (!song.Song.Notes!.TryGetValue(song.Track(role), out var notes))
                continue;
            var positions = notes.Select(x => x.Position).ToHashSet();
            foreach (var note in notes.Where(x => x.Value.State.GetStateValue(CompositionStateKinds.PalmMute) > 0))
            {
                dead++;
                var between = Math.Abs(Math.IEEERemainder(note.Position - 0.25, 0.5)) < 0.05;
                await Assert.That(between).IsTrue().Because($"seed {song.Seed}, {role} at {note.Position}");
            }
        }

        Console.WriteLine($"{dead} dead notes in 32 Funk songs asked to play them");
        await Assert.That(dead).IsGreaterThan(50);
    }
}
