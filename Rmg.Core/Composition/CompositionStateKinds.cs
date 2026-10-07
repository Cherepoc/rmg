using System.Collections.Immutable;
using Rmg.Core.Events;

namespace Rmg.Core.Composition;

internal static class CompositionStateKinds
{
    private const string Prefix = "Composition";

    public static RhythmStateKinds Rhythm { get; } = new(Prefix);

    public static FillStateKinds Fill { get; } = new(Prefix);

    public static StateKind<int> ValueSeed { get; } = StateKinds.CreateAdditive<int>(Prefix + "ValueSeedValue");

    public static IncrementalStateKinds IncrementalArticulationOffset { get; } =
        new(Prefix + StateKinds.ArticulationOffset.Name);

    // how much a line, such as the melody, moves by step rather than by leap, from 0 to 1
    public static StateKind<double> LineStepwiseness { get; } = StateKinds.CreateAdditive<double>(Prefix + "LineStepwiseness");

    // how strong a note's beat is, 0 the strongest, from its rhythm pattern; kept by the song, as the render state is,
    // for placing its lines once it is put together, as the line state below is
    public static StateKind<int> BeatRank { get; } = StateKinds.CreateAdditive<int>(Prefix + "BeatRank", StateScope.Render);

    // how far a line's note means to go: 1 a step, 2 a leap, 0 the same note
    public static StateKind<int> LineStep { get; } = StateKinds.CreateAdditive<int>(Prefix + "LineStep", StateScope.Render);

    // a line's note's draw, from 0 to 1, of whether it goes on the way the line goes or turns back, which the aim of
    // its phrase leans (LineProfile.GetContinueChance)
    public static StateKind<double> LineTurn { get; } = StateKinds.CreateAdditive<double>(Prefix + "LineTurn", StateScope.Render);

    // which note of a figure a line's note is: the key of its beat in its bar pattern's rhythm, the same for a beat of a
    // bar that comes back and for a beat of a cycle that repeats the one before, so that the note plays again, and is
    // mutated alike, wherever the figure does; 0 for none
    public static StateKind<int> NoteKey { get; } = StateKinds.CreateAdditive<int>(Prefix + "NoteKey", StateScope.Render);

    // how far above or below the middle of its range a line aims in a bar, in semitones, for the phrase's shape
    public static StateKind<double> LineRegister { get; } = StateKinds.CreateAdditive<double>(Prefix + "LineRegister", StateScope.Render);

    // a hooked line's bar's letter in its phrase's scheme, from 1, where its section hooks (LinePattern.Hook); 0 for none
    public static StateKind<int> LineHookBar { get; } = StateKinds.CreateAdditive<int>(Prefix + "LineHookBar", StateScope.Render);

    // how far a section moves a line's range up or down its instrument's, with where it aims, in semitones, such as a
    // chorus's melody over its verse's (MelodyLayers.EnergyRegister)
    public static StateKind<double> LineLift { get; } = StateKinds.CreateAdditive<double>(Prefix + "LineLift", StateScope.Render);

    // the chance a line's note on a strong beat lands on a tension, a note of the scale outside its chord, rather than on
    // one of the chord's (LinePattern.Tensions)
    public static StateKind<double> LineTension { get; } = StateKinds.CreateAdditive<double>(Prefix + "LineTension", StateScope.Render);

    // how a line's note's bar leads out into the next chord (a ChordApproach), and what its first note lands on (a
    // ChordArrival), the line's own, where bar state would be every track's
    public static StateKind<int> LineApproach { get; } = StateKinds.CreateAdditive<int>(Prefix + "LineApproach", StateScope.Render);

    // the notes a line's passing notes take (a LineScale), a melody's or a riff's (Line), 0 for all of the section's scale
    public static StateKind<int> LineScale { get; } = StateKinds.CreateAdditive<int>(Prefix + "LineScale", StateScope.Render);
    public static StateKind<int> LineLanding { get; } = StateKinds.CreateAdditive<int>(Prefix + "LineLanding", StateScope.Render);

    // how a note is played beyond its pitch, as Expression decides it: its vibrato in cents, how it bends (a BendKind),
    // 1 for a swell, its tremolo's and its sweep's depth in percent, its echo's delay in 16ths, and its reverb, 1 more
    // than the send, and chorus, from 0 to 127
    public static StateKind<int> Vibrato { get; } = StateKinds.CreateAdditive<int>(Prefix + "Vibrato", StateScope.Render);

