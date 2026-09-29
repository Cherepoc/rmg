using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;

namespace Rmg.Tests.MelodyRhythms;

/// <summary>
///     How the melody plays again what comes back: a section's second 4-bar pattern against its first, which has the
///     same state, and a section that recurs against where it first played; and how the melody moves, within its
///     phrases and where a phrase starts again, which it does mostly after a rest.
/// </summary>
public sealed class MelodyRepetitionTest
{
    private const int SongCount = 100;

    /// <param name="Pairs">Notes at the same place in a pattern that comes back, both played.</param>
    /// <param name="Same">Of those, the ones that play the same note.</param>
    /// <param name="SameClass">Of those, the ones that play the same note in some octave.</param>
    /// <param name="SectionStartLeaps">Moves into a section's first note that are leaps.</param>
    /// <param name="Joins">Moves into the first note of a 4-bar pattern, where a phrase starts again.</param>
    /// <param name="JoinLeaps">Of those, the leaps.</param>
    /// <param name="RestedJoinLeaps">Of those, the ones after a rest of half a beat or more.</param>
    internal sealed record Measures(
        int Notes,
        int Pairs,
        int Same,
        int SameClass,
        double MeanMove,
        double LeapShare,
        double OnBeatChordNotes,
        int SectionStarts,
        int SectionStartLeaps,
        int Joins,
        int JoinLeaps,
        int RestedJoinLeaps
    );

    internal static Measures Measure(IEnumerable<CorpusSong> songs)
    {
        int notes = 0, pairs = 0, same = 0, sameClass = 0, leaps = 0, moves = 0, onBeat = 0, onBeatChord = 0, starts = 0, startLeaps = 0;
        int joins = 0, joinLeaps = 0, restedJoinLeaps = 0;
        double moveSum = 0;
        foreach (var song in songs)
        {
            var melody = song.Song.Notes![SongTracks.MelodyTrack].ToArray();
            var chords = song.Song.Notes[SongTracks.ChordsTrack].GroupBy(x => x.Position)
                .Select(x => (Position: x.Key, Classes: x.SelectMany(n => n.Value.Pitches).Select(p => p.Mod(12)).ToHashSet()))
                .ToArray();
            notes += melody.Length;
            var patternStarts = song.Map.Sections.SelectMany(x => new[] { x.Start, x.Start + Meter.PatternDuration }).ToArray();
            for (var i = 1; i < melody.Length; i++)
            {
                var move = Math.Abs(melody[i].Value.Pitches[0] - melody[i - 1].Value.Pitches[0]);
                var isLeap = move >= MelodyLayers.Line.LeapSize;
                moves++;
                moveSum += move;
                leaps += isLeap ? 1 : 0;
                if (!patternStarts.Any(x => melody[i - 1].Position < x && melody[i].Position >= x))
                    continue;
                joins++;
                joinLeaps += isLeap ? 1 : 0;
                var rest = melody[i].Position - (melody[i - 1].Position + melody[i - 1].Value.Duration);
                restedJoinLeaps += isLeap && rest >= 0.5 ? 1 : 0;
            }

            foreach (var note in melody.Where(x => Math.Abs(x.Position - Math.Round(x.Position)) < 1e-9))
            {
                var chord = chords.LastOrDefault(x => x.Position <= note.Position);
                if (chord.Classes is null)
                    continue;
                onBeat++;
                onBeatChord += chord.Classes.Contains(note.Value.Pitches[0].Mod(12)) ? 1 : 0;
            }

            // what comes back: every section's second pattern against its first, and a section against its first place
            var pitches = melody.ToDictionary(x => Math.Round(x.Position, 6), x => x.Value.Pitches[0]);
            var spans = song.Map.Sections;
            var comparisons = spans.Select(x => (From: x.Start, To: x.Start + Meter.PatternDuration, Length: Meter.PatternDuration))
                .Concat(spans.Select((x, i) => (Span: x, First: spans.First(s => s.SectionId == x.SectionId)))
                    .Where(x => x.First != x.Span)
                    .Select(x => (From: x.First.Start, To: x.Span.Start, Length: x.Span.Duration)));
            foreach (var (from, to, length) in comparisons)
            foreach (var note in melody.Where(x => x.Position >= from && x.Position < from + length))
            {
                if (!pitches.TryGetValue(Math.Round(note.Position - from + to, 6), out var again))
                    continue;
                pairs++;
                same += again == note.Value.Pitches[0] ? 1 : 0;
                sameClass += (again - note.Value.Pitches[0]).Mod(12) == 0 ? 1 : 0;
            }

            foreach (var span in spans.Skip(1))
            {
                var index = Array.FindIndex(melody, x => x.Position >= span.Start);
                if (index <= 0 || melody[index].Position >= span.End)
                    continue;
                starts++;
                startLeaps += Math.Abs(melody[index].Value.Pitches[0] - melody[index - 1].Value.Pitches[0]) >= MelodyLayers.Line.LeapSize ? 1 : 0;
            }
        }

        return new Measures(
            notes,
            pairs,
            same,
            sameClass,
            moveSum / moves,
            leaps / (double)moves,
            onBeatChord / (double)onBeat,
            starts,
            startLeaps,
            joins,
            joinLeaps,
            restedJoinLeaps
        );
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var m = Measure(TestCorpus.Range(SongCount));
        Console.WriteLine($"{m.Notes} melody notes; of {m.Pairs} that come back, the same note {m.Same / (double)m.Pairs:P0}, " +
                          $"in some octave {m.SameClass / (double)m.Pairs:P0}; " +
                          $"mean move {m.MeanMove:F2} semitones, leaps {m.LeapShare:P1}, chord notes on the beat {m.OnBeatChordNotes:P1}; " +
                          $"leaps into a section {m.SectionStartLeaps / (double)m.SectionStarts:P1} of {m.SectionStarts}; " +
                          $"leaps where a phrase starts again {m.JoinLeaps / (double)m.Joins:P1} of {m.Joins}, " +
                          $"after a rest {m.RestedJoinLeaps / (double)m.JoinLeaps:P0}");
        await Task.CompletedTask;
    }

