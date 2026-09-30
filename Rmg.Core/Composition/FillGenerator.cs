using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     The drums at the lines between the sections of a song, once they are put one after another: the only stage that
///     knows where a section ends and the next begins. Before a line a drummer plays a fill, or lets the groove run on,
///     and after it lands the next section on its downbeat; a section change is marked most, and the line in the middle
///     of a section now and then, each as the song's drummer plays them. Every fill is a run (see
///     <see cref="FillLayers" />): its span, none where the groove runs on, what it does with the groove, the sounds it
///     walks, and a layer over the groove's rhythm, in the section it ends; the more the section strays from convention,
///     the stranger the draws. Its draws come after all the others, so a song is the same outside the lines it marks.
/// </summary>
internal sealed class FillGenerator
{
    /// <summary>The track of a trace entry that records a decision for all the drums, such as a line's fill.</summary>
    public const int DrumsTrace = -1;

    private static readonly Func<IGenerationContext, int> SeedGenerator = Generators.Int();

    private readonly IGenerationContext _context;
    private readonly RhythmicUnconventionality _songRhythm;
    private readonly ImmutableArray<int> _drumTracks;

    // every sound of the song's drums a run or a landing may play
    private readonly FillSounds _sounds;

    // the song's drums' own state, before any section's
    private readonly FillGrooves _songDrums;

    // the chances of the fills' rarer choices, which the drummer's and the section's layers multiply
    private readonly StateMap _chances;

    /// <param name="songRhythm">How far the song's rhythm strays, which makes a drummer's signature likelier.</param>
    // the meter the song's bars are in
    private readonly Meter _meter;

    /// <param name="meter">The meter the song's bars are in.</param>
    public FillGenerator(IGenerationContext context, SongTracks tracks, RhythmicUnconventionality songRhythm, Meter meter)
    {
        _meter = meter;
        _context = context;
        _songRhythm = songRhythm;
        _drumTracks = [..tracks.SongDrums.Select(DrumGroups.GetTrackNumber)];
        _sounds = new FillSounds(tracks.SongDrums);
        _songDrums = new FillGrooves(
            ResolvedRhythm.DefaultState,
            _drumTracks.ToImmutableDictionary(x => x, x => SongTracks.GetGenerationStateMap(tracks.Definitions[x]))
        );
        var chances = new StateMapBuilder("Fill", perTrack: true);
        foreach (var (kind, chance) in FillLayers.Chances)
            chances.Add(kind, chance);
        _chances = chances.ToStateMap(context);
    }

    /// <summary>Every sound a run may play, by role.</summary>
    internal FillSounds Sounds => _sounds;

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
        var tempo = Meter.BaseTempo * song.CommonStateTimelineMap.GetEffectiveStateMapAt(0).GetStateValue(StateKinds.Tempo);
        var minNote = FillLayers.MinNoteSeconds * tempo / 60;

        var drummer = Drummer.Generate(_context, _songRhythm.Value);
        var edits = new TimelineEdits(_context, _meter, map);
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
    /// <param name="meter">The meter the song's bars are in.</param>
    public static ImmutableArray<FillLine> GetSectionLines(IReadOnlyList<FillSection> sections, Meter meter, double start = 0)
    {
        var lines = ImmutableArray.CreateBuilder<FillLine>();
        for (var i = 0; i < sections.Count; i++)
        {
            // the fill before a line belongs to the section it ends, one out of a breakdown the drums coming back; no line
            // leads into a section whose drums rest, nor marks its phrases
            if (i > 0 && sections[i].HasDrums)
                lines.Add(new FillLine(start, sections[i - 1], sections[i], 0));

            // a drum solo fills every bar, and any other section its phrases
            var every = sections[i].IsDrumSolo ? meter.BarDuration : meter.PatternDuration;
            for (var line = start + every;
                 sections[i].HasDrums && line < start + sections[i].Duration;
                 line += every)
                lines.Add(new FillLine(line, sections[i], sections[i], FillLayers.PhraseWeight));

            start += sections[i].Duration;
        }

        return lines.ToImmutable();
    }

