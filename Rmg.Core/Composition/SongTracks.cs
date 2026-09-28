using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     The tracks of a song: the chords, the melody and the bass, each with an instrument of its role, and the drums
///     of the song, which share their rhythm as a group. Every track draws its own state for the whole song.
/// </summary>
internal sealed class SongTracks
{
    public const int ChordsTrack = 4;
    public const int MelodyTrack = 5;
    public const int BassTrack = 6;

    private SongTracks(
        ImmutableSortedDictionary<int, IInstrumentTrack> definitions,
        ImmutableArray<PercussionInstrumentDefinition> songDrums,
        ImmutableArray<TrackGroup> groups,
        double bassLeading
    )
    {
        Definitions = definitions;
        SongDrums = songDrums;
        Groups = groups;
        BassLeading = bassLeading;
        NonGroupedTrackNumbers = [..definitions.Keys.Except(groups.SelectMany(x => x.TrackNumbers))];
    }

    public ImmutableSortedDictionary<int, IInstrumentTrack> Definitions { get; }

    /// <summary>The drums the song has; a section plays some of them.</summary>
    public ImmutableArray<PercussionInstrumentDefinition> SongDrums { get; }

    /// <summary>The tracks that share their rhythm, which are the drums.</summary>
    public ImmutableArray<TrackGroup> Groups { get; }

    /// <summary>The pitched tracks, which make their patterns each on its own.</summary>
    public ImmutableSortedSet<int> NonGroupedTrackNumbers { get; }

    /// <summary>How much the bass leads into the chords in the song, before a section moves it.</summary>
    public double BassLeading { get; }

