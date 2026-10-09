using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Rendering;

namespace Rmg.Tests.Melodies;

/// <summary>How a line's loudness follows its phrase: its highest note against its lowest, a phrase ending on its held note.</summary>
public sealed class PhraseArcReportTest
{
    /// <summary>
    ///     Every phrase of a line, its notes up to and including a held one, of three notes or more and not all at one
    ///     pitch: how much louder its highest note plays than its lowest, in MIDI velocity steps.
    /// </summary>
    internal static double[] Arcs(CorpusSong song, int track)
    {
        if (!song.Song.Notes!.TryGetValue(track, out var notes))
            return [];
        var arcs = new List<double>();
        var phrase = new List<Rmg.Core.Songs.RealizedNote>();
        foreach (var note in notes.Select(x => x.Value))
        {
            phrase.Add(note);
            if (note.State.GetStateValue(StateKinds.HeldDuration) <= 0)
                continue;
            if (phrase.Count >= 3 && phrase.Select(x => x.Pitches.Max()).Distinct().Count() > 1)
            {
                var high = phrase.MaxBy(x => x.Pitches.Max())!;
                var low = phrase.MinBy(x => x.Pitches.Max())!;
                arcs.Add(127 * (Render.GetMidiVelocity(high) - Render.GetMidiVelocity(low)));
            }

            phrase.Clear();
        }

        return [..arcs];
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var (name, role) in new[] { ("melody", TrackRole.Melody), ("counter-melody", TrackRole.CounterMelody), ("solo", TrackRole.Solo) })
        {
            var arcs = TestCorpus.Measure(128, song => Arcs(song, song.Track(role))).SelectMany(x => x).ToArray();
            Console.WriteLine($"{name}: {arcs.Length} phrases, the highest note {arcs.Average():+0.0;-0.0} velocity steps over the lowest, louder in {100.0 * arcs.Count(x => x > 0) / arcs.Length:0}%");
        }

        await Task.CompletedTask;
    }
}
