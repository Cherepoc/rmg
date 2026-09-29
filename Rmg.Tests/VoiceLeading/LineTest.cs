using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Tests.VoiceLeading;

public sealed class LineTest
{
    // C3 to B5; the melody keeps to 17 semitones in the middle, from A3 to D5
    private const int MinNote = 48;
    private const int MaxNote = 83;

    private static readonly ImmutableArray<int> Major = [0, 2, 4, 5, 7, 9, 11];

    private static ChordContext ChordOn(int rootStep) =>
        new(step => 60 + Major[(rootStep + step).Mod(7)] + 12 * (int)Math.Floor((rootStep + step) / 7.0));

    private static readonly ChordContext C = ChordOn(0);
    private static readonly HashSet<int> CTones = [0, 4, 7];

    private const int Strong = 0;
    private const int Weak = 2;

    /// <summary>
    ///     A note placed where it means to go, as the steps were written: 1 a step on, -1 a step back, 2 and -2 the
    ///     same with a leap, 0 staying; a note that goes on draws 0, and one that turns back 1, whatever the aim leans.
    /// </summary>
    private static int Place(Line line, ChordContext chord, IReadOnlyCollection<int> tones, int rank, int step, double register, int echo = 0) =>
        line.Place(chord, tones, rank, Math.Abs(step), step < 0 ? 1 : 0, register, echo);

    /// <summary>A melody line that has played E4, having moved up to it, and so goes on up.</summary>
    private static Line StartedOnE()
    {
        var line = new Line(MelodyLayers.Line, MinNote, MaxNote);
        Place(line, C, CTones, Strong, 0, 0);
        Place(line, C, CTones, Weak, 1, 0);
        return line;
    }

    [Test]
    public async Task FirstNote_IsTheChordsNoteNearestWhereThePhraseAims()
    {
        var line = new Line(MelodyLayers.Line, MinNote, MaxNote);

        // the middle of the range is about G4 (67), so aiming 2 below is about F4, nearest E4 or G4
        var note = Place(line, C, CTones, Strong, 0, -2);

        await Assert.That(CTones).Contains(note % 12);
        await Assert.That(Math.Abs(note - 65)).IsLessThanOrEqualTo(2);
    }

    [Test]
    public async Task NextBar_GoesOnTheWayTheMelodyWent()
    {
        var line = new Line(MelodyLayers.Line, MinNote, MaxNote);
        var first = Place(line, C, CTones, Strong, 0, 0);
        // turning back from the start, the melody goes down
        var down = Place(line, C, CTones, Weak, -1, 0);

        var next = Place(line, C, CTones, Weak, 1, 0);

        await Assert.That(down).IsLessThan(first);
        await Assert.That(next).IsLessThan(down);
    }

    [Test]
    public async Task StrongBeat_TakesTheNextChordNoteTheWayItGoes()
    {
        var line = new Line(MelodyLayers.Line, MinNote, MaxNote);
        var first = Place(line, C, CTones, Strong, 0, 0);

        var on = Place(line, C, CTones, Strong, 1, 0);

        await Assert.That(CTones).Contains(on % 12);
        await Assert.That(on).IsGreaterThan(first);
        await Assert.That(on - first).IsLessThanOrEqualTo(5);
    }

    [Test]
    public async Task Leap_OnAStrongBeat_SkipsAChordNote()
    {
        var line = new Line(MelodyLayers.Line, MinNote, MaxNote);
        var first = Place(line, C, CTones, Strong, 0, 0);

        var leap = Place(line, C, CTones, Strong, 2, 0);

        // two chord notes up: over a third and a fifth or more away
        await Assert.That(CTones).Contains(leap % 12);
        await Assert.That(leap - first).IsGreaterThanOrEqualTo(7);
    }

    [Test]
    public async Task WeakBeat_StepsAlongTheScale()
    {
        var line = new Line(MelodyLayers.Line, MinNote, MaxNote);
        var first = Place(line, C, CTones, Strong, 0, 0);

        var step = Place(line, C, CTones, Weak, 1, 0);

        await Assert.That(step - first).IsBetween(1, 2);
        await Assert.That(Major.Select(x => x % 12)).Contains(step % 12);
    }

    [Test]
    public async Task StepBack_TurnsTheWayTheMelodyGoes()
    {
        var line = StartedOnE();
        var before = Place(line, C, CTones, Weak, 1, 0);

        var back = Place(line, C, CTones, Weak, -1, 0);

        await Assert.That(back).IsLessThan(before);
    }

    [Test]
    public async Task AfterALeap_TheMelodyStepsBack()
    {
        var line = new Line(MelodyLayers.Line, MinNote, MaxNote);
        Place(line, C, CTones, Strong, 0, -5);
        var leap = Place(line, C, CTones, Strong, 2, -5);

        // it meant to go on up, but the leap is filled in by a step down
        var next = Place(line, C, CTones, Weak, 1, -5);

        await Assert.That(next).IsLessThan(leap);
        await Assert.That(leap - next).IsLessThanOrEqualTo(2);
    }

