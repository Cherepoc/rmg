using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.SongGenerators;

public sealed class MelodyFillTest
{
    /// <summary>The melody's walks into a change of section: the line and its notes' places.</summary>
    private static (double Line, double[] Positions)[] Walks(CorpusSong song) =>
    [
        ..song.Trace.Where(x => x.Point == TracePoints.LineFill && x.Track == song.Track(TrackRole.Melody)).Select(x => ((double, double[]))x.Value!)
    ];

    [Test]
    public async Task TheMelody_LeadsIntoASection_OnlyWhereItRests_ByStepIntoItsNote()
    {
        var (walks, stepping) = (0, 0);
        foreach (var song in TestCorpus.Range(256))
        {
            var melody = song.Song.Notes![song.Song.Track(TrackRole.Melody)].OrderBy(x => x.Position).ToArray();
            foreach (var (line, positions) in Walks(song))
            {
                // a solo's melody rests, its walk with it; a walk in the intro, into the first section, none's
                if (song.Map.Sections.LastOrDefault(x => x.Start <= positions[0] + 1e-9) is { } from && song.IsSolo(from))
                    continue;

                // its notes the walk's alone from its first to the line, three at most, and the note it leads into a step away
                var walked = melody.Where(x => x.Position >= positions[0] - 1e-9 && x.Position < line - 1e-9).ToArray();
                var target = melody.FirstOrDefault(x => x.Position >= line - 1e-9);
                await Assert.That(walked.Select(x => Math.Round(x.Position, 6))).IsEquivalentTo(positions.Select(x => Math.Round(x, 6)), CollectionOrdering.Matching);
                await Assert.That(positions.Length).IsLessThanOrEqualTo(3);
                // a step, two semitones at most, three for an augmented second, not the note itself again
                if (target.Value is not null && walked.Length > 0 && Math.Abs(target.Value.Pitches[0] - walked[^1].Value.Pitches[0]) is > 0 and <= 3)
                    stepping++;
                // each a note of its own, on the weakest beat, going on in its phrase
                await Assert.That(walked.Select(x => x.Value.State.GetStateValue(CompositionStateKinds.NoteKey)).Distinct().Count()).IsEqualTo(walked.Length).Because($"seed {song.Seed} at {line}");
                await Assert.That(walked.All(x => x.Value.State.GetStateValue(CompositionStateKinds.LinePhraseStart) == 0)).IsTrue().Because($"seed {song.Seed} at {line}");
                walks++;
            }
        }

        // a floor well under the corpus's count, which moves a walk or two with every change of the songs
        await Assert.That(walks).IsGreaterThan(10);
        // into its note by a step, but where the note it leads into is moved an octave into its range after
        await Assert.That(stepping / (double)walks).IsGreaterThan(0.85);
    }

    [Test]
    public async Task TheCounterMelody_LeadsIntoASection_WhereTheMelodyDoesNot()
    {
        var walks = 0;
        foreach (var song in TestCorpus.Range(256))
        {
            double[] Lines(int track) => [..song.Trace.Where(x => x.Point == TracePoints.LineFill && x.Track == track).Select(x => (((double Line, double[]))x.Value!).Line)];
            var counter = Lines(song.Track(TrackRole.CounterMelody));
            await Assert.That(counter.Intersect(Lines(song.Track(TrackRole.Melody)))).IsEmpty().Because($"seed {song.Seed}");
            walks += counter.Length;
        }

        // a floor well under the corpus's count, which moves a walk or two with every change of the songs
        await Assert.That(walks).IsGreaterThan(10);
    }

    /// <summary>How often the melody leads into a section, of its changes with a run, and with how many notes.</summary>
    [Test]
    [Explicit]
    public async Task Report()
    {
        var songs = TestCorpus.Measure(1024, song => (Walks: Walks(song), Changes: song.Map.Sections.Length - 1));
        var walks = songs.SelectMany(x => x.Walks).ToArray();
        Console.WriteLine($"  {walks.Length} walks into {songs.Sum(x => x.Changes)} changes of section, in {songs.Count(x => x.Walks.Length > 0) / (double)songs.Length:P0} of the songs; " +
                          $"notes {string.Join(", ", walks.GroupBy(x => x.Positions.Length).OrderBy(x => x.Key).Select(x => $"{x.Key}: {x.Count()}"))}");
        await Task.CompletedTask;
    }
}
