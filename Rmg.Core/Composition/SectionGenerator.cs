using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     A section of a song: its harmony (its unconventionality, chords, home and progression), its state over the
///     song's, the drums it plays, the state that changes by bar, and the patterns of its tracks, played twice.
/// </summary>
internal sealed class SectionGenerator
{
    /// <summary>The track of a trace entry that records a decision for the whole section, such as its energy.</summary>
    public const int SectionTrace = -2;

    // the stream of the song's own shift of its lines' freedom to change register, apart from every section's
    private const int SongRegisterFreedomStream = -1;

    private readonly IGenerationContext _context;
    private readonly int _seed;
    private readonly SongTracks _tracks;
    private readonly HarmonicUnconventionality _songUnconventionality;
    private readonly RhythmicUnconventionality _songRhythmicUnconventionality;
    private readonly MelodyBusyness _songMelodyBusyness;
    private readonly Scale _songScale;
    private readonly StateMap _songStateMap;
    private readonly ImmutableDictionary<int, StateMap> _sectionEnergies;
    private readonly Tilt _songPercussion;
    private readonly int _key;
    private readonly BarStateGenerator _barStateGenerator;
    private readonly PatternGenerator _patternGenerator;

    // the song's shift of its lines' freedom to change register, before a section's, in spreads (LineProfile.RegisterFreedomSpread)
    private readonly double _songRegisterFreedomShift;


    /// <param name="seed">The seed of the sections' random sequences, from which every section derives its own by its id.</param>
    /// <param name="songPercussion">How far the song leans to sections of percussion only (<see cref="PercussionSections" />).</param>
    public SectionGenerator(
        IGenerationContext context,
        int seed,
        ProgressionSettings settings,
        SongTracks tracks,
        HarmonicUnconventionality songUnconventionality,
        RhythmicUnconventionality songRhythmicUnconventionality,
        MelodyBusyness songMelodyBusyness,
        Scale songScale,
        StateMap songStateMap,
        ImmutableDictionary<int, StateMap> sectionEnergies,
        Tilt songPercussion,
        int key
    )
    {
        _context = context;
        _seed = seed;
        _tracks = tracks;
        _songUnconventionality = songUnconventionality;
        _songRhythmicUnconventionality = songRhythmicUnconventionality;
        _songMelodyBusyness = songMelodyBusyness;
        _songScale = songScale;
        _songStateMap = songStateMap;
        _sectionEnergies = sectionEnergies;
        _songPercussion = songPercussion;
        _key = key;
        _songRegisterFreedomShift = Generators.SplineValue()(context.CreateContext(Seeds.Derive(seed, SongRegisterFreedomStream)));
        _barStateGenerator = new BarStateGenerator(settings);
        _patternGenerator = new PatternGenerator(context, tracks.Definitions);
    }

    /// <summary>The random sequence a decision of a section draws from, apart from its own and every other decision's.</summary>
    private IGenerationContext Stream(int sectionId, SectionStream stream)
    {
        return _context.CreateContext(StreamSeed(sectionId, stream));
    }

    /// <summary>The seed of <see cref="Stream" />, for a decision that derives its own sequences from it.</summary>
    private int StreamSeed(int sectionId, SectionStream stream)
    {
        return Seeds.Derive(Seeds.Derive(_seed, sectionId), (int)stream);
    }

