using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Rendering;

namespace Rmg.Tests.Fills;

public sealed class FillTest
{
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
        var edits = new TimelineEdits(new GenerationContext(1), Meter.FourFour);

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
        for (var seed = 0; seed < 8; seed++)
        {
            var song = TestCorpus.Get(seed).Song;
            // a percussion song has no toms
            if (!song.TrackEventStateTimelineMap.TrackTimelineMap.TryGetValue(DrumGroups.GetTrackNumber(DrumDefinitions.Tom), out var toms))
                continue;

            foreach (var span in song.Map!.Sections)
                await Assert.That(toms.GetEffectiveStateMapAt(span.Start).GetStateValue(StateKinds.Velocity)).IsNotEqualTo(0);
        }
    }

    [Test]
    public async Task EveryLine_RecordsItsFillDecision()
    {
        for (var seed = 0; seed < 8; seed++)
        {
            var song = TestCorpus.Get(seed);
            var decisions = song.Trace.Where(x => x.Point == TracePoints.FillDecision).ToArray();

            var expected = song.FillLines().Length;
            await Assert.That(decisions.Length).IsEqualTo(expected).Because($"seed {seed}");
            await Assert.That(decisions.All(x => x.Track == FillGenerator.DrumsTrace && x.Bar == 3)).IsTrue();
        }
    }

    [Test]
    public async Task EarlyLandings_ComeBeforeTheLine()
    {
        var early = 0;
        for (var seed = 0; seed < 256 && early < 5; seed++)
        {
            var landings = new List<StateTraceEntry>();
            var song = TestCorpus.Get(seed);
            foreach (var entry in song.Trace)
            {
                if (entry.Point == TracePoints.Fill && entry.Phrase == "Landing")
                    landings.Add(entry);
                if (entry.Point != TracePoints.FillDecision)
                    continue;

                if (((FillDecision)entry.Value!).IsEarly && landings.Count > 0)
                {
                    early++;
                    // in the last bar before the line, where the fill is
                    await Assert.That(landings.All(x => x.Bar == entry.Bar && x.Position > song.Map.Sections.First(s => s.SectionId == entry.Section).Meter.BarDuration - 1)).IsTrue();
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
        for (var seed = 0; seed < 128; seed++)
        {
            var corpusSong = TestCorpus.Get(seed);
            if (!corpusSong.PlaysDrums)
                continue;

            var (song, origin) = corpusSong;
            var drums = Render.RenderSong(song).Tracks.Single(x => x.IsPercussionInstrument).NoteTimeline;
            // the sections of percussion only land on the percussion
            var percussionOnly = corpusSong.Trace.Where(x => x.Point == TracePoints.PercussionOnly).ToDictionary(x => x.Section, x => (bool)x.Value!);
            // the bars of the sections in their meters, after the first, up to the ending's, which lands every time, but the
            // ending's two
            var bars = corpusSong.Map.Sections
                .SelectMany(span => Enumerable.Range(0, (int)Math.Round(span.Duration / span.Meter.BarDuration)).Select(bar => span.Start + bar * span.Meter.BarDuration))
                .Skip(1)
                .Where(x => x < song.Duration - 2 * corpusSong.Map.MeterAt(x).BarDuration);
            foreach (var position in bars)
            {
                var hits = drums.Where(x => x.Position.IsEqualToByEpsilon(position)).Select(x => x.Value.Offset).ToArray();
                // nor does a section whose drums rest, which no fill leads into
                if (corpusSong.Map.Sections.Any(x => x.Start.IsEqualToByEpsilon(position) && (percussionOnly[x.SectionId] || !corpusSong.HasDrums(x))))
                    continue;
                if (corpusSong.Map.Sections.Any(x => x.Start.IsEqualToByEpsilon(position)))
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
        // the wildest songs' changes into a meter of their own land on a kick a little less often
        await Assert.That(kicks / (double)changes).IsGreaterThan(0.88);
        // a section of more energy grooves on the crash more often, on its downbeats
        await Assert.That(otherCrashes / (double)downbeats).IsLessThan(0.15);
    }
}
