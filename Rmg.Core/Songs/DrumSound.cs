namespace Rmg.Core.Songs;

/// <summary>
///     One of a drum's sounds: its General MIDI note number, how likely a run or a landing is to play it among the
///     drum's sounds, such as the crash over the china, how loud it plays over the drum, such as the open hi-hat over
///     the closed one, and how likely the groove is to play it steadily, its stroke, such as the snare's head over its
///     cross-stick.
/// </summary>
/// <param name="Weight">How likely the sound is among its drum's, in runs and landings.</param>
/// <param name="Loudness">How much louder or quieter it plays than its drum, from -1 to 1, which leans its stroke by energy too.</param>
/// <param name="Stroke">How likely the sound is as the stroke of a drum that strikes rather than walks its sounds; 0 for never.</param>
public sealed record DrumSound(int Code, double Weight = 1, double Loudness = 0, double Stroke = 1)
{
    public static implicit operator DrumSound(int code) => new(code);
}