    /// <summary>The section: its 4-bar pattern played twice, with what the fills need to know of its rhythm.</summary>
    /// <param name="plan">The section's place in the song's form, which some of its draws keep to.</param>
    public GeneratedSection Generate(SectionPlan plan)
    {
        // every section draws from its own sequence, so a change to one leaves the others as they are
        var sectionId = plan.Id;
        var context = _context.CreateContext(Seeds.Derive(_seed, sectionId));
        var unconventionality = _songUnconventionality.GenerateSection(context);
        var rhythm = _songRhythmicUnconventionality.GenerateSection(context);
        var chords = LayerStates.CreateChordPool(unconventionality)(context);

        var (songStateMap, energy) = GetEnergy(sectionId, rhythm);
        var tilt = SectionEnergy.Tilt(energy, rhythm.Coupling);
        var scale = plan.KeepsSongScale ? _songScale : PickScale(sectionId, unconventionality, energy);
        StateTrace.Record(TracePoints.SectionScale, SectionTrace, sectionId, 0, StateMap.Default, 0, scale.Name, scale);

        // the section's chords move around its home, which every track's root starts from; the song's last section
        // leads home to its tonic, where the song ends
        var home = Progressions.GenerateHome(context, scale, plan.Role);
        if (plan.HasTonicHome)
            home = 0;
        // how often its chords change, from a sequence of its own, faster the more energy it has
        var harmonicRhythm = HarmonicRhythm.Draw(Stream(sectionId, SectionStream.HarmonicRhythm), tilt);
        StateTrace.Record(TracePoints.HarmonicRhythm, SectionTrace, sectionId, 0, StateMap.Default, 0, $"{harmonicRhythm.Span}", harmonicRhythm);
        var progression = Progressions.Generate(context, scale, home, unconventionality.ProgressionStrictness, harmonicRhythm.Count);

        var sectionStateMap = CreateSectionStateMap(
            songStateMap,
            new StateMapBuilder("Section")
                .AddRhythmLayer(rhythm.Lean(RhythmLayers.Section))
                .AddNoteWalkLayer()
                .Add(CompositionStateKinds.ChordPool.Index, LayerStates.ChordPoolIndex)
                .Add(StateKinds.Velocity, VelocityLayers.CreateGenerator(VelocityLayers.Section, tilt))
                .Add(CompositionStateKinds.NoteDynamics, Math.Pow(rhythm.ChanceScale, VelocityLayers.DynamicsLean))
                .AddNoteDurationLayer()
                .ToStateMap(context),
            new StateMapBuilder("Section")
                .Add(CompositionStateKinds.ChordPool.Collection, chords)
                .Add(StateKinds.ChordRoot, home)
                .ToStateMap(context)
        );
        // the roles the section draws again for the song's drums, as their parts in its grooves and its fills, from a
        // sequence of its own
        var roleContext = Stream(sectionId, SectionStream.DrumRoles);
        var sectionRoles = _tracks.SongDrums.Select(DrumGroups.GetTrackNumber).Order()
            .ToImmutableDictionary(x => x, x => DrumRoles.DrawSection(roleContext, DrumGroups.GetDrum(x), SongRole(x), rhythm.Tilt));
        StateTrace.Record(
            TracePoints.DrumRoles,
            SectionTrace,
            sectionId,
            0,
            StateMap.Default,
            0,
            string.Join(", ", sectionRoles.Where(x => !x.Value.IsDefault).Select(x => $"{x.Key} {(DrumRole)x.Value.GetStateValue(CompositionStateKinds.DrumRole).Value}")),
            sectionRoles.Where(x => !x.Value.IsDefault).ToImmutableDictionary(x => x.Key, x => (DrumRole)x.Value.GetStateValue(CompositionStateKinds.DrumRole).Value)
        );

        // the drums: the kit's or, now and then, the percussion's alone, by the roles they play, each from a sequence of
        // its own
        var percussionContext = Stream(sectionId, SectionStream.Percussion);
        var songPercussion = _tracks.SongDrums.Count(DrumGroups.Percussion.Drums.Contains);
        // a percussion song's every section, and now and then a section of a song of the kit and percussion
        var isPercussionOnly = _tracks.DrumSetup == DrumSetup.Percussion ||
                               (_tracks.DrumSetup == DrumSetup.KitAndPercussion &&
                                PercussionSections.Draw(percussionContext, _songPercussion, rhythm.Tilt, tilt, songPercussion));
        StateTrace.Record(TracePoints.PercussionOnly, SectionTrace, sectionId, 0, StateMap.Default, 0, isPercussionOnly ? "percussion only" : "drum kit", isPercussionOnly);
        var kit = DrumKitGenerator.SelectKit(
            Stream(sectionId, SectionStream.Kit),
            _tracks.SongDrums,
            tilt,
            isPercussionOnly
        );
        StateTrace.Record(TracePoints.Kit, SectionTrace, sectionId, 0, StateMap.Default, 0, string.Join(", ", kit.Drums.Select(x => x.Name)), kit);
        var activeDrumTrackNumbers = kit.Drums.Select(DrumGroups.GetTrackNumber).ToImmutableHashSet();
        // a drum that doubles a lead plays its lead's beats up to the rank its role doubles, all of them, a share, or a
        // figure of its own on the lead's feel, as it is bound to it, drawn from a sequence of its own, leaning to its own
        // figure the less conventional the section
        var bindingContext = Stream(sectionId, SectionStream.DrumBindings);
        var doubles = kit.Doubles.OrderBy(x => DrumGroups.GetTrackNumber(x.Key)).ToImmutableDictionary(
            x => DrumGroups.GetTrackNumber(x.Key),
            x => new Doubling(
                DrumGroups.GetTrackNumber(x.Value),
                DrumRoles.DoublingRanks[x.Value.MainRole],
                bindingContext.Pick(rhythm.Tilt.Weigh(x.Key.Bindings, binding => binding == DrumBinding.Figure ? 1 : 0)),
                DrumKitGenerator.AccentShare
            )
        );
        StateTrace.Record(TracePoints.Doubles, SectionTrace, sectionId, 0, StateMap.Default, 0, string.Join(", ", kit.Doubles.Select(x => $"{x.Key.Name} on {x.Value.Name}")), doubles);
        // a drum that does not lead plays on its lead's feel: one bound to it always, one that colours the section on the
        // lead of its main role's by a chance, drawn from a sequence of its own, crossing it the less conventional the section
        var feelContext = Stream(sectionId, SectionStream.DrumFeels);
        var feelLeads = ImmutableDictionary.CreateBuilder<int, int>();
        foreach (var drum in kit.Drums.Except(kit.Leads).OrderBy(DrumGroups.GetTrackNumber))
        {
            var track = DrumGroups.GetTrackNumber(drum);
            if (doubles.TryGetValue(track, out var doubling))
                feelLeads[track] = doubling.Lead;
            else if (kit.Leads.FirstOrDefault(x => x.MainRole == drum.MainRole) is { } lead && feelContext.TestProbability(rhythm.Tilt.Chance(DrumKitGenerator.FeelChance, -1)))
                feelLeads[track] = DrumGroups.GetTrackNumber(lead);
        }

        StateTrace.Record(TracePoints.FeelLeads, SectionTrace, sectionId, 0, StateMap.Default, 0, string.Join(", ", feelLeads.Select(x => $"{DrumGroups.GetDrum(x.Key).Name} on {DrumGroups.GetDrum(x.Value).Name}")), feelLeads.ToImmutable());

        // the section state reaches the notes through the track state maps, so the bar state holds only the state
        // that changes by bar
        var bassLeading = Math.Clamp(
            _tracks.BassLeading + BassLeadingLayers.CreateGenerator(BassLeadingLayers.Section)(context),
            0,
            1
        );
        var barStateTimelineMap = _barStateGenerator.Generate(context, scale, progression, harmonicRhythm, home, unconventionality, bassLeading, rhythm.Tilt);
        var contour = barStateTimelineMap.GetStateTimeline(CompositionStateKinds.LineRegister).Select(x => x.Value).ToImmutableArray();
        StateTrace.Record(TracePoints.MelodyContour, SectionTrace, sectionId, 0, StateMap.Default, 0, string.Join(", ", contour), contour);

        // every track follows the section's phrase scheme, so they repeat their bars in the same places
        var scheme = PhraseSchemes.Pick(context, rhythm);
        var sectionRhythm = new SectionRhythm(rhythm, scheme, _songMelodyBusyness.GenerateSection(context, tilt), tilt);

        // the strokes the section changes from the song's, and how its drums play its bars, each from a sequence of its own
        var strokeContext =Stream(sectionId, SectionStream.DrumStrokes);
        var sectionStrokes = ImmutableDictionary.CreateBuilder<int, int>();
        foreach (var track in activeDrumTrackNumbers.Order().Where(x => DrumGroups.GetDrum(x).HasStrokes))
            if (DrumStrokes.DrawChange(strokeContext, DrumGroups.GetDrum(track), SongStroke(track), DrumStrokes.SectionChangeChance, rhythm.Tilt, tilt) is { } change)
                sectionStrokes[track] = change;
        StateTrace.Record(TracePoints.DrumStrokes, SectionTrace, sectionId, 0, StateMap.Default, 0, string.Join(", ", sectionStrokes), sectionStrokes.ToImmutable());
        var barDrums = DrumPresence.Draw(
            Stream(sectionId, SectionStream.DrumPresence),
            activeDrumTrackNumbers,
            scheme,
            kit.Leads.Select(DrumGroups.GetTrackNumber).ToHashSet(),
            track => sectionStrokes.TryGetValue(track, out var stroke) ? stroke : SongStroke(track),
            rhythm.Tilt,
            tilt
        );
        StateTrace.Record(TracePoints.DrumPresence, SectionTrace, sectionId, 0, StateMap.Default, 0, $"{string.Join(", ", barDrums.Resting)}; {string.Join(", ", barDrums.Strokes)}", barDrums);

        var drums = GenerateDrums(context, sectionId, sectionStateMap, activeDrumTrackNumbers, doubles, feelLeads.ToImmutable(), sectionRoles, sectionStrokes.ToImmutable(), barDrums, barStateTimelineMap, sectionRhythm).ToArray();
        var pitched = GeneratePitchedTracks(context, sectionId, sectionStateMap, barStateTimelineMap, harmonicRhythm, sectionRhythm).ToArray();
        // the section's pattern played once, twice or four times, its melody as a question and its answer, from a sequence
        // of its own, the less conventional the section the likelier it plays other than twice
        var plays = SectionLength.Draw(Stream(sectionId, SectionStream.Length), rhythm.Tilt, plan.Role);
        StateTrace.Record(TracePoints.SectionLength, SectionTrace, sectionId, 0, StateMap.Default, 0, $"{plays}", plays);
        var timeline = KeepRenderState([..drums.Select(x => x.Timeline), ..pitched.Select(x => x.Bars.Timeline)], barStateTimelineMap).Repeat(plays);
        // the parts the section leaves out the first time it plays, from a sequence of its own, the likelier the less energy
        // it has; a later appearance draws them again (Appear)
        var arrangementSeed = StreamSeed(sectionId, SectionStream.Arrangement);
        var resting = Arrangement.DrawRests(new GenerationContext(arrangementSeed), tilt, plan.Role);
        ImmutableArray<SectionLine> lines = [..pitched.Select(x => x.Line).OfType<SectionLine>()];
        var section = new GeneratedSection(
            timeline,
            rhythm,
            GetGrooves(drums.SelectMany(x => x.Feels)),
            energy,
            isPercussionOnly,
            sectionRoles.Keys.ToImmutableDictionary(x => x, x => SectionRole(sectionRoles, x)),
            lines,
            plays,
            doubles.ToImmutableDictionary(x => x.Key, x => x.Value.Lead),
            resting,
            plan.Role,
            arrangementSeed,
            _tracks.Definitions.ToImmutableDictionary(x => x.Key, x => x.Value.Role)
        );
        // every part, which each appearance leaves out of it what it rests
        return section;
    }

