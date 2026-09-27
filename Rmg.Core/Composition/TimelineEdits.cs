using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     Changes to a song's notes, gathered, and made at once: the spans they clear and the hits they add, as the fills
///     and the song's form make them.
/// </summary>
/// <param name="map">Where the song's parts are, from which the trace counts the bars of the sections' patterns; none for a
///     song that starts with its first section.</param>
internal sealed class TimelineEdits(IGenerationContext context, SongMap? map = null)
{
    private const double Epsilon = 1e-6;

    private readonly Dictionary<int, List<(double From, double To)>> _cleared = [];
    private readonly Dictionary<int, List<TimelineItem<StateMap>>> _hits = [];
    private readonly Dictionary<int, List<double>> _cuts = [];

    /// <summary>Takes a track's notes out from one position up to another; the fills' own hits stay.</summary>
    public void Clear(int track, double from, double to)
    {
        if (!_cleared.TryGetValue(track, out var spans))
            _cleared[track] = spans = [];
        spans.Add((from, to));
    }

    /// <summary>Makes a track's last note before a position end there, as the band stops.</summary>
    public void Cut(int track, double position)
    {
        if (!_cuts.TryGetValue(track, out var cuts))
            _cuts[track] = cuts = [];
        cuts.Add(position);
    }

    /// <summary>A hit of a track, in place of any it or a fill has there.</summary>
    /// <param name="articulation">The drum's sound, counted from 1; 0 for the groove's.</param>
    public void Hit(int track, double position, double velocity, int articulation, int sectionId, string fill)
    {
        var builder = new StateMapBuilder("Fill", perTrack: true).Add(StateKinds.Velocity, velocity);
        if (articulation > 0)
            builder.Add(StateKinds.ArticulationIndex, articulation);
        var stateMap = builder.ToStateMap(context);
        // sections are made of whole 4-bar patterns, so the bar of the pattern and the beat in it follow from the song's
        var fromOrigin = position - (map?.Origin ?? 0);
        var bar = (int)Math.Floor(fromOrigin / Meter.BarDuration);
        StateTrace.Record(TracePoints.Fill, track, sectionId, bar.Mod(Meter.PatternBarCount), stateMap, fromOrigin - bar * Meter.BarDuration, fill);

        Clear(track, position, position + Epsilon);
        if (!_hits.TryGetValue(track, out var hits))
            _hits[track] = hits = [];
        hits.RemoveAll(x => x.Position.IsEqualToByEpsilon(position));
        hits.Add(stateMap.ToTimelineItem(position));
    }

    public TrackEventStateTimelineMap<StateMap> ApplyTo(TrackEventStateTimelineMap<StateMap> song)
    {
        var maps = new Dictionary<int, Func<EventTimeline<StateMap>, EventTimeline<StateMap>>>();
        foreach (var track in _cleared.Keys.Union(_hits.Keys).Union(_cuts.Keys))
            maps[track] = timeline =>
            {
                foreach (var cut in _cuts.GetValueOrDefault(track) ?? [])
                    timeline = CutBefore(timeline, cut);
                foreach (var (from, to) in _cleared.GetValueOrDefault(track) ?? [])
                    timeline = timeline.RemoveSpan(from, to);
                return EventTimeline.Merge([timeline, EventTimeline.Create(timeline.Duration, _hits.GetValueOrDefault(track) ?? [])]);
            };
        return song.MapTrackEvents(maps);
    }

    /// <summary>
    ///     The notes once they are decided, with every note that would sound past a cut ending there, such as a chord
    ///     that outlasts the notes after it: before the notes are decided, only the last note's length is known to reach
    ///     the cut, which it is held to.
    /// </summary>
    public ImmutableSortedDictionary<int, EventTimeline<RealizedNote>> CutNotes(ImmutableSortedDictionary<int, EventTimeline<RealizedNote>> notes)
    {
        foreach (var (track, cuts) in _cuts)
        {
            if (!notes.TryGetValue(track, out var timeline))
                continue;

            foreach (var cut in cuts)
                timeline = EventTimeline.Create(
                    timeline.Duration,
                    timeline.Select(x => x.Position < cut - Epsilon && x.Position + x.Value.Duration > cut + Epsilon
                        ? (x.Value with { Duration = cut - x.Position }).ToTimelineItem(x.Position)
                        : x
                    )
                );
            notes = notes.SetItem(track, timeline);
        }

        return notes;
    }

    /// <summary>The timeline with its last note before the position held until there, unless it ends sooner already.</summary>
    private static EventTimeline<StateMap> CutBefore(EventTimeline<StateMap> timeline, double position)
    {
        var index = -1;
        for (var i = 0; i < timeline.Count && timeline[i].Position < position; i++)
            index = i;
        if (index < 0)
            return timeline;

        var note = timeline[index];
        var held = note.Value.GetStateValue(StateKinds.HeldDuration);
        var length = position - note.Position;
        if (held > 0 && held <= length)
            return timeline;

        var cutNote = note.Value
            .MergeWith(StateMap.FromStates([StateKinds.HeldDuration.CreateState(length - held)]))
            .ToTimelineItem(note.Position);
        return EventTimeline.Create(timeline.Duration, timeline.Select((x, i) => i == index ? cutNote : x));
    }
}
