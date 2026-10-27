using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Tests.Arrangements;

public sealed class SoloAnswerTest
{
    private static SongOverrides Melody(double melody) => new(Facets: TestCorpus.FacetsOf((Facet.Melody, melody)));

    /// <summary>The solo part's notes outside its solos, and the melody's, of a song.</summary>
    private static (TimelineItem<RealizedNote>[] Answers, TimelineItem<RealizedNote>[] Melody) Notes(CorpusSong song)
    {
        TimelineItem<RealizedNote>[] Of(TrackRole role) =>
            [..song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key].Role == role).SelectMany(x => x.Value).OrderBy(x => x.Position)];
        return ([..Of(TrackRole.Solo).Where(x => x.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 0)], Of(TrackRole.Melody));
    }

    [Test]
    public async Task AnAnswer_PlaysInTheMelodysGaps_UnderIt()
    {
        var (answers, louder, sections) = (0, 0, 0);
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 128), seed => TestCorpus.Get(seed, Melody(0.5))))
        {
            var (answerNotes, melody) = Notes(song);
            var onsets = melody.Select(x => x.Position).ToArray();
            var phrases = song.Map.Sections.SelectMany(x => x.Phrases.Select(p => p.Start).Append(x.End)).ToArray();
            foreach (var note in answerNotes)
            {
                // a beat or more after the melody's note before it, and over before its next
                var before = onsets.LastOrDefault(x => x <= note.Position + 1e-9, double.NaN);
                var next = onsets.FirstOrDefault(x => x > note.Position + 1e-9, double.MaxValue);
                await Assert.That(note.Position - before).IsGreaterThanOrEqualTo(1 - 1e-9).Because($"seed {song.Seed} at {note.Position}");
                await Assert.That(note.Position + note.Value.Duration).IsLessThanOrEqualTo(next + 1e-6).Because($"seed {song.Seed} at {note.Position}");
                // between the melody's phrases, a phrase starting after the note before, its next at most a pickup before it
                await Assert.That(phrases.Any(x => x > before + 1e-9 && x <= next + Rmg.Core.Composition.SoloAnswers.Pickup + 1e-9)).IsTrue().Because($"seed {song.Seed} at {note.Position}");
                // a short line, starting a beat after the note before at the soonest and played over two bars at most
                var bar = song.Map.MeterAt(note.Position).BarDuration;
                await Assert.That(note.Position - before).IsLessThanOrEqualTo(Rmg.Core.Composition.SoloAnswers.After + Rmg.Core.Composition.SoloAnswers.LongestBars * bar + 1)
                    .Because($"seed {song.Seed} at {note.Position}");
                answers++;
            }

            // as heard in each section it answers in, against the melody there
            foreach (var span in song.Map.Sections)
            {
                var there = answerNotes.Where(x => x.Position >= span.Start && x.Position < span.End).ToArray();
                var sung = melody.Where(x => x.Position >= span.Start && x.Position < span.End).ToArray();
                if (there.Length > 0 && sung.Length > 0)
                {
                    sections++;
                    if (there.Average(x => Render.ToMidiVelocity(x.Value.Velocity)) > sung.Average(x => Render.ToMidiVelocity(x.Value.Velocity)))
                        louder++;
                }
            }
        }

        // a calibration, its bound wide, hooked choruses leaving fewer gaps
        Console.WriteLine($"{answers} answers; louder than the melody in {louder} of {sections} sections");
        await Assert.That(answers).IsGreaterThan(50);
        await Assert.That(louder / (double)sections).IsLessThan(0.1);
    }

    [Test]
    public async Task ThePlainestSongs_DoNotAnswer()
    {
        foreach (var seed in Enumerable.Range(0, 16))
            await Assert.That(Notes(TestCorpus.Get(seed, Melody(0))).Answers).IsEmpty();
    }

    /// <summary>How often the solo part plays, and answers, by fifth of the melody facet, and how its answers sit against the melody.</summary>
    [Test]
    [Explicit]
    public async Task Report()
    {
        var songs = TestCorpus.Measure(1024, song =>
        {
            var (answers, melody) = Notes(song);
            var plays = song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key].Role == TrackRole.Solo).Sum(x => x.Value.Count) > 0;
            var facet = song.Unconventionality[Facet.Melody];
            // the bars of the sections it answers in
            var bars = song.Map.Sections.Where(s => answers.Any(a => a.Position >= s.Start && a.Position < s.End)).Sum(s => s.Duration / s.Meter.BarDuration);
            return (Facet: facet, Plays: plays, Answers: answers.Length, PerBar: answers.Length / Math.Max(1, bars), Velocity: answers.Length > 0 ? answers.Average(x => Render.ToMidiVelocity(x.Value.Velocity)) - melody.Average(x => Render.ToMidiVelocity(x.Value.Velocity)) : double.NaN);
        });
        Console.WriteLine($"  the solo part playing in {songs.Count(x => x.Plays) / (double)songs.Length:P0} of the songs, answering in {songs.Count(x => x.Answers > 0) / (double)songs.Length:P0}; " +
                          $"{songs.Where(x => x.Answers > 0).Average(x => x.PerBar):F2} notes a bar in the sections answering, {songs.Where(x => x.Answers > 0).Average(x => x.Velocity):+0.0;-0.0} of MIDI velocity, as heard, against the melody");
        foreach (var band in songs.GroupBy(x => Math.Clamp((int)(x.Facet * 5), 0, 4)).OrderBy(x => x.Key))
            Console.WriteLine($"  melody {band.Key}/5, {band.Count()} songs: answering {band.Count(x => x.Answers > 0) / (double)band.Count():P0}");
        await Task.CompletedTask;
    }
}
