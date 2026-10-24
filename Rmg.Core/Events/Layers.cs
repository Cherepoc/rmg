namespace Rmg.Core.Events;

/// <summary>
///     The names of the layers of state a trace explains a value by (<see cref="StateMap.Explain" />), those the tests
///     read: the layers that make it, from the song's down to a bar pattern's, by these names wherever they are made.
/// </summary>
public static class Layers
{
    public const string Song = "Song";
    public const string Section = "Section";
    public const string Track = "Track";
    public const string SectionTrack = "Section track";
    public const string DrumGroup = "Drum group";
    public const string SectionDrumGroup = "Section drum group";
    public const string BarPattern = "Bar pattern";
    public const string Progression = "Progression";
    public const string SectionRecurrence = "Section recurrence";
    public const string SectionArc = "Section arc";
    public const string SongMood = "Song mood";
}
