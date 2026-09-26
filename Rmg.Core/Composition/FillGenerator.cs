using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     The drums at the lines between the sections of a song, once they are put one after another: the only stage that
///     knows where a section ends and the next begins. Before a line a drummer plays a fill, or lets the groove run on,
///     and after it lands the next section on its downbeat; a section change is marked most, and the line in the middle
///     of a section now and then, each as the song's drummer plays them. A fill is played from its spec
///     (<see cref="FillLayers.Specs" />). Its draws come after all the others, so a song is the same outside the lines it
///     marks.
/// </summary>
internal sealed class FillGenerator
{
    /// <summary>The track of a trace entry that records a decision for all the drums, such as a line's fill.</summary>
    public const int DrumsTrace = -1;

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

    // the drum the song has for every role a fill plays
    private readonly ImmutableDictionary<DrumRole, (int Track, PercussionInstrumentDefinition Drum)> _roles;

    public FillGenerator(IGenerationContext context, SongTracks tracks)
    {
        _context = context;
        _drumTracks = [..tracks.SongDrums.Select(DrumGroups.GetTrackNumber)];

        var roles = new Dictionary<DrumRole, (int, PercussionInstrumentDefinition)>();
        void AddRole(DrumRole role, PercussionInstrumentDefinition? drum)
        {
            if (drum is not null && tracks.SongDrums.Contains(drum))
                roles[role] = (DrumGroups.GetTrackNumber(drum), drum);
        }

        AddRole(DrumRole.Kick, DrumDefinitions.Kick);
        AddRole(DrumRole.Snare, Snares.FirstOrDefault(tracks.SongDrums.Contains));
        AddRole(DrumRole.Toms, DrumDefinitions.Tom);
        AddRole(DrumRole.HiHat, DrumDefinitions.HiHat);
        AddRole(DrumRole.Cymbal, DrumDefinitions.Cymbal);
        _roles = roles.ToImmutableDictionary();
    }

    /// <param name="song">The sections put one after another.</param>
    /// <param name="sections">Every section in the song's order.</param>
    public TrackEventStateTimelineMap<StateMap> Generate(TrackEventStateTimelineMap<StateMap> song, IReadOnlyList<FillSection> sections)
    {
        // a fast song's runs play 8ths, which 16ths would blur
        var tempo = BaseTempo * song.CommonStateTimelineMap.GetEffectiveStateMapAt(0).GetStateValue(StateKinds.Tempo);
        var grid = tempo <= FillLayers.MaxSixteenthTempo ? 0.25 : 0.5;

        var drummer = Drummer.Generate(_context);
        var edits = new FillEdits(_context);
        var start = 0.0;
        for (var i = 0; i < sections.Count; i++)
        {
            var (sectionId, duration, _, drumTuplet) = sections[i];
            if (i > 0)
            {
                // the fill before the line belongs to the section it ends, and plays in its feel
                var previousSectionId = sections[i - 1].SectionId;
                var fill = PickFill(FillLayers.SectionFills, drummer, sections[i - 1].DrumTuplet);
                var span = Fill(edits, fill, start, grid, drummer, previousSectionId);
                var landing = IsForcingLanding(fill) ? FillLanding.CrashAndKick : Pick(FillLayers.SectionLandings);
                Land(song, edits, start, sectionId, landing);
                RecordDecision(previousSectionId, start, fill, span, landing);
            }

            // the line in the middle of the section, between its 4-bar pattern and the pattern's repeat
            for (var line = start + BarStateGenerator.PatternDuration; line < start + duration; line += BarStateGenerator.PatternDuration)
            {
                var fill = PickFill(FillLayers.PhraseFills, drummer, drumTuplet);
                var span = Fill(edits, fill, line, grid, drummer, sectionId);
                var landing = IsForcingLanding(fill)
                              || fill != FillKind.None && _context.TestProbability(FillLayers.PhraseLandingChance)
                    ? FillLanding.CrashAndKick
                    : FillLanding.None;
                Land(song, edits, line, sectionId, landing);
                RecordDecision(sectionId, line, fill, span, landing);
            }

            start += duration;
        }

        return edits.ApplyTo(song);
    }

    /// <summary>The song with one fill before the given line, as the given drummer plays it, and no landing.</summary>
    internal TrackEventStateTimelineMap<StateMap> ApplyFill(
        TrackEventStateTimelineMap<StateMap> song,
        FillKind fill,
        double line,
        double grid,
        Drummer drummer
    )
    {
        var edits = new FillEdits(_context);
        Fill(edits, fill, line, grid, drummer, 0);
        return edits.ApplyTo(song);
    }

