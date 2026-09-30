using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Tests.MelodyRhythms;

public sealed class MelodyRhythmTest
{
    private const int MelodyTrackNumber = 5;

    private static EventTimeline<StateMap> CreateBar(params double[] positions)
    {
        return EventTimeline.Create(
            4,
            positions.Select((x, i) => StateMap.FromStates([StateKinds.Velocity.CreateState(i + 1.0)]).ToTimelineItem(x))
        );
    }

    [Test]
    public async Task EndPhrase_LeavesOutTheNotesFromTheEnd_AndHoldsTheLastUntilTheRest()
    {
        var ended = LinePattern.EndPhrase(CreateBar(0, 1, 1.5, 2, 3), 2);

        await Assert.That(ended.Select(x => x.Position).ToArray()).IsEquivalentTo([0.0, 1.0, 1.5]);
        await Assert.That(ended[2].Value.GetStateValue(StateKinds.HeldDuration)).IsEqualTo(4 - MelodyLayers.PhraseEndRest - 1.5);
        // only the last note is held
        await Assert.That(ended[1].Value.GetStateValue(StateKinds.HeldDuration)).IsEqualTo(0);
        await Assert.That(ended[2].Value.GetStateValue(StateKinds.Velocity)).IsEqualTo(3);
    }

    [Test]
    public async Task EndPhrase_WithNoNoteBeforeTheEnd_HoldsTheFirst()
    {
        var ended = LinePattern.EndPhrase(CreateBar(1.5, 2.5), 1);

        await Assert.That(ended.Select(x => x.Position).ToArray()).IsEquivalentTo([1.5]);
        await Assert.That(ended[0].Value.GetStateValue(StateKinds.HeldDuration)).IsEqualTo(4 - MelodyLayers.PhraseEndRest - 1.5);
    }

    [Test]
    public async Task EndPhrase_LeavesTheBar_WithNoEndOrNoNoteBeforeTheRest()
    {
        var bar = CreateBar(0, 2);
        var late = CreateBar(3, 3.5);

        await Assert.That(LinePattern.EndPhrase(bar, 0)).IsSameReferenceAs(bar);
        await Assert.That(LinePattern.EndPhrase(late, 1)).IsSameReferenceAs(late);
    }

    [Test]
    [Arguments(0.0)]
    [Arguments(0.5)]
    [Arguments(1.0)]
    public async Task Busyness_MovesTheFullness_AndSpeedsUpMoreOften_TheBusierItIs(double value)
    {
        var busyness = new MelodyBusyness(value);
        var context = new GenerationContext(1);
        var stateMaps = Enumerable.Range(0, 4_000)
            .Select(_ => busyness.AddTo(new StateMapBuilder("Test"), Tilt.None).ToStateMap(context))
            .ToArray();

        var fullness = stateMaps[0].GetStateValue(CompositionStateKinds.Rhythm.Fullness);
        var speedUpShare = stateMaps.Count(x => x.GetStateValue(CompositionStateKinds.Rhythm.Period.Power) == -1) / (double)stateMaps.Length;

        await Assert.That(fullness).IsEqualTo(MelodyBusyness.Fullness + (value - 0.5) * 2 * MelodyBusyness.FullnessRange).Within(1e-9);
        await Assert.That(speedUpShare)
            .IsEqualTo(MelodyBusyness.MinSpeedUpChance + value * (1 - MelodyBusyness.MinSpeedUpChance))
            .Within(0.03);
    }

    [Test]
    public async Task Songs_SpreadOverTheRange_AroundTheMiddle()
    {
        var context = new GenerationContext(1);
        var values = Enumerable.Range(0, 20_000).Select(_ => MelodyBusyness.Generate(context).Value).Order().ToArray();

        await Assert.That(values[values.Length / 2]).IsEqualTo(0.5).Within(0.03);
        await Assert.That(values.All(x => x is >= 0 and <= 1)).IsTrue();
    }

    private static TimelineItem<RenderedNote>[] RenderMelody(int seed) => RenderMelodyFrom(seed).Melody;

    /// <summary>The melody, and where the song's first section starts.</summary>
    private static (TimelineItem<RenderedNote>[] Melody, double Origin) RenderMelodyFrom(int seed)
    {
        var (song, origin) = TestCorpus.Get(seed);
        var program = ((PitchInstrumentTrack)song.TrackDefinitions[MelodyTrackNumber]).InstrumentCode;
        return (Render.RenderSong(song)
            .Tracks.First(x => !x.IsPercussionInstrument && x.PitchInstrumentCode == program)
            .NoteTimeline.ToArray(), origin);
    }