    [Test]
    public async Task FarFromWhereThePhraseAims_TheMelodyTurnsTowardsIt()
    {
        var line = new Line(MelodyLayers.Line, MinNote, MaxNote);
        var low = Place(line, C, CTones, Strong, 0, -7);

        // the phrase now aims 7 above the middle, 14 above the melody: it goes up whatever the note meant
        var next = Place(line, C, CTones, Weak, -1, 7);

        await Assert.That(next).IsGreaterThan(low);
    }

    [Test]
    public async Task Melody_StaysInItsRange()
    {
        var line = new Line(MelodyLayers.Line, MinNote, MaxNote);
        var notes = Enumerable.Range(0, 200).Select(i => Place(line, C, CTones, i % 2 == 0 ? Strong : Weak, 2, 0)).ToArray();

        await Assert.That(notes.Max() - notes.Min()).IsLessThanOrEqualTo(MelodyLayers.Line.RangeWidth);
    }

    [Test]
    public async Task EchoesOverAnotherChord_PlayTheirShapeAsASequence_FromANoteOfTheChord()
    {
        // low in the range, so that the shape has room to rise; it ends on a weak beat, which keeps its note
        var line = new Line(MelodyLayers.Line, MinNote, MaxNote);
        const double register = -6;
        // bar 0 over C: the notes are heard, rising along the scale
        var first = new[] { (Strong, 0, 1), (Weak, 1, 2), (Weak, 1, 3), (Weak, 1, 4) }
            .Select(x => Place(line, C, CTones, x.Item1, x.Item2, register, x.Item3)).ToArray();
        // bar 1 over D minor (the second step of C major): the same notes again, meaning to go down this time
        var d = ChordOn(1);
        HashSet<int> dTones = [2, 5, 9];
        var second = new[] { (Strong, 0, 1), (Weak, -1, 2), (Weak, -1, 3), (Weak, -1, 4) }
            .Select(x => Place(line, d, dTones, x.Item1, x.Item2, register, x.Item3)).ToArray();

        static int[] Steps(int[] notes) => [..notes.Zip(notes.Skip(1)).Select(x => Math.Sign(x.Second - x.First))];

        // it plays the rising shape, and starts on a note of its own chord
        await Assert.That(Steps(first)).IsEquivalentTo([1, 1, 1]);
        await Assert.That(Steps(second)).IsEquivalentTo([1, 1, 1]);
        await Assert.That(dTones).Contains(second[0] % 12);
    }

    [Test]
    public async Task EchoesOverTheSameChord_RepeatTheNotes_AsARiff()
    {
        var line = new Line(MelodyLayers.Line, MinNote, MaxNote);
        var first = new[] { (Strong, 0, 1), (Weak, 2, 2), (Weak, -1, 3) }
            .Select(x => Place(line, C, CTones, x.Item1, x.Item2, 0, x.Item3)).ToArray();
        var again = new[] { (Strong, 1, 1), (Weak, 1, 2), (Weak, 1, 3) }
            .Select(x => Place(line, C, CTones, x.Item1, x.Item2, 0, x.Item3)).ToArray();

        await Assert.That(again).IsEquivalentTo(first);
    }

    [Test]
    public async Task AnEchoOnAStrongBeatThatMissesTheChord_MovesToTheChordCloseBy()
    {
        var line = new Line(MelodyLayers.Line, MinNote, MaxNote);
        foreach (var (rank, step, echo) in new[] { (Strong, 0, 1), (Weak, 1, 2), (Strong, 1, 3) })
            Place(line, C, CTones, rank, step, -6, echo);
        var d = ChordOn(1);
        HashSet<int> dTones = [2, 5, 9];

        var notes = new[] { (Strong, 0, 1), (Weak, 0, 2), (Strong, 0, 3) }
            .Select(x => Place(line, d, dTones, x.Item1, x.Item2, -6, x.Item3))
            .ToArray();

        await Assert.That(dTones).Contains(notes[2] % 12);
    }

    [Test]
    public async Task Steps_RepeatSometimes_GoOnMostly_AndLeapLessWhenStepwise()
    {
        const int drawCount = 50_000;
        var context = new GenerationContext(1);
        var stepwise = Enumerable.Range(0, drawCount).Select(_ => MelodyLayers.Line.GenerateStep(context, 1)).ToArray();
        var leaping = Enumerable.Range(0, drawCount).Select(_ => MelodyLayers.Line.GenerateStep(context, 0)).ToArray();

        await Assert.That(stepwise.Count(x => x.Step == 0) / (double)drawCount).IsEqualTo(MelodyLayers.Line.RepeatChance).Within(0.01);
        var moving = stepwise.Where(x => x.Step != 0).ToArray();
        await Assert.That(moving.Count(x => x.Turn < MelodyLayers.Line.GetContinueChance(0)) / (double)moving.Length)
            .IsEqualTo(MelodyLayers.Line.ContinueChance).Within(0.01);
        await Assert.That(stepwise.Count(x => x.Step == 2)).IsEqualTo(0);
        await Assert.That(leaping.Count(x => x.Step == 2) / (double)drawCount)
            .IsEqualTo((1 - MelodyLayers.Line.RepeatChance) * MelodyLayers.Line.MaxLeapChance).Within(0.01);
    }

