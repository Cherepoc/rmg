using System.Collections.Immutable;
using System.Diagnostics;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     A General MIDI instrument a role can be played with, and how likely it is among the role's others, in the plainest
///     songs, the middling and the wildest, by their sound facet: a common instrument as likely in all of them, one with
///     more character never in the plainest.
/// </summary>
/// <param name="Program">The General MIDI program, from 0.</param>
/// <param name="Leading">
///     How the instrument moves from chord to chord. For a chord instrument it is how smoothly its chords move (see
///     <see cref="StateKinds.VoiceLeading" />): strings, pads and organs hold their notes and move them little, pianos
///     are in between, and guitars move their chord shapes up and down the neck. For a bass it is how much it leads
///     into the next chord (see <see cref="BassLeadingLayers" />): upright and fretless basses walk, synth basses sit on
///     the roots. For a melody instrument it is how stepwise its melody is (see <see cref="MelodyLayers" />): winds
///     and strings sing by step, mallets leap.
/// </param>
public sealed record RoleInstrument(int Program, string Name, ByConvention Weight, double Leading = VoiceLeadingLayers.Piano)
{
    /// <summary>An instrument as likely in every song.</summary>
    public RoleInstrument(int program, string name, double weight, double leading = VoiceLeadingLayers.Piano) : this(program, name, new ByConvention(weight, weight, weight), leading)
    {
    }
}

/// <summary>
///     What a pitched track does in the song, and the instruments that suit it: ones that sustain chords, ones that
///     carry a line above them, and ones that hold the low end.
/// </summary>
[DebuggerDisplay("InstrumentRole {Name}")]
public sealed class InstrumentRole
{
    public InstrumentRole(string name, ImmutableArray<RoleInstrument> instruments)
    {
        if (instruments.IsDefaultOrEmpty)
            throw new ArgumentException("A role needs instruments.", nameof(instruments));

        Name = name;
        Instruments = instruments;
    }

    public string Name { get; }

    public ImmutableArray<RoleInstrument> Instruments { get; }

    /// <summary>
    ///     An instrument of the role, the heavier ones being more likely, by their weights at the song's sound facet or a
    ///     pool's, such as a genre's, other than the ones given, which a pool that keeps nothing else lets play.
    /// </summary>
    /// <param name="sound">The sound facet, which the role's own weights lean by.</param>
    /// <param name="pool">An instrument's weight in the pool, in place of its own, from 0, out of it, to 1; none for the role's own.</param>
    public RoleInstrument Pick(IGenerationContext context, double sound, Func<RoleInstrument, double>? pool, params IEnumerable<int> excludedPrograms)
    {
        var excluded = excludedPrograms.ToHashSet();
        var kept = Instruments.Select(x => (Instrument: x, Weight: pool?.Invoke(x) ?? x.Weight.At(sound))).Where(x => x.Weight > 0).ToArray();
        var candidates = kept.Where(x => !excluded.Contains(x.Instrument.Program)).ToArray();
        if (candidates.Length == 0 && pool is not null)
            candidates = kept;
        if (candidates.Length == 0)
            throw new InvalidOperationException($"Every instrument of the {Name} role is excluded.");

        return context.Pick([..candidates.Select(x => new Weighted<RoleInstrument>(x.Weight, x.Instrument))]);
    }
}

/// <summary>
///     The roles of the pitched tracks. The common instruments of a role weigh the most, and ones with more character
///     less and never in the plainest songs, so most songs sound familiar and some unusual: the world's instruments, the
///     synth effects' pads and the orchestra hit the most at the wild end; none is a sound effect or a drum.
/// </summary>
internal static class InstrumentRoles
{
    /// <summary>Whether an instrument plays chords: one the song might give its chords or its rhythm part.</summary>
    public static bool PlaysChords(int program) => Chords.Instruments.Concat(Rhythm.Instruments).Any(x => x.Program == program);

