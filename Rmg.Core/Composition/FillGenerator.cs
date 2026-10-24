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

    private readonly IGenerationContext _context;

    /// <summary>
    ///     The stream of the line being marked, a line's own by its place, so that a change before it leaves its fill as it
    ///     was; the generator's own until a line is marked, as the tests play one.
    /// </summary>
    private IGenerationContext _line;

    /// <summary>The stream of the line's landing, apart from its fill's, whose draws vary with the fill; the line's own until a line is marked.</summary>
    private IGenerationContext _landing;

    /// <summary>The stream of the line's kick-out, apart from its fill's, so that a genre's chance of it leaves the fills as they were; the line's own until a line is marked.</summary>
    private IGenerationContext _kickOut;

    // the landing's and the kick-out's streams within the line's
    private const int LandingStream = 1;
    private const int KickOutStream = 2;
    private readonly RhythmicUnconventionality _songRhythm;
    private readonly ImmutableArray<int> _drumTracks;

    // every sound of the song's drums a run or a landing may play
    private readonly FillSounds _sounds;

    // the song's drums' own state, before any section's
    private readonly FillGrooves _songDrums;

    // the meter the song's bars are in
    private readonly Meter _meter;
    private readonly Genre _genre;

    // the sequence a drum of the fills and landings that never sounded lands from, by its track (LandTheUnheard)
    private readonly Func<int, IGenerationContext> _unheard;

    // what a section's fills keep on the kick under their runs, by the section
    private readonly Func<int, FillFeet> _feet;

    private const double Sixteenth = 0.25;

    // the drums the fills played, and the landings, as they are marked
    private readonly HashSet<int> _filled = [];
    private readonly List<(double Position, int SectionId, bool IsPercussionOnly, ImmutableArray<RunSound> Sounds)> _landings = [];

    // every line the drums mark, landed on or not, where a drum that never sounded lands where no landing fits it
    private readonly List<(double Position, int SectionId, bool IsPercussionOnly)> _lines = [];

    /// <param name="songRhythm">How far the song's rhythm strays, which makes a drummer's signature likelier.</param>
    /// <param name="meter">The meter the song's bars are in.</param>
    /// <param name="unheard">The sequence a drum of the fills and landings that never sounded lands from, by its track, so that another drum heard or not leaves it (<see cref="LandTheUnheard" />).</param>
    /// <param name="genre">The genre whose pool of the feels a fill's change keeps to.</param>
    /// <param name="feet">What a section's fills keep on the kick under their runs, by the section.</param>
    public FillGenerator(IGenerationContext context, SongTracks tracks, RhythmicUnconventionality songRhythm, Meter meter, Func<int, IGenerationContext> unheard, Genre genre,
        Func<int, FillFeet> feet)
    {
        _feet = feet;
        _meter = meter;
        _genre = genre;
        _unheard = unheard;
        _context = context;
        (_line, _landing, _kickOut) = (context, context, context);
        _songRhythm = songRhythm;
        _drumTracks = [..tracks.SongDrums.Select(DrumGroups.GetTrackNumber)];
        _sounds = new FillSounds(tracks.SongDrums, DrumSounds.OddsOf(genre, GenreChoices.DrumSounds));
        _songDrums = new FillGrooves(
            ResolvedRhythm.DefaultState,
            _drumTracks.ToImmutableDictionary(x => x, x => SongTracks.GetGenerationStateMap(tracks.Definitions[x]))
        );
    }

    /// <summary>Every sound a run may play, by role.</summary>
    internal FillSounds Sounds => _sounds;

    /// <param name="song">The song's sections and its intro and ending, put one after another.</param>
    /// <param name="lines">Every line the drums mark, in the song's order.</param>
    /// <param name="map">Where the song's parts are.</param>
    /// <param name="stops">Where the song takes the drums' groove out, its stop time and its drum solos, so that a drum heard only there is not.</param>
    public TrackEventStateTimelineMap<StateMap> Generate(
        TrackEventStateTimelineMap<StateMap> song,
        IReadOnlyList<FillLine> lines,
        SongMap map,
        IReadOnlyList<(double Start, double End, double BarDuration)> stops
    )
    {
        // a fast section's runs play coarser notes, which finer ones would blur, at the tempo the section the fill ends
        // starts at, before any slowing into the ending
        var tempo = song.CommonStateTimelineMap.GetStateTimeline(StateKinds.Tempo);
        double MinNote(double line) =>
            FillLayers.MinNoteSeconds * Meter.BeatsPerSecond(tempo.GetEffectiveValueAt(map.PlayingAt(line - 1e-6)?.Span.Start ?? 0));

        var drummer = Drummer.Generate(_context, _songRhythm.Value);
        var edits = new TimelineEdits(_context, _meter, map);
        // every line's fill from a stream of its own, by where it is
        var lineSeed = _context.GenerateSeed();
        foreach (var line in lines)
        {
            var key = map.PlaceKey(line.Position);
            (_line, _landing, _kickOut) = (new GenerationContext(Seeds.Derive(lineSeed, key)), new GenerationContext(Seeds.Derive(Seeds.Derive(lineSeed, key), LandingStream)),
                new GenerationContext(Seeds.Derive(Seeds.Derive(lineSeed, key), KickOutStream)));
            MarkLine(song, edits, line, MinNote(line.Position), drummer, map);
        }

        LandTheUnheard(song, edits, stops);

        return edits.ApplyTo(song);
    }

    /// <summary>
    ///     A drum the song has that it never sounded, in its grooves, its runs or its landings, lands once: a drum of the
    ///     fills and landings, such as a vibraslap, which a cymbal outweighs, or one of the groove that the song's form left
    ///     out of every section that had it (<see cref="DrumCoverage" />). It lands on its loudest sound, so that the one
    ///     note tells, as an open hi-hat or a ride's bell, with a landing of its role, as a vibraslap with a crash, or with
    ///     any where none is, into a section it fits, a drum of the kit not into one of percussion only, or on any line
    ///     into such a section where no landing fits it, drawn from a sequence of its own, so that a drum the song has is
    ///     heard.
    /// </summary>
    private void LandTheUnheard(TrackEventStateTimelineMap<StateMap> song, TimelineEdits edits, IReadOnlyList<(double Start, double End, double BarDuration)> stops)
    {
        bool Stopped(double position) => stops.Any(x => position >= x.Start - 1e-9 && position < x.End - 1e-9);
        foreach (var sounds in _sounds.Sounds.Values.SelectMany(x => x.GroupBy(y => y.Track)))
        {
            var drum = DrumGroups.GetDrum(sounds.Key);
            if (_filled.Contains(sounds.Key) || song.TrackTimelineMap.TryGetValue(sounds.Key, out var timeline) && timeline.EventTimeline.Any(x => !Stopped(x.Position)))
                continue;

            var sound = sounds.MaxBy(x => drum.Sounds.First(y => y.Code == x.Code).Loudness)!;
            bool Fits(double position, bool isPercussionOnly) => (!isPercussionOnly || drum.Family.HasFlag(DrumFamily.Percussion)) && !Stopped(position);
            var fits = _landings.Where(x => Fits(x.Position, x.IsPercussionOnly)).ToArray();
            var withRole = fits.Where(x => x.Sounds.Any(y => y.Role == sound.Role)).ToArray();
            // or any line it fits, a drum of the kit where every landing was the percussion's
            var landings = withRole.Length > 0 ? withRole : fits.Length > 0 ? fits
                : [.._lines.Where(x => Fits(x.Position, x.IsPercussionOnly)).Select(x => (x.Position, x.SectionId, x.IsPercussionOnly, ImmutableArray<RunSound>.Empty))];
            if (landings.Length == 0)
                continue;

            var (position, sectionId, _, _) = landings[_unheard(sound.Track).GenerateInt(0, landings.Length)];
            edits.Hit(sound.Track, position, FillLayers.LandingVelocity, sound.Articulation, sectionId, HitKind.Landing);
        }
    }

    /// <summary>
    ///     The lines between a song's sections and in the middle of each, as the fills mark them where the song has no
    ///     intro or ending of its own: a section change before every section but the first, and a phrase line between
    ///     every section's pattern and its repeat.
    /// </summary>
    /// <param name="start">Where the first section starts.</param>
    public static ImmutableArray<FillLine> GetSectionLines(IReadOnlyList<FillSection> sections, double start = 0)
    {
        var lines = ImmutableArray.CreateBuilder<FillLine>();
        for (var i = 0; i < sections.Count; i++)
        {
            // the fill before a line belongs to the section it ends, one out of a breakdown the drums coming back; no line
            // leads into a section whose drums rest, nor marks its phrases
            if (i > 0 && sections[i].HasDrums && sections[i].DrumsFrom == 0)
                lines.Add(new FillLine(start, sections[i - 1], sections[i], 0));

            // a section marks its phrases, but a drum solo, which plays its own (DrumSolos), and those before its drums
            // come in, the line they come in at with a fill
            // every phrase's end, a pattern's the heavier, the drums coming in at a phrase
            var (every, pattern) = (sections[i].Meter.PhraseDuration, sections[i].Meter.PatternDuration);
            for (var line = start + every; sections[i].HasDrums && !sections[i].IsDrumSolo && line < start + sections[i].Duration; line += every)
            {
                // the phrase the line is at the start of, which the drums have come in by or not
                var patterns = (line - start) / pattern;
                var endsPattern = Math.Abs(patterns - Math.Round(patterns)) < 1e-9;
                if ((int)Math.Floor((line - start) / every + 1e-9) >= sections[i].DrumsFrom)
                    lines.Add(new FillLine(line, sections[i], sections[i], endsPattern ? FillLayers.PhraseWeight : FillLayers.InnerPhraseWeight));
            }

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
        SongMap map
    )
    {
        var ending = line.Ending;
        // the fill is in the bars of the section it ends, the bar before the line a short one's where the section cuts it
        var meter = map.MeterAt(line.Position - 1e-6);
        var fills = ending.Fills.Value;
        // how much the line weighs: its own weight, and the energy it leads into, as far as the section's rhythm follows it
        var lift = line.Next.Energy - ending.Energy;
        var weight = line.Weight + lift * ending.Rhythm.Coupling;
        var tilt = Tilt.Of(SectionEnergy.HighOdds, weight);
        // and which way the energy goes, which makes stopping the groove likelier into a quieter section
        var direction = SectionEnergy.Tilt(lift, ending.Rhythm.Coupling);
        var play = line.HasFill ? DrawPlay(drummer, ending, tilt, direction, weight) : FillPlay.None;
        var rhythm = FillRhythm.Of(ending.Groove.Source, play.Layer, minNote, meter);
        _lastRun = [];
        var span = Fill(song, edits, play, line.Position, ending.Groove, rhythm, minNote, ending.SectionId, meter, line.Room);
        // a run into a change of section, another appearance of the same section's too, which the lines walk with
        if (span > 0 && (line.Ending != line.Next || map.Sections.Any(x => Math.Abs(x.Start - line.Position) < 1e-9)) && !_lastRun.IsEmpty)
            Runs.Add(new PlayedRun(line.Position, _lastRun, lift * ending.Rhythm.Coupling));

        // the drums land after they stopped, as they come back, and always where the song's form marks the line,
        // such as where the band comes in; on the drum kit, or on the percussion into a section of percussion only
        var landings = FillLayers.Landings.Where(x => x.Key == FillDrumRole.Percussion == line.Next.IsPercussionOnly).ToImmutableDictionary(
            x => x.Key,
            // a forced landing on the kit and the percussion, a call as often as ever, never on every last chord
            x => line.IsLandingForced && x.Key != FillDrumRole.Calls ? 1
                : span > 0 && play.Treatment == GrooveTreatment.Stop ? FillLayers.StopLandingChance
                : tilt.Chance(x.Value, FillLayers.LandingLean)
        );
        // the landing from a sequence of its own, so that the fill's draws before it leave it
        var landing = _sounds.DrawLanding(_landing, landings, fills);
        // a pushed landing comes a note of the fill's rhythm near an 8th early, after a fill, not where the band stops
        // or counts in and lands on the line together
        var isEarly = _landing.TestProbability(GetChance(drummer, FillRarity.EarlyLanding, fills)) && line.HasFill && span > 0 && !line.IsLandingForced;
        var landingPosition = isEarly ? line.Position - rhythm.Push : line.Position;
        Land(song, edits, landingPosition, line.Next.SectionId, landing);
        _lines.Add((line.Position, line.Next.SectionId, line.Next.IsPercussionOnly));
        if (!landing.IsEmpty)
        {
            _landings.Add((landingPosition, line.Next.SectionId, line.Next.IsPercussionOnly, landing));
            _filled.UnionWith(landing.Select(x => x.Track));
        }
        // the bar of the pattern the fill plays in, the last before the line
        var bar = map.PatternBarAt(line.Position - meter.BarDuration);
        RecordDecision(ending.SectionId, bar, meter, play, rhythm, span, landing, isEarly, lift);
    }

    /// <summary>
    ///     How a fill is played: its span, none for the groove running on, what it does with the groove, its layer over
    ///     the groove's rhythm, its run, and whether it starts off the beat or fades.
    /// </summary>
    /// <param name="tilt">How the line's weight leans the fill: longer on the heavy side.</param>
    /// <param name="direction">How the energy the line leads into leans it: stopping into a quieter section.</param>
    /// <param name="weight">How much the line weighs, which makes the fill fuller or sparser.</param>
    private FillPlay DrawPlay(
        Drummer drummer,
        FillSection section,
        Tilt tilt,
        Tilt direction,
        double weight
    )
    {
        var (rhythm, grooves) = (section.Fills, section.Groove);
        // a genre's drums filling more or less, its spans leaning as the line's weight leans them
        var amount = Tilt.Of(FillLayers.GenreAmountOdds, _genre.Shift(GenreChoices.Fills, "amount"));
        var span = _line.Pick(amount.Weigh(tilt.Weigh(drummer.WeighSpans(FillLayers.Spans), FillLayers.GetSpanLoudness), FillLayers.GetSpanLoudness));
        if (span <= 0)
            return FillPlay.None;

        // the kick taken out, a genre's, from a stream of its own
        if (_kickOut.TestProbability(FillLayers.KickOutChance.At(rhythm.Value, _genre.Chance(GenreChoices.KickOut, "lines"))))
            // a beat at least, as a kick left out for less goes unheard
            return new FillPlay(Math.Max(span, FillLayers.ShortestKickOut), GrooveTreatment.KickOut, FillRun.Rest, CreateLayer(drummer, rhythm, FillLayers.Fullness, Feels.Of(grooves.Source), section.Facets[Facet.Feel]), 0, false);

        // stopping is the unconventional treatment, and the quiet one
        var treatments = RhythmicUnconventionality.Weigh(FillLayers.Treatments, rhythm.Value);
        var treatment = _line.Pick(direction.Weigh(treatments, x => FillLayers.TreatmentLoudness[x]));
        var fullness = FillLayers.Fullness + FillLayers.FullnessPerWeight * weight + FillLayers.TreatmentFullness[treatment];
        var layer = CreateLayer(drummer, rhythm, fullness, Feels.Of(grooves.Source), section.Facets[Facet.Feel]);
        // where the drums stop, they rest half the time: a break
        // a run plays the percussion alone in a section of percussion only, and now and then in one of the drum kit
        var isPercussion = section.IsPercussionOnly ||
                           (_sounds.Sounds.ContainsKey(FillDrumRole.Percussion) && _line.TestProbability(RhythmicUnconventionality.Ends(FillLayers.PercussionRunChance, 1).At(rhythm.Value)));
        var run = treatment == GrooveTreatment.Stop && _line.TestProbability(direction.Chance(FillLayers.StopRestChance, -1))
            ? FillRun.Rest
            : _sounds.Draw(
                _line,
                drummer,
                rhythm.Value,
                (role, track) => !isPercussion ? GetRunChance(grooves.Of(track), rhythm.Value) : role == FillDrumRole.Percussion ? 1 : 0
            );
        var spanShift = _line.TestProbability(GetChance(drummer, FillRarity.OffBeat, rhythm.Value))
            ? _line.TestProbability(0.5) ? 1 : -1
            : 0;
        var fades = _line.TestProbability(GetChance(drummer, FillRarity.Fades, rhythm.Value));
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
        var feelChange = Feels.DrawChange(new GenerationContext(_line.GenerateSeed()), feel, Feels.FillChange, feelUnconventionality, _genre, GenreChoices.Feel);
        return new StateMapBuilder("Fill", perTrack: true)
            .Add(CompositionStateKinds.Rhythm.MaxRank, context => FillLayers.FinerRanks + density(context))
            .Add(CompositionStateKinds.Rhythm.RankOffset, density)
            .Add(CompositionStateKinds.Rhythm.Fullness, context => fullness + drummer.FullnessOffset + spread(context))
            .Add(CompositionStateKinds.Rhythm.Variation, -1.0)
            .ToStateMap(_line)
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
        var edits = new TimelineEdits(_line, _meter, null);
        Fill(song, edits, play, line, groove, FillRhythm.Of(groove.Source, play.Layer, minNote, _meter), minNote, 0, _meter, double.PositiveInfinity);
        return edits.ApplyTo(song);
    }

    /// <summary>
    ///     A rarer choice's chance at a line: its base chance at the section's fills facet, as an unconventional thing is,
    ///     never at the plain end and every time at the wild, its odds the drummer's signature's weight where it is the
    ///     drummer's signature.
    /// </summary>
    internal static double GetChance(Drummer drummer, FillRarity choice, double unconventionality)
    {
        var chance = FillLayers.Chances.Single(x => x.Choice == choice).Chance;
        return Tilt.Of(FillLayers.SignatureWeight, drummer.Signature == choice ? 1 : 0).Chance(RhythmicUnconventionality.Ends(chance, 1).At(unconventionality), 1);
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
        return _sounds.Draw(_line, drummer, unconventionality, (_, track) => GetRunChance(_songDrums.Of(track), unconventionality));
    }

    /// <summary>What was decided at a line, recorded in the last bar before it, where its fill is.</summary>
    /// <param name="line">Where the line is, from the start of the song's first section.</param>
    private void RecordDecision(
        int sectionId,
        int bar,
        Meter meter,
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
            bar,
            StateMap.Default,
            span > 0 ? Math.Max(0, meter.BarDuration - span) : 0,
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
        TrackEventStateTimelineMap<StateMap> song,
        TimelineEdits edits,
        FillPlay play,
        double line,
        FillGrooves groove,
        FillRhythm rhythm,
        double minNote,
        int sectionId,
        Meter meter,
        double room
    )
    {
        if (play.Span <= 0)
            return 0;

        // the bar's last node of the span's length in four
        var span = meter.SpanOf(play.Span);
        // a fill shorter than a beat is a note of the fill's rhythm, so that in a tuplet it falls on the tuplet
        if (rhythm.Tuplet != 1 && span < 1)
            span = rhythm.Push;
        // one off the beat starts a note of the fill's rhythm near an 8th earlier or later, and shorter where it is a
        // bar long
        if (play.SpanShift != 0)
            span = span < meter.BarDuration ? Math.Max(rhythm.Push, span + play.SpanShift * rhythm.Push) : span - rhythm.Push;
        // no further back than the line leaves it room
        span = Math.Min(span, room);
        var from = line - span;
        // a kick-out only where the kick plays into the line and the rest of the groove plays on, the groove kept where the
        // kick plays alone, as in an intro's bar of the kick and the bass, which taking it out would leave silent, and where
        // the kick plays none, which leaves nothing to take out
        var kick = DrumGroups.GetTrackNumber(DrumDefinitions.Kick);
        bool Strikes(int track) => song.TrackTimelineMap.TryGetValue(track, out var timeline)
                                   && timeline.EventTimeline.Any(x => x.Position >= from - 1e-9 && x.Position < line - 1e-9 && edits.Strikes(song, track, x.Position));
        if (play.Treatment == GrooveTreatment.KickOut && (!Strikes(kick) || !_drumTracks.Any(x => x != kick && Strikes(x))))
            play = play with { Treatment = GrooveTreatment.Keep };

        var cleared = play.Treatment switch
        {
            GrooveTreatment.Stop => _drumTracks,
            GrooveTreatment.Played => play.Run.Sounds.Select(x => x.Track).Distinct(),
            GrooveTreatment.KickOut => _drumTracks.Where(x => x == DrumGroups.GetTrackNumber(DrumDefinitions.Kick)),
            _ => []
        };
        // the kick kept under a run or a stop where the section's fills keep their feet, its groove's or a double kick's
        // 16ths, where the groove plays the kick
        var feet = play.Treatment is GrooveTreatment.Played or GrooveTreatment.Stop && Strikes(kick) ? _feet(sectionId) : FillFeet.None;
        foreach (var track in cleared.Where(x => x != kick || feet != FillFeet.Groove))
            edits.Clear(track, from, line);
        if (feet != FillFeet.None)
            StateTrace.RecordSection(TracePoints.FillFeet, DrumsTrace, sectionId, $"{feet} {from}-{line}", (from, line, feet));
        if (feet == FillFeet.DoubleKick)
        {
            edits.Clear(kick, from, line);
            // from the song's start at the earliest, a fill into its first section reaching before it
            for (var at = Math.Max(0, Math.Ceiling(from / Sixteenth - 1e-9) * Sixteenth); at < line - 1e-9; at += Sixteenth)
                edits.Hit(kick, at, 0, 0, sectionId, HitKind.Fill);
        }
        if (play.Treatment == GrooveTreatment.KickOut)
            KickOuts.Add((from, line));

        PlayRun(edits, play, from, line, groove, rhythm, minNote, sectionId, meter);
        // a fill where it plays a run or stops the groove, a break whose run rests among them, which the stages that play
        // around a fill keep out of; a kick-out alone, its groove playing on, is a kick-out's
        if (play.Treatment == GrooveTreatment.Stop || !_lastRun.IsEmpty)
            Spans.Add((from, line));
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
        int sectionId,
        Meter meter
    )
    {
        var run = play.Run;
        if (run.Sounds.IsEmpty)
            return;

        var (startVelocity, endVelocity) = play.Fades
            ? (FillLayers.RunEndVelocity, FillLayers.RunStartVelocity)
            : (FillLayers.RunStartVelocity, FillLayers.RunEndVelocity);

        var seed = _line.GenerateSeed();
        var notes = new List<(double Position, int Rank, int MaxRank)>();
        if (run.Speed != FillSpeed.Steady)
        {
            // a rank coarser for one half, folded into range; the halves meet on a note of the coarser, so that a
            // tuplet's notes stay on it
            var coarser = rhythm.Step(-1);
            var (first, second) = run.Speed == FillSpeed.SpeedsUp ? (coarser, rhythm.MaxRank) : (rhythm.MaxRank, coarser);
            var note = rhythm.NoteAt(Math.Min(first, second));
            var middle = from + Math.Round((line - from) / 2 / note) * note;
            notes.AddRange(rhythm.Play(_line, seed, line, from, middle, first));
            notes.AddRange(rhythm.Play(_line, Seeds.Derive(seed, 1), line, middle, line, second));
        }
        else
            notes.AddRange(rhythm.Play(_line, seed, line, from, line, rhythm.MaxRank));

        var drums = run.Sounds.Select(x => x.Track).Distinct().ToDictionary(x => x, x => FillRhythm.Of(groove.Of(x), play.Layer, minNote, meter));
        var places = FillSounds.Walk(_line, run.Path, run.Sounds.Length, notes.Count);
        var span = line - from;
        for (var k = 0; k < notes.Count; k++)
        {
            var (position, rank, maxRank) = notes[k];
            var loudness = startVelocity + (endVelocity - startVelocity) * (position - from) / span
                           // a run's accents its own, as a drummer's hands make them, whatever the groove's dynamics, as evenly as
                           // the genre plays
                           + FillLayers.AccentWeight * BeatAccent.CreateVelocityGenerator(rank, maxRank, 1, VelocityLayers.SpreadOf(_genre))(_line);
            foreach (var sound in FillSounds.GetNoteSounds(run, places[k], x => drums[x].Has(position, line)))
            {
                edits.Hit(sound.Track, position, loudness, sound.Articulation, sectionId, HitKind.Fill);
                _filled.Add(sound.Track);
            }
        }

        _lastRun = [..notes.Select(x => x.Position).Distinct().Order()];
    }

    // the positions of the run the last fill played, which a section change keeps for the pitched fills
    private ImmutableArray<double> _lastRun = [];

    /// <summary>The runs the drums played into a change of section, for the pitched tracks to fill with (<see cref="LineFills" />).</summary>
    public List<PlayedRun> Runs { get; } = [];

    /// <summary>Where the drums fill, from the fill's start to its line, every line's, which the stages that play around a fill keep out of (<see cref="Hold.Fill" />).</summary>
    public List<(double From, double To)> Spans { get; } = [];

    /// <summary>Where the kick is taken out, from the span's start to its line, which the bass leaves with it (<see cref="SongStages.KickOutBass" />).</summary>
    public List<(double From, double To)> KickOuts { get; } = [];

    /// <summary>The hits the drums land on at a line; a drum that plays there still, the edits made, keeps its note.</summary>
    private static void Land(
        TrackEventStateTimelineMap<StateMap> song,
        TimelineEdits edits,
        double position,
        int sectionId,
        ImmutableArray<RunSound> landing
    )
    {
        foreach (var sound in landing.Where(x => !edits.Strikes(song, x.Track, position)))
            edits.Hit(sound.Track, position, FillLayers.LandingVelocity, sound.Articulation, sectionId, HitKind.Landing);
    }
}