    /// <summary>
    ///     How much of a recurring section's melody plays the notes of its first appearance, in songs that improvise and
    ///     in songs that do not (<see cref="MelodyLayers.Improvisation" />), and how much of its answer the question's,
    ///     its first half and its second.
    /// </summary>
    internal static (double Fixed, double Improvised, double AnswerFirstHalf, double AnswerSecondHalf, double[] OnsetsKept, double[] ImprovisedByHalf, double FixedClass, double AnswerFirstHalfClass) MeasureRecurrence(IEnumerable<CorpusSong> songs)
    {
        // the same note, and the same note in any octave, as a tune an octave off
        int[] recurring = new int[2], recurringSame = new int[2], recurringSameClass = new int[2];
        // in songs that improvise, by the half of the phrase: the first appearance's onsets, those a later one keeps, and of those the same note
        int[] onsets = new int[2], onsetsKept = new int[2], halfSame = new int[2];
        int[] answer = new int[2], answerSame = new int[2], answerSameClass = new int[2];
        foreach (var song in songs)
        {
            var improvises = (double)song.Trace.Single(x => x.Point == TracePoints.MelodyImprovisation).Value! > 0 ? 1 : 0;
            var melody = song.Song.Notes![SongTracks.MelodyTrack].ToDictionary(x => Math.Round(x.Position, 6), x => x.Value.Pitches[0]);
            var firsts = song.Map.Sections.GroupBy(x => x.SectionId).ToDictionary(x => x.Key, x => x.First());
            foreach (var span in song.Map.Sections)
            {
                var first = firsts[span.SectionId];
                foreach (var (position, pitch) in melody.Where(x => x.Key >= first.Start && x.Key < first.End))
                {
                    var offset = position - first.Start;
                    var phraseHalf = offset % Meter.PatternDuration < Meter.PatternDuration / 2 ? 0 : 1;
                    if (span != first && improvises == 1)
                        onsets[phraseHalf]++;
                    if (span != first && melody.TryGetValue(Math.Round(span.Start + offset, 6), out var again))
                    {
                        recurring[improvises]++;
                        recurringSame[improvises] += again == pitch ? 1 : 0;
                        recurringSameClass[improvises] += (again - pitch).Mod(12) == 0 ? 1 : 0;
                        if (improvises == 1)
                        {
                            onsetsKept[phraseHalf]++;
                            halfSame[phraseHalf] += again == pitch ? 1 : 0;
                        }
                    }

                    // the question's bars against the answer's, its first half and its second
                    if (span == first && offset < Meter.PatternDuration && melody.TryGetValue(Math.Round(position + Meter.PatternDuration, 6), out var answered))
                    {
                        var half = offset < Meter.PatternDuration / 2 ? 0 : 1;
                        answer[half]++;
                        answerSame[half] += answered == pitch ? 1 : 0;
                        answerSameClass[half] += (answered - pitch).Mod(12) == 0 ? 1 : 0;
                    }
                }
            }
        }

        return (
            recurringSame[0] / (double)recurring[0],
            recurringSame[1] / (double)recurring[1],
            answerSame[0] / (double)answer[0],
            answerSame[1] / (double)answer[1],
            [..onsetsKept.Zip(onsets, (kept, all) => kept / (double)all)],
            [..halfSame.Zip(onsetsKept, (same, kept) => same / (double)kept)],
            recurringSameClass[0] / (double)recurring[0],
            answerSameClass[0] / (double)answer[0]
        );
    }

