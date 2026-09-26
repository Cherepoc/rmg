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
    private readonly MelodyBusyness _songMelodyBusyness;
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
        MelodyBusyness songMelodyBusyness,
        Scale songScale,
        StateMap songStateMap
    )
    {
        _context = context;
        _tracks = tracks;
        _songUnconventionality = songUnconventionality;
        _songRhythmicUnconventionality = songRhythmicUnconventionality;
        _songMelodyBusyness = songMelodyBusyness;
        _songScale = songScale;
        _songStateMap = songStateMap;
        _barStateGenerator = new BarStateGenerator(context, settings, songScale);
        _patternGenerator = new PatternGenerator(context, tracks.Definitions);
    }

    /// <summary>The section: its 4-bar pattern played twice, with what the fills need to know of its rhythm.</summary>
    public GeneratedSection Generate(int sectionId)
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
        var sectionRhythm = new SectionRhythm(rhythm, scheme, _songMelodyBusyness.GenerateSection(_context));

        var drums = GenerateDrums(sectionId, sectionStateMap, activeDrumTrackNumbers, barStateTimelineMap, sectionRhythm).ToArray();
        var trackTimelineMaps = new List<TrackEventStateTimelineMap<StateMap>>();
        trackTimelineMaps.AddRange(drums.Select(x => x.Timeline));
        trackTimelineMaps.AddRange(GeneratePitchedTracks(sectionId, sectionStateMap, barStateTimelineMap, sectionRhythm).Select(x => x.Timeline));

        // the song keeps what Render reads; the bar state for the generation, such as the chord pool's pick, stays here
        trackTimelineMaps.Add(
            barStateTimelineMap.OfScope(StateScope.Render).ToTrackEventStateTimelineMap<StateMap>(BarStateGenerator.PatternDuration)
        );
        return new GeneratedSection(
            TrackEventStateTimelineMap.Merge(trackTimelineMaps).Repeat(2),
            rhythm,
            GetDrumTuplet(drums.SelectMany(x => x.Feels))
        );
    }

    /// <summary>
    ///     The tuplet the drums play in the pattern's last bar, before both of the section's lines: the one most of their
    ///     tuplet notes there fall on, if it has enough of all their notes to set the feel; 1 for straight.
    /// </summary>
    internal static int GetDrumTuplet(IEnumerable<BarFeel> feels)
    {
        var lastBar = feels.Where(x => x.Bar == Progressions.BarCount - 1).ToArray();
        var noteCount = lastBar.Sum(x => x.NoteCount);
        var tuplet = lastBar
            .Where(x => x.Tuplet != 1)
            .GroupBy(x => x.Tuplet)
            .Select(x => (Tuplet: x.Key, NoteCount: x.Sum(y => y.NoteCount)))
            .OrderByDescending(x => x.NoteCount)
            .ThenBy(x => x.Tuplet)
            .FirstOrDefault();
        return noteCount > 0 && tuplet.NoteCount >= FillLayers.TupletFeelShare * noteCount ? tuplet.Tuplet : 1;
    }

    /// <summary>The drums the section plays, which make their patterns together, over the drums' shared state.</summary>
    private IEnumerable<GeneratedBars> GenerateDrums(
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

            // a drum out of the groove still has the state the drums share, with no notes, so that a note added
            // later, such as in a fill, plays as loud as the section
            var idleStateMap = groupStateMap.OfScope(StateScope.Render);
            yield return new GeneratedBars(TrackEventStateTimelineMap.Create(
                BarStateGenerator.PatternDuration,
                group.TrackNumbers
                    .Where(x => !trackStateMaps.ContainsKey(x))
                    .Select(x => new KeyValuePair<int, EventStateTimelineMap<StateMap>>(
                            x,
                            EventTimeline.Create<StateMap>(BarStateGenerator.PatternDuration).ToEventStateTimelineMap(idleStateMap)
                        )
                    ),
                StateTimelineMap.Create(BarStateGenerator.PatternDuration)
            ), []);

            if (trackStateMaps.Count == 0)
                continue;

            yield return _patternGenerator.GenerateBars(sectionId, trackStateMaps.ToImmutableDictionary(), barStateTimelineMap, sectionRhythm);
        }
    }

    /// <summary>The pitched tracks, each making its patterns on its own, over the section's state.</summary>
    private IEnumerable<GeneratedBars> GeneratePitchedTracks(
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
            if (trackNumber == SongTracks.MelodyTrack)
                sectionRhythm.MelodyBusyness.AddTo(
                    sectionTrackLayer.Add(CompositionStateKinds.MelodyStepwiseness, MelodyLayers.CreateGenerator(MelodyLayers.Section))
                );
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

/// <summary>
///     A section's rhythm: how far it strays from convention, the scheme its 4-bar pattern follows, and how busy its
///     melody is.
/// </summary>
/// <summary>A section's tracks, and what the fills need to know of its rhythm.</summary>
/// <param name="Rhythm">How far the section's rhythm strays from convention.</param>
/// <param name="DrumTuplet">The tuplet the drums play in the pattern's last bar, 1 for straight.</param>
internal sealed record GeneratedSection(TrackEventStateTimelineMap<StateMap> Timeline, RhythmicUnconventionality Rhythm, int DrumTuplet);

internal sealed record SectionRhythm(RhythmicUnconventionality Unconventionality, PhraseScheme Scheme, MelodyBusyness MelodyBusyness);