    private static bool IsForcingLanding(FillKind fill)
    {
        return fill != FillKind.None && FillLayers.Specs[fill].ForcesLanding;
    }

    /// <summary>What was decided at a line, recorded in the last bar before it, where its fill is.</summary>
    private static void RecordDecision(int sectionId, double line, FillKind fill, double span, FillLanding landing)
    {
        if (!StateTrace.IsRunning)
            return;

        var bar = (int)Math.Floor(line / BarDuration) - 1;
        StateTrace.Record(
            "Fill decision",
            DrumsTrace,
            sectionId,
            bar % Progressions.BarCount,
            StateMap.Default,
            fill == FillKind.None ? 0 : BarDuration - Math.Min(span, BarDuration),
            $"{fill}, {span} beats, landing {landing}"
        );
    }

    private T Pick<T>(ImmutableArray<Weighted<T>> weights)
    {
        return weights[Generators.WeightedIndex(weights)(_context)].Value;
    }

    /// <summary>The fill before a line; drums in a tuplet feel take only a fill that does not play straight notes.</summary>
    /// <param name="drumTuplet">The tuplet the drums play before the line, 1 for straight.</param>
    private FillKind PickFill(ImmutableArray<Weighted<FillKind>> weights, Drummer drummer, int drumTuplet)
    {
        weights = drummer.Weigh(weights);
        if (drumTuplet != 1)
            weights = [..weights.Where(x => FillLayers.TupletFills.Contains(x.Value))];
        return Pick(weights);
    }

    /// <summary>
    ///     A fill before the line, played from its spec: the groove in its span kept, left to the kick, or stopped, then
    ///     its hits at the span's start, then its voices.
    /// </summary>
    /// <returns>How long the fill is, in beats; 0 for none.</returns>
    private double Fill(FillEdits edits, FillKind fill, double line, double grid, Drummer drummer, int sectionId)
    {
        if (fill == FillKind.None)
            return 0;

        var spec = FillLayers.Specs[fill];
        var span = Pick(drummer.WeighSpans(spec.Spans));
        var from = line - span;
        var name = fill.ToString();

        if (spec.Groove != GrooveTreatment.Keep)
            foreach (var track in _drumTracks.Where(x => spec.Groove == GrooveTreatment.Stop || x != Track(DrumRole.Kick)))
                edits.Clear(track, from, line);

        foreach (var hit in spec.Hits)
            if (hit.Choices.FirstOrDefault(x => _roles.ContainsKey(x.Role)) is { } choice)
            {
                var (track, articulation) = Resolve(choice);
                edits.Hit(track, from, hit.Velocity, articulation, sectionId, name);
            }

        foreach (var voice in spec.Voices)
            PlayVoice(edits, voice, from, line, grid, drummer, sectionId, name);

        return span;
    }

    private int? Track(DrumRole role)
    {
        return _roles.TryGetValue(role, out var drum) ? drum.Track : null;
    }

    /// <summary>A sound's track and its number on the drum; the choice among several sounds is drawn.</summary>
    private (int Track, int Articulation) Resolve(FillSound sound)
    {
        var (track, drum) = _roles[sound.Role];
        var code = sound.Codes.Length switch
        {
            0 => 0,
            1 => sound.Codes[0].Value,
            _ => Pick(sound.Codes)
        };
        return (track, code == 0 ? 0 : drum.GetArticulationIndex(code));
    }

    /// <summary>A voice over the span; one that speeds up plays 8ths for the first half, where the span and the tempo allow.</summary>
    private void PlayVoice(FillEdits edits, FillVoice voice, double from, double line, double grid, Drummer drummer, int sectionId, string name)
    {
        var walk = voice.Alternative is { } alternative
                   && _roles.ContainsKey(GetRole(alternative))
                   && _context.TestProbability(voice.AlternativeChance)
            ? alternative
            : voice.Walk;
        Func<int, double> keepChance = voice.Keep switch
        {
            FillKeep.Run => rank => rank == 0 ? 1 : drummer.RunFullness,
            _ => rank => Math.Pow(FillLayers.PickupFullness, rank)
        };

        if (!voice.SpeedsUp)
        {
            PlaySegment(edits, from, line, grid, keepChance, walk, (voice.StartVelocity, voice.EndVelocity), sectionId, name);
            return;
        }

        var span = line - from;
        var middle = span >= 1 && grid < 0.5 ? from + span / 2 : from;
        var midVelocity = (voice.StartVelocity + voice.EndVelocity) / 2;
        PlaySegment(edits, from, middle, 0.5, keepChance, walk, (voice.StartVelocity, midVelocity), sectionId, name);
        PlaySegment(
            edits,
            middle,
            line,
            grid,
            keepChance,
            walk,
            (middle > from ? midVelocity : voice.StartVelocity, voice.EndVelocity),
            sectionId,
            name
        );
    }

