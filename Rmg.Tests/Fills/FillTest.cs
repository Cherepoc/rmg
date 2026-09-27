using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Rendering;

namespace Rmg.Tests.Fills;

public sealed class FillTest
{
    private const double SectionDuration = 2 * Meter.PatternDuration;

    private static readonly int[] Crashes = [..DrumDefinitions.Cymbal.ArticulationCodes];

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
        var edits = new TimelineEdits(new GenerationContext(1));

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
            var song = TestCorpus.Get(seed).Song;
            var toms = song.TrackEventStateTimelineMap.TrackTimelineMap[DrumGroups.GetTrackNumber(DrumDefinitions.Tom)];
            for (var position = 0.0; position < song.Duration; position += SectionDuration)
                await Assert.That(toms.GetEffectiveStateMapAt(position).GetStateValue(StateKinds.Velocity)).IsNotEqualTo(0);
        }
    }

    [Test]
    public async Task EveryLine_RecordsItsFillDecision()
    {
        for (var seed = 0; seed < 10; seed++)
        {
            var song = TestCorpus.Get(seed);
            var decisions = song.Trace.Where(x => x.Point == "Fill decision").ToArray();

            // a line between every two sections, one in the middle of every section, one after the intro's bars, if it
            // has any, and one before the ending's
            var map = song.Map;
            var sections = map.Sections.Length;
            var expected = sections - 1 + sections + (map.Intro.Duration > 0 ? 1 : 0) + (map.Ending.Kind == EndingKind.Open ? 0 : 1);
            await Assert.That(decisions.Length).IsEqualTo(expected).Because($"seed {seed}");
            await Assert.That(decisions.All(x => x.Track == FillGenerator.DrumsTrace && x.Bar == 3)).IsTrue();
        }
    }

    [Test]
    public async Task EarlyLandings_ComeBeforeTheLine()
    {
        var early = 0;
        for (var seed = 0; seed < 200 && early < 5; seed++)
        {
            var landings = new List<StateTraceEntry>();
            foreach (var entry in TestCorpus.Get(seed).Trace)
            {
                if (entry.Point == "Fill" && entry.Phrase == "Landing")
                    landings.Add(entry);
                if (entry.Point != "Fill decision")
                    continue;

                if (entry.Phrase!.Contains(" early") && landings.Count > 0)
                {
                    early++;
                    // in the last bar before the line, where the fill is
                    await Assert.That(landings.All(x => x.Bar == entry.Bar && x.Position > 3)).IsTrue();
                }

                landings.Clear();
            }
        }

        await Assert.That(early).IsGreaterThan(0);
    }

    [Test]
    public async Task SectionChanges_MostlyLandOnACrashAndAKick()
    {
        int changes = 0, crashes = 0, kicks = 0, downbeats = 0, otherCrashes = 0;
        for (var seed = 0; seed < 40; seed++)
        {
            var (song, origin) = TestCorpus.Get(seed);
            var drums = Render.RenderSong(song).Tracks.Single(x => x.IsPercussionInstrument).NoteTimeline;
            // the bars of the sections, after the first, up to the ending's, which lands every time
            for (var bar = 1.0; origin + bar * 4 < song.Duration - 8; bar++)
            {
                var position = origin + bar * 4;
                var hits = drums.Where(x => x.Position.IsEqualToByEpsilon(position)).Select(x => x.Value.Offset).ToArray();
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
        // a section of more energy grooves on the crash more often, on its downbeats
        await Assert.That(otherCrashes / (double)downbeats).IsLessThan(0.15);
    }
}
