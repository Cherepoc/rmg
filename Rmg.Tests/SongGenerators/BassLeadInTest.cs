using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;

namespace Rmg.Tests.SongGenerators;

public sealed class BassLeadInTest
{
    private const int MaxRank = 2;

    private static readonly Chord Triad = new([0, 2, 4], false);
    private static readonly Chord Seventh = new([0, 2, 4, 6], false);

    private static readonly StateMap TrackState = StateMap.FromStates([CompositionStateKinds.NoteDynamics.CreateState(1.0)]);

    /// <summary>The first bar of a pattern, of the chords and roots given at its bar lines and half bars, asking to lead in or not.</summary>
    private static StateTimelineMap BarStates(ChordApproach approach, (double Position, Chord Chord, double Root)[] chords)
    {
        return StateTimelineMap.Create(
            Meter.PatternDuration,
            [
                StateTimeline.Create(Meter.PatternDuration, StateKinds.ChordApproach, [((int)approach).ToTimelineItem(0.0)]),
                StateTimeline.Create(Meter.PatternDuration, CompositionStateKinds.RoleChord, chords.Select(x => ImmutableArray.Create(x.Chord).ToTimelineItem(x.Position))),
                StateTimeline.Create(Meter.PatternDuration, StateKinds.ChordRootNoteOffset, chords.Select(x => ImmutableArray.Create(x.Root).ToTimelineItem(x.Position)))
            ]
        );
    }

    private static EventTimeline<StateMap> Notes(params double[] positions)
    {
        return EventTimeline.Create(
            Meter.BarDuration,
            positions.Select(x => StateMap.FromStates([StateKinds.Velocity.CreateState(1.0), CompositionStateKinds.BeatRank.CreateState(0)]).ToTimelineItem(x))
        );
    }

    [Test]
    public async Task ABarThatLeadsIntoAChange_PlaysTheNoteSoundingInItsLastBeat_OverTheChordThere_Lightly()
    {
        var barStates = BarStates(ChordApproach.HalfStepBelow, [(0, Triad, 0), (2, Seventh, 0), (4, Triad, 3)]);

        var notes = PatternGenerator.LeadIn(Notes(0, 1), TrackState, barStates, 0, MaxRank);

        await Assert.That(notes.Select(x => x.Position)).IsEquivalentTo([0.0, 1, 3]);
        var pickup = notes[^1].Value;
        await Assert.That(pickup.GetStateValue(StateKinds.ChordNotePitchOffsets).SequenceEqual(Seventh.Heights)).IsTrue();
        await Assert.That(pickup.GetStateValue(CompositionStateKinds.BeatRank)).IsEqualTo(MaxRank);
        await Assert.That(pickup.GetStateValue(StateKinds.Velocity)).IsEqualTo(1 - BeatAccent.GetAccent(0, MaxRank)).Within(1e-9);
    }

    [Test]
    [Arguments(ChordApproach.None, 3.0, 0.0)]
    [Arguments(ChordApproach.ScaleStep, 0.0, 0.0)]
    [Arguments(ChordApproach.ScaleStep, 3.0, 3.5)]
    public async Task ABarThatDoesNotLead_OrLeadsIntoTheSameChord_OrHasANoteInItsLastBeat_IsLeftAsItIs(ChordApproach approach, double nextRoot, double lastNote)
    {
        var barStates = BarStates(approach, [(0, Triad, 0), (4, Triad, nextRoot)]);
        var notes = Notes(lastNote == 0 ? [0.0] : [0.0, lastNote]);

        var led = PatternGenerator.LeadIn(notes, TrackState, barStates, 0, MaxRank);

        await Assert.That(led.Select(x => x.Position)).IsEquivalentTo(notes.Select(x => x.Position));
    }
}