    // a wide vibrato, a guitar's: how many semitones above the note it reaches, a scale note, and back
    public static StateKind<int> WideVibrato { get; } = StateKinds.CreateAdditive<int>(Prefix + "WideVibrato", StateScope.Render);
    public static StateKind<int> Bend { get; } = StateKinds.CreateAdditive<int>(Prefix + "Bend", StateScope.Render);

    // how far a bend goes, in semitones, up or down, to the scale note its size in scale steps reaches
    public static StateKind<int> BendDepth { get; } = StateKinds.CreateAdditive<int>(Prefix + "BendDepth", StateScope.Render);
    public static StateKind<int> Swell { get; } = StateKinds.CreateAdditive<int>(Prefix + "Swell", StateScope.Render);
    public static StateKind<int> Tremolo { get; } = StateKinds.CreateAdditive<int>(Prefix + "Tremolo", StateScope.Render);

    // how far a note is played off its place, in beats, late above 0, by its part's role (Pocket.Apply)
    public static StateKind<double> TimingOffset { get; } = StateKinds.CreateAdditive<double>(Prefix + "TimingOffset", StateScope.Render);

    // 1 for a note palm-muted, its articulation, which it keeps its duration through: the rendering plays it, for General
    // MIDI short and a touch softer, for a plugin as a keyswitch or a controller ahead (Render)
    public static StateKind<int> PalmMute { get; } = StateKinds.CreateAdditive<int>(Prefix + "PalmMute", StateScope.Render);

    // how far apart in beats a guitar's chord strikes its strings, its stroke, down from the lowest above 0 and up from the
    // highest below, the rendering spreading its notes (Articulations.Apply)
    public static StateKind<double> Strum { get; } = StateKinds.CreateAdditive<double>(Prefix + "Strum", StateScope.Render);

    // 1 for a note slurred into from the note before, its articulation: the note before held into it, and for General MIDI
    // its attack softer, for a plugin a legato keyswitch (Render)
    public static StateKind<int> Legato { get; } = StateKinds.CreateAdditive<int>(Prefix + "Legato", StateScope.Render);

    // a note's ornament, a grace note's, a turn's or a trill's quick notes before it, each in semitones from it, 0 the note
    // itself, and how long each lasts in beats, the rendering playing them from the note's start and the note after them
    // (Ornaments.Apply); none for none
    public static StateKind<ImmutableArray<int>> Ornament { get; } = StateKinds.CreateCollection<int>(Prefix + "Ornament", StateScope.Render);

    public static StateKind<double> OrnamentStep { get; } = StateKinds.CreateAdditive<double>(Prefix + "OrnamentStep", StateScope.Render);

    // how many semitones a chord's root voice falls inside it, a chromatic line's, 1 to 3 a quarter of the chord, its scale
    // altered to hold it where it has it not; 0 for none (InnerLines.Apply)
    public static StateKind<int> InnerLine { get; } = StateKinds.CreateAdditive<int>(Prefix + "InnerLine", StateScope.Render);

    // 1 for a drum a song pins to its figure, which plays its song's rhythm settings in every bar (DrumRoles.Pins)
    public static StateKind<int> Pinned { get; } = StateKinds.CreateAdditive<int>(Prefix + "Pinned");

    // 1 for a line chugging on a pedal, its weak notes mostly on its chord's root at the bottom of its range and its accents
    // free to take any note of its scale, as a chugging riff's (Chugs)
    public static StateKind<int> LinePedal { get; } = StateKinds.CreateAdditive<int>(Prefix + "LinePedal", StateScope.Render);

    // 1 for a riff's note played as a power chord, its perfect fifth over it and its octave where the range has it
    public static StateKind<int> PowerChord { get; } = StateKinds.CreateAdditive<int>(Prefix + "PowerChord", StateScope.Render);

    // how deep a pump ducks the note on every beat, in percent of its expression, 0 for none (Expression.PumpDepth)
    public static StateKind<int> Pump { get; } = StateKinds.CreateAdditive<int>(Prefix + "Pump", StateScope.Render);
    public static StateKind<int> AutoPan { get; } = StateKinds.CreateAdditive<int>(Prefix + "AutoPan", StateScope.Render);
    public static StateKind<int> Echo { get; } = StateKinds.CreateAdditive<int>(Prefix + "Echo", StateScope.Render);

