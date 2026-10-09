namespace Rmg.Core.Songs;

/// <summary>
///     One of a drum's sounds: its General MIDI note number, how likely a run or a landing is to play it among the
///     drum's sounds, such as the crash over the china, how loud it plays over the drum, such as the open hi-hat over
///     the closed one, and how likely the groove is to play it steadily, its stroke, such as the snare's head over its
///     cross-stick.
/// </summary>
/// <param name="Weight">How likely the sound is among its drum's, in runs and landings.</param>
/// <param name="Loudness">How much louder or quieter it sounds than its drum, from -1 to 1, which leans its stroke and its accents by energy; it is struck as hard as the drum.</param>
/// <param name="Stroke">How likely the sound is as the stroke of a drum that strikes rather than walks its sounds; 0 for never.</param>
/// <param name="Accent">
///     The chance a note of a drum that strikes plays the sound in place of its stroke, such as the open hi-hat, with no
///     lean; 0 for never.
/// </param>
/// <param name="AccentLean">How the accent leans to the beats: 1 to the weak ones, as the open hi-hat's, -1 to the strong, as the ride bell's.</param>
/// <param name="Wildness">
///     How far a run or a landing leans to the sound the wilder its section, from 0, alike everywhere: above 0 never at the
///     plain end and its weight times one and that at the wild, as the china and the splash are.
/// </param>
public sealed record DrumSound(int Code, double Weight = 1, double Loudness = 0, double Stroke = 1, double Accent = 0, double AccentLean = 0, double Wildness = 0)
{
    public static implicit operator DrumSound(int code) => new(code);
}
