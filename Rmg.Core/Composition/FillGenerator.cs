using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     The drums at the lines between the sections of a song, once they are put one after another: the only stage that
///     knows where a section ends and the next begins. Before a line a drummer plays a fill, or lets the groove run on,
///     and after it lands the next section on its downbeat; a section change is marked most, and the line in the middle
///     of a section now and then, each as the song's drummer plays them. A fill is played from its spec
///     (<see cref="FillLayers.Specs" />), in the feel of the section it ends, and with the twists the section's rhythm
///     draws: the more it strays from convention, the more adventurous fills it takes, and the more twists. Its draws
///     come after all the others, so a song is the same outside the lines it marks.
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
    private readonly RhythmicUnconventionality _songRhythm;
    private readonly ImmutableArray<int> _drumTracks;

    // the drum the song has for every role a fill plays
    private readonly ImmutableDictionary<DrumRole, (int Track, PercussionInstrumentDefinition Drum)> _roles;

    /// <param name="songRhythm">How far the song's rhythm strays, which its drummer plays to.</param>
    public FillGenerator(IGenerationContext context, SongTracks tracks, RhythmicUnconventionality songRhythm)
    {
        _context = context;
        _songRhythm = songRhythm;
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
        AddRole(DrumRole.Percussion, DrumGroups.Percussion.Drums.FirstOrDefault(tracks.SongDrums.Contains));
        _roles = roles.ToImmutableDictionary();
    }

    /// <param name="song">The sections put one after another.</param>
    /// <param name="sections">Every section in the song's order.</param>
    public TrackEventStateTimelineMap<StateMap> Generate(TrackEventStateTimelineMap<StateMap> song, IReadOnlyList<FillSection> sections)
    {
        // a fast song's runs play 8ths, which 16ths would blur
        var tempo = BaseTempo * song.CommonStateTimelineMap.GetEffectiveStateMapAt(0).GetStateValue(StateKinds.Tempo);
        var grid = tempo <= FillLayers.MaxSixteenthTempo ? 0.25 : 0.5;

        var drummer = Drummer.Generate(_context, _songRhythm);
        var edits = new FillEdits(_context);
        var start = 0.0;
        for (var i = 0; i < sections.Count; i++)
        {
            // the fill before a line belongs to the section it ends
            if (i > 0)
                MarkLine(song, edits, start, sections[i - 1], sections[i].SectionId, true, grid, drummer);

            // the line in the middle of the section, between its 4-bar pattern and the pattern's repeat
            for (var line = start + BarStateGenerator.PatternDuration;
                 line < start + sections[i].Duration;
                 line += BarStateGenerator.PatternDuration)
                MarkLine(song, edits, line, sections[i], sections[i].SectionId, false, grid, drummer);

            start += sections[i].Duration;
        }

        return edits.ApplyTo(song);
    }

    /// <summary>The fill before a line, in the feel and with the twists of the section it ends, and the landing after it.</summary>
    /// <param name="ending">The section the line ends, or the one it is in.</param>
    /// <param name="landingSectionId">The section that starts at the line.</param>
    private void MarkLine(
        TrackEventStateTimelineMap<StateMap> song,
        FillEdits edits,
        double line,
        FillSection ending,
        int landingSectionId,
        bool isSectionChange,
        double grid,
        Drummer drummer
    )
    {
        var chanceScale = ending.Rhythm.ChanceScale;
        var fill = Pick(drummer.Weigh(isSectionChange ? FillLayers.SectionFills : FillLayers.PhraseFills, chanceScale));
        var play = DrawPlay(fill, drummer, chanceScale, ending.DrumTuplet);
        var span = Fill(edits, play, line, grid, drummer, ending.SectionId);

        FillLanding landing;
        if (isSectionChange)
            landing = IsForcingLanding(fill) ? FillLanding.CrashAndKick : Pick(FillLayers.SectionLandings);
        else
            landing = IsForcingLanding(fill) || fill != FillKind.None && _context.TestProbability(FillLayers.PhraseLandingChance)
                ? FillLanding.CrashAndKick
                : FillLanding.None;
        if (play.Twists.HasFlag(FillTwist.NoLanding))
            landing = FillLanding.None;

        // a pushed landing comes a note of the coarser grid early, an 8th, or a tuplet's note
        var landingPosition = play.Twists.HasFlag(FillTwist.EarlyLanding) ? line - GetGrids(play.Tuplet, grid).Coarse : line;
        Land(song, edits, landingPosition, landingSectionId, landing);
        RecordDecision(ending.SectionId, line, play, span, landing);
    }

    /// <summary>
    ///     How a fill is played: its twists, each by its chance, and its tuplet, the section's own if its drums play one,
    ///     else one of the tuplet twist's.
    /// </summary>
    private FillPlay DrawPlay(FillKind fill, Drummer drummer, double chanceScale, int drumTuplet)
    {
        if (fill == FillKind.None)
            return FillPlay.Plain(fill);

        var twists = FillTwist.None;
        foreach (var twist in FillLayers.Twists)
            if (_context.TestProbability(drummer.GetTwistChance(twist, chanceScale)))
                twists |= twist.Value;

        var tuplet = drumTuplet != 1 ? drumTuplet
            : twists.HasFlag(FillTwist.Tuplet) ? Pick(FillLayers.TwistTuplets)
            : 1;

        OddVoice? oddVoice = null;
        if (twists.HasFlag(FillTwist.OddVoice))
        {
            ImmutableArray<Weighted<OddVoice>> voices = [..FillLayers.OddVoices.Where(x => CanPlay(x.Value))];
            if (voices.Length > 0)
                oddVoice = Pick(voices);
        }

        return new FillPlay(fill, twists, tuplet, oddVoice);
    }

    private bool CanPlay(OddVoice voice)
    {
        return voice switch
        {
            OddVoice.Kick => _roles.ContainsKey(DrumRole.Kick),
            OddVoice.Percussion => _roles.ContainsKey(DrumRole.Percussion),
            OddVoice.Crashes => _roles.ContainsKey(DrumRole.Cymbal),
            _ => _roles.ContainsKey(DrumRole.Snare) && _roles.ContainsKey(DrumRole.Toms)
        };
    }

    /// <summary>The song with one fill before the given line, played as given, and no landing.</summary>
    internal TrackEventStateTimelineMap<StateMap> ApplyFill(
        TrackEventStateTimelineMap<StateMap> song,
        FillPlay play,
        double line,
        double grid,
        Drummer drummer
    )
    {
        var edits = new FillEdits(_context);
        Fill(edits, play, line, grid, drummer, 0);
        return edits.ApplyTo(song);
    }

    private static bool IsForcingLanding(FillKind fill)
    {
        return fill != FillKind.None && FillLayers.Specs[fill].ForcesLanding;
    }

    /// <summary>What was decided at a line, recorded in the last bar before it, where its fill is.</summary>
    private static void RecordDecision(int sectionId, double line, FillPlay play, double span, FillLanding landing)
    {
        if (!StateTrace.IsRunning)
            return;

        var bar = (int)Math.Floor(line / BarDuration) - 1;
        var description = $"{play.Kind}, {span} beats, landing {landing}"
                          + (play.Tuplet != 1 ? $", in {play.Tuplet}s" : "")
                          + (play.OddVoice is { } oddVoice ? $", on {oddVoice}" : "")
                          + (play.Twists != FillTwist.None ? $", twists {play.Twists}" : "");
        StateTrace.Record(
            "Fill decision",
            DrumsTrace,
            sectionId,
            bar % Progressions.BarCount,
            StateMap.Default,
            play.Kind == FillKind.None ? 0 : Math.Max(0, BarDuration - span),
            description
        );
    }

    private T Pick<T>(ImmutableArray<Weighted<T>> weights)
    {
        return weights[Generators.WeightedIndex(weights)(_context)].Value;
    }

    /// <summary>
    ///     The grids a fill's voices play on: the coarser one a roll starts on, 8ths, and the finest, the song's; in a
    ///     tuplet, triplet 8ths and sextuplets where the song plays 16ths, or the tuplet's notes of the beat.
    /// </summary>
    internal static (double Coarse, double Fine) GetGrids(int tuplet, double grid)
    {
        return tuplet switch
        {
            1 => (0.5, grid),
            3 => (1.0 / 3, grid < 0.5 ? 1.0 / 6 : 1.0 / 3),
            _ => (1.0 / tuplet, 1.0 / tuplet)
        };
    }

    /// <summary>
    ///     A fill before the line, played from its spec: the groove in its span kept, left to the kick, or stopped, then
    ///     its hits at the span's start, then its voices.
    /// </summary>
    /// <returns>How long the fill is, in beats; 0 for none.</returns>
    private double Fill(FillEdits edits, FillPlay play, double line, double grid, Drummer drummer, int sectionId)
    {
        if (play.Kind == FillKind.None)
            return 0;

        var spec = FillLayers.Specs[play.Kind];
        var span = Pick(drummer.WeighSpans(spec.Spans));
        var (coarse, fine) = GetGrids(play.Tuplet, grid);
        // a fill shorter than a beat is a note of the coarser grid, so that in a tuplet it falls on the tuplet
        if (play.Tuplet != 1 && span < 1)
            span = coarse;
        // an odd span starts a note of the coarser grid earlier, or later where it is a bar long
        if (play.Twists.HasFlag(FillTwist.OddSpan))
            span = span < BarDuration ? span + coarse : span - coarse;
        var from = line - span;
        var name = play.Kind.ToString();

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
            PlayVoice(edits, voice, play, from, line, coarse, fine, drummer, sectionId, name);

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

    /// <summary>
    ///     A voice over the span. One that speeds up plays the coarser grid for the first half and the finest for the
    ///     second, where the span and the grids allow, and one that slows down the other way round.
    /// </summary>
    private void PlayVoice(
        FillEdits edits,
        FillVoice voice,
        FillPlay play,
        double from,
        double line,
        double coarse,
        double fine,
        Drummer drummer,
        int sectionId,
        string name
    )
    {
        var walk = voice.Alternative is { } alternative
                   && _roles.ContainsKey(GetRole(alternative))
                   && _context.TestProbability(voice.AlternativeChance)
            ? alternative
            : voice.Walk;
        var isGappy = play.Twists.HasFlag(FillTwist.Gappy);
        Func<int, double> keepChance = voice.Keep switch
        {
            FillKeep.Run => rank => rank == 0 ? 1 : isGappy ? FillLayers.GappyFullness : drummer.RunFullness,
            _ => rank => Math.Pow(isGappy ? FillLayers.GappySparseFullness : FillLayers.PickupFullness, rank)
        };
        var (startVelocity, endVelocity) = play.Twists.HasFlag(FillTwist.Fading)
            ? (voice.EndVelocity, voice.StartVelocity)
            : (voice.StartVelocity, voice.EndVelocity);

        var slowsDown = play.Twists.HasFlag(FillTwist.SlowDown);
        if (!voice.SpeedsUp && !slowsDown)
        {
            PlaySegment(edits, from, line, fine, keepChance, walk, play, (startVelocity, endVelocity), sectionId, name);
            return;
        }

        var span = line - from;
        var (first, second) = slowsDown ? (fine, coarse) : (coarse, fine);
        // the halves meet on a note of the coarser grid, so that a tuplet's notes stay on it
        var middle = span >= 1 && fine < coarse ? from + Math.Round(span / 2 / coarse) * coarse : from;
        var midVelocity = (startVelocity + endVelocity) / 2;
        PlaySegment(edits, from, middle, first, keepChance, walk, play, (startVelocity, midVelocity), sectionId, name);
        PlaySegment(
            edits,
            middle,
            line,
            middle > from ? second : fine,
            keepChance,
            walk,
            play,
            (middle > from ? midVelocity : startVelocity, endVelocity),
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
    ///     A part of a voice: a dyadic pattern over its span, on notes of the given length, whose every note plays the
    ///     sounds of the walk at its place among the notes, louder or quieter along the span. Where the notes are not a
    ///     power of two, such as the six of a sextuplet beat, the span holds as many cycles as their odd factor.
    /// </summary>
    private void PlaySegment(
        FillEdits edits,
        double from,
        double to,
        double noteLength,
        Func<int, double> keepChance,
        FillWalk walk,
        FillPlay play,
        (double From, double To) velocity,
        int sectionId,
        string name
    )
    {
        var span = to - from;
        var noteCount = (int)Math.Round(span / noteLength);
        if (noteCount < 1)
            return;

        var cycles = noteCount;
        var maxRank = 0;
        while (cycles % 2 == 0)
        {
            cycles /= 2;
            maxRank++;
        }

        var hits = DyadicRankThresholdPattern.Create(
                _context,
                SeedGenerator(_context),
                keepChance,
                new DyadicTimelineDescriptor(span, span / cycles, 0, maxRank)
            )
            .OutcomeRankTimeline;
        for (var k = 0; k < hits.Count; k++)
        {
            var loudness = velocity.From + (velocity.To - velocity.From) * hits[k].Position / span;
            foreach (var (track, articulation) in GetWalkSounds(walk, play, k, hits.Count))
                edits.Hit(track, from + hits[k].Position, loudness, articulation, sectionId, name);
        }
    }

    /// <summary>The sounds of a walk's note by its place among the notes; none if the song lacks their drums.</summary>
    private IEnumerable<(int Track, int Articulation)> GetWalkSounds(FillWalk walk, FillPlay play, int index, int count)
    {
        (int Track, int Articulation)? sound;
        switch (play.OddVoice)
        {
            case OddVoice.Kick:
                sound = GetGrooveSound(DrumRole.Kick);
                break;
            case OddVoice.Percussion:
                sound = GetGrooveSound(DrumRole.Percussion);
                break;
            case OddVoice.Crashes:
                sound = _roles.TryGetValue(DrumRole.Cymbal, out var cymbal)
                    ? (cymbal.Track, cymbal.Drum.GetArticulationIndex(DrumSounds.CrashCymbal1))
                    : null;
                break;
            case OddVoice.Unison:
                if (GetGrooveSound(DrumRole.Snare) is { } snare)
                    yield return snare;
                sound = _roles.TryGetValue(DrumRole.Toms, out var toms)
                    ? (toms.Track, toms.Drum.GetArticulationIndex(DrumSounds.LowFloorTom))
                    : null;
                break;
            default:
                sound = walk switch
                {
                    FillWalk.TomsDown => GetTom(index, count, play.Twists),
                    FillWalk.SnareThenTomsDown => index < (int)Math.Ceiling(count * FillLayers.AroundTheKitSnareShare)
                                                 || !_roles.ContainsKey(DrumRole.Toms)
                        ? GetGrooveSound(DrumRole.Snare)
                        : GetTom(
                            index - (int)Math.Ceiling(count * FillLayers.AroundTheKitSnareShare),
                            count - (int)Math.Ceiling(count * FillLayers.AroundTheKitSnareShare),
                            play.Twists
                        ),
                    _ => GetGrooveSound(DrumRole.Snare)
                };
                break;
        }

        if (sound is { } played)
            yield return played;
    }

    private (int Track, int Articulation)? GetGrooveSound(DrumRole role)
    {
        return _roles.TryGetValue(role, out var drum) ? (drum.Track, 0) : null;
    }

    /// <summary>
    ///     The tom of a run's note: from the high tom down to the floor tom over the run, or up, or zigzagging, the high
    ///     toms and the low ones in turn, both coming down.
    /// </summary>
    private (int Track, int Articulation)? GetTom(int index, int count, FillTwist twists)
    {
        if (!_roles.TryGetValue(DrumRole.Toms, out var toms))
            return null;

        var tomCount = DrumSounds.TomsHighToLow.Length;
        var step = index * tomCount / Math.Max(1, count);
        var tomIndex = twists.HasFlag(FillTwist.Zigzag) ? step / 2 + (index % 2 == 0 ? 0 : tomCount / 2)
            : twists.HasFlag(FillTwist.Upward) ? tomCount - 1 - step
            : step;
        return (toms.Track, toms.Drum.GetArticulationIndex(DrumSounds.TomsHighToLow[tomIndex]));
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

/// <summary>How a fill is played: its kind, its twists, the tuplet it plays, 1 for straight, and its odd sound, if any.</summary>
internal sealed record FillPlay(FillKind Kind, FillTwist Twists, int Tuplet, OddVoice? OddVoice)
{
    public static FillPlay Plain(FillKind kind) => new(kind, FillTwist.None, 1, null);
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
