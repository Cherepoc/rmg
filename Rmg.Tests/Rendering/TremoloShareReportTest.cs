using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Rendering;

/// <summary>
///     How much of a genre's lines are tremolo-picked, read from the notes as played whatever picks them: a note struck
///     again at its pitches a 16th or quicker after one held to it, or before one so struck, by part; the appearances whose
///     riff picks the most of its notes, and those whose articulation picks them; and how much of its appearances the riff is silent.
/// </summary>
public sealed class TremoloShareReportTest
{
    [Test]
    [Explicit]
    [Arguments("blackMetal")]
    [Arguments("surf")]
    [Arguments("thrash")]
    [Arguments("metal")]
    [Arguments("symphonic")]
    [Arguments("postRock")]
    public async Task Report(string genre)
    {
        var (picked, notes) = (new Dictionary<TrackRole, int>(), new Dictionary<TrackRole, int>());
        var (appearances, picking, tremolo, silent, played) = (0, 0, 0, 0.0, 0.0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre)))))
        foreach (var role in new[] { TrackRole.Riff, TrackRole.Rhythm, TrackRole.Melody }.Where(x => song.Song.TrackDefinitions.Values.Any(t => t.Role == x)))
        {
            var line = song.Song.Notes!.GetValueOrDefault(song.Track(role))?.ToArray() ?? [];
            var restruck = new bool[line.Length];
            for (var i = 0; i + 1 < line.Length; i++)
                if (line[i + 1].Value.Pitches.SequenceEqual(line[i].Value.Pitches) && line[i + 1].Position - line[i].Position <= 0.25 + 1e-6
                                                                                 && line[i].Position + line[i].Value.Duration >= line[i + 1].Position - 1e-6)
                    restruck[i] = restruck[i + 1] = true;
            picked[role] = picked.GetValueOrDefault(role) + restruck.Count(x => x);
            notes[role] = notes.GetValueOrDefault(role) + line.Length;
            if (role != TrackRole.Riff)
                continue;
            foreach (var span in song.PlayedSpans.Where(x => !song.Resting(x).Contains(TrackRole.Riff)))
            {
                var inSpan = Enumerable.Range(0, line.Length).Where(i => line[i].Position >= span.Start - 1e-9 && line[i].Position < span.End - 1e-9).ToArray();
                if (inSpan.Length == 0)
                    continue;
                appearances++;
                if (inSpan.Count(i => restruck[i]) * 2 > inSpan.Length)
                    picking++;
                // and most of them tremolo-picked by its articulation
                if (inSpan.Count(i => line[i].Value.State.GetStateValue(CompositionStateKinds.Picked) > 0) * 2 > inSpan.Length)
                    tremolo++;
                // its silence: the span less what its notes sound of it
                var sounding = 0.0;
                var until = span.Start;
                foreach (var i in inSpan)
                {
                    var (from, to) = (Math.Max(until, line[i].Position), Math.Min(span.End, line[i].Position + line[i].Value.Duration));
                    if (to > from)
                        sounding += to - from;
                    until = Math.Max(until, to);
                }
                silent += span.Duration - sounding;
                played += span.Duration;
            }
        }

        Console.WriteLine($"{genre}: notes picked, riff {Share(TrackRole.Riff)}, rhythm part {Share(TrackRole.Rhythm)}, melody {Share(TrackRole.Melody)}; " +
                          $"riff appearances picking most of their notes {picking / (double)Math.Max(1, appearances):P0} of {appearances}, tremolo-picked {tremolo / (double)Math.Max(1, appearances):P0}; the riff silent {silent / Math.Max(1e-9, played):P0} of its appearances");
        await Task.CompletedTask;
        string Share(TrackRole role) => notes.GetValueOrDefault(role) == 0 ? "-" : $"{picked[role] / (double)notes[role]:P0}";
    }
}
