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

    /// <param name="song">The song's sections and its intro and ending, put one after another.</param>
    /// <param name="lines">Every line the drums mark, in the song's order.</param>
    /// <param name="map">Where the song's parts are; none for a song that starts with its first section.</param>
    public TrackEventStateTimelineMap<StateMap> Generate(
        TrackEventStateTimelineMap<StateMap> song,
        IReadOnlyList<FillLine> lines,
        SongMap? map = null
    )
    {
        // a fast song's runs play 8ths, which 16ths would blur
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
        var play = DrawPlay(fill, drummer, chanceScale, ending.Groove.PrimeIndex.ToTuplet());
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
    ///     How a fill is played: how much finer than the groove, its twists, each by its chance, and its tuplet, the
    ///     groove's own if it plays one, else one of the tuplet twist's.
    /// </summary>
    private FillPlay DrawPlay(FillKind fill, Drummer drummer, double chanceScale, int grooveTuplet)
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

        OddVoice? oddVoice = null;
        if (twists.HasFlag(FillTwist.OddVoice))
        {
            ImmutableArray<Weighted<OddVoice>> voices = [..FillLayers.OddVoices.Where(x => CanPlay(x.Value))];
            if (voices.Length > 0)
                oddVoice = Pick(voices);
        }

        return new FillPlay(fill, twists, tuplet, oddVoice, extraRanks);
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
        ResolvedRhythm groove,
        double minNote,
        Drummer drummer
    )
    {
        var edits = new TimelineEdits(_context);
        Fill(edits, play, line, FillRhythm.Of(groove, play.ExtraRanks, play.Tuplet, minNote), drummer, 0);
        return edits.ApplyTo(song);
    }

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
                          + (play.OddVoice is { } oddVoice ? $", on {oddVoice}" : "")
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
    ///     A fill before the line, played from its spec: the groove in its span kept, left to the kick, or stopped, then
    ///     its hits at the span's start, then its voices.
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
            PlayVoice(edits, voice, play, from, line, rhythm, drummer, sectionId, name);
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
    ///     A voice over the span, in the fill's rhythm, each note accented by its rank as the groove's are, and louder or
    ///     quieter along the span. One that speeds up plays a rank coarser for the first half and the finest for the
    ///     second, where the span allows, and one that slows down the other way round; the walk runs over all its notes.
    /// </summary>
    private void PlayVoice(
        TimelineEdits edits,
        FillVoice voice,
        FillPlay play,
        double from,
        double line,
        FillRhythm rhythm,
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
        var seed = SeedGenerator(_context);
        var startsOnAHit = voice.Keep == FillKeep.Run;
        var notes = new List<(double Position, int Rank, int MaxRank)>();
        if ((voice.SpeedsUp || slowsDown) && rhythm.MaxRank > 0)
        {
            // the halves meet on a note of the coarser notes, so that a tuplet's notes stay on it
            var middle = from + Math.Round((line - from) / 2 / rhythm.Coarse) * rhythm.Coarse;
            var (first, second) = slowsDown ? (rhythm.MaxRank, rhythm.MaxRank - 1) : (rhythm.MaxRank - 1, rhythm.MaxRank);
            notes.AddRange(rhythm.Play(_context, seed, keepChance, line, from, middle, first, startsOnAHit));
            notes.AddRange(rhythm.Play(_context, seed + 1, keepChance, line, middle, line, second));
        }
        else
            notes.AddRange(rhythm.Play(_context, seed, keepChance, line, from, line, rhythm.MaxRank, startsOnAHit));

        var span = line - from;
        for (var k = 0; k < notes.Count; k++)
        {
            var (position, rank, maxRank) = notes[k];
            var loudness = startVelocity + (endVelocity - startVelocity) * (position - from) / span
                           + FillLayers.AccentWeight * BeatAccent.CreateVelocityGenerator(rank, maxRank)(_context);
            foreach (var (track, articulation) in GetWalkSounds(walk, play, k, notes.Count))
                edits.Hit(track, position, loudness, articulation, sectionId, name);
        }
    }

    /// <summary>The drum a walk needs, besides the snare that some walks start on.</summary>
    private static DrumRole GetRole(FillWalk walk)
    {
        return walk == FillWalk.Snare ? DrumRole.Snare : DrumRole.Toms;
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

/// <summary>How a fill is played: its kind, its twists, the tuplet it plays, 1 for straight, and its odd sound, if any.</summary>
/// <param name="ExtraRanks">How many ranks finer than the groove the fill plays.</param>
internal sealed record FillPlay(FillKind Kind, FillTwist Twists, int Tuplet, OddVoice? OddVoice, int ExtraRanks = 1)
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
