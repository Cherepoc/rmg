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

    // the streams a section draws its scale, which bars its drums sit out and whether it plays percussion only from,
    // apart from its own
    private const int ScaleStream = 1;
    private const int DrumPresenceStream = 2;
    private const int PercussionStream = 3;
    private const int DrumStrokeStream = 4;
    private const int DrumRoleStream = 5;
    private const int KitStream = 6;
    private const int MelodyAnswerStream = 7;

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
        _barStateGenerator = new BarStateGenerator(settings);
        _patternGenerator = new PatternGenerator(context, tracks.Definitions);
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
        var home = Progressions.GenerateHome(context, scale);
        if (plan.HasTonicHome)
            home = 0;
        var progression = Progressions.Generate(context, scale, home, unconventionality.ProgressionStrictness);

        var sectionStateMap = CreateSectionStateMap(
            songStateMap,
            new StateMapBuilder("Section")
                .AddRhythmLayer(rhythm.Scale(RhythmLayers.Section))
                .AddNoteWalkLayer()
                .Add(CompositionStateKinds.ChordPool.Index, LayerStates.ChordPoolIndex)
                .Add(StateKinds.Velocity, VelocityLayers.CreateGenerator(VelocityLayers.Section, tilt))
                .Add(CompositionStateKinds.NoteDynamics, Math.Pow(rhythm.ChanceScale, VelocityLayers.DynamicsLean))
                .AddNoteDurationLayer()
                .ToStateMap(context),
            new StateMapBuilder("Section")
                .Add(CompositionStateKinds.ChordPool.Collection, chords)
                .Add(StateKinds.ChordRootNoteOffset, [Progressions.ToRootOffset(home)])
                .ToStateMap(context)
        );
        // the roles the section draws again for the song's drums, as their parts in its grooves and its fills, from a
        // sequence of its own
        var roleContext = _context.CreateContext(Seeds.Derive(Seeds.Derive(_seed, sectionId), DrumRoleStream));
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
        var percussionContext = _context.CreateContext(Seeds.Derive(Seeds.Derive(_seed, sectionId), PercussionStream));
        var songPercussion = _tracks.SongDrums.Count(DrumGroups.Percussion.Drums.Contains);
        // a percussion song's every section, and now and then a section of a song of the kit and percussion
        var isPercussionOnly = _tracks.DrumSetup == DrumSetup.Percussion ||
                               (_tracks.DrumSetup == DrumSetup.KitAndPercussion &&
                                PercussionSections.Draw(percussionContext, _songPercussion, rhythm.Tilt, tilt, songPercussion));
        StateTrace.Record(TracePoints.PercussionOnly, SectionTrace, sectionId, 0, StateMap.Default, 0, isPercussionOnly ? "percussion only" : "drum kit", isPercussionOnly);
        var kit = DrumKitGenerator.SelectKit(
            _context.CreateContext(Seeds.Derive(Seeds.Derive(_seed, sectionId), KitStream)),
            _tracks.SongDrums,
            drum => SectionRole(sectionRoles, DrumGroups.GetTrackNumber(drum)),
            tilt,
            isPercussionOnly
        );
        StateTrace.Record(TracePoints.Kit, SectionTrace, sectionId, 0, StateMap.Default, 0, string.Join(", ", kit.Drums.Select(x => x.Name)), kit);
        var activeDrumTrackNumbers = kit.Drums.Select(DrumGroups.GetTrackNumber).ToImmutableHashSet();
        // a drum that doubles a lead plays its lead's beats up to the rank its role doubles
        var doubles = kit.Doubles.ToImmutableDictionary(
            x => DrumGroups.GetTrackNumber(x.Key),
            x => new Doubling(DrumGroups.GetTrackNumber(x.Value), DrumRoles.DoublingRanks[x.Value.MainRole])
        );
        StateTrace.Record(TracePoints.Doubles, SectionTrace, sectionId, 0, StateMap.Default, 0, string.Join(", ", kit.Doubles.Select(x => $"{x.Key.Name} on {x.Value.Name}")), doubles);

        // the section state reaches the notes through the track state maps, so the bar state holds only the state
        // that changes by bar
        var bassLeading = Math.Clamp(
            _tracks.BassLeading + BassLeadingLayers.CreateGenerator(BassLeadingLayers.Section)(context),
            0,
            1
        );
        var barStateTimelineMap = _barStateGenerator.Generate(context, scale, progression, home, unconventionality, bassLeading, rhythm.Tilt);
        var contour = barStateTimelineMap.GetStateTimeline(CompositionStateKinds.MelodyRegister).Select(x => x.Value).ToImmutableArray();
        StateTrace.Record(TracePoints.MelodyContour, SectionTrace, sectionId, 0, StateMap.Default, 0, string.Join(", ", contour), contour);

        // every track follows the section's phrase scheme, so they repeat their bars in the same places
        var scheme = PhraseSchemes.Pick(context, rhythm);
        var sectionRhythm = new SectionRhythm(rhythm, scheme, _songMelodyBusyness.GenerateSection(context), tilt);

        // the strokes the section changes from the song's, and how its drums play its bars, each from a sequence of its own
        var strokeContext =_context.CreateContext(Seeds.Derive(Seeds.Derive(_seed, sectionId), DrumStrokeStream));
        var sectionStrokes = ImmutableDictionary.CreateBuilder<int, int>();
        foreach (var track in activeDrumTrackNumbers.Order().Where(x => DrumGroups.GetDrum(x).HasStrokes))
            if (DrumStrokes.DrawChange(strokeContext, DrumGroups.GetDrum(track), SongStroke(track), DrumStrokes.SectionChangeChance, rhythm.Tilt, tilt) is { } change)
                sectionStrokes[track] = change;
        StateTrace.Record(TracePoints.DrumStrokes, SectionTrace, sectionId, 0, StateMap.Default, 0, string.Join(", ", sectionStrokes), sectionStrokes.ToImmutable());
        var barDrums = DrumPresence.Draw(
            _context.CreateContext(Seeds.Derive(Seeds.Derive(_seed, sectionId), DrumPresenceStream)),
            activeDrumTrackNumbers,
            scheme,
            kit.Leads.Select(DrumGroups.GetTrackNumber).ToHashSet(),
            track => sectionStrokes.TryGetValue(track, out var stroke) ? stroke : SongStroke(track),
            rhythm.Tilt,
            tilt
        );
        StateTrace.Record(TracePoints.DrumPresence, SectionTrace, sectionId, 0, StateMap.Default, 0, $"{string.Join(", ", barDrums.Resting)}; {string.Join(", ", barDrums.Strokes)}", barDrums);

        var drums = GenerateDrums(context, sectionId, sectionStateMap, activeDrumTrackNumbers, doubles, sectionRoles, sectionStrokes.ToImmutable(), barDrums, barStateTimelineMap, sectionRhythm).ToArray();
        var pitched = GeneratePitchedTracks(context, sectionId, sectionStateMap, barStateTimelineMap, sectionRhythm).ToArray();
        // the section's pattern played twice, its melody as a question and its answer
        var timeline = KeepRenderState([..drums.Select(x => x.Timeline), ..pitched.Select(x => x.Bars.Timeline)], barStateTimelineMap).Repeat(2);
        foreach (var (track, answered) in pitched.Where(x => x.Answered is not null).Select(x => (x.Track, x.Answered!)))
            timeline = timeline.MapTrackEvents(
                new Dictionary<int, Func<EventTimeline<StateMap>, EventTimeline<StateMap>>>
                {
                    [track] = _ => answered.TrackTimelineMap[track].EventTimeline.MapValues(x => x.OfScope(StateScope.Render))
                }
            );
        return new GeneratedSection(
            timeline,
            rhythm,
            GetGrooves(drums.SelectMany(x => x.Feels)),
            energy,
            isPercussionOnly
        );
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
            _context.CreateContext(Seeds.Derive(Seeds.Derive(_seed, sectionId), ScaleStream)),
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
                .AddRhythmLayer(sectionRhythm.Unconventionality.Scale(RhythmLayers.SectionDrumGroup).Tilted(sectionRhythm.Energy))
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

            yield return _patternGenerator.GenerateBars(context, sectionId, DoubleLeads(trackStateMaps, doubles), barDrums, doubles, barStateTimelineMap, sectionRhythm, null);
        }
    }

    /// <summary>The pitched tracks, each making its patterns on its own, over the section's state.</summary>
    private IEnumerable<(int Track, GeneratedBars Bars, TrackEventStateTimelineMap<StateMap>? Answered)> GeneratePitchedTracks(
        IGenerationContext context,
        int sectionId,
        StateMap sectionStateMap,
        StateTimelineMap barStateTimelineMap,
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
                    sectionTrackLayer.Add(CompositionStateKinds.MelodyStepwiseness, MelodyLayers.CreateGenerator(MelodyLayers.Section))
                );
            var trackStateMap = CreateSectionTrackLayer(context, trackNumber, sectionRhythm)
                .MergeWith(sectionStateMap)
                .MergeWith(sectionTrackLayer.ToStateMap(context));
            var trackStateMaps = new Dictionary<int, StateMap> { [trackNumber] = trackStateMap };
            // the melody answers its question: the answer's later bars draw their rhythm afresh now and then, and its notes
            // there are mutated, by the section's amount, each decision from the answer's own sequence
            var isMelody = _tracks.Definitions[trackNumber].Role == TrackRole.Melody;
            var amount = sectionRhythm.Unconventionality.Tilt.Chance(MelodyLayers.AnswerAmount, 1);
            var answerContext = _context.CreateContext(Seeds.Derive(Seeds.Derive(_seed, sectionId), MelodyAnswerStream));
            var answerSeed = answerContext.GenerateInt();
            var questionEnd = barStateTimelineMap.GetEffectiveStateMapAt(Meter.PatternDuration - Meter.BarDuration).GetStateValue(CompositionStateKinds.MelodyPhraseEnd);
            var answer = isMelody ? MelodyPattern.DrawAnswer(answerContext, amount, questionEnd) : null;
            var bars = _patternGenerator.GenerateBars(context, sectionId, trackStateMaps.ToImmutableDictionary(), BarDrums.None, ImmutableDictionary<int, Doubling>.Empty, barStateTimelineMap, sectionRhythm, answer);
            if (!isMelody)
            {
                yield return (trackNumber, bars, null);
                continue;
            }

            // the melody's notes are placed once its bars are made, in their order, over the question and its answer
            var answered = MelodyPattern.Place(
                MelodyPattern.Answer(bars.Timeline, trackNumber, answerSeed, amount),
                trackNumber,
                (PitchInstrumentTrack)_tracks.Definitions[trackNumber],
                barStateTimelineMap.Repeat(2),
                _key
            );
            StateTrace.Record(TracePoints.MelodyAnswer, SectionTrace, sectionId, 0, StateMap.Default, 0, $"{amount:F2}", amount);
            yield return (trackNumber, bars with { Timeline = answered.Trim(Meter.PatternDuration) }, answered);
        }
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
            sectionRhythm.Unconventionality.Scale(RhythmLayers.SectionTrack).Tilted(tilt)
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
internal sealed record SectionPlan(int Id, bool HasTonicHome, bool KeepsSongScale);

/// <summary>A section's tracks, and what the fills need to know of its rhythm and energy.</summary>
/// <param name="Rhythm">How far the section's rhythm strays from convention.</param>
/// <param name="Groove">The states of the rhythm the fills play from.</param>
/// <param name="Energy">How loud and busy the section is meant to be (<see cref="SectionEnergy" />).</param>
/// <param name="IsPercussionOnly">Whether the section plays its percussion without the drum kit.</param>
internal sealed record GeneratedSection(
    TrackEventStateTimelineMap<StateMap> Timeline,
    RhythmicUnconventionality Rhythm,
    FillGrooves Groove,
    double Energy,
    bool IsPercussionOnly
);

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
