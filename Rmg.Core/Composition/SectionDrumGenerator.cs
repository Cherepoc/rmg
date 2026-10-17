using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     A section's drums, the drum half of making it (<see cref="SectionGenerator" />): which drums it plays and how, their
///     roles, doubles and feels, their strokes and bars, and their patterns, over the section's state; every decision from
///     the section's sequence of its own, as the section gives it.
/// </summary>
internal sealed class SectionDrumGenerator
{
    /// <summary>The shortest note the drum keeping time plays in double time, in beats: an 8th, its kick and snare doubling under it.</summary>
    public const double DoubleTimeShortest = 0.5;

    private readonly SongTracks _tracks;
    private readonly Genre _genre;
    private readonly Func<int, double> _energy;
    private readonly Tilt _songPercussion;
    private readonly Func<int, SectionStream, IGenerationContext> _stream;
    private readonly Func<int, SectionStream, int, IGenerationContext> _trackStream;
    private readonly Func<int, SectionRole, Tilt, ImmutableHashSet<TrackRole>> _firstRests;
    private readonly Func<int, Unconventionality> _facets;
    private readonly Func<IGenerationContext, int, SectionRhythm, Tilt, DrumRole?, StateMap> _trackLayer;
    private readonly Func<Meter, PatternGenerator> _patterns;

    /// <param name="energy">A section's energy, the song's with the section's layers of it, as the section has it.</param>
    /// <param name="stream">A decision's sequence in a section, by the section and the decision.</param>
    /// <param name="trackStream">A track's own sequence of a decision in a section, by the section, the decision and the track.</param>
    /// <param name="firstRests">The parts a section leaves out the first time it plays, by its role and its energy's tilt.</param>
    /// <param name="facets">A section's facets of its unconventionality.</param>
    /// <param name="trackLayer">A track's layer of the section's state, the drums' as the pitched parts'.</param>
    /// <param name="patterns">The patterns' generator of a meter.</param>
    public SectionDrumGenerator(
        SongTracks tracks,
        Genre genre,
        Func<int, double> energy,
        Tilt songPercussion,
        Func<int, SectionStream, IGenerationContext> stream,
        Func<int, SectionStream, int, IGenerationContext> trackStream,
        Func<int, SectionRole, Tilt, ImmutableHashSet<TrackRole>> firstRests,
        Func<int, Unconventionality> facets,
        Func<IGenerationContext, int, SectionRhythm, Tilt, DrumRole?, StateMap> trackLayer,
        Func<Meter, PatternGenerator> patterns
    )
    {
        (_tracks, _genre, _energy, _songPercussion) = (tracks, genre, energy, songPercussion);
        (_stream, _trackStream, _firstRests, _facets, _trackLayer, _patterns) = (stream, trackStream, firstRests, facets, trackLayer, patterns);
    }

