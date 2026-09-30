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

    /// <summary>Makes a track's notes end by a position, its last one before it held until there, as the band stops.</summary>
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
        foreach (var track in _cleared.Keys.Union(_hits.Keys))
            maps[track] = timeline =>
            {
                foreach (var (from, to) in _cleared.GetValueOrDefault(track) ?? [])
                    timeline = timeline.RemoveSpan(from, to);
                return EventTimeline.Merge([timeline, EventTimeline.Create(timeline.Duration, _hits.GetValueOrDefault(track) ?? [])]);
            };
        return song.MapTrackEvents(maps);
    }

    /// <summary>
    ///     The notes once they are decided, as the band stops at every cut: a pitched track's last note before it held
    ///     until there, unless it ends sooner as a phrase's last note is held, and every note that would sound past it,
    ///     such as a chord that outlasts the notes after it, ending there. A drum's hit keeps its length.
    /// </summary>
    /// <param name="roles">What every track plays, by its number.</param>
    public ImmutableSortedDictionary<int, EventTimeline<RealizedNote>> CutNotes(
        ImmutableSortedDictionary<int, EventTimeline<RealizedNote>> notes,
        IReadOnlyDictionary<int, TrackRole> roles
    )
    {
        foreach (var (track, cuts) in _cuts)
        {
            if (!notes.TryGetValue(track, out var timeline))
                continue;

            foreach (var cut in cuts)
            {
                // the last note before the cut, which a pitched track holds until there where it still sounds in the bar
                // before it, not a note of a part that rested since
                var last = -1;
                if (roles[track] != TrackRole.Drum)
                    for (var i = 0; i < timeline.Count && timeline[i].Position < cut - Epsilon; i++)
                        last = i;
                if (last >= 0 && timeline[last].Position + timeline[last].Value.Duration < cut - Meter.BarDuration - Epsilon)
                    last = -1;

                timeline = EventTimeline.Create(timeline.Duration, timeline.Select((x, i) => EndBy(x, cut, i == last)));
            }

            notes = notes.SetItem(track, timeline);
        }

        return notes;
    }

    /// <summary>
    ///     A note ending by a cut: held until there if it is the last before it and not held for less already, and cut
    ///     there if it would sound past it.
    /// </summary>
    private static TimelineItem<RealizedNote> EndBy(TimelineItem<RealizedNote> note, double cut, bool isLast)
    {
        var length = cut - note.Position;
        var held = note.Value.State.GetStateValue(StateKinds.HeldDuration);
        var isHeld = isLast && (held <= 0 || held > length);
        var soundsPast = note.Position < cut - Epsilon && note.Position + note.Value.Duration > cut + Epsilon;
        return isHeld || soundsPast ? (note.Value with { Duration = length }).ToTimelineItem(note.Position) : note;
    }
}