    // a filter's movement (the StrongEffect it is), how far it closes, in percent, and its cycle or its opening's length, in
    // beats, counted from its section's start (ExpressionRender)
    public static StateKind<int> Filter { get; } = StateKinds.CreateAdditive<int>(Prefix + "Filter", StateScope.Render);
    public static StateKind<int> FilterDepth { get; } = StateKinds.CreateAdditive<int>(Prefix + "FilterDepth", StateScope.Render);
    public static StateKind<double> FilterCycle { get; } = StateKinds.CreateAdditive<double>(Prefix + "FilterCycle", StateScope.Render);

    // a part's tone, how far below its sound's own its brightness sits in its section, in the brightness controller's
    // steps, 0 for its own, which a filter's movement closes further from (Expression.Tone)
    public static StateKind<int> Tone { get; } = StateKinds.CreateAdditive<int>(Prefix + "Tone", StateScope.Render);

    // the 16ths a trance gate lets a held note sound in, a bar's sixteen as bits from its first, 0 for none (Expression.Gate)
    public static StateKind<int> Gate { get; } = StateKinds.CreateAdditive<int>(Prefix + "Gate", StateScope.Render);

    public static StateKind<int> Reverb { get; } = StateKinds.CreateAdditive<int>(Prefix + "Reverb", StateScope.Render);
    public static StateKind<int> Chorus { get; } = StateKinds.CreateAdditive<int>(Prefix + "Chorus", StateScope.Render);

    // the General MIDI program a note plays, 1 more than its number, 0 for its track's own
    public static StateKind<int> Program { get; } = StateKinds.CreateAdditive<int>(Prefix + "Program", StateScope.Render);

    // how a section's chords are broken, an ArpeggioPattern, 0 for the whole chord at once
    public static StateKind<int> Arpeggio { get; } = StateKinds.CreateAdditive<int>(Prefix + "Arpeggio", StateScope.Render);

    // the tone a part breaking its chord lays on it, a ChordColour, 0 for none
    public static StateKind<int> Colour { get; } = StateKinds.CreateAdditive<int>(Prefix + "Colour", StateScope.Render);

    // 1 for a note of a solo's line, which any part plays as a line, one note at a time, rather than as its part would
    public static StateKind<int> LineSolo { get; } = StateKinds.CreateAdditive<int>(Prefix + "LineSolo", StateScope.Render);

    // 1 for a solo's release, the held note a phrase ends on
    public static StateKind<int> SoloRelease { get; } = StateKinds.CreateAdditive<int>(Prefix + "SoloRelease", StateScope.Render);

    // the part whose sound a note plays in, its role's number plus 1, as a counter-melody doubling the melody in its
    // timbre does; 0 for its own part's
    public static StateKind<int> SoundsAs { get; } = StateKinds.CreateAdditive<int>(Prefix + "SoundsAs", StateScope.Render);

    // 1 for a bass note holding a pedal under a key change, which is not struck again on the chord where the chord changes
    public static StateKind<int> BassPedal { get; } = StateKinds.CreateAdditive<int>(Prefix + "BassPedal", StateScope.Render);

    // how many notes a line's note stacks, its own on top and the chord's below, as a solo's or a riff's landing may, 0 or 1 for the note alone
    public static StateKind<int> LineVoices { get; } = StateKinds.CreateAdditive<int>(Prefix + "LineVoices", StateScope.Render);

    // 1 for a line's stacked note that planes, its chord's shape moved with it whatever the band plays, rather than the band's chord under it
    public static StateKind<int> LinePlanes { get; } = StateKinds.CreateAdditive<int>(Prefix + "LinePlanes", StateScope.Render);

    // how intense a solo is at a note, from 0 to 100, which leans how it is played
    public static StateKind<int> SoloIntensity { get; } = StateKinds.CreateAdditive<int>(Prefix + "SoloIntensity", StateScope.Render);

    // 1 for a line's note played in octaves by its own part, the note and the note an octave from it
    public static StateKind<int> Octave { get; } = StateKinds.CreateAdditive<int>(Prefix + "Octave", StateScope.Render);

    // how a line's note starts its phrase (a PhraseStart): afresh, at where the phrase aims, or going on from the note
    // before, as the line's freedom to change register draws it (LineProfile.RegisterFreedom); none for a note within one
    public static StateKind<int> LinePhraseStart { get; } = StateKinds.CreateAdditive<int>(Prefix + "LinePhraseStart", StateScope.Render);