    /// <summary>
    ///     How loud and busy the section is meant to be: the song's state with the section's layers of the energy, and
    ///     their sum, which leans the section's draws as far as its rhythm follows it.
    /// </summary>
    private (StateMap SongStateMap, double Energy) GetEnergy(int sectionId, RhythmicUnconventionality rhythm)
    {
        var songStateMap = _songStateMap.MergeWith(_sectionEnergies[sectionId]);
        var energy = songStateMap.GetStateValue(CompositionStateKinds.Energy);
        StateTrace.Record(
            TracePoints.SectionEnergy,
            SectionTrace,
            sectionId,
            0,
            songStateMap.Subset([CompositionStateKinds.Energy]),
            0,
            $"energy {energy:F2}, pull {energy * rhythm.Coupling:F2}",
            new SectionEnergyTrace(energy, energy * rhythm.Coupling)
        );
        return (songStateMap, energy);
    }

    /// <summary>The role a drum plays in the section: its own, where it draws the drum's again, or the song's.</summary>
    private DrumRole SectionRole(ImmutableDictionary<int, StateMap> sectionRoles, int track)
    {
        return sectionRoles[track].IsDefault ? SongRole(track) : (DrumRole)sectionRoles[track].GetStateValue(CompositionStateKinds.DrumRole).Value;
    }