    /// <summary>
    ///     The fill before a line, in the rhythm of the section it ends, and the landing after it, leaning by how much
    ///     more energy the section it leads into has than the one it ends: into a louder one longer and fuller fills and
    ///     likelier landings, into a quieter one likelier stops and breaks, as far as the ending section's rhythm follows
    ///     its energy.
    /// </summary>
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
        var chances = GetChances(drummer);
        var fills = ending.Fills.Value;
        // how much the line weighs: its own weight, and the energy it leads into, as far as the section's rhythm follows it
        var lift = line.Next.Energy - ending.Energy;
        var weight = line.Weight + lift * ending.Rhythm.Coupling;
        var tilt = Tilt.Of(SectionEnergy.HighOdds, weight);
        // and which way the energy goes, which makes stopping the groove likelier into a quieter section
        var direction = SectionEnergy.Tilt(lift, ending.Rhythm.Coupling);
        var play = line.HasFill ? DrawPlay(drummer, ending, chances, tilt, direction, weight) : FillPlay.None;
        var rhythm = FillRhythm.Of(ending.Groove.Source, play.Layer, minNote, _meter);
        _lastRun = [];
        var span = Fill(edits, play, line.Position, ending.Groove, rhythm, minNote, ending.SectionId);
        if (span > 0 && line.Ending != line.Next && !_lastRun.IsEmpty)
            Runs.Add(new PlayedRun(line.Position, _lastRun, lift * ending.Rhythm.Coupling));

