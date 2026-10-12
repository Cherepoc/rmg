using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;

namespace Rmg.Tests.MelodyRhythms;

/// <summary>
///     How the melody plays again what comes back: a section's second pattern against its first, which has the
///     same state, and a section that recurs against where it first played; and how the melody moves, within its
///     phrases and where a phrase starts again, which it does mostly after a rest.
/// </summary>
public sealed class MelodyRepetitionTest
{
    /// <summary>The melody's pitches by their places, less the key there, so that a section moved up a key comes back as it was.</summary>
    private static Dictionary<double, int> InTheKey(CorpusSong song)
    {
        var keys = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetStateTimeline(StateKinds.KeyOffset);
        return song.Song.Notes![song.Song.Track(TrackRole.Melody)].ToDictionary(x => Math.Round(x.Position, 6), x => x.Value.Pitches[0] - keys.GetEffectiveValueAt(x.Position));
    }

    private const int SongCount = 128;

    /// <param name="Pairs">Notes at the same place in a pattern that comes back, both played.</param>
    /// <param name="Same">Of those, the ones that play the same note.</param>
    /// <param name="SameClass">Of those, the ones that play the same note in some octave.</param>
    /// <param name="SectionStartLeaps">Moves into a section's first note that are leaps.</param>
    /// <param name="Joins">Moves into the first note of a pattern, where a phrase starts again.</param>
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
            var melody = song.Song.Notes![song.Song.Track(TrackRole.Melody)].ToArray();
            var chords = song.Song.Notes[song.Song.Track(TrackRole.Chords)].GroupBy(x => x.Position)
                .Select(x => (Position: x.Key, Classes: x.SelectMany(n => n.Value.Pitches).Select(p => p.Mod(12)).ToHashSet()))
                .ToArray();
            notes += melody.Length;
            // every pattern's start in its section, the third and fourth of a long section's too
            var patternStarts = song.Map.Sections.SelectMany(x => x.Patterns.Select(p => p.Start)).ToArray();
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

            // on a pulse of its bar
            foreach (var note in melody.Where(x => song.Map.PlaceOf(x.Position) is var at && at.Meter.Pulses.Any(p => Math.Abs(at.Beat - p) < 1e-9)))
            {
                var chord = chords.LastOrDefault(x => x.Position <= note.Position);
                if (chord.Classes is null)
                    continue;
                onBeat++;
                onBeatChord += chord.Classes.Contains(note.Value.Pitches[0].Mod(12)) ? 1 : 0;
            }