    /// <summary>Every drum's state, a drum that doubles a lead with the lead's rhythm in place of its own.</summary>
    private static ImmutableDictionary<int, StateMap> DoubleLeads(Dictionary<int, StateMap> trackStateMaps, ImmutableDictionary<int, Doubling> doubles)
    {
        var rhythm = CompositionStateKinds.Rhythm.All;
        return trackStateMaps.ToImmutableDictionary(
            x => x.Key,
            x => doubles.TryGetValue(x.Key, out var doubling)
                ? x.Value.Except(rhythm).MergeWith(trackStateMaps[doubling.Lead].Subset(rhythm))
                : x.Value
        );
    }

    /// <summary>The role a drum plays in the song, before a section draws it again.</summary>
    private DrumRole SongRole(int track)
    {
        return (DrumRole)_tracks.Definitions[track].StateMap.GetStateValue(CompositionStateKinds.DrumRole).Value;
    }

    /// <summary>The stroke a drum that has strokes plays in the song, before a section changes it.</summary>
    private int SongStroke(int track)
    {
        return _tracks.Definitions[track].StateMap.GetStateValue(StateKinds.DrumStroke).Value;
    }

    /// <summary>
    ///     The section's scale: the song's, or now and then another on its tonic, leaning brighter the more energy the
    ///     section has, as far as its harmony follows it; drawn from a sequence of its own, so that the section's other
    ///     draws stay as they are.
    /// </summary>
    private Scale PickScale(int sectionId, HarmonicUnconventionality harmony, double energy)
    {
        return Scales.PickSection(
            Stream(sectionId, SectionStream.Scale),
            _songScale,
            harmony,
            SectionEnergy.Tilt(energy, harmony.Coupling)
        );
    }

    /// <summary>
    ///     The section's tracks as the song keeps them: what Render reads of their notes, now that the melody is placed,
    ///     and of the bar state, whose state for the generation, such as the chord pool's pick, stays here.
    /// </summary>
    private static TrackEventStateTimelineMap<StateMap> KeepRenderState(
        IEnumerable<TrackEventStateTimelineMap<StateMap>> tracks,
        StateTimelineMap barStateTimelineMap
    )
    {
        var timeline = TrackEventStateTimelineMap.Merge(
            [..tracks, barStateTimelineMap.OfScope(StateScope.Render).ToTrackEventStateTimelineMap<StateMap>(Meter.PatternDuration)]
        );
        return timeline.MapTrackEvents(
            timeline.TrackTimelineMap.Keys.ToDictionary(
                x => x,
                _ => (Func<EventTimeline<StateMap>, EventTimeline<StateMap>>)(notes => notes.MapValues(x => x.OfScope(StateScope.Render)))
            )
        );
    }