        // the drums land after they stopped, as they come back, and always where the song's form marks the line,
        // such as where the band comes in; on the drum kit, or on the percussion into a section of percussion only
        var landings = FillLayers.Landings.Where(x => x.Key == FillDrumRole.Percussion == line.Next.IsPercussionOnly).ToImmutableDictionary(
            x => x.Key,
            x => line.IsLandingForced ? 1
                : span > 0 && play.Treatment == GrooveTreatment.Stop ? FillLayers.StopLandingChance
                : tilt.Chance(x.Value, FillLayers.LandingLean)
        );
        var landing = _sounds.DrawLanding(_context, landings);
        // a pushed landing comes a note of the fill's rhythm near an 8th early, after a fill, not where the band stops
        // or counts in and lands on the line together
        var isEarly = _context.TestProbability(GetChance(chances, CompositionStateKinds.Fill.EarlyLandingChance, fills)) && line.HasFill;
        var landingPosition = isEarly ? line.Position - rhythm.Push : line.Position;
        Land(song, edits, landingPosition, line.Next.SectionId, landing);
        RecordDecision(ending.SectionId, line.Position - origin, play, rhythm, span, landing, isEarly, lift);
    }

    /// <summary>
    ///     How a fill is played: its span, none for the groove running on, what it does with the groove, its layer over
    ///     the groove's rhythm, its run, and whether it starts off the beat or fades.
    /// </summary>
    /// <param name="chances">The chances of the fills' rarer choices at the line.</param>
    /// <param name="tilt">How the line's weight leans the fill: longer on the heavy side.</param>
    /// <param name="direction">How the energy the line leads into leans it: stopping into a quieter section.</param>
    /// <param name="weight">How much the line weighs, which makes the fill fuller or sparser.</param>
    private FillPlay DrawPlay(
        Drummer drummer,
        FillSection section,
        StateMap chances,
        Tilt tilt,
        Tilt direction,
        double weight
    )
    {
        var (rhythm, grooves) = (section.Fills, section.Groove);
        var span = _context.Pick(tilt.Weigh(drummer.WeighSpans(FillLayers.Spans), FillLayers.GetSpanLoudness));
        if (span <= 0)
            return FillPlay.None;

        // stopping is the unconventional treatment, and the quiet one
        var treatments = ByConvention.Weigh(FillLayers.Treatments.Select(x => (x.Value, RhythmicUnconventionality.WeightEnds(x.Weight, x.Value == GrooveTreatment.Stop ? 1 : 0))), rhythm.Value);
        var treatment = _context.Pick(direction.Weigh(treatments, x => FillLayers.TreatmentLoudness[x]));
        var fullness = FillLayers.Fullness + FillLayers.FullnessPerWeight * weight + FillLayers.TreatmentFullness[treatment];
        var layer = CreateLayer(drummer, rhythm, fullness, Feels.Of(grooves.Source), section.Facets[Facet.Feel]);
        // where the drums stop, they rest half the time: a break
        // a run plays the percussion alone in a section of percussion only, and now and then in one of the drum kit
        var isPercussion = section.IsPercussionOnly ||
                           (_sounds.Sounds.ContainsKey(FillDrumRole.Percussion) && _context.TestProbability(RhythmicUnconventionality.Ends(FillLayers.PercussionRunChance, 1).At(rhythm.Value)));
        var run = treatment == GrooveTreatment.Stop && _context.TestProbability(direction.Chance(FillLayers.StopRestChance, -1))
            ? FillRun.Rest
            : _sounds.Draw(
                _context,
                drummer,
                rhythm.Value,
                (role, track) => !isPercussion ? GetRunChance(grooves.Of(track), rhythm.Value) : role == FillDrumRole.Percussion ? 1 : 0
            );
        var spanShift = _context.TestProbability(GetChance(chances, CompositionStateKinds.Fill.OffBeatChance, rhythm.Value))
            ? _context.TestProbability(0.5) ? 1 : -1
            : 0;
        var fades = _context.TestProbability(GetChance(chances, CompositionStateKinds.Fill.FadeChance, rhythm.Value));
        return new FillPlay(span, treatment, run, layer, spanShift, fades);
    }

    /// <summary>
    ///     A fill's layer over the groove's rhythm: finer, a step more or less, fuller by the line's and the treatment's
    ///     share and as the drummer plays, its cycles repeating, and the steps of the fill's rhythm layer, as strange as
    ///     the section; how fine it plays stays near the groove's.
    /// </summary>
    /// <param name="feel">The groove's feel, which the fill changes now and then (<see cref="Feels.FillChange" />).</param>
    /// <param name="feelUnconventionality">The feel facet of the section's unconventionality.</param>
    private StateMap CreateLayer(Drummer drummer, RhythmicUnconventionality rhythm, double fullness, int feel, double feelUnconventionality)
    {
        var layer = rhythm.Lean(RhythmLayers.Fill);
        var density = layer.CreateDensityGenerator();
        var spread = layer.CreateFullnessGenerator();
        // the fill's change of feel from a sequence of its own, a single draw of the fill's
        var feelChange = Feels.DrawChange(new GenerationContext((uint)SeedGenerator(_context)), feel, Feels.FillChange, feelUnconventionality);
        return new StateMapBuilder("Fill", perTrack: true)
            .Add(CompositionStateKinds.Rhythm.MaxRank, context => FillLayers.FinerRanks + density(context))
            .Add(CompositionStateKinds.Rhythm.RankOffset, density)
            .Add(CompositionStateKinds.Rhythm.Fullness, context => fullness + drummer.FullnessOffset + spread(context))
            .Add(CompositionStateKinds.Rhythm.Variation, -1.0)
            .ToStateMap(_context)
            .MergeWith(feelChange is { } changed ? Feels.At(StateDepths.Note, changed) : StateMap.Default);
    }

    /// <summary>The song with one fill before the given line, played as given, and no landing.</summary>
    internal TrackEventStateTimelineMap<StateMap> ApplyFill(
        TrackEventStateTimelineMap<StateMap> song,
        FillPlay play,
        double line,
        FillGrooves groove,
        double minNote
    )
    {
        var edits = new TimelineEdits(_context, _meter);
        Fill(edits, play, line, groove, FillRhythm.Of(groove.Source, play.Layer, minNote, _meter), minNote, 0);
        return edits.ApplyTo(song);
    }

    /// <summary>
    ///     The chances of the fills' rarer choices at a line, as layers: the base and the drummer's, such as its
    ///     signature, which multiplies a choice's odds.
    /// </summary>
    internal StateMap GetChances(Drummer drummer)
    {
        return _chances.MergeWith(drummer.Layer ?? StateMap.Default);
    }

    /// <summary>
    ///     A rarer choice's chance at a line (<see cref="GetChances" />): its base chance at the section's fills facet, as
    ///     an unconventional thing is, never at the plain end and every time at the wild, its odds times what the drummer
    ///     multiplies them by.
    /// </summary>
    internal static double GetChance(StateMap chances, StateKind<double> kind, double unconventionality)
    {
        var chance = FillLayers.Chances.Single(x => x.Kind == kind).Chance;
        return new Tilt(Math.Log(chances.GetStateValue(kind) / chance)).Chance(RhythmicUnconventionality.Ends(chance, 1).At(unconventionality), 1);
    }

    /// <summary>
    ///     The chance a run plays a drum, as its group's state has it: its run chance, by the fills facet as its group
    ///     leans, an unconventional group never at the plain end and every time at the wild.
    /// </summary>
    internal static double GetRunChance(StateMap drum, double unconventionality)
    {
        return RhythmicUnconventionality.Ends(
            Math.Clamp(drum.GetStateValue(CompositionStateKinds.Fill.RunChance), 0, 1),
            drum.GetStateValue(CompositionStateKinds.Fill.Unconventionality)
        ).At(unconventionality);
    }

    /// <summary>A run as drawn over the song's own drum states at a fills facet, for the tests to play.</summary>
    internal FillRun DrawRun(Drummer drummer, double unconventionality)
    {
        return _sounds.Draw(_context, drummer, unconventionality, (_, track) => GetRunChance(_songDrums.Of(track), unconventionality));
    }

    /// <summary>What was decided at a line, recorded in the last bar before it, where its fill is.</summary>
    /// <param name="line">Where the line is, from the start of the song's first section.</param>
    private void RecordDecision(
        int sectionId,
        double line,
        FillPlay play,
        FillRhythm rhythm,
        double span,
        ImmutableArray<RunSound> landing,
        bool isEarly,
        double lift
    )
    {
        if (!StateTrace.IsRunning)
            return;

        var bar = (int)Math.Floor(line / _meter.BarDuration) - 1;
        var description = $"{span} beats, landing {(landing.IsEmpty ? "on nothing" : $"on {string.Join(" ", landing)}")}"
                          + (isEarly ? " early" : "")
                          + (span > 0
                              ? $", {play.Treatment}"
                                + (rhythm.Tuplet != 1 ? $", in {rhythm.Tuplet}s" : "")
                                + $", {rhythm.MaxRank} ranks of {rhythm.Period} beats, fullness {rhythm.Rhythm.Fullness:F2}, {play.Run}"
                                + (play.SpanShift != 0 ? ", off the beat" : "")
                                + (play.Fades ? ", fading" : "")
                              : "")
                          + (lift != 0 ? $", into energy {(lift > 0 ? "+" : "")}{lift:F2}" : "");
        StateTrace.Record(
            TracePoints.FillDecision,
            DrumsTrace,
            sectionId,
            bar.Mod(Progressions.BarCount),
            StateMap.Default,
            span > 0 ? Math.Max(0, _meter.BarDuration - span) : 0,
            description,
            new FillDecision(span, play.Treatment, play.Run == FillRun.Rest, rhythm.Tuplet, rhythm.Rhythm.Fullness, landing, isEarly, lift)
        );
    }

    /// <summary>
    ///     A fill before the line: the groove in its span kept, left by the drums the fill plays, or stopped, then its
    ///     run.
    /// </summary>
    /// <returns>How long the fill is, in beats; 0 for none.</returns>
    private double Fill(
        TimelineEdits edits,
        FillPlay play,
        double line,
        FillGrooves groove,
        FillRhythm rhythm,
        double minNote,
        int sectionId
    )
    {
        if (play.Span <= 0)
            return 0;

        // the bar's last node of the span's length in four
        var span = _meter.SpanOf(play.Span);
        // a fill shorter than a beat is a note of the fill's rhythm, so that in a tuplet it falls on the tuplet
        if (rhythm.Tuplet != 1 && span < 1)
            span = rhythm.Push;
        // one off the beat starts a note of the fill's rhythm near an 8th earlier or later, and shorter where it is a
        // bar long
        if (play.SpanShift != 0)
            span = span < _meter.BarDuration ? Math.Max(rhythm.Push, span + play.SpanShift * rhythm.Push) : span - rhythm.Push;
        var from = line - span;

        var cleared = play.Treatment switch
        {
            GrooveTreatment.Stop => _drumTracks,
            GrooveTreatment.Played => play.Run.Sounds.Select(x => x.Track).Distinct(),
            _ => []
        };
        foreach (var track in cleared)
            edits.Clear(track, from, line);

        PlayRun(edits, play, from, line, groove, rhythm, minNote, sectionId);
        return span;
    }

    /// <summary>
    ///     A run over the span, in the fill's rhythm, each note accented by its rank as the groove's are, and louder or
    ///     quieter along the span. One that speeds up plays a rank coarser for the first half and the finest for the
    ///     second, where the span allows, and one that slows down the other way round. Its walk runs over all its notes,
    ///     and a sound plays a note where its drum's own rhythm, with the fill's layer, has it, such as a crash on the
    ///     strongest notes alone.
    /// </summary>
    private void PlayRun(
        TimelineEdits edits,
        FillPlay play,
        double from,
        double line,
        FillGrooves groove,
        FillRhythm rhythm,
        double minNote,
        int sectionId
    )
    {
        var run = play.Run;
        if (run.Sounds.IsEmpty)
            return;

        var (startVelocity, endVelocity) = play.Fades
            ? (FillLayers.RunEndVelocity, FillLayers.RunStartVelocity)
            : (FillLayers.RunStartVelocity, FillLayers.RunEndVelocity);

        var seed = SeedGenerator(_context);
        var notes = new List<(double Position, int Rank, int MaxRank)>();
        if (run.Speed != FillSpeed.Steady)
        {
            // a rank coarser for one half, folded into range; the halves meet on a note of the coarser, so that a
            // tuplet's notes stay on it
            var coarser = rhythm.Step(-1);
            var (first, second) = run.Speed == FillSpeed.SpeedsUp ? (coarser, rhythm.MaxRank) : (rhythm.MaxRank, coarser);
            var note = rhythm.Period / Math.Pow(2, Math.Min(first, second));
            var middle = from + Math.Round((line - from) / 2 / note) * note;
            notes.AddRange(rhythm.Play(_context, seed, line, from, middle, first));
            notes.AddRange(rhythm.Play(_context, seed + 1, line, middle, line, second));
        }
        else
            notes.AddRange(rhythm.Play(_context, seed, line, from, line, rhythm.MaxRank));

        var drums = run.Sounds.Select(x => x.Track).Distinct().ToDictionary(x => x, x => FillRhythm.Of(groove.Of(x), play.Layer, minNote, _meter));
        var places = FillSounds.Walk(_context, run.Path, run.Sounds.Length, notes.Count);
        var span = line - from;
        for (var k = 0; k < notes.Count; k++)
        {
            var (position, rank, maxRank) = notes[k];
            var loudness = startVelocity + (endVelocity - startVelocity) * (position - from) / span
                           + FillLayers.AccentWeight * BeatAccent.CreateVelocityGenerator(rank, maxRank)(_context);
            foreach (var sound in FillSounds.GetNoteSounds(run, places[k], x => drums[x].Has(position, line)))
                edits.Hit(sound.Track, position, loudness, sound.Articulation, sectionId, "Fill");
        }

        _lastRun = [..notes.Select(x => x.Position).Distinct().Order()];
    }

    // the positions of the run the last fill played, which a section change keeps for the pitched fills
    private ImmutableArray<double> _lastRun = [];

    /// <summary>The runs the drums played into a change of section, for the pitched tracks to fill with (<see cref="BassFills" />).</summary>
    public List<PlayedRun> Runs { get; } = [];

    /// <summary>The hits the drums land on at a line; a drum that already plays there keeps its note.</summary>
    private static void Land(
        TrackEventStateTimelineMap<StateMap> song,
        TimelineEdits edits,
        double position,
        int sectionId,
        ImmutableArray<RunSound> landing
    )
    {
        foreach (var sound in landing.Where(x => !HasHitAt(song, x.Track, position)))
            edits.Hit(sound.Track, position, FillLayers.LandingVelocity, sound.Articulation, sectionId, "Landing");
    }

    private static bool HasHitAt(TrackEventStateTimelineMap<StateMap> song, int track, double position)
    {
        return song.TrackTimelineMap.TryGetValue(track, out var timeline)
               && timeline.EventTimeline.Any(x => x.Position.IsEqualToByEpsilon(position));
    }
}