    /// <param name="rhythmicUnconventionality">How far the song's rhythm strays, which its tracks' layers are scaled by.</param>
    public static SongTracks Create(IGenerationContext context, RhythmicUnconventionality rhythmicUnconventionality)
    {
        var trackRhythmLayer = rhythmicUnconventionality.Scale(RhythmLayers.Track);

        // the melody plays something other than the chords, so the two can be told apart
        var chordsInstrument = InstrumentRoles.Chords.Pick(context);
        var melodyInstrument = InstrumentRoles.Melody.Pick(context, chordsInstrument.Program);
        var bassInstrument = InstrumentRoles.Bass.Pick(context);

        // how much the bass leads into the chords: its instrument sets where the song starts, a section moves it
        var bassLeading = bassInstrument.Leading + BassLeadingLayers.CreateGenerator(BassLeadingLayers.Song)(context);
        var minOctaveOffsetGenerator = Generators.Int(-2, 1).WithContext(context);
        var maxOctaveOffsetGenerator = Generators.Int(0, 3).WithContext(context);

        var definitions = new Dictionary<int, IInstrumentTrack>
        {
            [ChordsTrack] = new PitchInstrumentTrack(
                LayerStates.CreateTrackLayer(
                    context,
                    "Track",
                    new StateMapBuilder("Track role", perTrack: true)
                        .Add(CompositionStateKinds.NoteDynamics, VelocityLayers.GetDynamics(TrackRole.Chords))
                        // the chords play whole: their root and their notes do not walk
                        .Add(CompositionStateKinds.IncrementalChordRootNoteOffset.Multiplier, 0)
                        .Add(CompositionStateKinds.IncrementalChordNoteOffset.Multiplier, 0)
                        // the chord instrument sets how smoothly the chords move, and the song moves it a little
                        .Add(StateKinds.VoiceLeading, VoiceLeadingLayers.CreateGenerator(VoiceLeadingLayers.Song).Then(x => chordsInstrument.Leading + x))
                        .ToStateMap(context),
                    _ => VelocityLayers.GetLevel(TrackRole.Chords),
                    trackRhythmLayer
                ),
                chordsInstrument.Program,
                minOctaveOffsetGenerator(),
                maxOctaveOffsetGenerator(),
                TrackRole.Chords
            ),
            [MelodyTrack] = new PitchInstrumentTrack(
                LayerStates.CreateTrackLayer(
                    context,
                    "Track",
                    new StateMapBuilder("Track role", perTrack: true)
                        .Add(CompositionStateKinds.NoteDynamics, VelocityLayers.GetDynamics(TrackRole.Melody))
                        // the melody keeps to the chords: its root does not walk away from theirs
                        .Add(CompositionStateKinds.IncrementalChordRootNoteOffset.Multiplier, 0)
                        // the melody instrument sets how stepwise the melody is, and the song moves it a little
                        .Add(CompositionStateKinds.MelodyStepwiseness, MelodyLayers.CreateGenerator(MelodyLayers.Song).Then(x => melodyInstrument.Leading + x))
                        .Add(StateKinds.MelodyLine, 1)
                        // a melody repeats its cycles more than the other tracks, as riffs
                        .Add(CompositionStateKinds.Rhythm.Variation, MelodyLayers.RhythmVariation)
                        .ToStateMap(context),
                    _ => VelocityLayers.GetLevel(TrackRole.Melody),
                    trackRhythmLayer
                ),
                melodyInstrument.Program,
                minOctaveOffsetGenerator(),
                maxOctaveOffsetGenerator(),
                TrackRole.Melody
            ),
            [BassTrack] = new PitchInstrumentTrack(
                LayerStates.CreateTrackLayer(
                    context,
                    "Track",
                    new StateMapBuilder("Track role", perTrack: true)
                        .Add(CompositionStateKinds.NoteDynamics, VelocityLayers.GetDynamics(TrackRole.Bass))
                        // the bass keeps close to the root and walks through the chord's notes
                        .Add(CompositionStateKinds.IncrementalChordRootNoteOffset.Multiplier, Generators.AbsSplineValue())
                        .Add(CompositionStateKinds.IncrementalChordNoteOffset.Multiplier, Generators.AbsSplineValue().Then(x => 1 - x))
                        // the bass plays the chord roots, so its line leads into the chords and lands on them
                        .Add(StateKinds.FollowsChordRoots, 1)
                        .ToStateMap(context),
                    _ => VelocityLayers.GetLevel(TrackRole.Bass),
                    trackRhythmLayer
                ),
                bassInstrument.Program,
                -3,
                -2,
                TrackRole.Bass
            )
        };

        // the song has its own drums, and the drums picked by the section kit play in a section
        var songDrums = DrumKitGenerator.SelectSongDrums(context);
        foreach (var drumGroup in DrumGroups.All)
        {
            foreach (var drum in drumGroup.Drums.Where(songDrums.Contains))
            {
                var drumStateMap = drum.ConfigureStateMap(drumGroup.ConfigureStateMap(new StateMapBuilder("Drum", perTrack: true)))
                    .ToStateMap(context);
                definitions[DrumGroups.GetTrackNumber(drum)] = new PercussionInstrumentTrack(
                    LayerStates.CreateTrackLayer(context, "Track", drumStateMap, _ => VelocityLayers.GetLevel(drum), trackRhythmLayer),
                    drum.ArticulationCodes
                );
            }
        }

        // the drums share the rhythm-related state
        ImmutableArray<TrackGroup> groups =
        [
            new(
                [..songDrums.Select(DrumGroups.GetTrackNumber)],
                LayerStates.CreateTrackLayer(
                    context,
                    "Drum group",
                    StateMap.Default,
                    _ => VelocityLayers.GetLevel(TrackRole.Drum),
                    rhythmicUnconventionality.Scale(RhythmLayers.DrumGroup)
                )
            )
        ];

        return new SongTracks(definitions.ToImmutableSortedDictionary(), songDrums, groups, bassLeading);
    }

    /// <summary>
    ///     The state a track definition brings into the generation of its notes. Render applies the definition's
    ///     rendered state to every note of the track, so only the composition state is taken here.
    /// </summary>
    internal static StateMap GetGenerationStateMap(IInstrumentTrack trackDefinition)
    {
        return trackDefinition.StateMap.OfScope(StateScope.Composition);
    }
}
