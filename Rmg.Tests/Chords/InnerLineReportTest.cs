using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Tests.Chords;

/// <summary>
///     Whether the melody clashes with a chromatic inner line (<see cref="InnerLines.Apply" />), which moves the chords'
///     root voice after the notes are decided, where the melody does not see it.
/// </summary>
public sealed class InnerLineReportTest
{
    /// <summary>
    ///     The melody's notes on a beat a semitone from a note the chords hold then, under a chord whose inner line has moved
    ///     and under any other, each of its notes on a beat over a chord.
    /// </summary>
    private static ((int Clashes, int Notes) Moved, (int Clashes, int Notes) Other) Clashes(CorpusSong song)
    {
        var notes = song.Song.Notes!;
        var chords = notes.GetValueOrDefault(SongTracks.ChordsTrack)?.ToArray() ?? [];
        var melody = notes.GetValueOrDefault(SongTracks.MelodyTrack)?.ToArray() ?? [];
        (int, int) moved = (0, 0), other = (0, 0);
        foreach (var note in melody.Where(x => Math.Abs(x.Position - Math.Round(x.Position)) < 1e-6))
        {
            var held = chords.Where(x => x.Position <= note.Position + 1e-6 && x.Position + x.Value.Duration > note.Position + 1e-6).ToArray();
            if (held.Length == 0)
                continue;
            var clashes = held.Any(x => x.Value.Pitches.Any(p => note.Value.Pitches.Any(m => ((p - m) % 12 + 12) % 12 is 1 or 11)));
            if (held.Any(x => x.Value.State.GetStateValue(CompositionStateKinds.InnerLine) > 0))
                moved = (moved.Item1 + (clashes ? 1 : 0), moved.Item2 + 1);
            else
                other = (other.Item1 + (clashes ? 1 : 0), other.Item2 + 1);
        }

        return (moved, other);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        // every section that may take the line, so that its chords are many
        var genre = new Genre(ImmutableDictionary<string, ImmutableDictionary<string, ByConvention>>.Empty
            .Add(GenreChoices.HarmonyDevices.Name, ImmutableDictionary<string, ByConvention>.Empty.Add("innerLine", new ByConvention(1, 1, 1))));
        foreach (var (name, songs) in new[]
                 {
                     ("the songs", TestCorpus.Measure(256, Clashes)),
                     ("every section lining", TestCorpus.InParallel(Enumerable.Range(0, 256), seed => Clashes(TestCorpus.Get(seed, new SongOverrides(Genre: genre)))))
                 })
        {
            var (moved, movedNotes) = (songs.Sum(x => x.Moved.Clashes), songs.Sum(x => x.Moved.Notes));
            var (other, otherNotes) = (songs.Sum(x => x.Other.Clashes), songs.Sum(x => x.Other.Notes));
            Console.WriteLine($"{name}: melody notes on a beat a semitone from the chords, under a moved inner line {moved} of {movedNotes}, "
                              + $"{100.0 * moved / Math.Max(1, movedNotes):F1}%; elsewhere {other} of {otherNotes}, {100.0 * other / Math.Max(1, otherNotes):F1}%");
        }

        var lined = TestCorpus.Measure(256, song => song.Song.Notes!.GetValueOrDefault(SongTracks.ChordsTrack)?.Any(x => x.Value.State.GetStateValue(CompositionStateKinds.InnerLine) > 0) == true);
        Console.WriteLine($"songs sounding an inner line: {lined.Count(x => x)} of {lined.Length}");
        await Task.CompletedTask;
    }
}