    /// <summary>
    ///     The rhythm the fills play from, in the pattern's last bar, before both of the section's lines: every drum's
    ///     state, which sets the notes of a run the drum plays, and the snare's, which the run's rhythm is, or where the
    ///     section plays no snare, that of the drum with the most notes there.
    /// </summary>
    internal static FillGrooves GetGrooves(IEnumerable<BarFeel> feels)
    {
        var snares = DrumGroups.Snare.Drums.Select(DrumGroups.GetTrackNumber).ToHashSet();
        var lastBar = feels.Where(x => x.Bar == Progressions.BarCount - 1).ToArray();
        var source = lastBar
            .Where(x => x.NoteCount > 0)
            .OrderByDescending(x => snares.Contains(x.Track))
            .ThenByDescending(x => x.NoteCount)
            .ThenBy(x => x.Track)
            .Select(x => x.Rhythm)
            .FirstOrDefault() ?? ResolvedRhythm.DefaultState;
        return new FillGrooves(source, lastBar.ToImmutableDictionary(x => x.Track, x => x.Rhythm));
    }

    /// <summary>The drums the section plays, which make their patterns together, over the drums' shared state.</summary>
    private IEnumerable<GeneratedBars> GenerateDrums(
        IGenerationContext context,
        int sectionId,
        StateMap sectionStateMap,
        ImmutableHashSet<int> activeDrumTrackNumbers,
        ImmutableDictionary<int, Doubling> doubles,
        ImmutableDictionary<int, int> feelLeads,
        ImmutableDictionary<int, StateMap> sectionRoles,
        ImmutableDictionary<int, int> sectionStrokes,
        BarDrums barDrums,
        StateTimelineMap barStateTimelineMap,
        SectionRhythm sectionRhythm
    )
    {
        foreach (var group in _tracks.Groups)
        {
            var groupStateMap = new StateMapBuilder("Section drum group", perTrack: true)
                .AddRhythmLayer(sectionRhythm.Unconventionality.Lean(RhythmLayers.SectionDrumGroup).Tilted(sectionRhythm.Energy))
                .AddNoteWalkLayer()
                .Add(StateKinds.Velocity, VelocityLayers.CreateGenerator(VelocityLayers.SectionDrumGroup))
                .AddNoteDurationLayer()
                .ToStateMap(context)
                .MergeWith(group.StateMap)
                .MergeWith(sectionStateMap);
            var trackStateMaps = new Dictionary<int, StateMap>();
            foreach (var trackNumber in group.TrackNumbers.Where(activeDrumTrackNumbers.Contains))
                trackStateMaps[trackNumber] = CreateSectionTrackLayer(context, trackNumber, sectionRhythm, sectionRhythm.Energy)
                    .MergeWith(groupStateMap)
                    .MergeWith(sectionRoles[trackNumber])
                    .MergeWith(sectionStrokes.TryGetValue(trackNumber, out var stroke) ? DrumStrokes.At(StateDepths.Section, stroke) : StateMap.Default);

            // a drum out of the groove still has the state the drums share, with no notes, so that a note added
            // later, such as in a fill, plays as loud as the section, and its own rhythm's state, which a fill plays it by
            var idleStateMap = groupStateMap.OfScope(StateScope.Render);
            var idleTrackNumbers = group.TrackNumbers.Where(x => !trackStateMaps.ContainsKey(x)).ToArray();
            yield return new GeneratedBars(TrackEventStateTimelineMap.Create(
                Meter.PatternDuration,
                idleTrackNumbers.Select(x => new KeyValuePair<int, EventStateTimelineMap<StateMap>>(
                        x,
                        EventTimeline.Create<StateMap>(Meter.PatternDuration).ToEventStateTimelineMap(idleStateMap)
                    )
                ),
                StateTimelineMap.Create(Meter.PatternDuration)
            ), [
                ..idleTrackNumbers.Select(x => new BarFeel(
                        x,
                        Progressions.BarCount - 1,
                        SongTracks.GetGenerationStateMap(_tracks.Definitions[x]).MergeWith(groupStateMap).MergeWith(sectionRoles[x]),
                        0
                    )
                )
            ]);

            if (trackStateMaps.Count == 0)
                continue;

            yield return _patternGenerator.GenerateBars(context, sectionId, DoubleLeads(trackStateMaps, doubles), barDrums, doubles, feelLeads, barStateTimelineMap, sectionRhythm, null);
        }
    }