    public static InstrumentRole Chords { get; } = new(
        nameof(Chords),
        [
            new(0, "Acoustic Grand Piano", 1),
            new(1, "Bright Acoustic Piano", 0.5),
            new(2, "Electric Grand Piano", 0.4),
            new(4, "Electric Piano 1", 1),
            new(5, "Electric Piano 2", 0.6),
            new(6, "Harpsichord", 0.2),
            new(11, "Vibraphone", 0.3),
            new(16, "Drawbar Organ", 0.6, VoiceLeadingLayers.Sustained),
            new(17, "Percussive Organ", 0.4, VoiceLeadingLayers.Sustained),
            new(18, "Rock Organ", 0.3, VoiceLeadingLayers.Sustained),
            new(19, "Church Organ", 0.15, VoiceLeadingLayers.Sustained),
            new(21, "Accordion", 0.15, VoiceLeadingLayers.Sustained),
            new(24, "Acoustic Guitar (nylon)", 0.6, VoiceLeadingLayers.Guitar),
            new(25, "Acoustic Guitar (steel)", 0.7, VoiceLeadingLayers.Guitar),
            new(26, "Electric Guitar (jazz)", 0.5, VoiceLeadingLayers.Guitar),
            new(27, "Electric Guitar (clean)", 0.6, VoiceLeadingLayers.Guitar),
            new(29, "Overdriven Guitar", 0.2, VoiceLeadingLayers.Guitar),
            new(30, "Distortion Guitar", 0.2, VoiceLeadingLayers.Guitar),
            new(46, "Orchestral Harp", 0.2),
            new(48, "String Ensemble 1", 0.8, VoiceLeadingLayers.Sustained),
            new(49, "String Ensemble 2", 0.4, VoiceLeadingLayers.Sustained),
            new(50, "Synth Strings 1", 0.5, VoiceLeadingLayers.Sustained),
            new(52, "Choir Aahs", 0.2, VoiceLeadingLayers.Sustained),
            new(88, "Pad 1 (new age)", 0.4, VoiceLeadingLayers.Sustained),
            new(89, "Pad 2 (warm)", 0.6, VoiceLeadingLayers.Sustained),
            new(90, "Pad 3 (polysynth)", 0.5, VoiceLeadingLayers.Sustained),
            new(91, "Pad 4 (choir)", 0.2, VoiceLeadingLayers.Sustained),
            new(94, "Pad 7 (halo)", 0.3, VoiceLeadingLayers.Sustained),
            new(95, "Pad 8 (sweep)", 0.2, VoiceLeadingLayers.Sustained),
            new(3, "Honky-tonk Piano", new ByConvention(0, 0.15, 0.3)),
            new(15, "Dulcimer", new ByConvention(0, 0.05, 0.2)),
            new(20, "Reed Organ", new ByConvention(0, 0.08, 0.2), VoiceLeadingLayers.Sustained),
            new(23, "Tango Accordion", new ByConvention(0, 0.05, 0.2), VoiceLeadingLayers.Sustained),
            new(44, "Tremolo Strings", new ByConvention(0, 0.1, 0.3), VoiceLeadingLayers.Sustained),
            new(45, "Pizzicato Strings", new ByConvention(0, 0.1, 0.3)),
            new(54, "Synth Voice", new ByConvention(0, 0.08, 0.3), VoiceLeadingLayers.Sustained),
            new(55, "Orchestra Hit", new ByConvention(0, 0.02, 0.2)),
            new(62, "Synth Brass 1", new ByConvention(0, 0.15, 0.3), VoiceLeadingLayers.Sustained),
            new(63, "Synth Brass 2", new ByConvention(0, 0.1, 0.3), VoiceLeadingLayers.Sustained)
        ]
    );

