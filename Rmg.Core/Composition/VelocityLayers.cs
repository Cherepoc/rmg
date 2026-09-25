using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     How much each layer of a note's velocity counts. Every layer draws a random value around 0, and the velocity is
///     their sum, weighted by these, before <c>Render</c> spreads the song's velocities over the MIDI range. The note
///     layer, which carries the beat accent, counts the most, so that the accent is heard over the slower layers; the
///     section layer comes next, so that sections still differ in loudness.
/// </summary>
public static class VelocityLayers
{
    /// <summary>A track's own loudness for the whole song, which sets the balance between the instruments.</summary>
    public const double Track = 0.25;

    /// <summary>The drums' loudness for the whole song, on top of each drum's own.</summary>
    public const double DrumGroup = 0.25;

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

    /// <summary>A layer's random velocity, scaled by its weight.</summary>
    public static Func<IGenerationContext, double> CreateGenerator(double weight)
    {
        return Generators.SplineValue().Then(x => x * weight);
    }
}