/// <summary>
///     How a fill is played: how long it is, none for the groove running on, what it does with the groove, its run, its
///     layer over the groove's rhythm, and whether it starts off the beat or fades.
/// </summary>
/// <param name="SpanShift">How many notes near an 8th it starts later, or earlier where negative.</param>
internal sealed record FillPlay(
    double Span,
    GrooveTreatment Treatment,
    FillRun Run,
    StateMap Layer,
    int SpanShift = 0,
    bool Fades = false
)
{
    public static FillPlay None { get; } = new(0, GrooveTreatment.Keep, FillRun.Rest, FillRhythm.PlainLayer);
}

/// <summary>What was decided at a line, as a trace records it.</summary>
/// <param name="Span">How long the fill is, in beats; 0 for none.</param>
/// <param name="Rests">Whether the run rests, playing nothing, as a break does.</param>
/// <param name="Tuplet">The tuplet the fill plays in; 1 for straight.</param>
/// <param name="Fullness">How full the fill's rhythm is.</param>
/// <param name="Landing">The sounds the drums land on after the line.</param>
/// <param name="IsEarly">Whether the landing is pushed early.</param>
/// <param name="Lift">How much more energy the section the line leads into has than the one it ends.</param>
internal sealed record FillDecision(
    double Span,
    GrooveTreatment Treatment,
    bool Rests,
    int Tuplet,
    double Fullness,
    ImmutableArray<RunSound> Landing,
    bool IsEarly,
    double Lift
);

