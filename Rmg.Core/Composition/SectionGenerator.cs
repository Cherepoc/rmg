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
    private readonly RhythmicUnconventionality _songRhythmicUnconventionality;
    private readonly Scale _songScale;
    private readonly StateMap _songStateMap;
    private readonly BarStateGenerator _barStateGenerator;
    private readonly PatternGenerator _patternGenerator;


    public SectionGenerator(
        IGenerationContext context,
        ProgressionSettings settings,
        SongTracks tracks,
        HarmonicUnconventionality songUnconventionality,
        RhythmicUnconventionality songRhythmicUnconventionality,
        Scale songScale,
        StateMap songStateMap
    )
    {
        _context = context;
        _tracks = tracks;
        _songUnconventionality = songUnconventionality;
        _songRhythmicUnconventionality = songRhythmicUnconventionality;
        _songScale = songScale;
        _songStateMap = songStateMap;
        _barStateGenerator = new BarStateGenerator(context, settings, songScale);
        _patternGenerator = new PatternGenerator(context, tracks.Definitions);
    }

    /// <summary>The section: its 4-bar pattern played twice.</summary>
    public TrackEventStateTimelineMap<StateMap> Generate(int sectionId)
    {
        var unconventionality = _songUnconventionality.GenerateSection(_context);
        var rhythm = _songRhythmicUnconventionality.GenerateSection(_context);
        var chords = LayerStates.CreateChordPool(unconventionality)(_context);

        // the section's chords move around its home, which every track's root starts from
        var home = Progressions.GenerateHome(_context, _songScale);
        var progression = Progressions.Generate(_context, _songScale, home, unconventionality.ProgressionStrictness);

        var sectionStateMap = CreateSectionStateMap(
            _songStateMap,
            new StateMapBuilder("Section")
                .AddRhythmLayer(rhythm.Scale(RhythmLayers.Section))
                .AddNoteWalkLayer()
                .Add(CompositionStateKinds.ChordPool.Index, LayerStates.ChordPoolIndex)
                .Add(StateKinds.Velocity, VelocityLayers.CreateGenerator(VelocityLayers.Section))
                .AddNoteDurationLayer()
                .ToStateMap(_context),
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

        // every track follows the section's phrase scheme, so they repeat their bars in the same places
        var scheme = PhraseSchemes.Pick(_context, rhythm);
        var sectionRhythm = new SectionRhythm(rhythm, scheme);

        var trackTimelineMaps = new List<TrackEventStateTimelineMap<StateMap>>();
        trackTimelineMaps.AddRange(GenerateDrums(sectionId, sectionStateMap, activeDrumTrackNumbers, barStateTimelineMap, sectionRhythm));
        trackTimelineMaps.AddRange(GeneratePitchedTracks(sectionId, sectionStateMap, barStateTimelineMap, sectionRhythm));

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
        StateTimelineMap barStateTimelineMap,
        SectionRhythm sectionRhythm
    )
    {
        foreach (var group in _tracks.Groups)
        {
            var groupStateMap = new StateMapBuilder("Section drum group", perTrack: true)
                .AddRhythmLayer(sectionRhythm.Unconventionality.Scale(RhythmLayers.SectionDrumGroup))
                .AddNoteWalkLayer()
                .Add(StateKinds.Velocity, VelocityLayers.CreateGenerator(VelocityLayers.SectionDrumGroup))
                .AddNoteDurationLayer()
                .ToStateMap(_context)
                .MergeWith(group.StateMap)
                .MergeWith(sectionStateMap);
            var trackStateMaps = new Dictionary<int, StateMap>();
            foreach (var trackNumber in group.TrackNumbers.Where(activeDrumTrackNumbers.Contains))
                trackStateMaps[trackNumber] = CreateSectionTrackLayer(trackNumber, sectionRhythm).MergeWith(groupStateMap);

            if (trackStateMaps.Count == 0)
                continue;

            yield return _patternGenerator.GenerateBars(sectionId, trackStateMaps.ToImmutableDictionary(), barStateTimelineMap, sectionRhythm);
        }
    }

    /// <summary>The pitched tracks, each making its patterns on its own, over the section's state.</summary>
    private IEnumerable<TrackEventStateTimelineMap<StateMap>> GeneratePitchedTracks(
        int sectionId,
        StateMap sectionStateMap,
        StateTimelineMap barStateTimelineMap,
        SectionRhythm sectionRhythm
    )
    {
        foreach (var trackNumber in _tracks.NonGroupedTrackNumbers)
        {
            // a section's chords move more smoothly or more in blocks than the song's, and its melody more or less by step
            var sectionTrackLayer = new StateMapBuilder("Section track", perTrack: true)
                .Add(StateKinds.VoiceLeading, VoiceLeadingLayers.CreateGenerator(VoiceLeadingLayers.Section));
            if (trackNumber == SongTracks.MelodyTrack)
                sectionTrackLayer.Add(CompositionStateKinds.MelodyStepwiseness, MelodyLayers.CreateGenerator(MelodyLayers.Section));
            var trackStateMap = CreateSectionTrackLayer(trackNumber, sectionRhythm)
                .MergeWith(sectionStateMap)
                .MergeWith(sectionTrackLayer.ToStateMap(_context));
            var trackStateMaps = new Dictionary<int, StateMap> { [trackNumber] = trackStateMap };
            yield return _patternGenerator.GenerateBars(sectionId, trackStateMaps.ToImmutableDictionary(), barStateTimelineMap, sectionRhythm);
        }
    }

    /// <summary>A track's own layer in the section, over the state its definition brings.</summary>
    private StateMap CreateSectionTrackLayer(int trackNumber, SectionRhythm sectionRhythm)
    {
        return LayerStates.CreateTrackLayer(
            _context,
            "Section track",
            SongTracks.GetGenerationStateMap(_tracks.Definitions[trackNumber]),
            VelocityLayers.SectionTrack,
            sectionRhythm.Unconventionality.Scale(RhythmLayers.SectionTrack)
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

/// <summary>A section's rhythm: how far it strays from convention, and the scheme its 4-bar pattern follows.</summary>
internal sealed record SectionRhythm(RhythmicUnconventionality Unconventionality, PhraseScheme Scheme);