    /// <summary>The pitched tracks, each making its patterns on its own, over the section's state.</summary>
    private IEnumerable<(int Track, GeneratedBars Bars, SectionLine? Line)> GeneratePitchedTracks(
        IGenerationContext context,
        int sectionId,
        StateMap sectionStateMap,
        StateTimelineMap barStateTimelineMap,
        HarmonicRhythm harmonicRhythm,
        SectionRhythm sectionRhythm
    )
    {
        foreach (var trackNumber in _tracks.NonGroupedTrackNumbers)
        {
            // a section's chords move more smoothly or more in blocks than the song's, and its melody more or less by step,
            // and is as busy as the section has it
            var sectionTrackLayer = new StateMapBuilder("Section track", perTrack: true)
                .Add(StateKinds.VoiceLeading, VoiceLeadingLayers.CreateGenerator(VoiceLeadingLayers.Section));
            if (_tracks.Definitions[trackNumber].Role == TrackRole.Melody)
                sectionRhythm.MelodyBusyness.AddTo(
                    sectionTrackLayer.Add(CompositionStateKinds.LineStepwiseness, MelodyLayers.CreateGenerator(MelodyLayers.Section)),
                    sectionRhythm.Energy
                );
            // fuller and busier the more energy the section has, as its drums are
            var trackStateMap = CreateSectionTrackLayer(context, trackNumber, sectionRhythm, sectionRhythm.Energy)
                .MergeWith(sectionStateMap)
                .MergeWith(sectionTrackLayer.ToStateMap(context));
            if (_tracks.Definitions[trackNumber].Role == TrackRole.Pad)
            {
                yield return (trackNumber, GeneratePad(trackNumber, trackStateMap, barStateTimelineMap, harmonicRhythm), null);
                continue;
            }

            var trackStateMaps = new Dictionary<int, StateMap> { [trackNumber] = trackStateMap };
            // the melody answers its question: the answer's later bars draw their rhythm afresh now and then, and its notes
            // there are mutated, by the section's amount, each decision from the answer's own sequence
            var isMelody = _tracks.Definitions[trackNumber].Role == TrackRole.Melody;
            var amount = sectionRhythm.Unconventionality.Tilt.Chance(MelodyLayers.AnswerAmount, 1);
            var answerContext = Stream(sectionId, SectionStream.MelodyAnswer);
            var answerSeed = answerContext.GenerateInt();
            var questionEnd = barStateTimelineMap.GetEffectiveStateMapAt(Meter.PatternDuration - Meter.BarDuration).GetStateValue(CompositionStateKinds.MelodyPhraseEnd);
            var answer = isMelody ? LinePattern.DrawAnswer(answerContext, amount, questionEnd) : null;
            var seeds = PatternGenerator.DrawSeeds(context, trackStateMaps.Keys, sectionRhythm.Scheme);
            GeneratedBars BuildBars(ImmutableArray<int> rhythmKeys) => _patternGenerator.BuildBars(
                seeds,
                sectionId,
                trackStateMaps.ToImmutableDictionary(),
                BarDrums.None,
                ImmutableDictionary<int, Doubling>.Empty,
                ImmutableDictionary<int, int>.Empty,
                barStateTimelineMap,
                sectionRhythm,
                answer,
                rhythmKeys
            );
            var bars = BuildBars([]);
            if (_tracks.Definitions[trackNumber].Role == TrackRole.Bass)
            {
                // the bass's line, placed with the song's, its pattern played twice as the section does, leading and
                // landing as its chords have it
                ChordApproach Approach(double change) =>
                    (ChordApproach)barStateTimelineMap.GetEffectiveStateMapAt(change).GetStateValue(StateKinds.ChordApproach);
                ChordArrival Landing(double change) =>
                    (ChordArrival)barStateTimelineMap.GetEffectiveStateMapAt(change).GetStateValue(StateKinds.ChordArrival);
                var bass = new SectionLine(
                    trackNumber,
                    BassLeadingLayers.Line,
                    bars.Timeline.Repeat(2),
                    rhythmKeys =>
                    {
                        using var pause = StateTrace.Pause();
                        return BuildBars(rhythmKeys).Timeline.Repeat(2);
                    },
                    sectionRhythm.Scheme.Letters,
                    harmonicRhythm,
                    [..harmonicRhythm.Changes.Select(Approach)],
                    [..harmonicRhythm.Changes.Select(Landing)],
                    BassLeadingLayers.Line.RegisterFreedom,
                    StreamSeed(sectionId, SectionStream.BassImprovisation)
                );
                yield return (trackNumber, bars with { Timeline = bass.Appear(0, 0).Trim(Meter.PatternDuration) }, bass);
                continue;
            }

            if (!isMelody)
            {
                yield return (trackNumber, bars, null);
                continue;
            }

            // the melody's notes are placed once its bars are made, in their order, over the question and its answer,
            // leading into the chord changes within a phrase as often as the section has it
            var leadingContext = Stream(sectionId, SectionStream.MelodyLeading);
            var leading = Math.Clamp(MelodyLayers.Leading + Generators.SplineValue()(leadingContext) * MelodyLayers.LeadingSpread, 0, 1);
            StateTrace.Record(TracePoints.MelodyLeading, SectionTrace, sectionId, 0, StateMap.Default, 0, $"{leading:F2}", leading);
            // how freely the melody changes register where a phrase starts: the line's, as the song and the section move it
            var freedomContext = Stream(sectionId, SectionStream.RegisterFreedom);
            var freedom = Math.Clamp(
                MelodyLayers.Line.RegisterFreedom + (_songRegisterFreedomShift + Generators.SplineValue()(freedomContext)) * MelodyLayers.Line.RegisterFreedomSpread,
                0,
                1
            );
            StateTrace.Record(TracePoints.LineRegisterFreedom, SectionTrace, sectionId, 0, StateMap.Default, 0, $"{freedom:F2}", freedom);
            // a later appearance builds its bars afresh where its rhythm is improvised, as recorded the first time
            var melody = new SectionLine(
                trackNumber,
                MelodyLayers.Line,
                LinePattern.Answer(bars.Timeline, trackNumber, MelodyLayers.Line, answerSeed, amount),
                rhythmKeys =>
                {
                    using var pause = StateTrace.Pause();
                    return LinePattern.Answer(BuildBars(rhythmKeys).Timeline, trackNumber, MelodyLayers.Line, answerSeed, amount);
                },
                sectionRhythm.Scheme.Letters,
                harmonicRhythm,
                LinePattern.DrawApproaches(leadingContext, leading, harmonicRhythm.Count),
                [..Enumerable.Repeat(ChordArrival.Free, harmonicRhythm.Count)],
                freedom,
                StreamSeed(sectionId, SectionStream.MelodyImprovisation)
            );
            StateTrace.Record(TracePoints.MelodyAnswer, SectionTrace, sectionId, 0, StateMap.Default, 0, $"{amount:F2}", amount);
            yield return (trackNumber, bars with { Timeline = melody.Appear(0, 0).Trim(Meter.PatternDuration) }, melody);
        }
    }