    [Test]
    public async Task ASectionThatRecurs_PlaysTheSameNotes_UnlessTheSongImprovises_AndItsAnswer_StartsAsItsQuestion_AndChangesAfter()
    {
        var m = MeasureRecurrence(TestCorpus.Range(20));

        // placed over the song, a recurring section and an answer that go on from the note before replay the tune heard,
        // an octave off where it would leap from it
        await Assert.That(m.FixedClass).IsGreaterThan(0.93);
        await Assert.That(m.Improvised).IsBetween(0.6, 0.95);
        await Assert.That(m.AnswerFirstHalfClass).IsGreaterThan(0.9);
        await Assert.That(m.AnswerSecondHalf).IsBetween(0.5, 0.9);
    }

    [Test]
    [Explicit]
    public async Task RecurrenceReport()
    {
        var m = MeasureRecurrence(TestCorpus.Range(100));
        Console.WriteLine($"a recurring section plays its first appearance's notes {m.Fixed:P0} in songs that do not improvise, {m.Improvised:P0} in songs that do; " +
                          $"its answer the question's {m.AnswerFirstHalf:P0} in its first half, {m.AnswerSecondHalf:P0} in its second; " +
                          $"in any octave, a recurring section {m.FixedClass:P0} and the answer's first half {m.AnswerFirstHalfClass:P0}; " +
                          $"in songs that improvise, a later appearance keeps {m.OnsetsKept[0]:P0} of the onsets of a phrase's first half and {m.OnsetsKept[1]:P0} of its second, " +
                          $"the same note on {m.ImprovisedByHalf[0]:P0} and {m.ImprovisedByHalf[1]:P0} of them");
        await Task.CompletedTask;
    }

    [Test]
    public async Task APhraseThatStartsAgain_LeapsMostlyAfterARest()
    {
        var m = Measure(TestCorpus.Range(20));

        await Assert.That(m.RestedJoinLeaps / (double)m.JoinLeaps).IsGreaterThan(0.7);
    }
}
