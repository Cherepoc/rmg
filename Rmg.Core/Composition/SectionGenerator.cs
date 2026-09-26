using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     A section of a song: its harmony (its unconventionality, chords, home and progression), its state over the
///     song's, the drums it plays, the state that changes by bar, and the patterns of its tracks, played twice.
/// </summary>
internal sealed class SectionGenerator
{
    private readonly IGenerationContext _context;
    private readonly SongTracks _tracks;
    private readonly HarmonicUnconventionality _songUnconventionality;
    private readonly Scale _songScale;
    private readonly StateMap _songStateMap;
    private readonly BarStateGenerator _barStateGenerator;
    private readonly PatternGenerator _patternGenerator;

    private readonly Func<IGenerationContext, StateMap> _sectionLayerGenerator = new StateMapBuilder("Section")
        .AddRhythmLayer(RhythmLayers.Section)
        .AddNoteWalkLayer()
        .Add(CompositionStateKinds.ChordPool.Index, LayerStates.ChordPoolIndex)
        .Add(StateKinds.Velocity, VelocityLayers.CreateGenerator(VelocityLayers.Section))
        .AddNoteDurationLayer()
        .ToStateMapGenerator();

    private readonly Func<IGenerationContext, StateMap> _drumGroupLayerGenerator = new StateMapBuilder("Section drum group", perTrack: true)
        .AddRhythmLayer(RhythmLayers.SectionDrumGroup)
        .AddNoteWalkLayer()
        .Add(StateKinds.Velocity, VelocityLayers.CreateGenerator(VelocityLayers.SectionDrumGroup))
        .AddNoteDurationLayer()
        .ToStateMapGenerator();

    public SectionGenerator(
        IGenerationContext context,
        ProgressionSettings settings,
        SongTracks tracks,
        HarmonicUnconventionality songUnconventionality,
        Scale songScale,
        StateMap songStateMap
    )
    {
        _context = context;
        _tracks = tracks;
        _songUnconventionality = songUnconventionality;
        _songScale = songScale;
        _songStateMap = songStateMap;
        _barStateGenerator = new BarStateGenerator(context, settings, songScale);
        _patternGenerator = new PatternGenerator(context, tracks.Definitions);
    }

    /// <summary>The section: its 4-bar pattern played twice.</summary>
    public TrackEventStateTimelineMap<StateMap> Generate(int sectionId)
    {
        var unconventionality = _songUnconventionality.GenerateSection(_context);
        var chords = LayerStates.CreateChordPool(unconventionality)(_context);

        // the section's chords move around its home, which every track's root starts from
        var home = Progressions.GenerateHome(_context, _songScale);
        var progression = Progressions.Generate(_context, _songScale, home, unconventionality.ProgressionStrictness);

        var sectionStateMap = CreateSectionStateMap(
            _songStateMap,
            _sectionLayerGenerator(_context),
            new StateMapBuilder("Section")
                .Add(CompositionStateKinds.ChordPool.Collection, chords)
                .Add(StateKinds.ChordRootNoteOffset, [Progressions.ToRootOffset(home)])
                .ToStateMap(_context)
        );
        var activeDrumTrackNumbers = DrumKitGenerator.SelectActiveDrums(_context, _tracks.SongDrums)
            .Select(DrumGroups.GetTrackNumber)
            .ToImmutableHashSet();

        // the section state reaches the notes through the track state maps, so the bar state holds only the state
        // that changes by bar
        var bassLeading = Math.Clamp(
            _tracks.BassLeading + BassLeadingLayers.CreateGenerator(BassLeadingLayers.Section)(_context),
            0,
            1
        );
        var barStateTimelineMap = _barStateGenerator.Generate(progression, home, unconventionality, bassLeading);

        var trackTimelineMaps = new List<TrackEventStateTimelineMap<StateMap>>();
        trackTimelineMaps.AddRange(GenerateDrums(sectionId, sectionStateMap, activeDrumTrackNumbers, barStateTimelineMap));
        trackTimelineMaps.AddRange(GeneratePitchedTracks(sectionId, sectionStateMap, barStateTimelineMap));

        // the song keeps what Render reads; the bar state for the generation, such as the chord pool's pick, stays here
        trackTimelineMaps.Add(
            barStateTimelineMap.OfScope(StateScope.Render).ToTrackEventStateTimelineMap<StateMap>(BarStateGenerator.PatternDuration)
        );
        return TrackEventStateTimelineMap.Merge(trackTimelineMaps).Repeat(2);
    }

    /// <summary>The drums the section plays, which make their patterns together, over the drums' shared state.</summary>
    private IEnumerable<TrackEventStateTimelineMap<StateMap>> GenerateDrums(
        int sectionId,
        StateMap sectionStateMap,
        ImmutableHashSet<int> activeDrumTrackNumbers,
        StateTimelineMap barStateTimelineMap
    )
    {
        foreach (var group in _tracks.Groups)
        {
            var groupStateMap = _drumGroupLayerGenerator(_context)
                .MergeWith(group.StateMap)
                .MergeWith(sectionStateMap);
            var trackStateMaps = new Dictionary<int, StateMap>();
            foreach (var trackNumber in group.TrackNumbers.Where(activeDrumTrackNumbers.Contains))
                trackStateMaps[trackNumber] = CreateSectionTrackLayer(trackNumber).MergeWith(groupStateMap);

            if (trackStateMaps.Count == 0)
                continue;

            yield return _patternGenerator.GenerateBars(sectionId, trackStateMaps.ToImmutableDictionary(), barStateTimelineMap);
        }
    }

    /// <summary>The pitched tracks, each making its patterns on its own, over the section's state.</summary>
    private IEnumerable<TrackEventStateTimelineMap<StateMap>> GeneratePitchedTracks(
        int sectionId,
        StateMap sectionStateMap,
        StateTimelineMap barStateTimelineMap
    )
    {
        foreach (var trackNumber in _tracks.NonGroupedTrackNumbers)
        {
            var trackStateMap = CreateSectionTrackLayer(trackNumber)
                .MergeWith(sectionStateMap)
                // a section's chords move more smoothly or more in blocks than the song's
                .MergeWith(
                    new StateMapBuilder("Section track", perTrack: true)
                        .Add(StateKinds.VoiceLeading, VoiceLeadingLayers.CreateGenerator(VoiceLeadingLayers.Section))
                        .ToStateMap(_context)
                );
            var trackStateMaps = new Dictionary<int, StateMap> { [trackNumber] = trackStateMap };
            yield return _patternGenerator.GenerateBars(sectionId, trackStateMaps.ToImmutableDictionary(), barStateTimelineMap);
        }
    }

    /// <summary>A track's own layer in the section, over the state its definition brings.</summary>
    private StateMap CreateSectionTrackLayer(int trackNumber)
    {
        return LayerStates.CreateTrackLayer(
            _context,
            "Section track",
            SongTracks.GetGenerationStateMap(_tracks.Definitions[trackNumber]),
            VelocityLayers.SectionTrack,
            RhythmLayers.SectionTrack
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