    /// <summary>
    ///     The section's drums as parts: the roles its kit plays the song's drums in, the kit, the drums that
    ///     double a lead and how, and the leads whose feel the others play on, each from a sequence of its own.
    /// </summary>
    public (ImmutableDictionary<int, StateMap> Roles, SectionKit Kit, bool IsPercussionOnly, ImmutableDictionary<int, Doubling> Doubles, ImmutableDictionary<int, int> FeelLeads)
        DrawDrumParts(int sectionId, SectionDrums planned, RhythmicUnconventionality rhythm)
    {
        // the roles the section plays the song's drums in, as their parts in its grooves and its fills, as its kit has them
        var (isPercussionOnly, kit, _) = planned;
        var sectionRoles = _tracks.SongDrums.Select(DrumGroups.GetTrackNumber).Order()
            .ToImmutableDictionary(x => x, x => DrumRoles.SectionPart(SongRole(x), kit.Roles[DrumGroups.GetDrum(x)], _genre));
        StateTrace.Record(
            TracePoints.DrumRoles,
            SectionGenerator.SectionTrace,
            sectionId,
            0,
            StateMap.Default,
            0,
            string.Join(", ", sectionRoles.Where(x => !x.Value.IsDefault).Select(x => $"{x.Key} {(DrumRole)x.Value.GetStateValue(CompositionStateKinds.DrumRole).Value}")),
            sectionRoles.Where(x => !x.Value.IsDefault).ToImmutableDictionary(x => x.Key, x => (DrumRole)x.Value.GetStateValue(CompositionStateKinds.DrumRole).Value)
        );

        // the drums: the kit's or, now and then, the percussion's alone, by the roles they play, as the song drew them
        // (DrawDrums)
        StateTrace.RecordSection(TracePoints.PercussionOnly, SectionGenerator.SectionTrace, sectionId, isPercussionOnly ? "percussion only" : "drum kit", isPercussionOnly);
        StateTrace.RecordSection(TracePoints.Kit, SectionGenerator.SectionTrace, sectionId, string.Join(", ", kit.Drums.Select(x => x.Name)), kit);
        // a drum that doubles a lead plays its lead's beats up to the rank its role doubles, all of them, a share, or a
        // figure of its own on the lead's feel, as it is bound to it, drawn from a sequence of its own, leaning to its own
        // figure the less conventional the section
        var doubles = kit.Doubles.OrderBy(x => DrumGroups.GetTrackNumber(x.Key)).ToImmutableDictionary(
            x => DrumGroups.GetTrackNumber(x.Key),
            x => new Doubling(
                DrumGroups.GetTrackNumber(x.Value),
                // up to the rank of the role the lead leads in the section, not its main one's
                DrumRoles.DoublingRanks[kit.Leads.First(l => l.Value == x.Value).Key],
                _trackStream(sectionId, SectionStream.DrumBindings, DrumGroups.GetTrackNumber(x.Key)).Pick(ByConvention.Weigh(x.Key.Bindings, rhythm.Value))
            )
        );
        StateTrace.RecordSection(TracePoints.Doubles, SectionGenerator.SectionTrace, sectionId, string.Join(", ", kit.Doubles.Select(x => $"{x.Key.Name} on {x.Value.Name}")), doubles);
        // a drum that does not lead plays on its lead's feel: one bound to it always, one that colours the section on the
        // lead of its main role's by a chance, drawn from a sequence of its own, crossing it the less conventional the section
        var feelLeads = ImmutableDictionary.CreateBuilder<int, int>();
        foreach (var drum in kit.Drums.Except(kit.Leads.Values).OrderBy(DrumGroups.GetTrackNumber))
        {
            var track = DrumGroups.GetTrackNumber(drum);
            if (doubles.TryGetValue(track, out var doubling))
                feelLeads[track] = doubling.Lead;
            else if (kit.Leads.GetValueOrDefault(drum.MainRole) is { } lead && _trackStream(sectionId, SectionStream.DrumFeels, track).TestProbability(DrumKitGenerator.OnTheLeadsFeel.At(rhythm.Value)))
                feelLeads[track] = DrumGroups.GetTrackNumber(lead);
        }

        StateTrace.RecordSection(TracePoints.FeelLeads, SectionGenerator.SectionTrace, sectionId, string.Join(", ", feelLeads.Select(x => $"{DrumGroups.GetDrum(x.Key).Name} on {DrumGroups.GetDrum(x.Value).Name}")), feelLeads.ToImmutable());
        return (sectionRoles, kit, isPercussionOnly, doubles, feelLeads.ToImmutable());
    }

