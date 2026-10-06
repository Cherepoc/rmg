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
    public const int PadTrack = 7;
    public const int CounterMelodyTrack = 8;
    public const int RiffTrack = 9;
    public const int RhythmTrack = 10;
    public const int RiffTwinTrack = 11;
    public const int SoloTrack = 12;

    /// <summary>How much fuller the rhythm part's rhythm is than a track's, as a comping part strums more often than the chords.</summary>
    public const double RhythmPartFullness = 0.2;

    private SongTracks(
        ImmutableSortedDictionary<int, IInstrumentTrack> definitions,
        ImmutableArray<PercussionInstrumentDefinition> songDrums,
        ImmutableArray<TrackGroup> groups,
        double bassLeading,
        DrumSetup drumSetup
    )
    {
        Definitions = definitions;
        SongDrums = songDrums;
        DrumSetup = drumSetup;
        Groups = groups;
        BassLeading = bassLeading;
        NonGroupedTrackNumbers = [..definitions.Keys.Except(groups.SelectMany(x => x.TrackNumbers))];
    }

    public ImmutableSortedDictionary<int, IInstrumentTrack> Definitions { get; }

    /// <summary>What the song's drums are: the drum kit, the kit with some percussion, or percussion alone.</summary>
    public DrumSetup DrumSetup { get; }

    /// <summary>The drums the song has; a section plays some of them.</summary>
    public ImmutableArray<PercussionInstrumentDefinition> SongDrums { get; }

    /// <summary>The tracks that share their rhythm, which are the drums.</summary>
    public ImmutableArray<TrackGroup> Groups { get; }

    /// <summary>The pitched tracks, which make their patterns each on its own.</summary>
    public ImmutableSortedSet<int> NonGroupedTrackNumbers { get; }

    /// <summary>How much the bass leads into the chords in the song, before a section moves it.</summary>
    public double BassLeading { get; }

    /// <param name="rhythmicUnconventionality">How far the song's rhythm strays, which its tracks' layers are scaled by.</param>
    /// <param name="strokeContext">The sequence the drums' strokes in the song are drawn from (<see cref="DrumStrokes" />).</param>
    /// <param name="roleContext">The sequence the drums' roles in the song are drawn from (<see cref="DrumRoles" />).</param>
    /// <param name="drumSetup">What the song's drums are (<see cref="DrumSetups" />).</param>
    /// <param name="panningContext">The sequence the pitched tracks' places from left to right are drawn from (<see cref="Panning" />).</param>
    /// <param name="padContext">The sequence the pad's instrument and state are drawn from.</param>
    /// <param name="counterContext">The sequence the counter-melody's instrument and state are drawn from.</param>
    /// <param name="riffContext">The sequence the riff's instrument and state are drawn from.</param>
    /// <param name="rhythmContext">The sequence the rhythm part's instrument, register and state are drawn from.</param>
    /// <param name="soloContext">The sequence the solo part's instrument and state are drawn from.</param>
    /// <param name="hasTwin">Whether the song's riff has a twin, which the two play apart, on either side.</param>
    /// <param name="givenDrums">The drums given in place of those the song draws, which it still draws, so that the rest is as it was.</param>
    /// <param name="genre">The genre whose pools the instruments and the drums keep to, and whose rhythms and feels the parts play.</param>
    /// <param name="feelStreams">The sequence a part's own feel is drawn from, where the genre names one.</param>
    /// <param name="feel">The feel facet of the song's unconventionality, which a part's own feel is drawn by.</param>
    /// <param name="sound">The sound facet of the song's unconventionality, which a genre's pools of the instruments lean by.</param>
    public static SongTracks Create(
        IGenerationContext context,
        IGenerationContext panningContext,
        IGenerationContext padContext,
        IGenerationContext counterContext,
        IGenerationContext riffContext,
        IGenerationContext rhythmContext,
        IGenerationContext soloContext,
        bool hasTwin,
        RhythmicUnconventionality rhythmicUnconventionality,
        IGenerationContext strokeContext,
        IGenerationContext roleContext,
        DrumSetup drumSetup,
        ImmutableArray<PercussionInstrumentDefinition>? givenDrums,
        Genre genre,
        Func<RhythmPart, IGenerationContext> feelStreams,
        double feel,
        double sound,
        ImmutableHashSet<RhythmPart> pinned
    )
    {
        // a part's level, louder or quieter by the genre's (GenreChoices.Levels)
        double Level(TrackRole role) => VelocityLayers.GetLevel(role) + VelocityLayers.GenreLevel * genre.Shift(GenreChoices.Levels, GenreChoices.Name(role.ToString()));
        // a part's own feel over the song's, drawn once from its own sequence where the genre names it
        var partFeels = new Dictionary<RhythmPart, StateMap>();
        StateMap FeelOf(RhythmPart part) => genre.Names(GenreChoices.PartFeels[part])
            ? partFeels.TryGetValue(part, out var drawn) ? drawn
            : partFeels[part] = Feels.At(StateDepths.Track, Feels.Draw(feelStreams(part), feel, genre.Pool<int>(GenreChoices.PartFeels[part], Feels.NameOf)))
            : StateMap.Default;

        var trackRhythmLayer = rhythmicUnconventionality.Lean(RhythmLayers.Track);
        // a pinned part's own layer moving none of its rhythm (PatternGenerator)
        RhythmLayer RhythmLayerOf(TrackRole role) => GenreRhythm.PartOf(role) is { } part && pinned.Contains(part) ? RhythmLayers.Still : trackRhythmLayer;
        // an instrument's weight in the genre's pool of its role, at the sound facet
        Func<RoleInstrument, double>? Pool(TrackRole role) => InstrumentsOverTime.PoolOf(role, sound, genre);

        // the melody plays something other than the chords, so the two can be told apart
        var chordsInstrument = InstrumentRoles.Chords.Pick(context, sound, Pool(TrackRole.Chords));
        var melodyInstrument = InstrumentRoles.Melody.Pick(context, sound, Pool(TrackRole.Melody), chordsInstrument.Program);
        var bassInstrument = InstrumentRoles.Bass.Pick(context, sound, Pool(TrackRole.Bass));

        // how much the bass leads into the chords: its instrument sets where the song starts, a section moves it
        var bassLeading = bassInstrument.Leading + BassLeadingLayers.CreateGenerator(BassLeadingLayers.Song)(context);
        var minOctaveOffsetGenerator = Generators.Int(-2, 1).WithContext(context);
        var maxOctaveOffsetGenerator = Generators.Int(0, 3).WithContext(context);

        var pans = Panning.Draw(
            panningContext,
            new Dictionary<int, TrackRole>
            {
                [ChordsTrack] = TrackRole.Chords, [MelodyTrack] = TrackRole.Melody, [BassTrack] = TrackRole.Bass, [PadTrack] = TrackRole.Pad,
                [CounterMelodyTrack] = TrackRole.CounterMelody, [RiffTrack] = TrackRole.Riff,
                [RhythmTrack] = TrackRole.Rhythm
            },
            genre
        );
        // a riff and its twin all the way to either side, the riff on the side it drew; the twin draws no pan, so that a
        // song without it pans as it would
        var riffSide = Math.Sign(pans[RiffTrack]) is 0 ? 1 : Math.Sign(pans[RiffTrack]);
        pans = hasTwin ? pans.SetItem(RiffTrack, riffSide).Add(RiffTwinTrack, -riffSide) : pans.Add(RiffTwinTrack, -pans[RiffTrack]);
        StateTrace.Record(TracePoints.Panning, ChordsTrack, 0, 0, StateMap.Default, 0, string.Join(", ", pans.Select(x => $"{x.Key} {x.Value:F2}")), pans);

        // the pad holds chords in a sound of its own, apart from the chords', which it plays under
        var padInstrument = InstrumentRoles.Pad.Pick(padContext, sound, Pool(TrackRole.Pad), chordsInstrument.Program);
        // and the counter-melody a line in a sound of its own, apart from the melody's and the chords'
        var counterInstrument = InstrumentRoles.CounterMelody.Pick(counterContext, sound, Pool(TrackRole.CounterMelody), melodyInstrument.Program, chordsInstrument.Program);
        // and the riff one of its own, apart from the melody's and the chords'
        var riffInstrument = InstrumentRoles.Riff.Pick(riffContext, sound, Pool(TrackRole.Riff), melodyInstrument.Program, chordsInstrument.Program);
        // and the rhythm part comps in a sound of its own, apart from the chords' and the riff's, in a register of its own
        var rhythmInstrument = InstrumentRoles.Rhythm.Pick(rhythmContext, sound, Pool(TrackRole.Rhythm), chordsInstrument.Program, riffInstrument.Program);

        // and the solo part a lead of its own, apart from the melody's, which it takes over from
        var soloInstrument = InstrumentRoles.Solo.Pick(soloContext, sound, Pool(TrackRole.Solo), melodyInstrument.Program);

        // the riff's state, which its twin shares, so that the two play its notes alike
        var riffLayer = LayerStates.CreateTrackLayer(
            riffContext,
            "Track",
            new StateMapBuilder("Track role", perTrack: true)
                .Add(CompositionStateKinds.NoteDynamics, VelocityLayers.GetDynamics(TrackRole.Riff))
                .Add(CompositionStateKinds.LineStepwiseness, RiffLayers.Stepwiseness)
                .Add(CompositionStateKinds.Rhythm.Fullness, RiffLayers.Fullness)
                .ToStateMap(riffContext),
            _ => Level(TrackRole.Riff),
            RhythmLayerOf(TrackRole.Riff)
        );
        var definitions = new Dictionary<int, IInstrumentTrack>
        {
            // the solo part, near the middle as the melody is, on its other side, drawing no pan of its own, so that a song
            // pans as it would; its notes are a solo's alone (Solos)
            [SoloTrack] = new PitchInstrumentTrack(
                LayerStates.CreateTrackLayer(
                    soloContext,
                    "Track",
                    new StateMapBuilder("Track role", perTrack: true)
                        .Add(CompositionStateKinds.NoteDynamics, VelocityLayers.GetDynamics(TrackRole.Solo))
                        .ToStateMap(soloContext),
                    _ => Level(TrackRole.Solo),
                    RhythmLayerOf(TrackRole.Solo)
                ),
                soloInstrument.Program,
                -1,
                1,
                TrackRole.Solo,
                -pans[MelodyTrack]
            ),
            // the twin plays the riff's instrument in the riff's state, its notes the riff's, moved (LineDoubling)
            [RiffTwinTrack] = new PitchInstrumentTrack(riffLayer, riffInstrument.Program, -1, 0, TrackRole.RiffTwin, pans[RiffTwinTrack]),
            [RhythmTrack] = new PitchInstrumentTrack(
                LayerStates.CreateTrackLayer(
                    rhythmContext,
                    "Track",
                    new StateMapBuilder("Track role", perTrack: true)
                        .Add(CompositionStateKinds.NoteDynamics, VelocityLayers.GetDynamics(TrackRole.Rhythm))
                        // as the chords', its instrument sets how smoothly its chords move, and the song moves it a little
                        .Add(StateKinds.VoiceLeading, VoiceLeadingLayers.CreateGenerator(VoiceLeadingLayers.Song).Then(x => rhythmInstrument.Leading + x))
                        .Add(CompositionStateKinds.Rhythm.Fullness, RhythmPartFullness)
                        .ToStateMap(rhythmContext),
                    _ => Level(TrackRole.Rhythm),
                    RhythmLayerOf(TrackRole.Rhythm)
                ),
                rhythmInstrument.Program,
                Generators.Int(-1, 1).WithContext(rhythmContext)(),
                Generators.Int(1, 3).WithContext(rhythmContext)(),
                TrackRole.Rhythm,
                pans[RhythmTrack]
            ),
            [RiffTrack] = new PitchInstrumentTrack(
                riffLayer,
                riffInstrument.Program,
                -1,
                0,
                TrackRole.Riff,
                pans[RiffTrack]
            ),
            [CounterMelodyTrack] = new PitchInstrumentTrack(
                LayerStates.CreateTrackLayer(
                    counterContext,
                    "Track",
                    new StateMapBuilder("Track role", perTrack: true)
                        .Add(CompositionStateKinds.NoteDynamics, VelocityLayers.GetDynamics(TrackRole.CounterMelody))
                        .Add(CompositionStateKinds.LineStepwiseness, CounterLayers.Stepwiseness)
                        .Add(CompositionStateKinds.Rhythm.Period.Power, CounterLayers.SlowerBy)
                        .Add(CompositionStateKinds.Rhythm.Fullness, CounterLayers.Fullness)
                        .ToStateMap(counterContext),
                    _ => Level(TrackRole.CounterMelody),
                    RhythmLayerOf(TrackRole.CounterMelody)
                ),
                counterInstrument.Program,
                -1,
                0,
                TrackRole.CounterMelody,
                pans[CounterMelodyTrack]
            ),
            [PadTrack] = new PitchInstrumentTrack(
                LayerStates.CreateTrackLayer(
                    padContext,
                    "Track",
                    new StateMapBuilder("Track role", perTrack: true)
                        .Add(CompositionStateKinds.NoteDynamics, VelocityLayers.GetDynamics(TrackRole.Pad))
                        .Add(StateKinds.VoiceLeading, padInstrument.Leading)
                        .ToStateMap(padContext),
                    _ => Level(TrackRole.Pad),
                    RhythmLayerOf(TrackRole.Pad)
                ),
                padInstrument.Program,
                -1,
                1,
                TrackRole.Pad,
                pans[PadTrack]
            ),
            [ChordsTrack] = new PitchInstrumentTrack(
                LayerStates.CreateTrackLayer(
                    context,
                    "Track",
                    new StateMapBuilder("Track role", perTrack: true)
                        .Add(CompositionStateKinds.NoteDynamics, VelocityLayers.GetDynamics(TrackRole.Chords))
                        // the chord instrument sets how smoothly the chords move, and the song moves it a little
                        .Add(StateKinds.VoiceLeading, VoiceLeadingLayers.CreateGenerator(VoiceLeadingLayers.Song).Then(x => chordsInstrument.Leading + x))
                        .ToStateMap(context),
                    _ => Level(TrackRole.Chords),
                    RhythmLayerOf(TrackRole.Chords)
                ),
                chordsInstrument.Program,
                minOctaveOffsetGenerator(),
                maxOctaveOffsetGenerator(),
                TrackRole.Chords,
                pans[ChordsTrack]
            ),
            [MelodyTrack] = new PitchInstrumentTrack(
                LayerStates.CreateTrackLayer(
                    context,
                    "Track",
                    new StateMapBuilder("Track role", perTrack: true)
                        .Add(CompositionStateKinds.NoteDynamics, VelocityLayers.GetDynamics(TrackRole.Melody))
                        // the melody instrument sets how stepwise the melody is, and the song moves it a little
                        .Add(CompositionStateKinds.LineStepwiseness, MelodyLayers.CreateGenerator(MelodyLayers.Song).Then(x => melodyInstrument.Leading + x))
                        // a melody repeats its cycles more than the other tracks, as riffs
                        .Add(CompositionStateKinds.Rhythm.Variation, MelodyLayers.RhythmVariation)
                        .ToStateMap(context),
                    _ => Level(TrackRole.Melody),
                    RhythmLayerOf(TrackRole.Melody)
                ),
                melodyInstrument.Program,
                minOctaveOffsetGenerator(),
                maxOctaveOffsetGenerator(),
                TrackRole.Melody,
                pans[MelodyTrack]
            ),
            [BassTrack] = new PitchInstrumentTrack(
                LayerStates.CreateTrackLayer(
                    context,
                    "Track",
                    new StateMapBuilder("Track role", perTrack: true)
                        .Add(CompositionStateKinds.NoteDynamics, VelocityLayers.GetDynamics(TrackRole.Bass))
                        .Add(CompositionStateKinds.LineStepwiseness, BassLeadingLayers.Stepwiseness)
                        .ToStateMap(context),
                    _ => Level(TrackRole.Bass),
                    RhythmLayerOf(TrackRole.Bass)
                ),
                bassInstrument.Program,
                -3,
                -2,
                TrackRole.Bass,
                pans[BassTrack]
            )
        };

        // the song has its own drums, and the drums picked by the section kit play in a section
        // a genre's pieces weighed against its heaviest, which a drum the songs have now and then always is (SongDrumRule.Optional)
        var drawnDrums = DrumSetups.SelectSongDrums(context, drumSetup, genre.Pool<PercussionInstrumentDefinition>(GenreChoices.DrumPieces, DrumPieces.NameOf) is { } pieces
            && ByConvention.Within(DrumGroups.AllDrums, rhythmicUnconventionality.Value, pieces) is var within
            && DrumGroups.AllDrums.Max(within) is var heaviest
                ? drum => within(drum) / heaviest
                : null);
        var songDrums = givenDrums ?? drawnDrums;
        foreach (var drumGroup in DrumGroups.All)
        {
            foreach (var drum in drumGroup.Drums.Where(songDrums.Contains))
            {
                // a drum that strikes plays its stroke, the song's here, and does not walk its sounds
                var builder = drum.ConfigureStateMap(drumGroup.ConfigureStateMap(new StateMapBuilder("Drum", perTrack: true)));
                if (!drum.Walks)
                    builder.Add(CompositionStateKinds.IncrementalArticulationOffset.Multiplier, 0.0);
                // a percussion song's figures repeat more, as an ensemble's do
                if (drumSetup == DrumSetup.Percussion)
                    builder.Add(CompositionStateKinds.Rhythm.Variation, DrumSetups.PercussionVariation);
                var drumStateMap = builder.ToStateMap(context)
                    .MergeWith(DrumStrokes.GenerateSong(strokeContext, drum, DrumSounds.OddsOf(genre)))
                    .MergeWith(DrumRoles.GenerateSong(roleContext, drum, rhythmicUnconventionality.Value, genre));
                drumStateMap = drumStateMap.MergeWith(FeelOf(DrumRoles.PartOf(DrumRoles.Of(drumStateMap))));
                // a drum of a pinned role plays its figure as given, its own layer moving none of it (PatternGenerator)
                var pins = pinned.Contains(DrumRoles.PartOf(DrumRoles.Of(drumStateMap)));
                if (pins)
                    drumStateMap = drumStateMap.MergeWith(StateMap.FromStates([CompositionStateKinds.Pinned.CreateState(1)]));
                definitions[DrumGroups.GetTrackNumber(drum)] = new PercussionInstrumentTrack(
                    LayerStates.CreateTrackLayer(context, "Track", drumStateMap, _ => VelocityLayers.GetLevel(drum),
                        pins ? RhythmLayers.Still : DrumRoles.Hold(trackRhythmLayer, DrumRoles.Of(drumStateMap))),
                    drum.Sounds
                );
            }
        }

        // every pitched part's rhythm and feel as the genre gives them, a riff's twin's as the riff's
        foreach (var (track, pitched) in definitions.Where(x => x.Value is PitchInstrumentTrack).Select(x => (x.Key, (PitchInstrumentTrack)x.Value)).ToArray())
        {
            var part = GenreRhythm.PartOf(pitched.Role);
            // a pinned part plays its song's rhythm settings in every bar
            var pin = part is { } held && pinned.Contains(held) ? StateMap.FromStates([CompositionStateKinds.Pinned.CreateState(1)]) : StateMap.Default;
            if (part is { } moved && (genre.Names(GenreChoices.Rhythms[moved]) || genre.Names(GenreChoices.PartFeels[moved]) || pin != StateMap.Default))
                definitions[track] = new PitchInstrumentTrack(
                    pitched.StateMap.MergeWith(GenreRhythm.Of(genre, GenreChoices.Rhythms[moved])).MergeWith(FeelOf(moved)).MergeWith(pin),
                    pitched.InstrumentCode,
                    pitched.MinOctaveOffset,
                    pitched.MaxOctaveOffset,
                    pitched.Role,
                    pitched.Pan
                );
        }

        // every pitched part within its instrument's range, and the melody over the chords (Registers)
        foreach (var (track, pitched) in definitions.Where(x => x.Value is PitchInstrumentTrack).Select(x => (x.Key, (PitchInstrumentTrack)x.Value)).ToArray())
            definitions[track] = Registers.WithinInstrument(pitched, genre);
        (definitions[MelodyTrack], definitions[ChordsTrack]) = Registers.Apart((PitchInstrumentTrack)definitions[MelodyTrack], (PitchInstrumentTrack)definitions[ChordsTrack]);

        // the drums share the rhythm-related state
        ImmutableArray<TrackGroup> groups =
        [
            new(
                [..songDrums.Select(DrumGroups.GetTrackNumber)],
                LayerStates.CreateTrackLayer(
                    context,
                    "Drum group",
                    StateMap.Default,
                    _ => Level(TrackRole.Drum),
                    rhythmicUnconventionality.Lean(RhythmLayers.DrumGroup)
                )
            )
        ];

        return new SongTracks(definitions.ToImmutableSortedDictionary(), songDrums, groups, bassLeading, drumSetup);
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
