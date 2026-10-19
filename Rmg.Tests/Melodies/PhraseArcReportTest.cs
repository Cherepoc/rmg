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

    /// <summary>
    ///     Every phrase of a line of five notes or more, as <see cref="Arcs" /> takes them: how much louder its first note and
    ///     its held last note play than its middle notes, the second to the fourth fifths of its notes, on average, in MIDI
    ///     velocity steps.
    /// </summary>
    internal static (double First, double Last)[] Ends(CorpusSong song, int track)
    {
        if (!song.Song.Notes!.TryGetValue(track, out var notes))
            return [];
        var ends = new List<(double, double)>();
        var phrase = new List<Rmg.Core.Songs.RealizedNote>();
        foreach (var note in notes.Select(x => x.Value))
        {
            phrase.Add(note);
            if (note.State.GetStateValue(StateKinds.HeldDuration) <= 0)
                continue;
            if (phrase.Count >= 5)
            {
                var middle = phrase.Skip(phrase.Count / 5).Take(phrase.Count * 4 / 5 - phrase.Count / 5).Average(Render.GetMidiVelocity);
                ends.Add((127 * (Render.GetMidiVelocity(phrase[0]) - middle), 127 * (Render.GetMidiVelocity(phrase[^1]) - middle)));
            }

            phrase.Clear();
        }

        return [..ends];
    }

    /// <summary>
    ///     A melody's phrase lets its close fall away: its held last note plays softer than its middle (seen 4.6 velocity
    ///     steps under it in 128 songs, 1.4 before the phrase swelled), its first, on its downbeat's accent, louder still.
    /// </summary>
    [Test]
    public async Task AMelodysPhrase_EndsSofterThanItsMiddle()
    {
        var ends = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => Ends(TestCorpus.Get(seed), TestCorpus.Get(seed).Track(TrackRole.Melody))).SelectMany(x => x).ToArray();
        Console.WriteLine($"  {ends.Length} phrases, the first note {ends.Average(x => x.First):+0.0;-0.0} and the last {ends.Average(x => x.Last):+0.0;-0.0} velocity steps over the middle");
        await Assert.That(ends.Length).IsGreaterThan(500);
        await Assert.That(ends.Average(x => x.Last)).IsLessThan(-2.5);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var (name, role) in new[] { ("melody", TrackRole.Melody), ("counter-melody", TrackRole.CounterMelody), ("solo", TrackRole.Solo) })
        {
            var arcs = TestCorpus.Measure(128, song => Arcs(song, song.Track(role))).SelectMany(x => x).ToArray();
            Console.WriteLine($"{name}: {arcs.Length} phrases, the highest note {arcs.Average():+0.0;-0.0} velocity steps over the lowest, louder in {100.0 * arcs.Count(x => x > 0) / arcs.Length:0}%");
            var ends = TestCorpus.Measure(128, song => Ends(song, song.Track(role))).SelectMany(x => x).ToArray();
            Console.WriteLine($"  {ends.Length} phrases of five notes or more, the first note {ends.Average(x => x.First):+0.0;-0.0} and the last {ends.Average(x => x.Last):+0.0;-0.0} velocity steps over the middle");
        }

        await Task.CompletedTask;
    }
}
