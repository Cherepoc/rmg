using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Registers;

/// <summary>
///     Where the parts sit: every part's notes outside its instrument's range, those on an instrument it switched to apart,
///     and the melody under the chords (measured over 128 songs on 26 October 2026, a switched instrument's notes placed in
///     the part's own octaves: 507 of 14940 outside its range, 3.4%; within the octaves it plays there: 256, 1.7%, as a part's
///     own instrument's 2.2%, its bass's 8% the most; a line's range then cut to its instrument's, the bass's below its low E
///     an octave up: 48 of 15102 on a switched instrument, 0.3%, 488 of 157963 on a part's own, 0.3%, the chords' 1% the most;
///     on 28 October a line kept under its instrument's top 42 and 403, and a register moved once on an instrument switched to,
///     a chord's floor its instrument's as tuned and a guitar tuned on every guitar: 20, 0.1%, and 254, 0.2%).
/// </summary>
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
        var (switched, switchedOutside) = (0, 0);
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
                    var outOfRange = note.Value.Pitches.Any(x => x < low || x > high);
                    if (outOfRange)
                        outside[part.Role] = outside.GetValueOrDefault(part.Role) + 1;
                    // on an instrument the part switched to, or its own
                    if (program != part.InstrumentCode)
                        (switched, switchedOutside) = (switched + 1, switchedOutside + (outOfRange ? 1 : 0));
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
        Console.WriteLine($"notes on an instrument switched to {switched}, outside its range {switchedOutside} ({switchedOutside / (double)Math.Max(1, switched):P1}); on their own {all.Values.Sum() - switched}, outside {outside.Values.Sum() - switchedOutside}");
        Console.WriteLine("notes outside their instrument's range: " + string.Join(", ", all.OrderBy(x => x.Key).Select(x => $"{x.Key} {outside.GetValueOrDefault(x.Key) / (double)x.Value:P0}")));
        await Task.CompletedTask;
    }
}