    /// <summary>The sustained sounds that hold chords under the band: strings, a choir, synth pads, an organ.</summary>
    public static InstrumentRole Pad { get; } = new(
        nameof(Pad),
        [
            new(48, "String Ensemble 1", 1, VoiceLeadingLayers.Sustained),
            new(49, "String Ensemble 2", 0.6, VoiceLeadingLayers.Sustained),
            new(50, "Synth Strings 1", 0.6, VoiceLeadingLayers.Sustained),
            new(51, "Synth Strings 2", 0.3, VoiceLeadingLayers.Sustained),
            new(52, "Choir Aahs", 0.4, VoiceLeadingLayers.Sustained),
            new(53, "Voice Oohs", 0.3, VoiceLeadingLayers.Sustained),
            new(16, "Drawbar Organ", 0.3, VoiceLeadingLayers.Sustained),
            new(88, "Pad 1 (new age)", 0.5, VoiceLeadingLayers.Sustained),
            new(89, "Pad 2 (warm)", 0.8, VoiceLeadingLayers.Sustained),
            new(90, "Pad 3 (polysynth)", 0.5, VoiceLeadingLayers.Sustained),
            new(91, "Pad 4 (choir)", 0.3, VoiceLeadingLayers.Sustained),
            new(92, "Pad 5 (bowed)", 0.3, VoiceLeadingLayers.Sustained),
            new(94, "Pad 7 (halo)", 0.4, VoiceLeadingLayers.Sustained),
            new(95, "Pad 8 (sweep)", 0.2, VoiceLeadingLayers.Sustained),
            new(44, "Tremolo Strings", new ByConvention(0, 0.15, 0.3), VoiceLeadingLayers.Sustained),
            new(54, "Synth Voice", new ByConvention(0, 0.15, 0.3), VoiceLeadingLayers.Sustained),
            new(93, "Pad 6 (metallic)", new ByConvention(0, 0.1, 0.4), VoiceLeadingLayers.Sustained),
            new(96, "Rain (FX 1)", new ByConvention(0, 0.02, 0.3), VoiceLeadingLayers.Sustained),
            new(97, "Soundtrack (FX 2)", new ByConvention(0, 0.08, 0.4), VoiceLeadingLayers.Sustained),
            new(98, "Crystal (FX 3)", new ByConvention(0, 0.05, 0.4), VoiceLeadingLayers.Sustained),
            new(99, "Atmosphere (FX 4)", new ByConvention(0, 0.1, 0.4), VoiceLeadingLayers.Sustained),
            new(100, "Brightness (FX 5)", new ByConvention(0, 0.05, 0.4), VoiceLeadingLayers.Sustained),
            new(101, "Goblins (FX 6)", new ByConvention(0, 0, 0.2), VoiceLeadingLayers.Sustained),
            new(102, "Echoes (FX 7)", new ByConvention(0, 0.05, 0.4), VoiceLeadingLayers.Sustained),
            new(103, "Sci-fi (FX 8)", new ByConvention(0, 0, 0.2), VoiceLeadingLayers.Sustained)
        ]
    );

    /// <summary>The instruments that comp beside the chords: guitars most, a clavinet, an electric piano, an organ, a banjo.</summary>
    public static InstrumentRole Rhythm { get; } = new(
        nameof(Rhythm),
        [
            new(25, "Acoustic Guitar (steel)", 1, VoiceLeadingLayers.Guitar),
            new(27, "Electric Guitar (clean)", 1, VoiceLeadingLayers.Guitar),
            new(28, "Electric Guitar (muted)", 0.6, VoiceLeadingLayers.Guitar),
            new(24, "Acoustic Guitar (nylon)", 0.5, VoiceLeadingLayers.Guitar),
            new(29, "Overdriven Guitar", 0.5, VoiceLeadingLayers.Guitar),
            new(26, "Electric Guitar (jazz)", 0.4, VoiceLeadingLayers.Guitar),
            new(30, "Distortion Guitar", 0.4, VoiceLeadingLayers.Guitar),
            new(7, "Clavinet", 0.3),
            new(4, "Electric Piano 1", 0.3),
            new(18, "Rock Organ", 0.2, VoiceLeadingLayers.Sustained),
            new(105, "Banjo", 0.1, VoiceLeadingLayers.Guitar),
            new(3, "Honky-tonk Piano", new ByConvention(0, 0.08, 0.2)),
            new(23, "Tango Accordion", new ByConvention(0, 0.03, 0.15), VoiceLeadingLayers.Sustained)
        ]
    );

    /// <summary>
    ///     The instruments that take a solo: a lead guitar, overdriven or distorted most often, a sax, a trumpet, a violin, a
    ///     flute, a harmonica or a synth lead; an organ where a genre names it.
    /// </summary>
    public static InstrumentRole Solo { get; } = new(
        nameof(Solo),
        [
            new(29, "Overdriven Guitar", 0.7, MelodyLayers.Mixed),
            new(30, "Distortion Guitar", 0.7, MelodyLayers.Mixed),
            new(27, "Electric Guitar (clean)", 0.2, MelodyLayers.Mixed),
            new(26, "Electric Guitar (jazz)", 0.2, MelodyLayers.Mixed),
            new(65, "Alto Sax", 0.5, MelodyLayers.Stepwise),
            new(66, "Tenor Sax", 0.4, MelodyLayers.Stepwise),
            new(64, "Soprano Sax", 0.2, MelodyLayers.Stepwise),
            new(56, "Trumpet", 0.4, MelodyLayers.Stepwise),
            new(40, "Violin", 0.3, MelodyLayers.Stepwise),
            new(73, "Flute", 0.2, MelodyLayers.Stepwise),
            new(22, "Harmonica", 0.2, MelodyLayers.Stepwise),
            new(80, "Lead 1 (square)", 0.3, MelodyLayers.Mixed),
            new(81, "Lead 2 (sawtooth)", 0.4, MelodyLayers.Mixed),
            new(84, "Lead 5 (charang)", 0.2, MelodyLayers.Mixed),
            // an organ's solo, a hard rock band's, which only a genre plays
            new(18, "Rock Organ", 0, MelodyLayers.Mixed)
        ]
    );

