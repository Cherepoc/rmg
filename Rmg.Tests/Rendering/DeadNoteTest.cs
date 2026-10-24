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

    /// <summary>
    ///     A funk guitar's chords ring open and its dead notes scratch muted, on the one guitar: no dead note on a clean or jazz
    ///     guitar, which plays it on General MIDI's muted guitar, its other notes on its own, and the funk presets' rhythm part never on the
    ///     muted guitar but for its dead notes, a muted line the riff's sound alone.
    /// </summary>
    [Test]
    [Arguments("funk")]
    [Arguments("funkRock")]
    [Arguments("disco")]
    public async Task AFunkGuitarsChords_RingOpen_ItsDeadNotesMuted(string genre)
    {
        var (dead, open) = (0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre)))))
        foreach (var (track, definition) in song.Song.TrackDefinitions.Where(x => x.Value is PitchInstrumentTrack { Role: TrackRole.Rhythm or TrackRole.Riff }))
        foreach (var note in song.Song.Notes!.GetValueOrDefault(track)?.Where(x => x.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 0) ?? [])
        {
            var own = ((PitchInstrumentTrack)definition).InstrumentCode;
            var program = InstrumentsOverTime.ProgramOf(note.Value.State, own);
            var isDead = note.Value.State.GetStateValue(CompositionStateKinds.PalmMute) > 0;
            // a dead note never on a clean or a jazz guitar, its muted sound General MIDI's muted guitar, a driven one's played short
            if (isDead)
            {
                dead += program == 28 && own != 28 ? 1 : 0;
                await Assert.That(program is not (26 or 27)).IsTrue().Because($"seed {song.Seed}, track {track} at {note.Position}");
            }
            if (definition.Role == TrackRole.Rhythm && !isDead)
            {
                open++;
                await Assert.That(program).IsNotEqualTo(28).Because($"seed {song.Seed}, track {track} at {note.Position}");
            }
        }

        Console.WriteLine($"{genre}: {dead} dead notes muted on a clean guitar, {open} of the rhythm part's notes open");
        await Assert.That(open).IsGreaterThan(100);
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