            // what comes back: every section's second pattern against its first, of a section two patterns long or longer, and
            // a section against its first place, in the key, as a section moved up a key comes back
            var pitches = InTheKey(song);
            var spans = song.Map.Sections;
            var comparisons = spans.Where(x => x.Duration >= 2 * x.Meter.PatternDuration - 1e-9).Select(x => (From: x.Start, To: x.Start + x.Meter.PatternDuration, Length: x.Meter.PatternDuration))
                .Concat(spans.Select((x, i) => (Span: x, First: spans.First(s => s.SectionId == x.SectionId)))
                    .Where(x => x.First != x.Span)
                    .Select(x => (From: x.First.Start, To: x.Span.Start, Length: x.Span.Duration)));
            foreach (var (from, to, length) in comparisons)
            foreach (var note in pitches.Where(x => x.Key >= from && x.Key < from + length))
            {
                if (!pitches.TryGetValue(Math.Round(note.Key - from + to, 6), out var again))
                    continue;
                pairs++;
                same += again == note.Value ? 1 : 0;
                sameClass += (again - note.Value).Mod(12) == 0 ? 1 : 0;
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
    ///     A melody over three phrases states, restates and answers: its second phrase keeps more of the first's onsets
    ///     than its third, and of those it keeps, the first's contour more often.
    /// </summary>
    [Test]
    public async Task ATwelveBarMelody_StatesRestatesAndAnswers_ItsThirdPhraseTheOneThatChanges()
    {
        // of the first phrase's onsets in a section's first play, the share the second phrase keeps and the third, and of
        // those kept, the share whose step to the next note is the first phrase's, its contour
        int onsets = 0, keptSecond = 0, keptThird = 0, contourSecond = 0, contourThird = 0, sameSecond = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: TestCorpus.PhrasesOnly(3)))))
        {
            var notes = song.Notes(song.Track(TrackRole.Melody)).OrderBy(x => x.Position).ToArray();
            var at = notes.Select((x, i) => (Position: Math.Round(x.Position, 6), i)).GroupBy(x => x.Position).ToDictionary(x => x.Key, x => x.First().i);
            int? Step(int i) => i + 1 < notes.Length ? notes[i + 1].Value.Offset - notes[i].Value.Offset : null;
            foreach (var span in song.Map.Sections.Where(x => x.Meter.Phrases == 3).DistinctBy(x => x.SectionId))
            {
                var phrase = span.Meter.PhraseDuration;
                foreach (var (onset, i) in at.Where(x => x.Key >= span.Start && x.Key < span.Start + phrase))
                {
                    onsets++;
                    if (at.TryGetValue(Math.Round(onset + phrase, 6), out var second))
                    {
                        keptSecond++;
                        contourSecond += Step(second) == Step(i) ? 1 : 0;
                        sameSecond += notes[second].Value.Offset == notes[i].Value.Offset ? 1 : 0;
                    }
                    if (at.TryGetValue(Math.Round(onset + 2 * phrase, 6), out var third))
                    {
                        keptThird++;
                        contourThird += Step(third) == Step(i) ? 1 : 0;
                    }
                }
            }
        }

        Console.WriteLine($"{onsets} onsets: the second phrase keeps {keptSecond / (double)onsets:P0}, its contour {contourSecond / (double)keptSecond:P0}, its very notes {sameSecond / (double)keptSecond:P0}; "
                          + $"the third {keptThird / (double)onsets:P0}, its contour {contourThird / (double)keptThird:P0}");
        await Assert.That(onsets).IsGreaterThan(100);
        await Assert.That(keptSecond).IsGreaterThan(keptThird);
        await Assert.That(contourSecond / (double)keptSecond).IsGreaterThan(contourThird / (double)keptThird);
    }

    /// <summary>
    ///     A 12-bar's middle phrase, which restates the first's notes, the bass landing on the chord's root where the chords
    ///     change about as often as elsewhere, a landing kept rather than restated (<see cref="LinePattern" />).
    /// </summary>
    [Test]
    public async Task ARestatedPhrase_KeepsTheBassLandingOnItsChordsRoots()
    {
        int changes = 0, roots = 0, middleChanges = 0, middleRoots = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: TestCorpus.PhrasesOnly(3)))))
        {
            if (song.Song.Notes!.GetValueOrDefault(song.Song.Track(TrackRole.Bass)) is not { } bass)
                continue;
            var common = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap;
            var at = bass.GroupBy(x => Math.Round(x.Position, 6)).ToDictionary(x => x.Key, x => x.First().Value);
            foreach (var change in song.ChordChanges)
            {
                if (!at.TryGetValue(Math.Round(change, 6), out var note) || song.Map.SectionAt(change) is not { } span)
                    continue;
                var isRoot = note.Pitches.Min().Mod(12) == Realizer.GetChord(common.GetEffectiveStateMapAt(change)).Chord.GetPitch(0).Mod(12);
                var phrase = span.Meter.PhraseDuration;
                var inPattern = (change - span.Start) % span.Meter.PatternDuration;
                var isMiddle = span.Meter.Phrases == 3 && inPattern >= phrase - 1e-9 && inPattern < 2 * phrase - 1e-9;
                (changes, roots) = isMiddle ? (changes, roots) : (changes + 1, roots + (isRoot ? 1 : 0));
                (middleChanges, middleRoots) = isMiddle ? (middleChanges + 1, middleRoots + (isRoot ? 1 : 0)) : (middleChanges, middleRoots);
            }
        }

        Console.WriteLine($"the bass on the root where the chords change: {roots / (double)changes:P0} of {changes}, in a middle phrase {middleRoots / (double)middleChanges:P0} of {middleChanges}");
        await Assert.That(middleChanges).IsGreaterThan(500);
        await Assert.That(middleRoots / (double)middleChanges).IsGreaterThan(roots / (double)changes - 0.05);
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
            // every note in the song's key where it plays, which a key change for a last section moves
            var melody = InTheKey(song);
            var firsts = song.Map.Sections.GroupBy(x => x.SectionId).ToDictionary(x => x.Key, x => x.First());
            foreach (var span in song.Map.Sections)
            {
                var first = firsts[span.SectionId];
                foreach (var (position, pitch) in melody.Where(x => x.Key >= first.Start && x.Key < first.End))
                {
                    var offset = position - first.Start;
                    var phraseHalf = offset % first.Meter.PatternDuration < first.Meter.PatternDuration / 2 ? 0 : 1;
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

                    // the question's bars against the answer's, its first half and its second, where the section plays its
                    // pattern again
                    if (span == first && offset < first.Meter.PatternDuration && position + first.Meter.PatternDuration < first.End
                        && melody.TryGetValue(Math.Round(position + first.Meter.PatternDuration, 6), out var answered))
                    {
                        var half = offset < first.Meter.PatternDuration / 2 ? 0 : 1;
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
        var m = MeasureRecurrence(TestCorpus.Range(256));

        // placed over the song, a recurring section and an answer that go on from the note before replay the tune heard,
        // an octave off where it would leap from it
        // calibrations bounded widely, against the mechanism breaking, their targets in RecurrenceReport
        Console.WriteLine($"recurring notes of the same class, not improvised: {m.FixedClass:P1}; an answer's first half: {m.AnswerFirstHalfClass:P1}");
        await Assert.That(m.FixedClass).IsGreaterThan(0.6);
        // the wildest melodies improvise over their whole range, so that songs that improvise keep less of it
        await Assert.That(m.Improvised).IsBetween(0.5, 0.95);
        await Assert.That(m.AnswerFirstHalfClass).IsGreaterThan(0.6);
        await Assert.That(m.AnswerSecondHalf).IsBetween(0.5, 0.9);
        // the two sides the name contrasts: a song that improvises keeps less, and an answer changes after its first half
        Console.WriteLine($"not improvised {m.Fixed:P1}, improvised {m.Improvised:P1}; an answer's halves {m.AnswerFirstHalf:P1} and {m.AnswerSecondHalf:P1}");
        // seen over three corpora: 80% to 82% against 57% to 60%, an answer's halves 79% to 82% against 57% to 62%
        await Assert.That(m.Fixed).IsGreaterThan(m.Improvised);
        await Assert.That(m.AnswerFirstHalf).IsGreaterThan(m.AnswerSecondHalf);
    }

    [Test]
    [Explicit]
    public async Task RecurrenceReport()
    {
        var m = MeasureRecurrence(TestCorpus.Range(128));
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
        var m = Measure(TestCorpus.Range(256));

        await Assert.That(m.RestedJoinLeaps / (double)m.JoinLeaps).IsGreaterThan(0.7);
    }

    /// <summary>
    ///     In songs that improvise, how many of a recurring section's notes play its first appearance's, by where the
    ///     appearance falls in the song: in its first half, in its second, and the song's last section.
    /// </summary>
    [Test]
    [Explicit]
    public async Task ImprovisationByPlaceReport()
    {
        int[] notes = new int[3], same = new int[3];
        foreach (var song in TestCorpus.Range(128))
        {
            if ((double)song.Trace.Single(x => x.Point == TracePoints.MelodyImprovisation).Value! <= 0)
                continue;

            var melody = InTheKey(song);
            var sections = song.Map.Sections;
            var firsts = sections.GroupBy(x => x.SectionId).ToDictionary(x => x.Key, x => x.First());
            for (var i = 0; i < sections.Length; i++)
            {
                var (span, first) = (sections[i], firsts[sections[i].SectionId]);
                if (span == first)
                    continue;

                var place = i == sections.Length - 1 ? 2 : span.Start < song.Map.Duration / 2 ? 0 : 1;
                foreach (var (position, pitch) in melody.Where(x => x.Key >= first.Start && x.Key < first.End))
                    if (melody.TryGetValue(Math.Round(span.Start + position - first.Start, 6), out var again))
                    {
                        notes[place]++;
                        same[place] += again == pitch ? 1 : 0;
                    }
            }
        }

        Console.WriteLine($"in songs that improvise, a recurring section plays its first appearance's notes {same[0] / (double)notes[0]:P0} in the song's first half, " +
                          $"{same[1] / (double)notes[1]:P0} in its second, {same[2] / (double)notes[2]:P0} as its last section");
        await Task.CompletedTask;
    }
}