    /// <summary>
    ///     A pad's pattern: a chord at every change of chord, held until the next, over the state of the section and the
    ///     chord its pool picks there, as the chords' notes take theirs.
    /// </summary>
    private static GeneratedBars GeneratePad(int trackNumber, StateMap trackStateMap, StateTimelineMap barStateTimelineMap, HarmonicRhythm harmonicRhythm)
    {
        var notes = harmonicRhythm.Changes.Select(change => trackStateMap
            .MergeWith(PatternGenerator.PickChord(trackStateMap, barStateTimelineMap.GetEffectiveStateMapAt(change)))
            .With(CompositionStateKinds.BeatRank, 0)
            .With(StateKinds.HeldDuration, harmonicRhythm.Span)
            .ToTimelineItem(change)
        );
        var timeline = TrackEventStateTimelineMap.Create(
            Meter.PatternDuration,
            [
                new KeyValuePair<int, EventStateTimelineMap<StateMap>>(
                    trackNumber,
                    EventTimeline.Create(Meter.PatternDuration, notes).ToEventStateTimelineMap(StateMap.Default)
                )
            ],
            StateTimelineMap.Create(Meter.PatternDuration)
        );
        return new GeneratedBars(timeline, []);
    }

    /// <summary>
    ///     A track's own layer in the section, over the state its definition brings, its fullness and density leaning by
    ///     the tilt given, such as a drum's by the section's energy.
    /// </summary>
    private StateMap CreateSectionTrackLayer(IGenerationContext context, int trackNumber, SectionRhythm sectionRhythm, Tilt tilt = default)
    {
        return LayerStates.CreateTrackLayer(
            context,
            "Section track",
            SongTracks.GetGenerationStateMap(_tracks.Definitions[trackNumber]),
            VelocityLayers.CreateGenerator(VelocityLayers.SectionTrack),
            sectionRhythm.Unconventionality.Lean(RhythmLayers.SectionTrack).Tilted(tilt)
        );
    }

    /// <summary>
    ///     A section's state: the song's, then the section's own draws, then what the section adds to the song's
    ///     pools. A pool lists its entries in the order its layers are merged, and an index around 0 picks the first
    ///     ones most often, so the song's entries, such as its chord shapes, come first and are the likeliest, and the
    ///     section's add variety.
    /// </summary>
    internal static StateMap CreateSectionStateMap(StateMap songStateMap, StateMap sectionDraws, StateMap sectionPoolEntries)
    {
        return songStateMap
            .MergeWith(sectionDraws)
            .MergeWith(sectionPoolEntries);
    }
}

/// <summary>A section's place in the song's form, which some of its draws keep to.</summary>
/// <param name="HasTonicHome">Whether its home is the song's tonic, as the song's last section's is, where the song ends.</param>
/// <param name="KeepsSongScale">Whether it plays in the song's scale, as the song's first does, which sets the key.</param>
/// <param name="Role">What it does in the song's form, which leans its length and its home.</param>
internal sealed record SectionPlan(int Id, bool HasTonicHome, bool KeepsSongScale, SectionRole Role);

