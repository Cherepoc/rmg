using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     How loud a note is, as the sum of its layers, which <c>Render</c> plays on a fixed scale, a unit of it about 11 dB
///     around the middle: a track's level by its role, fixed, which sets the balance between the instruments; a
///     section's loudness, drawn and leaned by its energy; a track's in a section, a bar's and a bar pattern's, drawn;
///     and a note's, its beat's accent and a variation, as far as the track's dynamics have them.
/// </summary>
public static class VelocityLayers
{
    /// <summary>
    ///     A track's level by its role: the melody on top, as a melody leads the mix; the chords a little over the bass
    ///     and the drums, their stacked notes already softened (<c>Render</c>), which by measure alone sounded too quiet;
    ///     the bass and the drums as they are.
    /// </summary>
    public static double GetLevel(TrackRole role) => role switch
    {
        TrackRole.Melody => 0.25,
        TrackRole.Chords => 0.1,
        _ => 0
    };

    /// <summary>How much louder a drum plays for how loud it leans, such as the crash louder and the cross-stick quieter.</summary>
    public const double DrumLevel = 0.15;

    /// <summary>A drum's level: the drums', and its own by how loud it leans.</summary>
    public static double GetLevel(PercussionInstrumentDefinition drum) => DrumLevel * drum.Loudness;

    /// <summary>The loudness of a section, shared by all its tracks.</summary>
    public const double Section = 0.5;

    /// <summary>How much louder or quieter a track is in a section than it is in the song.</summary>
    public const double SectionTrack = 0.25;

    /// <summary>How much louder or quieter the drums are in a section.</summary>
    public const double SectionDrumGroup = 0.25;

    /// <summary>The loudness of a bar of the progression, shared by all the tracks.</summary>
    public const double Bar = 0.25;

    /// <summary>The loudness of a track's bar pattern.</summary>
    public const double BarPattern = 0.25;

    /// <summary>The loudness of a note, tipped by how strong its beat is.</summary>
    public const double Note = 1;

    /// <summary>
    ///     How far a track's notes move from its level (<see cref="CompositionStateKinds.NoteDynamics" />), by its role:
    ///     a bass and chords evenly, as their players aim at the same velocity, a melody more, and the drums as tuned.
    /// </summary>
    public static double GetDynamics(TrackRole role) => role switch
    {
        TrackRole.Bass => 0.4,
        TrackRole.Chords => 0.5,
        TrackRole.Melody => 0.8,
        _ => 1
    };

    /// <summary>
    ///     How much a section's conventionality moves the dynamics: its chance scale to this power, so that a plain
    ///     section plays about 0.7 of them and a wild one about 1.4.
    /// </summary>
    public const double DynamicsLean = 0.25;

    /// <summary>A layer's random velocity, scaled by its weight, leaning louder or quieter by the tilt, such as a section's energy.</summary>
    public static Func<IGenerationContext, double> CreateGenerator(double weight, Tilt tilt = default)
    {
        return tilt.SplineValue().Then(x => x * weight);
    }
}
