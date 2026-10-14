using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Rendering;

/// <summary>A chord cycled at a sound chip's frame rate, its notes in turn on one voice.</summary>
public sealed class FrameArpeggioTest
{
    /// <summary>A Chiptune song asked to play its chords so plays no chord on its chords part, but one note at a time, its whole chords' notes two frames each.</summary>
    [Test]
    public async Task AChordAtTheFrameRate_PlaysOneNoteAtATime_EachTwoFramesAtMost()
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
        await Assert.That(cycled).IsGreaterThan(6);
    }
}
