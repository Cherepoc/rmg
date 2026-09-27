using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

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

    // the drum the song has for every role a fill's hits play
    private readonly ImmutableDictionary<DrumRole, (int Track, PercussionInstrumentDefinition Drum)> _roles;

    // every sound of the song's drums a run may play
    private readonly FillSounds _sounds;

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
        _sounds = new FillSounds(tracks.SongDrums);
    }

    /// <param name="song">The song's sections and its intro and ending, put one after another.</param>
    /// <param name="lines">Every line the drums mark, in the song's order.</param>
    /// <param name="map">Where the song's parts are; none for a song that starts with its first section.</param>
    public TrackEventStateTimelineMap<StateMap> Generate(
        TrackEventStateTimelineMap<StateMap> song,
        IReadOnlyList<FillLine> lines,
        SongMap? map = null
    )
    {
        // a fast song's runs play coarser notes, which finer ones would blur
        var tempo = BaseTempo * song.CommonStateTimelineMap.GetEffectiveStateMapAt(0).GetStateValue(StateKinds.Tempo);
        var minNote = FillLayers.MinNoteSeconds * tempo / 60;

        var drummer = Drummer.Generate(_context, _songRhythm);
        var edits = new TimelineEdits(_context, map);
        foreach (var line in lines)
            MarkLine(song, edits, line, minNote, drummer, map?.Origin ?? 0);

        return edits.ApplyTo(song);
    }

    /// <summary>
    ///     The lines between a song's sections and in the middle of each, as the fills mark them where the song has no
    ///     intro or ending of its own: a section change before every section but the first, and a phrase line between
    ///     every section's 4-bar pattern and its repeat.
    /// </summary>
    /// <param name="start">Where the first section starts.</param>
    public static ImmutableArray<FillLine> GetSectionLines(IReadOnlyList<FillSection> sections, double start = 0)
    {
        var lines = ImmutableArray.CreateBuilder<FillLine>();
        for (var i = 0; i < sections.Count; i++)
        {
            // the fill before a line belongs to the section it ends
            if (i > 0)
                lines.Add(new FillLine(start, sections[i - 1], sections[i].SectionId, FillTable.Section, LandingRule.Section));

            for (var line = start + Meter.PatternDuration;
                 line < start + sections[i].Duration;
                 line += Meter.PatternDuration)
                lines.Add(new FillLine(line, sections[i], sections[i].SectionId, FillTable.Phrase, LandingRule.Phrase));

            start += sections[i].Duration;
        }

        return lines.ToImmutable();
    }

    /// <summary>The fill before a line, in the feel and with the twists of the section it ends, and the landing after it.</summary>
    private void MarkLine(
        TrackEventStateTimelineMap<StateMap> song,
        TimelineEdits edits,
        FillLine line,
        double minNote,
        Drummer drummer,
        double origin
    )
    {
        var ending = line.Ending;
        var chanceScale = ending.Rhythm.ChanceScale;
        var fill = line.Fills switch
        {
            FillTable.Section => Pick(drummer.Weigh(FillLayers.SectionFills, chanceScale)),
            FillTable.Phrase => Pick(drummer.Weigh(FillLayers.PhraseFills, chanceScale)),
            _ => FillKind.None
        };
        var play = DrawPlay(fill, drummer, chanceScale, ending.Groove.PrimeIndex.ToTuplet(), line.Fills);
        var rhythm = FillRhythm.Of(ending.Groove, play.ExtraRanks, play.Tuplet, minNote);
        var span = Fill(edits, play, line.Position, rhythm, drummer, ending.SectionId);

        var landing = line.Landing switch
        {
            LandingRule.Forced => FillLanding.CrashAndKick,
            LandingRule.Section => IsForcingLanding(fill) ? FillLanding.CrashAndKick : Pick(FillLayers.SectionLandings),
            _ => IsForcingLanding(fill) || fill != FillKind.None && _context.TestProbability(FillLayers.PhraseLandingChance)
                ? FillLanding.CrashAndKick
                : FillLanding.None
        };
        // a line the song's form marks, such as where the band comes in, always lands
        if (play.Twists.HasFlag(FillTwist.NoLanding) && line.Landing != LandingRule.Forced)
            landing = FillLanding.None;

        // a pushed landing comes a note of the fill's rhythm near an 8th early
        var landingPosition = play.Twists.HasFlag(FillTwist.EarlyLanding)
            ? line.Position - rhythm.Push
            : line.Position;
        Land(song, edits, landingPosition, line.LandingSectionId, landing);
        RecordDecision(ending.SectionId, line.Position - origin, play, span, landing);
    }

    /// <summary>
    ///     How a fill is played: how much finer than the groove, its twists, each by its chance, its tuplet, the groove's
    ///     own if it plays one, else one of the tuplet twist's, and for a run, its sounds and how it walks them.
    /// </summary>
    private FillPlay DrawPlay(FillKind fill, Drummer drummer, double chanceScale, int grooveTuplet, FillTable table)
    {
        if (fill == FillKind.None)
            return FillPlay.Plain(fill);

        var extraRanks = Pick(FillLayers.ExtraRanks);
        var twists = FillTwist.None;
        foreach (var twist in FillLayers.Twists)
            if (_context.TestProbability(drummer.GetTwistChance(twist, chanceScale)))
                twists |= twist.Value;

        var tuplet = grooveTuplet != 1 ? grooveTuplet
            : twists.HasFlag(FillTwist.Tuplet) ? Pick(FillLayers.TwistTuplets)
            : 1;
        var run = FillLayers.Specs[fill].Runs
            ? _sounds.Draw(
                _context,
                drummer,
                chanceScale,
                table == FillTable.Phrase ? FillLayers.PhraseRunFullness : FillLayers.SectionRunFullness
            )
            : null;
        return new FillPlay(fill, twists, tuplet, run, extraRanks);
    }

    /// <summary>The song with one fill before the given line, played as given, and no landing.</summary>
    internal TrackEventStateTimelineMap<StateMap> ApplyFill(
        TrackEventStateTimelineMap<StateMap> song,
        FillPlay play,
        double line,
        ResolvedRhythm groove,
        double minNote,
        Drummer drummer
    )
    {
        var edits = new TimelineEdits(_context);
        Fill(edits, play, line, FillRhythm.Of(groove, play.ExtraRanks, play.Tuplet, minNote), drummer, 0);
        return edits.ApplyTo(song);
    }

    /// <summary>A run as drawn for a line before a section change, for the tests to play.</summary>
    internal FillRun DrawRun(Drummer drummer, double chanceScale = 1)
    {
        return _sounds.Draw(_context, drummer, chanceScale, FillLayers.SectionRunFullness);
    }

    /// <summary>Every sound a run may play, by role.</summary>
    internal FillSounds Sounds => _sounds;

    private static bool IsForcingLanding(FillKind fill)
    {
        return fill != FillKind.None && FillLayers.Specs[fill].ForcesLanding;
    }

    /// <summary>What was decided at a line, recorded in the last bar before it, where its fill is.</summary>
    /// <param name="line">Where the line is, from the start of the song's first section.</param>
    private static void RecordDecision(int sectionId, double line, FillPlay play, double span, FillLanding landing)
    {
        if (!StateTrace.IsRunning)
            return;

        var bar = (int)Math.Floor(line / Meter.BarDuration) - 1;
        var description = $"{play.Kind}, {span} beats, landing {landing}"
                          + (play.Tuplet != 1 ? $", in {play.Tuplet}s" : "")
                          + (play.Run is { } run ? $", {run}" : "")
                          + (play.Twists != FillTwist.None ? $", twists {play.Twists}" : "");
        StateTrace.Record(
            "Fill decision",
            DrumsTrace,
            sectionId,
            bar.Mod(Progressions.BarCount),
            StateMap.Default,
            play.Kind == FillKind.None ? 0 : Math.Max(0, Meter.BarDuration - span),
            description
        );
    }

    private T Pick<T>(ImmutableArray<Weighted<T>> weights)
    {
        return weights[Generators.WeightedIndex(weights)(_context)].Value;
    }

    /// <summary>
    ///     A fill before the line, played from its spec: the groove in its span kept, left by the drums the fill plays,
    ///     or stopped, then its hits at the span's start, then its run.
    /// </summary>
    /// <returns>How long the fill is, in beats; 0 for none.</returns>
    private double Fill(TimelineEdits edits, FillPlay play, double line, FillRhythm rhythm, Drummer drummer, int sectionId)
    {
        if (play.Kind == FillKind.None)
            return 0;

        var spec = FillLayers.Specs[play.Kind];
        var span = Pick(drummer.WeighSpans(spec.Spans));
        // a fill shorter than a beat is a note of the fill's rhythm, so that in a tuplet it falls on the tuplet
        if (play.Tuplet != 1 && span < 1)
            span = rhythm.Push;
        // an odd span starts a note of the fill's rhythm near an 8th earlier, or later where it is a bar long
        if (play.Twists.HasFlag(FillTwist.OddSpan))
            span = span < Meter.BarDuration ? span + rhythm.Push : span - rhythm.Push;
        var from = line - span;
        var name = play.Kind.ToString();

        var cleared = spec.Groove switch
        {
            GrooveTreatment.Stop => _drumTracks,
            GrooveTreatment.Played => play.Run?.Sounds.Select(x => x.Track).Distinct() ?? [],
            _ => []
        };
        foreach (var track in cleared)
            edits.Clear(track, from, line);

        foreach (var hit in spec.Hits)
            if (hit.Choices.FirstOrDefault(x => _roles.ContainsKey(x.Role)) is { } choice)
            {
                var (track, articulation) = Resolve(choice);
                edits.Hit(track, from, hit.Velocity, articulation, sectionId, name);
            }

        if (play.Run is { } run)
            PlayRun(edits, run, play.Twists, from, line, rhythm, sectionId, name);
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
    ///     A run over the span, in the fill's rhythm, starting on a hit, each note accented by its rank as the groove's
    ///     are, and louder or quieter along the span. One that speeds up plays a rank coarser for the first half and the
    ///     finest for the second, where the span allows, and one that slows down the other way round. Its walk runs over
    ///     all its notes.
    /// </summary>
    private void PlayRun(
        TimelineEdits edits,
        FillRun run,
        FillTwist twists,
        double from,
        double line,
        FillRhythm rhythm,
        int sectionId,
        string name
    )
    {
        var fullness = run.Fullness;
        Func<int, double> keepChance = rank => Math.Pow(fullness, rank);
        var (startVelocity, endVelocity) = twists.HasFlag(FillTwist.Fading)
            ? (FillLayers.RunEndVelocity, FillLayers.RunStartVelocity)
            : (FillLayers.RunStartVelocity, FillLayers.RunEndVelocity);

        var slowsDown = twists.HasFlag(FillTwist.SlowDown);
        var seed = SeedGenerator(_context);
        var notes = new List<(double Position, int Rank, int MaxRank)>();
        if ((run.SpeedsUp || slowsDown) && rhythm.MaxRank > 0)
        {
            // the halves meet on a note of the coarser notes, so that a tuplet's notes stay on it
            var middle = from + Math.Round((line - from) / 2 / rhythm.Coarse) * rhythm.Coarse;
            var (first, second) = slowsDown ? (rhythm.MaxRank, rhythm.MaxRank - 1) : (rhythm.MaxRank - 1, rhythm.MaxRank);
            notes.AddRange(rhythm.Play(_context, seed, keepChance, line, from, middle, first, true));
            notes.AddRange(rhythm.Play(_context, seed + 1, keepChance, line, middle, line, second));
        }
        else
            notes.AddRange(rhythm.Play(_context, seed, keepChance, line, from, line, rhythm.MaxRank, true));

        var places = FillSounds.Walk(_context, run.Path, run.Sounds.Length, notes.Count);
        var span = line - from;
        for (var k = 0; k < notes.Count; k++)
        {
            var (position, rank, maxRank) = notes[k];
            var loudness = startVelocity + (endVelocity - startVelocity) * (position - from) / span
                           + FillLayers.AccentWeight * BeatAccent.CreateVelocityGenerator(rank, maxRank)(_context);
            foreach (var sound in FillSounds.GetNoteSounds(run, places[k], rank))
                edits.Hit(sound.Track, position, loudness, sound.Articulation, sectionId, name);
        }
    }

    /// <summary>The hits the drums land on at a line: a crash, and a kick if the groove has none there.</summary>
    private void Land(TrackEventStateTimelineMap<StateMap> song, TimelineEdits edits, double position, int sectionId, FillLanding landing)
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

/// <summary>How a fill is played: its kind, its twists, the tuplet it plays, 1 for straight, and its run, if it has one.</summary>
/// <param name="ExtraRanks">How many ranks finer than the groove the fill plays.</param>
internal sealed record FillPlay(FillKind Kind, FillTwist Twists, int Tuplet, FillRun? Run, int ExtraRanks = 1)
{
    public static FillPlay Plain(FillKind kind) => new(kind, FillTwist.None, 1, null);
}

/// <summary>The fills a line takes: none, those before a section change, or those in the middle of a section.</summary>
internal enum FillTable
{
    None,
    Section,
    Phrase
}

/// <summary>How the drums land after a line: as after a section change, as after a phrase line, or always.</summary>
internal enum LandingRule
{
    Section,
    Phrase,
    Forced
}

/// <summary>A line the drums mark: where it is, the section it ends, the one that starts there, and its fills and landing.</summary>
/// <param name="Ending">The section the line ends, whose rhythm and feel its fill plays in.</param>
internal sealed record FillLine(double Position, FillSection Ending, int LandingSectionId, FillTable Fills, LandingRule Landing);

/// <summary>A section as the fills see it: where it is, how far its rhythm strays, and the drums' feel before its lines.</summary>
/// <param name="Groove">The rhythm the fills play from, the snare's in the last bar of the section's 4-bar pattern.</param>
internal sealed record FillSection(int SectionId, double Duration, RhythmicUnconventionality Rhythm, ResolvedRhythm Groove);
