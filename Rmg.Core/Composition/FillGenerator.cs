using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     The drums at the lines between the sections of a song, once they are put one after another: the only stage that
///     knows where a section ends and the next begins. Before a line a drummer plays a fill, or lets the groove run on,
///     and after it lands the next section on its downbeat; a section change is marked most, and the line in the middle
///     of a section now and then. Its draws come after all the others, so a song is the same outside the lines it marks.
/// </summary>
internal sealed class FillGenerator
{
    // the song's tempo is a multiple of this
    private const double BaseTempo = 120;

    private const double BarDuration = 4;

    private static readonly Func<IGenerationContext, int> SeedGenerator = Generators.Int();

    // the snares in the order a fill prefers them, the snare itself first
    private static readonly ImmutableArray<PercussionInstrumentDefinition> Snares =
    [
        DrumDefinitions.AcousticSnare,
        DrumDefinitions.ElectricSnare,
        DrumDefinitions.Clap,
        DrumDefinitions.CrossStick
    ];

    private readonly IGenerationContext _context;
    private readonly ImmutableArray<int> _drumTracks;
    private readonly int? _kickTrack;
    private readonly int? _snareTrack;
    private readonly int? _tomTrack;
    private readonly int? _hiHatTrack;
    private readonly int? _cymbalTrack;

    public FillGenerator(IGenerationContext context, SongTracks tracks)
    {
        _context = context;
        _drumTracks = [..tracks.SongDrums.Select(DrumGroups.GetTrackNumber)];
        _kickTrack = GetTrack(tracks, DrumDefinitions.Kick);
        _snareTrack = Snares.Select(x => GetTrack(tracks, x)).FirstOrDefault(x => x is not null);
        _tomTrack = GetTrack(tracks, DrumDefinitions.Tom);
        _hiHatTrack = GetTrack(tracks, DrumDefinitions.HiHat);
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
        // a fast song's runs play 8ths, which 16ths would blur
        var tempo = BaseTempo * song.CommonStateTimelineMap.GetEffectiveStateMapAt(0).GetStateValue(StateKinds.Tempo);
        var grid = tempo <= FillLayers.MaxSixteenthTempo ? 0.25 : 0.5;

        var edits = new FillEdits(_context);
        var start = 0.0;
        for (var i = 0; i < sections.Count; i++)
        {
            var (sectionId, duration) = sections[i];
            if (i > 0)
            {
                // the fill before the line belongs to the section it ends
                var fill = PickFill(FillLayers.SectionFills, song, start);
                Fill(edits, fill, start, grid, sections[i - 1].SectionId);
                var landing = fill is FillKind.Break or FillKind.StopTime ? FillLanding.CrashAndKick : Pick(FillLayers.SectionLandings);
                Land(song, edits, start, sectionId, landing);
            }

            // the line in the middle of the section, between its 4-bar pattern and the pattern's repeat
            for (var line = start + BarStateGenerator.PatternDuration; line < start + duration; line += BarStateGenerator.PatternDuration)
            {
                var fill = PickFill(FillLayers.PhraseFills, song, line);
                Fill(edits, fill, line, grid, sectionId);
                var landing = fill is FillKind.Break or FillKind.StopTime
                              || fill != FillKind.None && _context.TestProbability(FillLayers.PhraseLandingChance)
                    ? FillLanding.CrashAndKick
                    : FillLanding.None;
                Land(song, edits, line, sectionId, landing);
            }

            start += duration;
        }

        return edits.ApplyTo(song);
    }

    /// <summary>The song with one fill before the given line, and no landing.</summary>
    internal TrackEventStateTimelineMap<StateMap> ApplyFill(TrackEventStateTimelineMap<StateMap> song, FillKind fill, double line, double grid)
    {
        var edits = new FillEdits(_context);
        Fill(edits, fill, line, grid, 0);
        return edits.ApplyTo(song);
    }

    private T Pick<T>(ImmutableArray<Weighted<T>> weights)
    {
        return weights[Generators.WeightedIndex(weights)(_context)].Value;
    }

    /// <summary>The fill before a line; a bar in a tuplet feel takes only a fill that does not play straight notes.</summary>
    private FillKind PickFill(ImmutableArray<Weighted<FillKind>> weights, TrackEventStateTimelineMap<StateMap> song, double line)
    {
        if (IsInTupletFeel(song, line - BarDuration, line))
            weights = [..weights.Where(x => FillLayers.TupletFills.Contains(x.Value))];
        return Pick(weights);
    }

    private bool IsInTupletFeel(TrackEventStateTimelineMap<StateMap> song, double from, double to)
    {
        var positions = _drumTracks
            .Where(song.TrackTimelineMap.ContainsKey)
            .SelectMany(x => song.TrackTimelineMap[x].EventTimeline)
            .Where(x => x.Position >= from && x.Position < to)
            .Select(x => x.Position * 4)
            .ToArray();
        return positions.Length > 0
               && positions.Count(x => !x.IsEqualToByEpsilon(Math.Round(x))) >= FillLayers.TupletFeelShare * positions.Length;
    }