    /// <summary>The strokes the section's drums change from the song's, and how they play its bars, each from a sequence of its own.</summary>
    public (ImmutableDictionary<int, int> Strokes, BarDrums Bars) DrawDrumBars(int sectionId, ImmutableHashSet<int> activeDrumTrackNumbers, SectionKit kit, PhraseScheme scheme,
        RhythmicUnconventionality rhythm, Tilt tilt)
    {
        // the strokes the section changes from the song's, and how its drums play its bars, each from a sequence of its own
        var sectionStrokes = ImmutableDictionary.CreateBuilder<int, int>();
        foreach (var track in activeDrumTrackNumbers.Order().Where(x => DrumGroups.GetDrum(x).HasStrokes))
            if (DrumStrokes.DrawChange(_trackStream(sectionId, SectionStream.DrumStrokes, track), DrumGroups.GetDrum(track), SongStroke(track), DrumStrokes.SectionChangeChance, rhythm.Value, tilt, DrumSounds.OddsOf(_genre, GenreChoices.DrumStrokes)) is { } change)
                sectionStrokes[track] = change;
        StateTrace.RecordSection(TracePoints.DrumStrokes, SectionGenerator.SectionTrace, sectionId, string.Join(", ", sectionStrokes), sectionStrokes.ToImmutable());
        var barDrums = DrumPresence.Draw(
            track => _trackStream(sectionId, SectionStream.DrumPresence, track),
            activeDrumTrackNumbers,
            scheme,
            kit.Leads.Values.Select(DrumGroups.GetTrackNumber).ToHashSet(),
            track => sectionStrokes.TryGetValue(track, out var stroke) ? stroke : SongStroke(track),
            rhythm.Value,
            tilt,
            DrumSounds.OddsOf(_genre, GenreChoices.DrumStrokes)
        );
        StateTrace.RecordSection(TracePoints.DrumPresence, SectionGenerator.SectionTrace, sectionId, $"{string.Join(", ", barDrums.Resting)}; {string.Join(", ", barDrums.Strokes)}", barDrums);
        return (sectionStrokes.ToImmutable(), barDrums);
    }

    /// <summary>
    ///     A section's drums, drawn before the sections are made, so that the song sees them all (<see cref="DrumCoverage" />):
    ///     whether it plays percussion only, a percussion song's every section and now and then a section of a song of the
    ///     kit and percussion, unless the song says, the roles it draws again for the song's drums, and its kit by them,
    ///     each from a sequence of its own.
    /// </summary>
    public SectionDrums DrawDrums(int sectionId, SectionRole role, bool? isPercussionOnly = null)
    {
        var rhythm = new RhythmicUnconventionality(_facets(sectionId)[Facet.Groove]);
        var tilt = SectionEnergy.Tilt(_energy(sectionId), rhythm.Coupling);
        // the drums a percussion section draws its kit from (DrumKitGenerator.SelectKit)
        var songPercussion = _tracks.SongDrums.Count(x => x.Family.HasFlag(DrumFamily.Percussion));
        var percussionOnly = isPercussionOnly ?? (_tracks.DrumSetup == DrumSetup.Percussion ||
                                                  (_tracks.DrumSetup == DrumSetup.KitAndPercussion &&
                                                   PercussionSections.Draw(_stream(sectionId, SectionStream.Percussion), _songPercussion, rhythm.Value, tilt, songPercussion)));
        // whether its drums play where it first plays, as its arrangement draws them there (Generate)
        var playsDrums = !_firstRests(sectionId, role, tilt).Contains(TrackRole.Drum);
        var roles = _tracks.SongDrums.OrderBy(DrumGroups.GetTrackNumber)
            .ToImmutableDictionary(x => x, x => DrumRoles.DrawSection(_trackStream(sectionId, SectionStream.DrumRoles, DrumGroups.GetTrackNumber(x)), x, SongRole(DrumGroups.GetTrackNumber(x)), rhythm.Value, _genre));
        return new SectionDrums(percussionOnly, DrumKitGenerator.SelectKit(_stream(sectionId, SectionStream.Kit), _tracks.SongDrums, tilt, percussionOnly, roles), playsDrums);
    }

    /// <summary>The role a drum plays in the section: its own, where it draws the drum's again, or the song's.</summary>
    public DrumRole SectionRole(ImmutableDictionary<int, StateMap> sectionRoles, int track)
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

