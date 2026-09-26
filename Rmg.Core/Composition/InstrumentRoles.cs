using System.Collections.Immutable;
using System.Diagnostics;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>A General MIDI instrument a role can be played with, and how likely it is among the role's others.</summary>
/// <param name="Program">The General MIDI program, from 0.</param>
/// <param name="VoiceLeading">
///     How smoothly the instrument's chords usually move (see <see cref="StateKinds.VoiceLeading" />): strings, pads
///     and organs hold their notes and move them little, pianos are in between, and guitars move their chord shapes
///     up and down the neck.
/// </param>
public sealed record RoleInstrument(int Program, string Name, double Weight, double VoiceLeading = VoiceLeadingLayers.Piano);

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

    /// <summary>An instrument of the role, the heavier ones being more likely, other than the ones given.</summary>
    public RoleInstrument Pick(IGenerationContext context, params IEnumerable<int> excludedPrograms)
    {
        var excluded = excludedPrograms.ToHashSet();
        var candidates = Instruments.Where(x => !excluded.Contains(x.Program)).ToImmutableArray();
        if (candidates.IsEmpty)
            throw new InvalidOperationException($"Every instrument of the {Name} role is excluded.");

        return candidates[Generators.WeightedIndex([..candidates.Select(x => new Weighted<RoleInstrument>(x.Weight, x))])(context)];
    }
}

/// <summary>
///     The roles of the pitched tracks. The common instruments of a role weigh the most, and ones with more character
///     less, so most songs sound familiar and some unusual; none is a sound effect or a drum.
/// </summary>
public static class InstrumentRoles
{
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
            new(95, "Pad 8 (sweep)", 0.2, VoiceLeadingLayers.Sustained)
        ]
    );

    public static InstrumentRole Melody { get; } = new(
        nameof(Melody),
        [
            new(0, "Acoustic Grand Piano", 0.6),
            new(4, "Electric Piano 1", 0.5),
            new(8, "Celesta", 0.2),
            new(9, "Glockenspiel", 0.2),
            new(11, "Vibraphone", 0.5),
            new(12, "Marimba", 0.4),
            new(13, "Xylophone", 0.15),
            new(22, "Harmonica", 0.2),
            new(24, "Acoustic Guitar (nylon)", 0.5),
            new(26, "Electric Guitar (jazz)", 0.4),
            new(27, "Electric Guitar (clean)", 0.5),
            new(40, "Violin", 0.6),
            new(42, "Cello", 0.3),
            new(56, "Trumpet", 0.5),
            new(59, "Muted Trumpet", 0.3),
            new(60, "French Horn", 0.2),
            new(64, "Soprano Sax", 0.3),
            new(65, "Alto Sax", 0.5),
            new(66, "Tenor Sax", 0.4),
            new(68, "Oboe", 0.3),
            new(71, "Clarinet", 0.5),
            new(73, "Flute", 0.7),
            new(75, "Pan Flute", 0.3),
            new(79, "Ocarina", 0.15),
            new(80, "Lead 1 (square)", 0.4),
            new(81, "Lead 2 (sawtooth)", 0.4),
            new(84, "Lead 5 (charang)", 0.15),
            new(104, "Sitar", 0.1),
            new(105, "Banjo", 0.1),
            new(108, "Kalimba", 0.2),
            new(110, "Fiddle", 0.1)
        ]
    );

    public static InstrumentRole Bass { get; } = new(
        nameof(Bass),
        [
            new(32, "Acoustic Bass", 0.8),
            new(33, "Electric Bass (finger)", 1),
            new(34, "Electric Bass (pick)", 0.6),
            new(35, "Fretless Bass", 0.5),
            new(36, "Slap Bass 1", 0.2),
            new(37, "Slap Bass 2", 0.15),
            new(38, "Synth Bass 1", 0.5),
            new(39, "Synth Bass 2", 0.4),
            new(43, "Contrabass", 0.3),
            new(58, "Tuba", 0.1),
            new(70, "Bassoon", 0.1)
        ]
    );
}
