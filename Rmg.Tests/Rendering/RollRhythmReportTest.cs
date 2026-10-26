using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Rendering;

/// <summary>How evenly a banjo's roll runs, Bluegrass's rhythm part rolling its chords (<see cref="Articulations.ThreeFingerRoll" />).</summary>
public sealed class RollRhythmReportTest
{
    /// <summary>
    ///     The rolled notes' onsets on and off the 8ths of their bar, the steps between one and the next within a phrase, and the
    ///     time a rolling phrase is silent between them, a solo's line, a break's own rhythm, left out (measured over 64 songs on
    ///     26 October 2026, the roll following the part's own rhythm, its solos counted: 12% of the notes off the 8ths, a 16th
    ///     step 5903 times, the phrases silent 11% of the time; rolled in even 8ths held through the part's rests, its solos
    ///     left out: every note on an 8th, 31146 of 31155 steps an 8th, the phrases silent none of the time).
    /// </summary>
    [Test]
    [Explicit]
    public async Task Report()
    {
        var (on, off, silent, rolling) = (0, 0, 0.0, 0.0);
        var steps = new Dictionary<double, int>();
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("bluegrass")))))
        {
            var notes = song.Song.Notes!.GetValueOrDefault(song.Track(TrackRole.Rhythm))?.Where(x => x.Value.State.GetStateValue(CompositionStateKinds.Arpeggio).Value == (int)ArpeggioPattern.Roll && x.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 0).ToArray() ?? [];
            foreach (var note in notes)
                if (Math.Abs(song.Map.PlaceOf(note.Position).Beat * 2 - Math.Round(song.Map.PlaceOf(note.Position).Beat * 2)) < 1e-6) on++;
                else off++;
            foreach (var (a, b) in notes.Zip(notes.Skip(1)))
            {
                if (song.Map.PlayingAt(a.Position)?.Span.Phrases.Where(p => a.Position >= p.Start - 1e-9 && a.Position < p.End - 1e-9).Select(p => (double?)p.End).FirstOrDefault() is not { } end || b.Position >= end - 1e-9)
                    continue;
                var step = Math.Round(b.Position - a.Position, 3);
                steps[step] = steps.GetValueOrDefault(step) + 1;
                rolling += b.Position - a.Position;
                silent += Math.Max(0, b.Position - (a.Position + a.Value.Duration));
            }
        }

        Console.WriteLine($"rolled notes on an 8th {on}, off it {off}; silent {silent / Math.Max(rolling, 1e-9):P0} of the rolling time; steps {string.Join(", ", steps.OrderByDescending(x => x.Value).Take(10).Select(x => $"{x.Key} x{x.Value}"))}");
        await Task.CompletedTask;
    }
}