    [Test]
    public async Task Songs_MelodiesMoveByLittle_AndTakeChordNotesOnTheBeat()
    {
        var moves = new List<int>();
        int onBeat = 0, onBeatChordNotes = 0;
        for (var seed = 0; seed < 20; seed++)
        {
            var song = TestCorpus.Get(seed).Song;
            var rendered = Render.RenderSong(song);
            RenderedTrack TrackOf(int number) =>
                rendered.Tracks.First(x => !x.IsPercussionInstrument && x.PitchInstrumentCode == ((PitchInstrumentTrack)song.TrackDefinitions[number]).InstrumentCode);
            var melody = TrackOf(SongTracks.MelodyTrack).NoteTimeline.ToArray();
            var chords = TrackOf(SongTracks.ChordsTrack).NoteTimeline.GroupBy(x => x.Position)
                .Select(x => (Position: x.Key, Classes: x.Select(note => note.Value.Offset % 12).ToHashSet()))
                .ToArray();

            moves.AddRange(melody.Zip(melody.Skip(1)).Select(x => Math.Abs(x.Second.Value.Offset - x.First.Value.Offset)));
            foreach (var note in melody.Where(x => Math.Abs(x.Position - Math.Round(x.Position)) < 1e-9))
            {
                var chord = chords.LastOrDefault(x => x.Position <= note.Position);
                if (chord.Classes is null)
                    continue;
                onBeat++;
                if (chord.Classes.Contains(note.Value.Offset % 12))
                    onBeatChordNotes++;
            }
        }

        await Assert.That(moves.Average()).IsLessThan(3.5);
        await Assert.That(moves.Count(x => x > 7) / (double)moves.Count).IsLessThan(0.05);
        await Assert.That(onBeatChordNotes / (double)onBeat).IsGreaterThan(0.8);
    }

    [Test]
    public async Task ALastNote_BendsToAStepFromTheNext_ByAThirdAtMost_OntoItsChordOnAStrongBeat()
    {
        var line = new Line(MelodyLayers.Line, MinNote, MaxNote);

        // a step from the next already: kept
        await Assert.That(line.Approach(ChordApproach.ScaleStep, C, CTones, Weak, 67, C, 69)).IsEqualTo(67);
        // B leads into G through A on a weak beat; on a strong beat only a note of its own chord may, and none is near
        await Assert.That(line.Approach(ChordApproach.ScaleStep, C, CTones, Weak, 71, C, 67)).IsEqualTo(69);
        await Assert.That(line.Approach(ChordApproach.ScaleStep, C, CTones, Strong, 71, C, 67)).IsEqualTo(71);
        // G leads into D through E, a note of its chord, on a strong beat
        await Assert.That(line.Approach(ChordApproach.ScaleStep, C, CTones, Strong, 67, C, 62)).IsEqualTo(64);
        // too far to bend without a leap: kept
        await Assert.That(line.Approach(ChordApproach.ScaleStep, C, CTones, Weak, 72, C, 62)).IsEqualTo(72);
    }

    [Test]
    [Arguments(ChordApproach.HalfStepBelow, 66)]
    [Arguments(ChordApproach.HalfStepAbove, 68)]
    [Arguments(ChordApproach.Fifth, 62)]
    [Arguments(ChordApproach.Anticipation, 67)]
    [Arguments(ChordApproach.None, 72)]
    public async Task AnApproach_LeadsIntoTheNextRoot_NearestWhereTheNextNoteIs(ChordApproach approach, int expected)
    {
        // C5 before G4, over G: the root nearest the next note is G4, and its fifth nearest G4 is D4
        var line = new Line(MelodyLayers.Line, MinNote, MaxNote);

        await Assert.That(line.Approach(approach, C, CTones, Weak, 72, ChordOn(4), 67)).IsEqualTo(expected);
    }

    [Test]
    [Arguments(ChordArrival.Root, 67)]
    [Arguments(ChordArrival.Third, 71)]
    [Arguments(ChordArrival.Fifth, 62)]
    public async Task AFirstNoteThatLands_TakesTheNoteOfTheChordItAsks_NearestTheNoteBefore(ChordArrival landing, int expected)
    {
        // after G4, the first note, nearest where its phrase aims, over G: G4, B4 and D4 are the nearest in the range
        var line = new Line(MelodyLayers.Line, MinNote, MaxNote);
        Place(line, C, CTones, Strong, 0, 4);

        await Assert.That(line.Place(ChordOn(4), [7, 11, 2], Strong, 1, 0, 4, landing: landing)).IsEqualTo(expected);
    }
}
