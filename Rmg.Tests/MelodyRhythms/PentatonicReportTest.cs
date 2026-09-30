using Rmg.Core.Composition;
using Rmg.Core.Events;

namespace Rmg.Tests.MelodyRhythms;

/// <summary>
///     How pentatonic the melodies of pentatonic sections are: the share of their notes on the scale's tritone pair, off
///     the beat and on it, against the other sections', and how the melody moves in each.
/// </summary>
public sealed class PentatonicReportTest
{
    [Test]
    [Explicit]
    public async Task Report()
    {
        var stats = new Dictionary<bool, (int Notes, int OnPair, int Moves, int Leaps, double Move)>();
        var sections = 0;
        var pentatonicSections = 0;
        foreach (var song in TestCorpus.Range(200))
        {
            var pentatonic = song.Trace.Where(x => x.Point == TracePoints.Pentatonic).ToDictionary(x => x.Section, x => (bool)x.Value!);
            var common = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap;
            var melody = song.Song.Notes![SongTracks.MelodyTrack].ToArray();
            foreach (var span in song.Map.Sections)
            {
                sections++;
                var isPentatonic = pentatonic[span.SectionId];
                pentatonicSections += isPentatonic ? 1 : 0;
                var notes = melody.Where(x => x.Position >= span.Start && x.Position < span.End).ToArray();
                var s = stats.GetValueOrDefault(isPentatonic);
                for (var i = 0; i < notes.Length; i++)
                {
                    var key = common.GetStateTimeline(StateKinds.KeyOffset).GetEffectiveValueAt(notes[i].Position);
                    var scale = common.GetStateTimeline(StateKinds.ScaleOffsets).GetEffectiveValueAt(notes[i].Position).Select(x => (x + key) % 12).ToArray();
                    var pair = scale.Where(x => scale.Contains((x + 6) % 12)).ToArray();
                    s.Notes++;
                    s.OnPair += pair.Length == 2 && pair.Contains(notes[i].Value.Pitches[0] % 12) ? 1 : 0;
                    if (i > 0)
                    {
                        var move = Math.Abs(notes[i].Value.Pitches[0] - notes[i - 1].Value.Pitches[0]);
                        (s.Moves, s.Leaps, s.Move) = (s.Moves + 1, s.Leaps + (move >= 7 ? 1 : 0), s.Move + move);
                    }
                }

                stats[isPentatonic] = s;
            }
        }

        Console.WriteLine($"{pentatonicSections} of {sections} sections pentatonic");
        foreach (var (isPentatonic, s) in stats)
            Console.WriteLine($"{(isPentatonic ? "pentatonic" : "the others")}: {s.Notes} notes, on the tritone pair {s.OnPair / (double)s.Notes:P1}; mean move {s.Move / s.Moves:F2}, leaps {s.Leaps / (double)s.Moves:P1}");
        await Task.CompletedTask;
    }
}
