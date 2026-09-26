using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Rendering;

namespace Rmg.Tests.Fills;

public sealed class FillTest
{
    private const double SectionDuration = 2 * BarStateGenerator.PatternDuration;

    private static readonly int[] Crashes = [49, 57];

    private static StateMap Velocity(double velocity) => StateMap.FromStates([StateKinds.Velocity.CreateState(velocity)]);

    private static EventTimeline<StateMap> Hits(params double[] positions) =>
        EventTimeline.Create(8, positions.Select(x => Velocity(0).ToTimelineItem(x)));

    [Test]
    public async Task RemoveSpan_TakesOutTheEventsFromItsStartUpToItsEnd()
    {
        var timeline = Hits(0, 1, 2, 3, 4).RemoveSpan(1, 3);

        await Assert.That(timeline.Select(x => x.Position).ToArray()).IsEquivalentTo([0.0, 3.0, 4.0]);
        await Assert.That(timeline.Duration).IsEqualTo(8);
    }

    [Test]
    public async Task MapTrackEvents_KeepsTheState_AndStartsATrackItDoesNotHave()
    {
        var stateMap = Velocity(0.5);
        var song = TrackEventStateTimelineMap.Create(
            8,
            [new KeyValuePair<int, EventStateTimelineMap<StateMap>>(1, Hits(0, 4).ToEventStateTimelineMap(stateMap))],
            StateTimelineMap.Create(8)
        );

        var mapped = song.MapTrackEvents(
            new Dictionary<int, Func<EventTimeline<StateMap>, EventTimeline<StateMap>>>
            {
                [1] = x => x.RemoveSpan(4, 8),
                [2] = x => EventTimeline.Merge([x, Hits(2)])
            }
        );

        await Assert.That(mapped.TrackTimelineMap[1].EventTimeline.Count).IsEqualTo(1);
        await Assert.That(mapped.TrackTimelineMap[1].GetEffectiveStateMapAt(6).GetStateValue(StateKinds.Velocity)).IsEqualTo(0.5);
        await Assert.That(mapped.TrackTimelineMap[2].EventTimeline.Single().Position).IsEqualTo(2);
    }

    [Test]
    public async Task Edits_ClearSpans_AndAHitTakesThePlaceOfTheTracksNoteThere()
    {
        var song = TrackEventStateTimelineMap.Create(
            8,
            [new KeyValuePair<int, EventStateTimelineMap<StateMap>>(1, Hits(0, 2, 3, 4).ToEventStateTimelineMap(StateMap.Default))],
            StateTimelineMap.Create(8)
        );
        var edits = new FillEdits(new GenerationContext(1));

        edits.Clear(1, 2, 3.5);
        edits.Hit(1, 4, 0.8, 2, 0, "Test");
        var events = edits.ApplyTo(song).TrackTimelineMap[1].EventTimeline;

        await Assert.That(events.Select(x => x.Position).ToArray()).IsEquivalentTo([0.0, 4.0]);
        await Assert.That(events[1].Value.GetStateValue(StateKinds.Velocity)).IsEqualTo(0.8);
        await Assert.That(events[1].Value.GetStateValue(StateKinds.ArticulationIndex)).IsEqualTo(2);
    }

    [Test]
    public async Task IdleDrums_KeepTheSectionsState()
    {
        // the toms play in few sections, but have the drums' state in all of them
        for (var seed = 0; seed < 10; seed++)
        {
            var song = SongGenerator.GenerateSong(seed);
            var toms = song.TrackEventStateTimelineMap.TrackTimelineMap[DrumGroups.GetTrackNumber(DrumDefinitions.Tom)];
            for (var position = 0.0; position < song.Duration; position += SectionDuration)
                await Assert.That(toms.GetEffectiveStateMapAt(position).GetStateValue(StateKinds.Velocity)).IsNotEqualTo(0);
        }
    }

    [Test]
    public async Task SectionChanges_MostlyLandOnACrashAndAKick()
    {
        int changes = 0, crashes = 0, kicks = 0, downbeats = 0, otherCrashes = 0;
        for (var seed = 0; seed < 40; seed++)
        {
            var song = SongGenerator.GenerateSong(seed);
            var drums = Render.RenderSong(song).Tracks.Single(x => x.IsPercussionInstrument).NoteTimeline;
            for (var bar = 1.0; bar * 4 < song.Duration; bar++)
            {
                var hits = drums.Where(x => x.Position.IsEqualToByEpsilon(bar * 4)).Select(x => x.Value.Offset).ToArray();
                if ((bar * 4 % SectionDuration).IsEqualToByEpsilon(0))
                {
                    changes++;
                    if (hits.Any(Crashes.Contains))
                        crashes++;
                    if (hits.Any(x => x is 35 or 36))
                        kicks++;
                }
                else
                {
                    downbeats++;
                    if (hits.Any(Crashes.Contains))
                        otherCrashes++;
                }
            }
        }

        await Assert.That(crashes / (double)changes).IsBetween(0.5, 0.8);
        await Assert.That(kicks / (double)changes).IsGreaterThan(0.9);
        await Assert.That(otherCrashes / (double)downbeats).IsLessThan(0.1);
    }
}
