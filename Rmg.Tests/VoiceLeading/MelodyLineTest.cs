using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Tests.VoiceLeading;

public sealed class MelodyLineTest
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

    /// <summary>A melody line that has played E4, having moved up to it, and so goes on up.</summary>
    private static MelodyLine StartedOnE()
    {
        var line = new MelodyLine(MinNote, MaxNote);
        line.Place(C, CTones, Strong, 0, 0);
        line.Place(C, CTones, Weak, 1, 0);
        return line;
    }

    [Test]
    public async Task FirstNote_IsTheChordsNoteNearestWhereThePhraseAims()
    {
        var line = new MelodyLine(MinNote, MaxNote);

        // the middle of the range is about G4 (67), so aiming 2 below is about F4, nearest E4 or G4
        var note = line.Place(C, CTones, Strong, 0, -2);

        await Assert.That(CTones).Contains(note % 12);
        await Assert.That(Math.Abs(note - 65)).IsLessThanOrEqualTo(2);
    }

    [Test]
    public async Task NextBar_GoesOnTheWayTheMelodyWent()
    {
        var line = new MelodyLine(MinNote, MaxNote);
        var first = line.Place(C, CTones, Strong, 0, 0);
        // turning back from the start, the melody goes down
        var down = line.Place(C, CTones, Weak, -1, 0);

        var next = line.Place(C, CTones, Weak, 1, 0);

        await Assert.That(down).IsLessThan(first);
        await Assert.That(next).IsLessThan(down);
    }

    [Test]
    public async Task StrongBeat_TakesTheNextChordNoteTheWayItGoes()
    {
        var line = new MelodyLine(MinNote, MaxNote);
        var first = line.Place(C, CTones, Strong, 0, 0);

        var on = line.Place(C, CTones, Strong, 1, 0);

        await Assert.That(CTones).Contains(on % 12);
        await Assert.That(on).IsGreaterThan(first);
        await Assert.That(on - first).IsLessThanOrEqualTo(5);
    }

    [Test]
    public async Task Leap_OnAStrongBeat_SkipsAChordNote()
    {
        var line = new MelodyLine(MinNote, MaxNote);
        var first = line.Place(C, CTones, Strong, 0, 0);

        var leap = line.Place(C, CTones, Strong, 2, 0);

        // two chord notes up: over a third and a fifth or more away
        await Assert.That(CTones).Contains(leap % 12);
        await Assert.That(leap - first).IsGreaterThanOrEqualTo(7);
    }

    [Test]
    public async Task WeakBeat_StepsAlongTheScale()
    {
        var line = new MelodyLine(MinNote, MaxNote);
        var first = line.Place(C, CTones, Strong, 0, 0);

        var step = line.Place(C, CTones, Weak, 1, 0);

        await Assert.That(step - first).IsBetween(1, 2);
        await Assert.That(Major.Select(x => x % 12)).Contains(step % 12);
    }

    [Test]
    public async Task StepBack_TurnsTheWayTheMelodyGoes()
    {
        var line = StartedOnE();
        var before = line.Place(C, CTones, Weak, 1, 0);

        var back = line.Place(C, CTones, Weak, -1, 0);

        await Assert.That(back).IsLessThan(before);
    }

    [Test]
    public async Task AfterALeap_TheMelodyStepsBack()
    {
        var line = new MelodyLine(MinNote, MaxNote);
        line.Place(C, CTones, Strong, 0, -5);
        var leap = line.Place(C, CTones, Strong, 2, -5);

        // it meant to go on up, but the leap is filled in by a step down
        var next = line.Place(C, CTones, Weak, 1, -5);

        await Assert.That(next).IsLessThan(leap);
        await Assert.That(leap - next).IsLessThanOrEqualTo(2);
    }

    [Test]
    public async Task FarFromWhereThePhraseAims_TheMelodyTurnsTowardsIt()
    {
        var line = new MelodyLine(MinNote, MaxNote);
        var low = line.Place(C, CTones, Strong, 0, -7);

        // the phrase now aims 7 above the middle, 14 above the melody: it goes up whatever the note meant
        var next = line.Place(C, CTones, Weak, -1, 7);

        await Assert.That(next).IsGreaterThan(low);
    }

    [Test]
    public async Task Melody_StaysInItsRange()
    {
        var line = new MelodyLine(MinNote, MaxNote);
        var notes = Enumerable.Range(0, 200).Select(i => line.Place(C, CTones, i % 2 == 0 ? Strong : Weak, 2, 0)).ToArray();

        await Assert.That(notes.Max() - notes.Min()).IsLessThanOrEqualTo(MelodyLine.RangeWidth);
    }

    [Test]
    public async Task EchoesOverAnotherChord_PlayTheirShapeAsASequence_FromANoteOfTheChord()
    {
        // low in the range, so that the shape has room to rise; it ends on a weak beat, which keeps its note
        var line = new MelodyLine(MinNote, MaxNote);
        const double register = -6;
        // bar 0 over C: the notes are heard, rising along the scale
        var first = new[] { (Strong, 0, 1), (Weak, 1, 2), (Weak, 1, 3), (Weak, 1, 4) }
            .Select(x => line.Place(C, CTones, x.Item1, x.Item2, register, x.Item3)).ToArray();
        // bar 1 over D minor (the second step of C major): the same notes again, meaning to go down this time
        var d = ChordOn(1);
        HashSet<int> dTones = [2, 5, 9];
        var second = new[] { (Strong, 0, 1), (Weak, -1, 2), (Weak, -1, 3), (Weak, -1, 4) }
            .Select(x => line.Place(d, dTones, x.Item1, x.Item2, register, x.Item3)).ToArray();

        static int[] Steps(int[] notes) => [..notes.Zip(notes.Skip(1)).Select(x => Math.Sign(x.Second - x.First))];

        // it plays the rising shape, and starts on a note of its own chord
        await Assert.That(Steps(first)).IsEquivalentTo([1, 1, 1]);
        await Assert.That(Steps(second)).IsEquivalentTo([1, 1, 1]);
        await Assert.That(dTones).Contains(second[0] % 12);
    }

    [Test]
    public async Task EchoesOverTheSameChord_RepeatTheNotes_AsARiff()
    {
        var line = new MelodyLine(MinNote, MaxNote);
        var first = new[] { (Strong, 0, 1), (Weak, 2, 2), (Weak, -1, 3) }
            .Select(x => line.Place(C, CTones, x.Item1, x.Item2, 0, x.Item3)).ToArray();
        var again = new[] { (Strong, 1, 1), (Weak, 1, 2), (Weak, 1, 3) }
            .Select(x => line.Place(C, CTones, x.Item1, x.Item2, 0, x.Item3)).ToArray();

        await Assert.That(again).IsEquivalentTo(first);
    }

    [Test]
    public async Task AnEchoOnAStrongBeatThatMissesTheChord_MovesToTheChordCloseBy()
    {
        var line = new MelodyLine(MinNote, MaxNote);
        foreach (var (rank, step, echo) in new[] { (Strong, 0, 1), (Weak, 1, 2), (Strong, 1, 3) })
            line.Place(C, CTones, rank, step, -6, echo);
        var d = ChordOn(1);
        HashSet<int> dTones = [2, 5, 9];

        var notes = new[] { (Strong, 0, 1), (Weak, 0, 2), (Strong, 0, 3) }
            .Select(x => line.Place(d, dTones, x.Item1, x.Item2, -6, x.Item3))
            .ToArray();

        await Assert.That(dTones).Contains(notes[2] % 12);
    }

    [Test]
    public async Task Steps_RepeatSometimes_GoOnMostly_AndLeapLessWhenStepwise()
    {
        const int drawCount = 50_000;
        var context = new GenerationContext(1);
        var stepwise = Enumerable.Range(0, drawCount).Select(_ => MelodyLayers.GenerateStep(context, 1)).ToArray();
        var leaping = Enumerable.Range(0, drawCount).Select(_ => MelodyLayers.GenerateStep(context, 0)).ToArray();

        await Assert.That(stepwise.Count(x => x == 0) / (double)drawCount).IsEqualTo(MelodyLayers.RepeatChance).Within(0.01);
        var moving = stepwise.Where(x => x != 0).ToArray();
        await Assert.That(moving.Count(x => x > 0) / (double)moving.Length).IsEqualTo(MelodyLayers.ContinueChance).Within(0.01);
        await Assert.That(stepwise.Count(x => Math.Abs(x) == 2)).IsEqualTo(0);
        await Assert.That(leaping.Count(x => Math.Abs(x) == 2) / (double)drawCount)
            .IsEqualTo((1 - MelodyLayers.RepeatChance) * MelodyLayers.MaxLeapChance).Within(0.01);
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
}