/// <summary>A line the drums mark: where it is, the section it ends, the one that starts there, and how it weighs.</summary>
/// <param name="Ending">The section the line ends, whose rhythm and feel its fill plays in.</param>
/// <param name="Next">The section that starts at the line, which the drums land in.</param>
/// <param name="Weight">How much the line weighs before the energy it leads into (<see cref="FillLayers.PhraseWeight" />).</param>
/// <param name="HasFill">Whether a fill may play before it; none where the song's form has the drums wait.</param>
/// <param name="IsLandingForced">Whether the drums always land on it, as where the band comes in.</param>
/// <summary>A run the drums played into a change of section: the line, the run's notes' positions, and the lift, the energy's rise into it as far as the rhythm follows it.</summary>
internal sealed record PlayedRun(double Line, ImmutableArray<double> Positions, double Lift);

internal sealed record FillLine(
    double Position,
    FillSection Ending,
    FillSection Next,
    double Weight,
    bool HasFill = true,
    bool IsLandingForced = false
);

/// <summary>
///     A section as the fills see it: where it is, how far its rhythm strays, the drums' feel before its lines, and its
///     energy.
/// </summary>
/// <param name="Groove">The states of the rhythm the fills play from, in the last bar of the section's 4-bar pattern.</param>
/// <param name="Energy">How loud and busy the section is meant to be (<see cref="SectionEnergy" />).</param>
/// <param name="IsPercussionOnly">Whether the section plays its percussion without the drum kit.</param>
internal sealed record FillSection(
    int SectionId,
    double Duration,
    RhythmicUnconventionality Rhythm,
    Unconventionality Facets,
    FillGrooves Groove,
    double Energy,
    bool IsPercussionOnly,
    bool HasDrums,
    bool IsDrumSolo
)
{
    /// <summary>How far the section's fills stray from convention, which their choices lean by.</summary>
    public RhythmicUnconventionality Fills => new(Facets[Facet.Fills]);
}

/// <summary>
///     The states of the groove's rhythm that a fill adds its layer to: the one its rhythm is, the snare's, and every
///     drum's own, whose notes of a run are the run's that fall on its own rhythm's.
/// </summary>
internal sealed record FillGrooves(StateMap Source, ImmutableDictionary<int, StateMap> Drums)
{
    public static FillGrooves FromSource(StateMap source) => new(source, ImmutableDictionary<int, StateMap>.Empty);

    /// <summary>A drum's own state, or the source's where the section has none of it.</summary>
    public StateMap Of(int track) => Drums.GetValueOrDefault(track, Source);
}