    // 1 for a note added in the pulse before a chord change for the line to lead into the next chord on, as the bass's pickup, which
    // stays only where the line does lead into a new chord (LinePattern.Place)
    public static StateKind<int> LinePickup { get; } = StateKinds.CreateAdditive<int>(Prefix + "LinePickup", StateScope.Render);

    // where the melody's phrase ends in its last bar: 0 for no end, or the beat, from 1 to 3, before which its last note
    // starts; it holds that note, and rests until the next phrase
    public static StateKind<int> MelodyPhraseEnd { get; } = StateKinds.CreateAdditive<int>(Prefix + "MelodyPhraseEnd");

    // the chord of a bar with a role in the phrase, such as the home chord, in place of the pool's pick; empty or one
    public static StateKind<ImmutableArray<Chord>> RoleChord { get; } =
        StateKinds.CreateCollection<Chord>(Prefix + "RoleChord", isShared: true);

    // how far a track's notes move from its level, by their beats' accents and at random: less for a bass that hits its
    // beats alike, more for a melody; a section's conventionality moves it
    public static StateKind<double> NoteDynamics { get; } = StateKinds.CreateMultiplicative<double>(Prefix + "NoteDynamics");

    // a drum's role in the groove (a DrumRole), set by the lowest layer, such as a section's over its song's
    public static StateKind<LayerValue<int>> DrumRole { get; } = StateKinds.CreateLowestLayerWins<int>(Prefix + "DrumRole");

    // how loud and busy a section is meant to be, around 0: it leans the section's draws, never decides them
    public static StateKind<double> Energy { get; } = StateKinds.CreateAdditive<double>(Prefix + "Energy", isShared: true);

    // every track plays the same chord, so the pool and the pick are the same for all of them
    public static PoolStateKinds<Chord> ChordPool { get; } = new(Prefix + "ChordPool", isShared: true);

    public sealed class IncrementalStateKinds
    {
        private readonly ImmutableArray<IStateKind> _all;

        public IncrementalStateKinds(string prefix)
        {
            prefix += "Incremental";

            ConsecutiveOffset = StateKinds.CreateAdditive<double>(prefix + "ConsecutiveOffset");
            RandomOffset = StateKinds.CreateAdditive<double>(prefix + "RandomOffset");
            Multiplier = StateKinds.CreateMultiplicative<double>(prefix + "Multiplier");

            _all =
            [
                ConsecutiveOffset,
                RandomOffset,
                Multiplier
            ];
        }

        public StateKind<double> ConsecutiveOffset { get; }

        public StateKind<double> RandomOffset { get; }

        public StateKind<double> Multiplier { get; }

        public ImmutableArray<IStateKind> GetAll()
        {
            return _all;
        }
    }

    /// <summary>
    ///     What the fills are drawn by: the chances of their rarer choices, which the layers multiply, such as the
    ///     section's chance scale and a drummer's signature, and each drum group's chance of joining a run.
    /// </summary>
    public sealed class FillStateKinds
    {
        public FillStateKinds(string prefix)
        {
            prefix += "Fill";

            OffBeatChance = StateKinds.CreateMultiplicative<double>(prefix + "OffBeatChance");
            FadeChance = StateKinds.CreateMultiplicative<double>(prefix + "FadeChance");
            EarlyLandingChance = StateKinds.CreateMultiplicative<double>(prefix + "EarlyLandingChance");
            RunChance = StateKinds.CreateAdditive<double>(prefix + "RunChance");
            Unconventionality = StateKinds.CreateAdditive<double>(prefix + "Unconventionality");
        }

        /// <summary>The chance a fill starts a note near an 8th off the beat, earlier or later.</summary>
        public StateKind<double> OffBeatChance { get; }

        /// <summary>The chance a run fades rather than swells.</summary>
        public StateKind<double> FadeChance { get; }

        /// <summary>The chance a landing is pushed a note near an 8th early.</summary>
        public StateKind<double> EarlyLandingChance { get; }

        /// <summary>A drum group's chance of joining a run, before the section's chance scale.</summary>
        public StateKind<double> RunChance { get; }

