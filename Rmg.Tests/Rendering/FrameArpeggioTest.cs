using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Rendering;

/// <summary>A chord cycled at a sound chip's frame rate, its notes in turn on one voice.</summary>
public sealed class FrameArpeggioTest
{
    /// <summary>A Chiptune song asked to play its chords so plays no chord on its chords part, but one note at a time, and cycles some, a note a frame or two.</summary>
    [Test]
    public async Task AChordAtTheFrameRate_PlaysOneNoteAtATime()
    {
        var genre = GenrePresets.With("chiptune", GenreChoices.Articulation, ("frames", 1), ("legato", 0), ("roll", 0));
        var cycled = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre))))
        {
            var chords = song.Song.Track(TrackRole.Chords);
            if (!song.Song.Notes!.TryGetValue(chords, out var notes))
                continue;
            await Assert.That(notes.All(x => x.Value.Pitches.Length == 1)).IsTrue().Because($"seed {song.Seed}");
            var tempo = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetStateTimeline(Rmg.Core.Events.StateKinds.Tempo);
            var frames = notes.Count(x => x.Value.Duration / Meter.BeatsPerSecond(tempo.GetEffectiveValueAt(x.Position)) < Articulations.FrameSeconds + 1e-6);
            // a song whose chords are all broken has none whole to cycle
            cycled += frames > 0 ? 1 : 0;
        }

        Console.WriteLine($"{cycled} of 16 songs cycling chords");
        // measured 9 at offset 0 and 5 at 816000000, the rest breaking every chord
        await Assert.That(cycled).IsGreaterThan(2);
    }

    /// <summary>
    ///     A short chord cycled two frames a note, a held one slower, in whole frames, so that it cycles three times at the
    ///     most, as a long chord buzzes at two frames a note: at a beat a second, a quarter's 0.1 s and two seconds' chord.
    /// </summary>
    [Test]
    [Arguments(0.1, 2)]
    [Arguments(2.0, 13)]
    public async Task AChordCycled_TakesTwoFramesANote_OrFewerCyclesHeld(double beats, int frames)
    {
        var chord = new RealizedNote([60, 64, 67], 0.8, beats, Rmg.Core.Events.StateMap.FromStates([CompositionStateKinds.Articulation.CreateState(new Rmg.Core.Events.LayerValue<ArticulationChoice>(Rmg.Core.Events.StateDepths.Note, new(PartArticulation.Frames, 0, 0)))]));
        var cycled = Articulations.Frames(Rmg.Core.Events.EventTimeline.Create(4, [new Rmg.Core.Events.TimelineItem<RealizedNote>(0, chord)]), _ => 1).ToArray();
        await Assert.That(cycled[1].Position - cycled[0].Position).IsEqualTo(frames * Articulations.Frame).Within(1e-9);
        // its notes cycled as many times at the most, and once more begun
        await Assert.That(cycled.Length).IsLessThanOrEqualTo(chord.Pitches.Length * Articulations.FrameCycles + 1);
    }

    /// <summary>
    ///     Chiptune's chords part never overlapping itself, a cycled chord's notes and a broken chord's on its square or saw
    ///     ending where the part's next starts, so that no two cycles run together nor broken notes smear; and no echo on a
    ///     cycled note, which would blur its cycle (heard 15 October 2026: cycles running together read as too fast).
    /// </summary>
    [Test]
    public async Task AChipsChords_NeverOverlapThemselves_AndACycledNoteNeverEchoes()
    {
        var checkedNotes = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("chiptune")))))
        {
            if (!song.Song.Notes!.TryGetValue(song.Track(TrackRole.Chords), out var notes))
                continue;
            var single = notes.Where(x => x.Value.Pitches.Length == 1).ToArray();
            foreach (var (note, next) in single.Zip(single.Skip(1)))
            {
                checkedNotes++;
                await Assert.That(note.Position + note.Value.Duration).IsLessThanOrEqualTo(next.Position + 1e-6).Because($"seed {song.Seed} at {note.Position}");
            }

            await Assert.That(notes.Any(x => x.Value.State.GetStateValue(CompositionStateKinds.Cycled) > 0 && x.Value.State.GetStateValue(CompositionStateKinds.Echo) > 0)).IsFalse().Because($"seed {song.Seed}");
        }

        await Assert.That(checkedNotes).IsGreaterThan(1000);
    }

    /// <summary>A broken chord's note on an instrument that holds its sound, a saw's or a pad's, ends where its part's next note starts, in the songs and in Synthwave's and Trance's, so that broken notes never smear into a cluster.</summary>
    [Test]
    [Arguments(null)]
    [Arguments("synthwave")]
    [Arguments("trance")]
    public async Task ABrokenChordsNote_OnAHeldSound_EndsWhereItsPartsNextStarts(string? genre)
    {
        var broken = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre is null ? null : GenrePresets.Genre(genre)))))
        foreach (var (track, definition) in song.Song.TrackDefinitions.Where(x => x.Value is PitchInstrumentTrack && song.Song.Notes!.ContainsKey(x.Key)))
        {
            var notes = song.Song.Notes![track].ToArray();
            for (var i = 0; i + 1 < notes.Length; i++)
            {
                var note = notes[i];
                if (note.Value.State.GetStateValue(CompositionStateKinds.Arpeggio).Value == 0 || note.Value.Pitches.Length != 1
                    || InstrumentTraits.Rings(InstrumentsOverTime.ProgramOf(note.Value.State, ((PitchInstrumentTrack)definition).InstrumentCode)))
                    continue;
                broken++;
                await Assert.That(note.Position + note.Value.Duration).IsLessThanOrEqualTo(notes[i + 1].Position + 1e-6).Because($"{genre} seed {song.Seed}, track {track} at {note.Position}");
            }
        }

        await Assert.That(broken).IsGreaterThan(0);
    }

    /// <summary>A chord's notes spread before they are cycled, none a tone or less above the one below, every note kept in its pitch class: a sus2 as root, fifth and ninth.</summary>
    [Test]
    [Arguments(new[] { 60, 62, 67 }, new[] { 60, 67, 74 })]
    [Arguments(new[] { 60, 61, 67 }, new[] { 60, 67, 73 })]
    [Arguments(new[] { 60, 65, 66 }, new[] { 60, 65, 78 })]
    [Arguments(new[] { 60, 63, 67 }, new[] { 60, 63, 67 })]
    [Arguments(new[] { 60, 63, 66 }, new[] { 60, 63, 66 })]
    public async Task AChordCycled_IsSpread_NoTwoNotesInTurnATonApart(int[] chord, int[] cycled)
    {
        var spread = Articulations.Spread(chord);
        await Assert.That(spread).IsEquivalentTo(cycled);
        await Assert.That(spread.Zip(spread.Skip(1)).All(x => x.Second - x.First > Articulations.FramesNearest)).IsTrue();
    }
}
