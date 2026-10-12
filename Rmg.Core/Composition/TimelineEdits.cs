using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     Changes to a song's notes, gathered, and made at once: the spans they clear and the hits they add, as the fills
///     and the song's form make them.
/// </summary>
/// <param name="meter">The meter the song's bars are in, where no map says otherwise.</param>
/// <param name="map">Where the song's parts are and the meter of each, from which the trace counts the bars of the
///     sections' patterns; none for a song that starts with its first section, in the meter given.</param>
internal sealed class TimelineEdits(IGenerationContext context, Meter meter, SongMap? map)
{
    private const double Epsilon = 1e-6;

    /// <summary>How long the bar ending at a position is, a short last bar's before a section's start, the song's meter's where there is no map.</summary>
    private double BarBefore(double position) => map?.BarBefore(position) ?? meter.BarDuration;

    private readonly Dictionary<int, List<(double From, double To)>> _cleared = [];
    private readonly Dictionary<int, List<TimelineItem<StateMap>>> _hits = [];
    private readonly Dictionary<int, List<double>> _cuts = [];
    private readonly Dictionary<int, List<(double From, double To)>> _silences = [];

    /// <summary>Takes a track's notes out from one position up to another; the fills' own hits stay.</summary>
    public void Clear(int track, double from, double to)
    {
        if (!_cleared.TryGetValue(track, out var spans))
            _cleared[track] = spans = [];
        spans.Add((from, to));
    }

    /// <summary>Whether a track strikes at a position once the edits are made: a hit of its own there, or the song's not cleared.</summary>
    public bool Strikes(TrackEventStateTimelineMap<StateMap> song, int track, double position) =>
        _hits.GetValueOrDefault(track)?.Any(x => x.Position.IsEqualToByEpsilon(position)) == true
        || song.TrackTimelineMap.TryGetValue(track, out var timeline) && timeline.EventTimeline.Any(x => x.Position.IsEqualToByEpsilon(position))
                                                                   && !(_cleared.GetValueOrDefault(track)?.Any(x => position >= x.From && position < x.To) ?? false);

    /// <summary>Makes a track's notes end by a position, its last one before it held until there, as the band stops.</summary>
    public void Cut(int track, double position)
    {
        if (!_cuts.TryGetValue(track, out var cuts))
            _cuts[track] = cuts = [];
        cuts.Add(position);
    }

    /// <summary>
    ///     Makes a track rest from one position up to another once its notes are decided, whatever the stages after the
    ///     form put there, as the band does before a drop: cut there, its notes in it taken out.
    /// </summary>
    public void Silence(int track, double from, double to)
    {
        Cut(track, from);
        if (!_silences.TryGetValue(track, out var spans))
            _silences[track] = spans = [];
        spans.Add((from, to));
    }

    /// <summary>Where a track's notes end as the band stops, in no order; none where it never does.</summary>
    public IReadOnlyList<double> CutsOf(int track) => _cuts.GetValueOrDefault(track) ?? [];

    /// <summary>
    ///     Where a track is silent as the band stops, from a cut to where it plays again: a pitched part's silences, and the
    ///     drums' notes cleared from their cut, whose own hits land after.
    /// </summary>
    public IEnumerable<(double From, double To)> StopsOf(int track) =>
        (_silences.GetValueOrDefault(track) ?? []).Concat((_cleared.GetValueOrDefault(track) ?? []).Where(x => CutsOf(track).Any(c => Math.Abs(c - x.From) < Epsilon)));

    /// <summary>A hit of a track, in place of any it or a fill has there.</summary>
    /// <param name="articulation">The drum's sound, counted from 1; 0 for the groove's.</param>
    /// <param name="kind">What plays the hit, which the trace records.</param>
    public void Hit(int track, double position, double velocity, int articulation, int sectionId, HitKind kind)
    {
        var builder = new StateMapBuilder("Fill", perTrack: true).Add(StateKinds.Velocity, velocity);
        if (articulation > 0)
            builder.Add(StateKinds.ArticulationIndex, articulation);
        var stateMap = builder.ToStateMap(context);
        // the bar of the pattern and the beat in it counted from the section's start, or from the song's where there is no map
        var place = map?.PlaceOf(position) ?? new Place((int)Math.Floor(position / meter.BarDuration), 0, 0, meter);
        var (patternBar, beat) = map is null ? (place.Bar.Mod(meter.PatternBarCount), position - place.Bar * meter.BarDuration) : (place.PatternBar, place.Beat);
        StateTrace.Record(TracePoints.Fill, track, sectionId, patternBar, stateMap, beat, $"{kind}", kind);

        Clear(track, position, position + Epsilon);
        if (!_hits.TryGetValue(track, out var hits))
            _hits[track] = hits = [];
        hits.RemoveAll(x => x.Position.IsEqualToByEpsilon(position));
        hits.Add(stateMap.ToTimelineItem(position));
    }

    /// <summary>A note of a track played again in place of itself, as loud as given, its sound and the rest as they are.</summary>
    public void Replay(int track, TimelineItem<StateMap> note, double velocity)
    {
        Clear(track, note.Position, note.Position + Epsilon);
        if (!_hits.TryGetValue(track, out var hits))
            _hits[track] = hits = [];
        hits.RemoveAll(x => x.Position.IsEqualToByEpsilon(note.Position));
        hits.Add(note.Value.With(StateKinds.Velocity, velocity).ToTimelineItem(note.Position));
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

            foreach (var (from, to) in _silences.GetValueOrDefault(track) ?? [])
                timeline = EventTimeline.Create(timeline.Duration, timeline.Where(x => x.Position < from - Epsilon || x.Position >= to - Epsilon));

            foreach (var cut in cuts)
            {
                // the last note before the cut, which a pitched track holds until there where it still sounds in the bar
                // before it, not a note of a part that rested since
                var last = -1;
                if (roles[track] != TrackRole.Drum)
                    for (var i = 0; i < timeline.Count && timeline[i].Position < cut - Epsilon; i++)
                        last = i;
                if (last >= 0 && timeline[last].Position + timeline[last].Value.Duration < cut - BarBefore(cut) - Epsilon)
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

/// <summary>What plays a drum's hit in place of the groove's, as the trace records it (<see cref="TimelineEdits.Hit" />).</summary>
public enum HitKind
{
    /// <summary>A fill's, into a line.</summary>
    Fill,

    /// <summary>The drums landing on the line after a fill.</summary>
    Landing,

    /// <summary>A drum solo's.</summary>
    DrumSolo,

    /// <summary>A build's roll into a drop.</summary>
    Build
}