        /// <summary>
        ///     How unconventional a drum group is in a run, the power of the section's chance scale that its run chance
        ///     is multiplied by: 0 for the snare and the toms, which a plain section plays as often as a wild one.
        /// </summary>
        public StateKind<double> Unconventionality { get; }
    }

    public sealed class RhythmStateKinds
    {
        public RhythmStateKinds(string prefix)
        {
            prefix += "Rhythm";

            Period = new PeriodStateKinds(prefix + "Period");
            Phase = new DyadicRhythmStateKinds(prefix + "Phase");
            MaxRank = StateKinds.CreateAdditive<int>(prefix + "MaxRank");
            Seed = StateKinds.CreateAdditive<int>(prefix + "SeedValue");
            RankOffset = StateKinds.CreateAdditive<int>(prefix + "RankOffset");
            Fullness = StateKinds.CreateAdditive<double>(prefix + "Fullness");
            Variation = StateKinds.CreateAdditive<double>(prefix + "Variation");
            Feel = StateKinds.CreateLowestLayerWins<int>(prefix + "Feel");
            All =
            [
                Period.Value, Period.Power, Feel, Phase.Value, Phase.Rank, Phase.RankedOffset, MaxRank, Seed, RankOffset,
                Fullness, Variation
            ];
        }

        /// <summary>Every kind of the rhythm's state, such as a drum that doubles another takes from it.</summary>
        public ImmutableArray<IStateKind> All { get; }

        public PeriodStateKinds Period { get; }

        /// <summary>
        ///     The feel its cycles play in (<see cref="Feels" />), straight, or a tuplet or a grouping of a prime: the song's,
        ///     or a section's, a bar's or a fill's where it changes it, the lowest layer's winning.
        /// </summary>
        public StateKind<LayerValue<int>> Feel { get; }

        public DyadicRhythmStateKinds Phase { get; }

        public StateKind<int> MaxRank { get; }

        public StateKind<int> Seed { get; }

        public StateKind<int> RankOffset { get; }

        /// <summary>
        ///     How much of a position's chance of being kept is left for every rank it is from the rank offset: near 1
        ///     every position is kept, as in a roll, and lower the pattern thins out.
        /// </summary>
        public StateKind<double> Fullness { get; }

        /// <summary>
        ///     The chance of a cycle drawing afresh what it keeps: 1 draws every cycle afresh, and 0 repeats the first,
        ///     as a riff.
        /// </summary>
        public StateKind<double> Variation { get; }
    }

    public sealed class PeriodStateKinds
    {
        public PeriodStateKinds(string prefix)
        {
            Value = StateKinds.CreateAdditive<double>(prefix + "Value");
            Power = StateKinds.CreateAdditive<int>(prefix + "Power");
        }

        public StateKind<double> Value { get; }

        public StateKind<int> Power { get; }
    }

    public sealed class DyadicRhythmStateKinds
    {
        public DyadicRhythmStateKinds(string prefix)
        {
            Value = StateKinds.CreateAdditive<double>(prefix + "Value");
            Rank = StateKinds.CreateAdditive<int>(prefix + "Rank");
            RankedOffset = StateKinds.CreateAdditive<double>(prefix + "RankedOffset");
        }

        public StateKind<double> Value { get; }

        public StateKind<int> Rank { get; }

        public StateKind<double> RankedOffset { get; }
    }

    /// <summary>
    ///     A pool the layers add entries to, in the order they are merged, and an index into it the layers add draws
    ///     to; the index is folded into the pool's length to pick an entry.
    /// </summary>
    public sealed class PoolStateKinds<T>
    {
        /// <param name="isShared">Whether the pool and the pick must be the same for every track.</param>
        public PoolStateKinds(string prefix, bool isShared = false)
        {
            Index = StateKinds.CreateAdditive<int>(prefix + "Index", isShared: isShared);
            Collection = StateKinds.CreateCollection<T>(prefix + "Collection", isShared: isShared);
        }

        public StateKind<int> Index { get; }

        public StateKind<ImmutableArray<T>> Collection { get; }

        /// <summary>The entry the map's index picks from its pool.</summary>
        public T Pick(StateMap stateMap)
        {
            var collection = stateMap.GetStateValue(Collection);
            if (collection.IsEmpty)
                throw new InvalidOperationException($"The pool {Collection.Name} is empty.");

            return collection[stateMap.GetStateValue(Index).BounceInBounds(0, collection.Length - 1)];
        }
    }
}