    [Test]
    public async Task Melody_PlaysOneNoteAtATime()
    {
        for (var seed = 0; seed < 32; seed++)
        {
            var melody = RenderMelody(seed);
            for (var i = 1; i < melody.Length; i++)
                await Assert.That(melody[i - 1].Position + melody[i - 1].Value.Duration).IsLessThanOrEqualTo(melody[i].Position + 1e-9);
        }
    }

    [Test]
    public async Task Phrases_MostlyEndWithAHeldNote_AndARest()
    {
        int phrases = 0, held = 0, rested = 0;
        for (var seed = 0; seed < 32; seed++)
        {
            var (melody, origin) = RenderMelodyFrom(seed);
            var meter = TestCorpus.Get(seed).Map.Meter;
            var duration = melody[^1].Position + meter.BarDuration;
            // a phrase is a section's 4-bar pattern, so its last bar ends at every pattern's end from the sections' start
            for (var end = origin + meter.PatternDuration; end < duration; end += meter.PatternDuration)
            {
                var last = melody.Where(x => x.Position < end && x.Position >= end - meter.BarDuration).ToArray();
                if (last.Length == 0)
                    continue;

                phrases++;
                var note = last[^1];
                if (note.Value.Duration >= meter.BarDuration / 2)
                    held++;
                if (end - (note.Position + note.Value.Duration) >= MelodyLayers.PhraseEndRest - 1e-9)
                    rested++;
            }
        }

        // a fifth of the phrases run on into the next, and a phrase ending late holds its note a short while
        await Assert.That(held / (double)phrases).IsGreaterThan(0.5);
        await Assert.That(rested / (double)phrases).IsGreaterThan(0.7);
    }

    [Test]
    public async Task Melody_IsBusierThanANoteOrTwoABar()
    {
        var notesPerBar = Enumerable.Range(0, 32)
            .Select(seed =>
                {
                    var melody = RenderMelody(seed);
                    var bar = TestCorpus.Get(seed).Map.Meter.BarDuration;
                    return melody.Length / ((melody[^1].Position + bar) / bar);
                }
            )
            .ToArray();

        await Assert.That(notesPerBar.Average()).IsGreaterThan(3);
        // and songs differ: some sparse, some busy
        await Assert.That(notesPerBar.Min()).IsLessThan(2.5);
        await Assert.That(notesPerBar.Max()).IsGreaterThan(5);
    }

    [Test]
    public async Task APhrasesContour_RisesToItsPeak_AndFallsFromIt()
    {
        var context = new Rmg.Core.Probabilities.GenerationContext(1);
        var peaks = new List<int>();
        var waves = 0;
        for (var i = 0; i < 2_000; i++)
        {
            var contour = MelodyLayers.GenerateContour(context, context, 0.5);
            await Assert.That(contour.Max()).IsEqualTo(MelodyLayers.PeakRegister);

            // a wave: the shape of half the phrase, twice, its peak and its low bar taking turns
            if (contour[0].IsEqualToByEpsilon(contour[2]) && contour[1].IsEqualToByEpsilon(contour[3]))
            {
                waves++;
                await Assert.That(contour[0]).IsNotEqualTo(contour[1]);
                continue;
            }

            var peak = contour.IndexOf(contour.Max());
            peaks.Add(peak);
            for (var bar = 1; bar < contour.Length; bar++)
                await Assert.That(bar <= peak ? contour[bar] > contour[bar - 1] : contour[bar] < contour[bar - 1]).IsTrue();
        }

        // an arch most often, peaking in the third bar, and now and then a wave
        await Assert.That(peaks.GroupBy(x => x).MaxBy(x => x.Count())!.Key).IsEqualTo(2);
        await Assert.That(waves / 2_000.0).IsBetween(0.15, 0.25);
    }

    [Test]
    public async Task APlainSection_WavesLessOften_AndAWildOneMore()
    {
        var context = new Rmg.Core.Probabilities.GenerationContext(1);

        double WaveShare(double unconventionality) => Enumerable.Range(0, 2_000)
            .Select(_ => MelodyLayers.GenerateContour(context, context, unconventionality))
            .Count(x => x[0].IsEqualToByEpsilon(x[2]) && x[1].IsEqualToByEpsilon(x[3])) / 2_000.0;

        await Assert.That(WaveShare(0)).IsLessThan(0.1);
        await Assert.That(WaveShare(1)).IsGreaterThan(0.4);
    }
}