    /// <summary>The drum a walk needs, besides the snare that some walks start on.</summary>
    private static DrumRole GetRole(FillWalk walk)
    {
        return walk == FillWalk.Snare ? DrumRole.Snare : DrumRole.Toms;
    }

    /// <summary>
    ///     A part of a voice: a dyadic pattern over its span, on the given grid, whose every note plays the sound of the
    ///     walk at its place among the notes, louder or quieter along the span.
    /// </summary>
    private void PlaySegment(
        FillEdits edits,
        double from,
        double to,
        double grid,
        Func<int, double> keepChance,
        FillWalk walk,
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
            if (GetWalkSound(walk, k, hits.Count) is not var (track, articulation))
                continue;

            var loudness = velocity.From + (velocity.To - velocity.From) * hits[k].Position / span;
            edits.Hit(track, from + hits[k].Position, loudness, articulation, sectionId, name);
        }
    }

    /// <summary>The sound of a walk's note by its place among the notes; none if the song lacks its drum.</summary>
    private (int Track, int Articulation)? GetWalkSound(FillWalk walk, int index, int count)
    {
        switch (walk)
        {
            case FillWalk.TomsDown:
                return GetTomDown(index, count);
            case FillWalk.SnareThenTomsDown:
                var snareCount = (int)Math.Ceiling(count * FillLayers.AroundTheKitSnareShare);
                return index < snareCount || !_roles.ContainsKey(DrumRole.Toms)
                    ? GetGrooveSound(DrumRole.Snare)
                    : GetTomDown(index - snareCount, count - snareCount);
            default:
                return GetGrooveSound(DrumRole.Snare);
        }
    }

    private (int Track, int Articulation)? GetGrooveSound(DrumRole role)
    {
        return _roles.TryGetValue(role, out var drum) ? (drum.Track, 0) : null;
    }

    /// <summary>The tom of a run's note, from the high tom down to the floor tom over the run.</summary>
    private (int Track, int Articulation)? GetTomDown(int index, int count)
    {
        if (!_roles.TryGetValue(DrumRole.Toms, out var toms))
            return null;

        var tom = DrumSounds.TomsHighToLow[index * DrumSounds.TomsHighToLow.Length / Math.Max(1, count)];
        return (toms.Track, toms.Drum.GetArticulationIndex(tom));
    }

    /// <summary>The hits the drums land on at a line: a crash, and a kick if the groove has none there.</summary>
    private void Land(TrackEventStateTimelineMap<StateMap> song, FillEdits edits, double position, int sectionId, FillLanding landing)
    {
        if (landing == FillLanding.None)
            return;

        if (landing == FillLanding.CrashAndKick && _roles.ContainsKey(DrumRole.Cymbal))
        {
            var (cymbal, crash) = Resolve(FillLayers.Crash);
            edits.Hit(cymbal, position, FillLayers.LandingVelocity, crash, sectionId, "Landing");
        }

        // the kick plays its own sound, as the groove's walk has it there
        if (Track(DrumRole.Kick) is { } kick && !HasHitAt(song, kick, position))
            edits.Hit(kick, position, FillLayers.LandingVelocity, 0, sectionId, "Landing");
    }

    private static bool HasHitAt(TrackEventStateTimelineMap<StateMap> song, int track, double position)
    {
        return song.TrackTimelineMap.TryGetValue(track, out var timeline)
               && timeline.EventTimeline.Any(x => x.Position.IsEqualToByEpsilon(position));
    }
}

/// <summary>A section as the fills see it: where it is, how far its rhythm strays, and the drums' feel before its lines.</summary>
/// <param name="DrumTuplet">The tuplet the drums play in the last bar of the section's 4-bar pattern, 1 for straight.</param>
internal sealed record FillSection(int SectionId, double Duration, RhythmicUnconventionality Rhythm, int DrumTuplet);

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
