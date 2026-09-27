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
    public FillGenerator(IGenerationContext context, SongTracks tracks, RhythmicUnconventionality songRhythm)
    {
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

    /// <summary>The fill before a line, in the rhythm of the section it ends, and the landing after it.</summary>
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
        var chances = GetChances(drummer, ending.Rhythm);
        var play = DrawPlay(drummer, ending.Rhythm, line.Fills, ending.Groove, chances);
        var rhythm = FillRhythm.Of(ending.Groove.Source, play.Layer, minNote);
        var span = Fill(edits, play, line.Position, ending.Groove, rhythm, minNote, ending.SectionId);

        // the drums land after they stopped, as they come back, and always where the song's form marks the line,
        // such as where the band comes in
        IReadOnlyDictionary<DrumRole, double> landings = line.Landing switch
        {
            LandingRule.Forced => FillLayers.SectionLandings.ToImmutableDictionary(x => x.Key, _ => 1.0),
            _ when span > 0 && play.Treatment == GrooveTreatment.Stop
                => FillLayers.SectionLandings.ToImmutableDictionary(x => x.Key, _ => FillLayers.StopLandingChance),
            LandingRule.Section => FillLayers.SectionLandings,
            _ => FillLayers.PhraseLandings
        };
        var landing = _sounds.DrawLanding(_context, landings);
        // a pushed landing comes a note of the fill's rhythm near an 8th early
        var isEarly = _context.TestProbability(Math.Min(1, chances.GetStateValue(CompositionStateKinds.Fill.EarlyLandingChance)));
        var landingPosition = isEarly ? line.Position - rhythm.Push : line.Position;
        Land(song, edits, landingPosition, line.LandingSectionId, landing);
        RecordDecision(ending.SectionId, line.Position - origin, play, rhythm, span, landing, isEarly);
    }

    /// <summary>
    ///     How a fill is played: its span, none for the groove running on, what it does with the groove, its layer over
    ///     the groove's rhythm, its run, and whether it starts off the beat or fades.
    /// </summary>
    /// <param name="chances">The chances of the fills' rarer choices at the line.</param>
    private FillPlay DrawPlay(
        Drummer drummer,
        RhythmicUnconventionality rhythm,
        FillTable table,
        FillGrooves grooves,
        StateMap chances
    )
    {
        var span = table switch
        {
            FillTable.Section => Pick(drummer.WeighSpans(FillLayers.SectionSpans)),
            FillTable.Phrase => Pick(drummer.WeighSpans(FillLayers.PhraseSpans)),
            _ => 0
        };
        if (span <= 0)
            return FillPlay.None;

        var chanceScale = rhythm.ChanceScale;
        ImmutableArray<Weighted<GrooveTreatment>> treatments =
            [..FillLayers.Treatments.Select(x => x.Value == GrooveTreatment.Stop ? x with { Weight = x.Weight * chanceScale } : x)];
        var treatment = Pick(treatments);
        var fullness = (table == FillTable.Phrase ? FillLayers.PhraseFullness : FillLayers.SectionFullness)
                       + FillLayers.TreatmentFullness[treatment];
        var layer = CreateLayer(drummer, rhythm, fullness);
        // where the drums stop, they rest half the time: a break
        var run = treatment == GrooveTreatment.Stop && _context.TestProbability(FillLayers.StopRestChance)
            ? FillRun.Rest
            : _sounds.Draw(_context, drummer, chanceScale, (_, track) => GetRunChance(grooves.Of(track), chanceScale));
        var spanShift = _context.TestProbability(Math.Min(1, chances.GetStateValue(CompositionStateKinds.Fill.OffBeatChance)))
            ? _context.TestProbability(0.5) ? 1 : -1
            : 0;
        var fades = _context.TestProbability(Math.Min(1, chances.GetStateValue(CompositionStateKinds.Fill.FadeChance)));
        return new FillPlay(span, treatment, run, layer, spanShift, fades);
    }

    /// <summary>
    ///     A fill's layer over the groove's rhythm: finer, a step more or less, fuller by the line's and the treatment's
    ///     share and as the drummer plays, its cycles repeating, and the steps of the fill's rhythm layer, as strange as
    ///     the section.
    /// </summary>
    private StateMap CreateLayer(Drummer drummer, RhythmicUnconventionality rhythm, double fullness)
    {
        var layer = rhythm.Scale(RhythmLayers.Fill);
        var density = layer.CreateDensityGenerator();
        var spread = layer.CreateFullnessGenerator();
        return new StateMapBuilder("Fill", perTrack: true)
            .Add(CompositionStateKinds.Rhythm.MaxRank, context => FillLayers.FinerRanks + density(context))
            .Add(CompositionStateKinds.Rhythm.RankOffset, density)
            .Add(CompositionStateKinds.Rhythm.Period.PrimeIndex, layer.CreateTupletGenerator())
            .Add(CompositionStateKinds.Rhythm.Fullness, context => fullness + drummer.FullnessOffset + spread(context))
            .Add(CompositionStateKinds.Rhythm.Variation, -1.0)
            .ToStateMap(_context);
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
        var edits = new TimelineEdits(_context);
        Fill(edits, play, line, groove, FillRhythm.Of(groove.Source, play.Layer, minNote), minNote, 0);
        return edits.ApplyTo(song);
    }

    /// <summary>
    ///     The chances of the fills' rarer choices at a line, as layers: the base, the drummer's, such as its signature,
    ///     and the section's chance scale over them all.
    /// </summary>
    internal StateMap GetChances(Drummer drummer, RhythmicUnconventionality rhythm)
    {
        var section = new StateMapBuilder("Section fill", perTrack: true);
        foreach (var (kind, _) in FillLayers.Chances)
            section.Add(kind, rhythm.ChanceScale);
        return _chances.MergeWith(drummer.Layer ?? StateMap.Default).MergeWith(section.ToStateMap(_context));
    }

    /// <summary>The chance a run plays a drum, as its group's state has it: its run chance, times the chance scale to the power of how unconventional it is.</summary>
    internal static double GetRunChance(StateMap drum, double chanceScale)
    {
        return drum.GetStateValue(CompositionStateKinds.Fill.RunChance)
               * Math.Pow(chanceScale, drum.GetStateValue(CompositionStateKinds.Fill.Unconventionality));
    }

    /// <summary>A run as drawn over the song's own drum states, for the tests to play.</summary>
    internal FillRun DrawRun(Drummer drummer, double chanceScale = 1)
    {
        return _sounds.Draw(_context, drummer, chanceScale, (_, track) => GetRunChance(_songDrums.Of(track), chanceScale));
    }

    /// <summary>What was decided at a line, recorded in the last bar before it, where its fill is.</summary>
    /// <param name="line">Where the line is, from the start of the song's first section.</param>
    private static void RecordDecision(
        int sectionId,
        double line,
        FillPlay play,
        FillRhythm rhythm,
        double span,
        ImmutableArray<RunSound> landing,
        bool isEarly
    )
    {
        if (!StateTrace.IsRunning)
            return;

        var bar = (int)Math.Floor(line / Meter.BarDuration) - 1;
        var description = $"{span} beats, landing {(landing.IsEmpty ? "on nothing" : $"on {string.Join(" ", landing)}")}"
                          + (isEarly ? " early" : "")
                          + (span > 0
                              ? $", {play.Treatment}"
                                + (rhythm.Tuplet != 1 ? $", in {rhythm.Tuplet}s" : "")
                                + $", {rhythm.MaxRank} ranks of {rhythm.Period} beats, fullness {rhythm.Rhythm.Fullness:F2}, {play.Run}"
                                + (play.SpanShift != 0 ? ", off the beat" : "")
                                + (play.Fades ? ", fading" : "")
                              : "");
        StateTrace.Record(
            "Fill decision",
            DrumsTrace,
            sectionId,
            bar.Mod(Progressions.BarCount),
            StateMap.Default,
            span > 0 ? Math.Max(0, Meter.BarDuration - span) : 0,
            description
        );
    }

    private T Pick<T>(ImmutableArray<Weighted<T>> weights)
    {
        return weights[Generators.WeightedIndex(weights)(_context)].Value;
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

        var span = play.Span;
        // a fill shorter than a beat is a note of the fill's rhythm, so that in a tuplet it falls on the tuplet
        if (rhythm.Tuplet != 1 && span < 1)
            span = rhythm.Push;
        // one off the beat starts a note of the fill's rhythm near an 8th earlier or later, and shorter where it is a
        // bar long
        if (play.SpanShift != 0)
            span = span < Meter.BarDuration ? Math.Max(rhythm.Push, span + play.SpanShift * rhythm.Push) : span - rhythm.Push;
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

        var drums = run.Sounds.Select(x => x.Track).Distinct().ToDictionary(x => x, x => FillRhythm.Of(groove.Of(x), play.Layer, minNote));
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
    }

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
/// <param name="Groove">The states of the rhythm the fills play from, in the last bar of the section's 4-bar pattern.</param>
internal sealed record FillSection(int SectionId, double Duration, RhythmicUnconventionality Rhythm, FillGrooves Groove);

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
