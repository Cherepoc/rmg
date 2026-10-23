using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Rendering;

/// <summary>Dead notes (<see cref="Articulations.DeadNoteChance" />): a funk bass's, guitar's or soloist's 16ths between the 8ths, in a chain, muted short and soft.</summary>
public sealed class DeadNoteTest
{
    /// <summary>
    ///     Funk's songs asked to play them mute only a chain's 16ths between the 8ths, its bass's, riff's or rhythm part's own
    ///     line's, a note a 16th before or after, a solo its soloist's ghosts alone (SoloTest); the songs' own play none, but a
    ///     chug's.
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
            foreach (var note in notes.Where(x => x.Value.State.GetStateValue(CompositionStateKinds.PalmMute) > 0 && x.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 0))
            {
                dead++;
                // between its bar's 8ths, counted from the bar's start
                var between = Math.Abs(Math.IEEERemainder(song.Map.PlaceOf(note.Position).Beat - 0.25, 0.5)) < 0.05;
                await Assert.That(between).IsTrue().Because($"seed {song.Seed}, {role} at {note.Position}");
            }
        }

        Console.WriteLine($"{dead} dead notes in 32 Funk songs asked to play them");
        await Assert.That(dead).IsGreaterThan(50);
    }

    /// <summary>The songs' own dead notes, none, leave a band that chugs muting no note but a chug's, in a section that chugs, a soloist's ghosts aside: Metal's without its articulations.</summary>
    [Test]
    public async Task TheSongsOwn_MuteOnlyAChugsNotes()
    {
        var chugged = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Without("metal", GenreChoices.Articulation.Name)))))
        {
            var chugs = song.Trace.Where(x => x.Point == TracePoints.Chugs).ToDictionary(x => x.Section, x => (bool)x.Value!);
            foreach (var role in new[] { TrackRole.Bass, TrackRole.Riff, TrackRole.RiffTwin, TrackRole.Rhythm, TrackRole.Chords })
            foreach (var note in song.Song.Notes!.GetValueOrDefault(song.Track(role))?.Where(x => x.Value.State.GetStateValue(CompositionStateKinds.PalmMute) > 0 && x.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 0) ?? [])
            {
                chugged++;
                var play = song.Map.PlayingAt(note.Position);
                await Assert.That(play is { } at && chugs.GetValueOrDefault(at.Span.SectionId)).IsTrue().Because($"seed {song.Seed}, {role} at {note.Position}");
            }
        }

        Console.WriteLine($"{chugged} muted notes in 32 Metal songs");
        await Assert.That(chugged).IsGreaterThan(0);
    }
}