    /// <summary>The instruments that play a riff: guitars, overdriven or clean, a clavinet, synth leads, a baritone sax, brass.</summary>
    public static InstrumentRole Riff { get; } = new(
        nameof(Riff),
        [
            new(29, "Overdriven Guitar", 1),
            new(30, "Distortion Guitar", 0.8),
            new(27, "Electric Guitar (clean)", 0.6),
            new(28, "Electric Guitar (muted)", 0.5),
            new(7, "Clavinet", 0.4),
            new(80, "Lead 1 (square)", 0.3),
            new(81, "Lead 2 (sawtooth)", 0.3),
            new(67, "Baritone Sax", 0.2),
            new(17, "Percussive Organ", 0.2),
            new(61, "Brass Section", 0.2),
            new(62, "Synth Brass 1", new ByConvention(0, 0.15, 0.3)),
            new(63, "Synth Brass 2", new ByConvention(0, 0.08, 0.3)),
            new(55, "Orchestra Hit", new ByConvention(0, 0.03, 0.2)),
            new(87, "Lead 8 (bass and lead)", new ByConvention(0, 0.05, 0.2)),
            new(114, "Steel Drums", new ByConvention(0, 0.03, 0.2))
        ]
    );

    /// <summary>The instruments that play a line under the melody: strings, horns, reeds, a clean guitar, an organ, a synth.</summary>
    public static InstrumentRole CounterMelody { get; } = new(
        nameof(CounterMelody),
        [
            new(40, "Violin", 0.6),
            new(41, "Viola", 0.5),
            new(42, "Cello", 0.8),
            new(48, "String Ensemble 1", 0.8),
            new(60, "French Horn", 0.6),
            new(57, "Trombone", 0.3),
            new(71, "Clarinet", 0.4),
            new(73, "Flute", 0.5),
            new(68, "Oboe", 0.3),
            new(27, "Electric Guitar (clean)", 0.4),
            new(18, "Rock Organ", 0.2),
            new(81, "Lead 2 (sawtooth)", 0.2),
            new(69, "English Horn", new ByConvention(0, 0.2, 0.3)),
            new(72, "Piccolo", new ByConvention(0, 0.05, 0.2)),
            new(45, "Pizzicato Strings", new ByConvention(0, 0.1, 0.3)),
            new(62, "Synth Brass 1", new ByConvention(0, 0.1, 0.3)),
            new(85, "Lead 6 (voice)", new ByConvention(0, 0.05, 0.2)),
            // a slow synth over a heavy band's guitars, a genre's, the songs' never
            new(50, "Synth Strings 1", 0)
        ]
    );