/// <summary>
///     How a fill is played: how long it is, none for the groove running on, what it does with the groove, its run, its
///     layer over the groove's rhythm, and whether it starts off the beat or fades.
/// </summary>
/// <param name="SpanShift">How many notes near an 8th it starts earlier, or later where negative; a bar-long one always later.</param>
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

/// <summary>A run the drums played into a change of section: the line, the run's notes' positions, and the lift, the energy's rise into it as far as the rhythm follows it.</summary>
internal sealed record PlayedRun(double Line, ImmutableArray<double> Positions, double Lift);

/// <summary>A line the drums mark: where it is, the section it ends, the one that starts there, and how it weighs.</summary>
/// <param name="Ending">The section the line ends, whose rhythm and feel its fill plays in.</param>
/// <param name="Next">The section that starts at the line, which the drums land in.</param>
/// <param name="Weight">How much the line weighs before the energy it leads into (<see cref="FillLayers.PhraseWeight" />).</param>
/// <param name="HasFill">Whether a fill may play before it; none where the song's form has the drums wait.</param>
/// <param name="IsLandingForced">Whether the drums always land on it, as where the band comes in.</param>
/// <param name="Room">How far before it a fill may start at the most, in beats, as where one part opens the song alone.</param>
internal sealed record FillLine(
    double Position,
    FillSection Ending,
    FillSection Next,
    double Weight,
    bool HasFill = true,
    bool IsLandingForced = false,
    double Room = double.PositiveInfinity
)
{
    /// <summary>The lift into the section the line starts (<see cref="FillSection.LiftInto" />).</summary>
    public double Lift => Ending.LiftInto(Next);

    /// <summary>Whether the line leads into another section louder enough to be lifted into, as its cues, drops and hits are (<see cref="LiftCues.Threshold" />).</summary>
    public bool Lifts => Ending != Next && Lift > LiftCues.Threshold;
}

