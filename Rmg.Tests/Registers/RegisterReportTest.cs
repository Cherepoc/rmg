using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Registers;

/// <summary>Where the parts sit: every part's notes outside its instrument's range, and the melody under the chords.</summary>
public sealed class RegisterReportTest
{
    private const int Count = 128;

    [Test]
    [Explicit]
    public async Task Report()
    {
        var songs = TestCorpus.Range(Count);
        var (outside, all) = (new Dictionary<TrackRole, int>(), new Dictionary<TrackRole, int>());
        var (under, melodies) = (0, 0);
        var (melodyMiddles, chordsMiddles) = (new List<double>(), new List<double>());
        foreach (var song in songs)
        {
            foreach (var (track, notes) in song.Song.Notes!)
            {
                if (song.Song.TrackDefinitions[track] is not PitchInstrumentTrack part)
                    continue;
                foreach (var note in notes)
                {
                    var program = InstrumentsOverTime.ProgramOf(note.Value.State, part.InstrumentCode);
                    var (low, high) = InstrumentRanges.Of(program);
                    all[part.Role] = all.GetValueOrDefault(part.Role) + 1;
                    if (note.Value.Pitches.Any(x => x < low || x > high))
                        outside[part.Role] = outside.GetValueOrDefault(part.Role) + 1;
                }
            }

            var melody = song.Song.Notes!.GetValueOrDefault(song.Song.Track(TrackRole.Melody));
            var chords = song.Song.Notes!.GetValueOrDefault(song.Song.Track(TrackRole.Chords));
            if (melody is not { Count: > 0 } || chords is not { Count: > 0 })
                continue;
            melodyMiddles.Add(melody.Average(x => x.Value.Pitches.Average()));
            chordsMiddles.Add(chords.Average(x => x.Value.Pitches.Average()));
            foreach (var note in melody)
            {
                melodies++;
                if (chords.Any(c => c.Position <= note.Position + 1e-9 && c.Position + c.Value.Duration > note.Position + 1e-9 && c.Value.Pitches.Max() > note.Value.Pitches.Max()))
                    under++;
            }
        }

        Console.WriteLine($"melody notes under a chord's top note sounding with them {under / (double)melodies:P0}; the melody's mean note {melodyMiddles.Average():F1}, the chords' {chordsMiddles.Average():F1}, songs whose chords sit higher on the average {melodyMiddles.Zip(chordsMiddles).Count(x => x.Second > x.First) / (double)melodyMiddles.Count:P0}");
        Console.WriteLine("notes outside their instrument's range: " + string.Join(", ", all.OrderBy(x => x.Key).Select(x => $"{x.Key} {outside.GetValueOrDefault(x.Key) / (double)x.Value:P0}")));
        await Task.CompletedTask;
    }
}