/// <summary>A section's tracks, and what the fills need to know of its rhythm and energy.</summary>
/// <param name="Rhythm">How far the section's rhythm strays from convention.</param>
/// <param name="Groove">The states of the rhythm the fills play from.</param>
/// <param name="Energy">How loud and busy the section is meant to be (<see cref="SectionEnergy" />).</param>
/// <param name="IsPercussionOnly">Whether the section plays its percussion without the drum kit.</param>
/// <param name="DrumRoles">The role every one of the song's drums plays in the section.</param>
/// <param name="Lines">The section's lines, such as its melody, before they are placed, placed afresh every time it plays.</param>
/// <param name="Plays">How many times the section plays its 4-bar pattern: once, twice or four times (<see cref="SectionLength" />).</param>
/// <param name="Bindings">The drums bound to a lead, by their tracks, and their leads' tracks (<see cref="Doubling" />).</param>
/// <param name="Resting">The parts the section leaves out as it plays (<see cref="Arrangement" />).</param>
/// <param name="Role">What the section does in the song's form.</param>
/// <param name="ArrangementSeed">The seed of the sequences a later appearance draws its parts from.</param>
/// <param name="Roles">What every track plays, by its number.</param>
internal sealed record GeneratedSection(
    TrackEventStateTimelineMap<StateMap> Timeline,
    RhythmicUnconventionality Rhythm,
    FillGrooves Groove,
    double Energy,
    bool IsPercussionOnly,
    ImmutableDictionary<int, DrumRole> DrumRoles,
    ImmutableArray<SectionLine> Lines,
    int Plays,
    ImmutableDictionary<int, int> Bindings,
    ImmutableHashSet<TrackRole> Resting,
    SectionRole Role,
    int ArrangementSeed,
    ImmutableDictionary<int, TrackRole> Roles
)
{
    /// <summary>Whether the drums play in the section, rather than rest for a breakdown.</summary>
    public bool HasDrums => !Resting.Contains(TrackRole.Drum);

    /// <summary>
    ///     The section as it plays the given time, its parts as <see cref="Appear(int, double)" /> plays them, the first
    ///     time as drawn and a later time drawn again by the appearance's own energy, the section's and its step, from a
    ///     sequence of the appearance's own, a part that played the time before playing on, so that parts only join as a
    ///     section comes back.
    /// </summary>
    /// <param name="energyStep">How far the appearance's energy is from the section's, such as more for a later one.</param>
    /// <param name="before">The parts that rested the time before, of which it may leave out no more.</param>
    public GeneratedSection Appear(int appearance, double improvisation, double energyStep, ImmutableHashSet<TrackRole> before)
    {
        var resting = appearance == 0
            ? Resting
            : Arrangement.DrawRests(new GenerationContext(Seeds.Derive(ArrangementSeed, appearance)), SectionEnergy.Tilt(Energy + energyStep, Rhythm.Coupling), Role)
                .Intersect(before);
        return (this with { Resting = resting }).Appear(appearance, improvisation);
    }

    /// <summary>
    ///     The section as it plays the given time, from 0: its lines placed for that time, mutated from the first by
    ///     the song's improvisation (<see cref="SectionLine.Place" />), and every other track as it was made. A section
    ///     played once plays its lines' question alone; one played four times plays the question and the answer twice,
    ///     the second time as a further appearance, improvised as far as the song improvises.
    /// </summary>
    public GeneratedSection Appear(int appearance, double improvisation)
    {
        // the resting parts' tracks play nothing, and their lines are not placed
        var silent = Timeline.TrackTimelineMap.Keys.Where(x => Resting.Contains(Roles[x])).ToHashSet();
        var timeline = Timeline.MapTrackEvents(silent.ToDictionary(
            x => x,
            _ => (Func<EventTimeline<StateMap>, EventTimeline<StateMap>>)(notes => EventTimeline.Create<StateMap>(notes.Duration))
        ));
        foreach (var line in Lines.Where(x => !silent.Contains(x.Track)))
        {
            var bars = Plays switch
            {
                1 => line.Appear(appearance, improvisation).Trim(Meter.PatternDuration),
                2 => line.Appear(appearance, improvisation),
                4 => new[] { line.Appear(2 * appearance, improvisation), line.Appear(2 * appearance + 1, improvisation) }.Unroll(),
                _ => throw new InvalidOperationException($"A section plays its pattern once, twice or four times, not {Plays} times.")
            };
            var placed = bars.TrackTimelineMap[line.Track].EventTimeline.MapValues(x => x.OfScope(StateScope.Render));
            timeline = timeline.MapTrackEvents(new Dictionary<int, Func<EventTimeline<StateMap>, EventTimeline<StateMap>>> { [line.Track] = _ => placed });
        }

        return this with { Timeline = timeline, Lines = [..Lines.Where(x => !silent.Contains(x.Track))] };
    }
}

/// <summary>
///     A section's rhythm: how far it strays from convention, the scheme its 4-bar pattern follows, how busy its
///     melody is, and how its energy leans its draws.
/// </summary>
internal sealed record SectionRhythm(
    RhythmicUnconventionality Unconventionality,
    PhraseScheme Scheme,
    MelodyBusyness MelodyBusyness,
    Tilt Energy
);

/// <summary>
///     The random sequences of a section's decisions apart from its own, each derived from the section's seed by its
///     number, which must stay as it is: a new decision takes a new number.
/// </summary>
internal enum SectionStream
{
    Scale = 1,
    DrumPresence = 2,
    Percussion = 3,
    DrumStrokes = 4,
    DrumRoles = 5,
    Kit = 6,
    MelodyAnswer = 7,
    MelodyLeading = 8,
    MelodyImprovisation = 9,
    RegisterFreedom = 10,
    BassImprovisation = 11,
    DrumBindings = 12,
    DrumFeels = 13,
    HarmonicRhythm = 14,
    Length = 15,
    Arrangement = 16
}