/// <summary>
///     A section as the fills see it: where it is, how far its rhythm strays, the drums' feel before its lines, and its
///     energy.
/// </summary>
/// <param name="Groove">The states of the rhythm the fills play from, in the last bar of the section's pattern.</param>
/// <param name="Energy">How loud and busy the section is meant to be (<see cref="SectionEnergy" />).</param>
/// <param name="IsPercussionOnly">Whether the section plays its percussion without the drum kit.</param>
/// <param name="Meter">The meter the section's bars are in, which its fills are counted in.</param>
internal sealed record FillSection(
    int SectionId,
    double Duration,
    RhythmicUnconventionality Rhythm,
    Unconventionality Facets,
    FillGrooves Groove,
    double Energy,
    bool IsPercussionOnly,
    bool HasDrums,
    bool IsDrumSolo,
    Meter Meter,
    int DrumsFrom
)
{
    /// <summary>How far the section's fills stray from convention, which their choices lean by.</summary>
    public RhythmicUnconventionality Fills => new(Facets[Facet.Fills]);

    /// <summary>The lift into a section after it, the energy's rise as far as its rhythm follows it, which the band grows louder by and its cues mark.</summary>
    public double LiftInto(FillSection next) => (next.Energy - Energy) * Rhythm.Coupling;
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