    /// <summary>The drums the section plays, which make their patterns together, over the drums' shared state.</summary>
    public IEnumerable<GeneratedBars> GenerateDrums(
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
        SectionRhythm sectionRhythm,
        int timeFeel,
        Meter meter
    )
    {
        // the section's time, as a step of every drum's period: half time twice as long, double time half
        var groupStateMap = new StateMapBuilder(Layers.SectionDrumGroup, perTrack: true)
            .AddRhythmLayer(sectionRhythm.Unconventionality.Lean(RhythmLayers.SectionDrumGroup).Tilted(sectionRhythm.Energy))
            .AddNoteWalkLayer()
            .Add(StateKinds.Velocity, VelocityLayers.CreateGenerator(VelocityLayers.SectionDrumGroup * VelocityLayers.SpreadOf(_genre)))
            .AddNoteDurationLayer()
            .Add(CompositionStateKinds.TimeStep, timeFeel)
            .ToStateMap(context)
            .MergeWith(_tracks.DrumStateMap)
            .MergeWith(sectionStateMap);
        var songDrums = _tracks.SongDrums.Select(DrumGroups.GetTrackNumber).Order().ToArray();
        var trackStateMaps = new Dictionary<int, StateMap>();
        foreach (var trackNumber in songDrums.Where(activeDrumTrackNumbers.Contains))
            // each drum's layer from a sequence of its own, so that it is the same whichever other drums play
            trackStateMaps[trackNumber] = _trackLayer(_trackStream(sectionId, SectionStream.DrumTracks, trackNumber), trackNumber, sectionRhythm, sectionRhythm.Energy, SectionRole(sectionRoles, trackNumber))
                .MergeWith(groupStateMap)
                .MergeWith(sectionRoles[trackNumber])
                .MergeWith(sectionStrokes.TryGetValue(trackNumber, out var stroke) ? DrumStrokes.At(StateDepths.Section, stroke) : StateMap.Default);
        // in double time the kick and the snare double, the drum keeping time no finer than 8ths, as a skank beat's hats
        foreach (var trackNumber in trackStateMaps.Keys.Where(x => timeFeel < 0 && DrumRoles.Of(trackStateMaps[x]) == DrumRole.Time).ToArray())
            trackStateMaps[trackNumber] = trackStateMaps[trackNumber].With(CompositionStateKinds.ShortestNote, DoubleTimeShortest);

        // a drum out of the groove still has the state the drums share, with no notes, so that a note added later, such as
        // in a fill, plays as loud as the section, and its own rhythm's state, which a fill plays it by
        var idleStateMap = groupStateMap.OfScope(StateScope.Render);
        var idleTrackNumbers = songDrums.Where(x => !trackStateMaps.ContainsKey(x)).ToArray();
        yield return new GeneratedBars(TrackEventStateTimelineMap.Create(
            meter.PatternDuration,
            idleTrackNumbers.Select(x => new KeyValuePair<int, EventStateTimelineMap<StateMap>>(
                    x,
                    EventTimeline.Create<StateMap>(meter.PatternDuration).ToEventStateTimelineMap(idleStateMap)
                )
            ),
            StateTimelineMap.Create(meter.PatternDuration)
        ), [
            ..idleTrackNumbers.Select(x => new BarFeel(
                    x,
                    meter.PatternBarCount - 1,
                    SongTracks.GetGenerationStateMap(_tracks.Definitions[x]).MergeWith(groupStateMap).MergeWith(sectionRoles[x]),
                    0
                )
            )
        ]);

        if (trackStateMaps.Count == 0)
            yield break;

        yield return _patterns(meter).GenerateBars(context, sectionId, DoubleLeads(trackStateMaps, doubles), barDrums, doubles, feelLeads, barStateTimelineMap, sectionRhythm, null);
    }
}
