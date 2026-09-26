using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     The drums at the lines between the sections of a song, once they are put one after another: the only stage that
///     knows where a section ends and the next begins. It lands the next section on its downbeat. Its draws come after
///     all the others, so a song is the same outside the lines it marks.
/// </summary>
internal sealed class FillGenerator
{
    private readonly IGenerationContext _context;
    private readonly int? _kickTrack;
    private readonly int? _cymbalTrack;

    public FillGenerator(IGenerationContext context, SongTracks tracks)
    {
        _context = context;
        _kickTrack = GetTrack(tracks, DrumDefinitions.Kick);
        _cymbalTrack = GetTrack(tracks, DrumDefinitions.Cymbal);
    }

    private static int? GetTrack(SongTracks tracks, PercussionInstrumentDefinition drum)
    {
        return tracks.SongDrums.Contains(drum) ? DrumGroups.GetTrackNumber(drum) : null;
    }

    /// <param name="song">The sections put one after another.</param>
    /// <param name="sections">Every section in the song's order, by its id, and how long it is.</param>
    public TrackEventStateTimelineMap<StateMap> Generate(
        TrackEventStateTimelineMap<StateMap> song,
        IReadOnlyList<(int SectionId, double Duration)> sections
    )
    {
        var edits = new FillEdits(_context);
        var start = 0.0;
        for (var i = 0; i < sections.Count; i++)
        {
            if (i > 0)
                Land(song, edits, start, sections[i].SectionId, Pick(FillLayers.SectionLandings));
            start += sections[i].Duration;
        }

        return edits.ApplyTo(song);
    }

    private T Pick<T>(ImmutableArray<Weighted<T>> weights)
    {
        return weights[Generators.WeightedIndex(weights)(_context)].Value;
    }

    /// <summary>The hits the drums land on at a line: a crash, and a kick if the groove has none there.</summary>
    private void Land(TrackEventStateTimelineMap<StateMap> song, FillEdits edits, double position, int sectionId, FillLanding landing)
    {
        if (landing == FillLanding.None)
            return;

        if (landing == FillLanding.CrashAndKick && _cymbalTrack is { } cymbal)
            edits.Hit(cymbal, position, FillLayers.LandingVelocity, Pick(FillLayers.Crashes), sectionId, "Landing");

        // the kick plays its own sound, as the groove's walk has it there
        if (_kickTrack is { } kick && !HasHitAt(song, kick, position))
            edits.Hit(kick, position, FillLayers.LandingVelocity, 0, sectionId, "Landing");
    }

    private static bool HasHitAt(TrackEventStateTimelineMap<StateMap> song, int track, double position)
    {
        return song.TrackTimelineMap.TryGetValue(track, out var timeline)
               && timeline.EventTimeline.Any(x => x.Position.IsEqualToByEpsilon(position));
    }
}

/// <summary>The changes the fills make to the drums, gathered, and made at once: the spans they clear and the hits they add.</summary>
internal sealed class FillEdits(IGenerationContext context)
{
    private readonly Dictionary<int, List<(double From, double To)>> _cleared = [];
    private readonly Dictionary<int, List<TimelineItem<StateMap>>> _hits = [];

    /// <summary>Takes a track's notes out from one position up to another.</summary>
    public void Clear(int track, double from, double to)
    {
        if (!_cleared.TryGetValue(track, out var spans))
            _cleared[track] = spans = [];
        spans.Add((from, to));
    }

    /// <summary>A hit of a track, in place of any it has there.</summary>
    /// <param name="articulation">The drum's sound, counted from 1; 0 for the groove's.</param>
    public void Hit(int track, double position, double velocity, int articulation, int sectionId, string fill)
    {
        var builder = new StateMapBuilder("Fill", perTrack: true).Add(StateKinds.Velocity, velocity);
        if (articulation > 0)
            builder.Add(StateKinds.ArticulationIndex, articulation);
        var stateMap = builder.ToStateMap(context);
        // sections are made of whole 4-bar patterns, so the bar of the pattern and the beat in it follow from the song's
        var bar = (int)Math.Floor(position / BarDuration);
        StateTrace.Record("Fill", track, sectionId, bar % PatternBarCount, stateMap, position - bar * BarDuration, fill);

        Clear(track, position, position + Epsilon);
        if (!_hits.TryGetValue(track, out var hits))
            _hits[track] = hits = [];
        hits.Add(stateMap.ToTimelineItem(position));
    }

    private const double Epsilon = 1e-6;
    private const double BarDuration = 4;
    private const int PatternBarCount = Progressions.BarCount;

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
}