    /// <summary>A fill before the line: the groove in its span kept, thinned to the kick, or cleared, and its voices.</summary>
    private void Fill(FillEdits edits, FillKind fill, double line, double grid, int sectionId)
    {
        if (fill == FillKind.None)
            return;

        var span = Pick(FillLayers.Spans[fill]);
        var from = line - span;
        var name = fill.ToString();
        var run = (FillLayers.RunStartVelocity, FillLayers.RunEndVelocity);
        switch (fill)
        {
            case FillKind.Pickup:
                var tomsPlay = _tomTrack is not null && _context.TestProbability(FillLayers.PickupTomChance);
                // weaker beats are kept less, so a pickup is a few hits, mostly on the beats
                Voice(
                    edits,
                    from,
                    line,
                    grid,
                    rank => Math.Pow(FillLayers.PickupFullness, rank),
                    (k, n) => tomsPlay ? (_tomTrack, TomDown(k, n)) : (_snareTrack, 0),
                    run,
                    sectionId,
                    name
                );
                break;
            case FillKind.TomRun:
                ClearGroove(edits, from, line, keepKick: true);
                Voice(edits, from, line, grid, Run, (k, n) => (_tomTrack, TomDown(k, n)), run, sectionId, name);
                break;
            case FillKind.SnareRoll:
                ClearGroove(edits, from, line, keepKick: true);
                // it speeds up, 8ths then 16ths for its second half, where it is long enough and the tempo allows
                var middle = span >= 1 && grid < 0.5 ? from + span / 2 : from;
                var midVelocity = (FillLayers.RollStartVelocity + FillLayers.RollEndVelocity) / 2;
                Voice(edits, from, middle, 0.5, Run, (_, _) => (_snareTrack, 0), (FillLayers.RollStartVelocity, midVelocity), sectionId, name);
                Voice(
                    edits,
                    middle,
                    line,
                    grid,
                    Run,
                    (_, _) => (_snareTrack, 0),
                    (middle > from ? midVelocity : FillLayers.RollStartVelocity, FillLayers.RollEndVelocity),
                    sectionId,
                    name
                );
                break;
            case FillKind.AroundTheKit:
                ClearGroove(edits, from, line, keepKick: true);
                Voice(
                    edits,
                    from,
                    line,
                    grid,
                    Run,
                    (k, n) =>
                    {
                        var snareCount = (int)Math.Ceiling(n * FillLayers.AroundTheKitSnareShare);
                        return k < snareCount || _tomTrack is null ? (_snareTrack, 0) : (_tomTrack, TomDown(k - snareCount, n - snareCount));
                    },
                    run,
                    sectionId,
                    name
                );
                break;
            case FillKind.Break:
                ClearGroove(edits, from, line, keepKick: false);
                break;
            case FillKind.StopTime:
                ClearGroove(edits, from, line, keepKick: false);
                foreach (var track in new[] { _kickTrack, _snareTrack })
                    if (track is { } hit)
                        edits.Hit(hit, from, FillLayers.LandingVelocity, 0, sectionId, name);
                if (_cymbalTrack is { } cymbal)
                    edits.Hit(cymbal, from, FillLayers.LandingVelocity, Pick(FillLayers.Crashes), sectionId, name);
                break;
            case FillKind.Lift:
                if (_hiHatTrack is { } hiHat)
                    edits.Hit(hiHat, from, FillLayers.RunEndVelocity, FillLayers.OpenHiHat, sectionId, name);
                else if (_cymbalTrack is { } liftCymbal)
                    edits.Hit(liftCymbal, from, FillLayers.RunEndVelocity, Pick(FillLayers.Crashes), sectionId, name);
                break;
        }
    }

    // a run keeps its first note, and nearly all the others
    private static double Run(int rank) => rank == 0 ? 1 : FillLayers.RunFullness;

    /// <summary>The tom of a run's note, from the high tom down to the floor tom over the run.</summary>
    internal static int TomDown(int index, int count)
    {
        return FillLayers.TomCount - index * FillLayers.TomCount / Math.Max(1, count);
    }

    /// <summary>Takes the drums' notes out of a span, all of them or all but the kick's.</summary>
    private void ClearGroove(FillEdits edits, double from, double to, bool keepKick)
    {
        foreach (var track in _drumTracks.Where(x => !keepKick || x != _kickTrack))
            edits.Clear(track, from, to);
    }

    /// <summary>
    ///     A voice of a fill: a dyadic pattern over its span, on the given grid, whose every note plays the sound it is
    ///     given by its place among the notes, louder or quieter along the span.
    /// </summary>
    private void Voice(
        FillEdits edits,
        double from,
        double to,
        double grid,
        Func<int, double> keepChance,
        Func<int, int, (int? Track, int Articulation)> sound,
        (double From, double To) velocity,
        int sectionId,
        string name
    )
    {
        var span = to - from;
        if (span < grid)
            return;

        var maxRank = (int)Math.Round(Math.Log2(span / grid));
        var hits = DyadicRankThresholdPattern.Create(
                _context,
                SeedGenerator(_context),
                keepChance,
                new DyadicTimelineDescriptor(span, span, 0, maxRank)
            )
            .OutcomeRankTimeline;
        for (var k = 0; k < hits.Count; k++)
        {
            var (track, articulation) = sound(k, hits.Count);
            if (track is not { } hitTrack)
                continue;

            var loudness = velocity.From + (velocity.To - velocity.From) * hits[k].Position / span;
            edits.Hit(hitTrack, from + hits[k].Position, loudness, articulation, sectionId, name);
        }
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
    private const double Epsilon = 1e-6;
    private const double BarDuration = 4;
    private const int PatternBarCount = Progressions.BarCount;

    private readonly Dictionary<int, List<(double From, double To)>> _cleared = [];
    private readonly Dictionary<int, List<TimelineItem<StateMap>>> _hits = [];

    /// <summary>Takes a track's notes out from one position up to another; the fills' own hits stay.</summary>
    public void Clear(int track, double from, double to)
    {
        if (!_cleared.TryGetValue(track, out var spans))
            _cleared[track] = spans = [];
        spans.Add((from, to));
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
        var bar = (int)Math.Floor(position / BarDuration);
        StateTrace.Record("Fill", track, sectionId, bar % PatternBarCount, stateMap, position - bar * BarDuration, fill);

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
}