    public static InstrumentRole Melody { get; } = new(
        nameof(Melody),
        [
            new(0, "Acoustic Grand Piano", 0.6, MelodyLayers.Mixed),
            new(4, "Electric Piano 1", 0.5, MelodyLayers.Mixed),
            new(8, "Celesta", 0.2, MelodyLayers.Leaping),
            new(9, "Glockenspiel", 0.2, MelodyLayers.Leaping),
            new(11, "Vibraphone", 0.5, MelodyLayers.Leaping),
            new(12, "Marimba", 0.4, MelodyLayers.Leaping),
            new(13, "Xylophone", 0.15, MelodyLayers.Leaping),
            new(22, "Harmonica", 0.2, MelodyLayers.Stepwise),
            new(24, "Acoustic Guitar (nylon)", 0.5, MelodyLayers.Mixed),
            new(26, "Electric Guitar (jazz)", 0.4, MelodyLayers.Mixed),
            new(27, "Electric Guitar (clean)", 0.5, MelodyLayers.Mixed),
            new(40, "Violin", 0.6, MelodyLayers.Stepwise),
            new(42, "Cello", 0.3, MelodyLayers.Stepwise),
            new(56, "Trumpet", 0.5, MelodyLayers.Stepwise),
            new(59, "Muted Trumpet", 0.3, MelodyLayers.Stepwise),
            new(60, "French Horn", 0.2, MelodyLayers.Stepwise),
            new(64, "Soprano Sax", 0.3, MelodyLayers.Stepwise),
            new(65, "Alto Sax", 0.5, MelodyLayers.Stepwise),
            new(66, "Tenor Sax", 0.4, MelodyLayers.Stepwise),
            new(68, "Oboe", 0.3, MelodyLayers.Stepwise),
            new(71, "Clarinet", 0.5, MelodyLayers.Stepwise),
            new(73, "Flute", 0.7, MelodyLayers.Stepwise),
            new(75, "Pan Flute", 0.3, MelodyLayers.Stepwise),
            new(79, "Ocarina", 0.15, MelodyLayers.Stepwise),
            new(80, "Lead 1 (square)", 0.4, MelodyLayers.Mixed),
            new(81, "Lead 2 (sawtooth)", 0.4, MelodyLayers.Mixed),
            new(84, "Lead 5 (charang)", 0.15, MelodyLayers.Mixed),
            new(104, "Sitar", 0.1, MelodyLayers.Mixed),
            new(105, "Banjo", 0.1, MelodyLayers.Mixed),
            new(108, "Kalimba", 0.2, MelodyLayers.Leaping),
            new(110, "Fiddle", 0.1, MelodyLayers.Stepwise),
            new(10, "Music Box", new ByConvention(0, 0.05, 0.2), MelodyLayers.Leaping),
            new(14, "Tubular Bells", new ByConvention(0, 0.02, 0.15), MelodyLayers.Leaping),
            new(15, "Dulcimer", new ByConvention(0, 0.03, 0.15), MelodyLayers.Mixed),
            new(62, "Synth Brass 1", new ByConvention(0, 0.05, 0.2), MelodyLayers.Stepwise),
            new(69, "English Horn", new ByConvention(0, 0.1, 0.2), MelodyLayers.Stepwise),
            new(72, "Piccolo", new ByConvention(0, 0.05, 0.2), MelodyLayers.Stepwise),
            new(74, "Recorder", new ByConvention(0, 0.08, 0.2), MelodyLayers.Stepwise),
            new(77, "Shakuhachi", new ByConvention(0, 0, 0.15), MelodyLayers.Stepwise),
            new(78, "Whistle", new ByConvention(0, 0.05, 0.2), MelodyLayers.Stepwise),
            new(82, "Lead 3 (calliope)", new ByConvention(0, 0.05, 0.2), MelodyLayers.Mixed),
            new(83, "Lead 4 (chiff)", new ByConvention(0, 0.05, 0.2), MelodyLayers.Mixed),
            new(85, "Lead 6 (voice)", new ByConvention(0, 0.08, 0.2), MelodyLayers.Stepwise),
            new(87, "Lead 8 (bass and lead)", new ByConvention(0, 0.05, 0.2), MelodyLayers.Mixed),
            new(106, "Shamisen", new ByConvention(0, 0, 0.15), MelodyLayers.Mixed),
            new(107, "Koto", new ByConvention(0, 0, 0.15), MelodyLayers.Mixed),
            new(109, "Bagpipe", new ByConvention(0, 0, 0.1), MelodyLayers.Stepwise),
            new(111, "Shanai", new ByConvention(0, 0, 0.1), MelodyLayers.Stepwise),
            new(114, "Steel Drums", new ByConvention(0, 0.05, 0.2), MelodyLayers.Leaping),
            // a lead guitar driven, a punk or a black metal band's, the songs' at their wildest alone
            new(29, "Overdriven Guitar", new ByConvention(0, 0, 0.1), MelodyLayers.Mixed),
            new(30, "Distortion Guitar", new ByConvention(0, 0, 0.1), MelodyLayers.Mixed)
        ]
    );

    public static InstrumentRole Bass { get; } = new(
        nameof(Bass),
        [
            new(32, "Acoustic Bass", 0.8, BassLeadingLayers.Walking),
            new(33, "Electric Bass (finger)", 1, BassLeadingLayers.Moderate),
            new(34, "Electric Bass (pick)", 0.6, BassLeadingLayers.Moderate),
            new(35, "Fretless Bass", 0.5, BassLeadingLayers.Walking),
            new(36, "Slap Bass 1", 0.2, BassLeadingLayers.Moderate),
            new(37, "Slap Bass 2", 0.15, BassLeadingLayers.Moderate),
            new(38, "Synth Bass 1", 0.5, BassLeadingLayers.Plain),
            new(39, "Synth Bass 2", 0.4, BassLeadingLayers.Plain),
            new(43, "Contrabass", 0.3, BassLeadingLayers.Walking),
            new(58, "Tuba", 0.1, BassLeadingLayers.Moderate),
            new(70, "Bassoon", 0.1, BassLeadingLayers.Moderate)
        ]
    );
}
