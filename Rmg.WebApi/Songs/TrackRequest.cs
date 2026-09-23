namespace Rmg.WebApi.Songs;

/// <param name="Channel">
///     MIDI channel of the track, counted from 1 the way a player shows them. Channel 10 is the percussion
///     one, where an instrument is a drum kit.
/// </param>
/// <param name="Instrument">
///     Instrument the track is to play: one of the 0-127 of General MIDI. None keeps the one the generator
///     picked.
/// </param>
/// <param name="Volume">
///     How loud the track plays, from 0 to 1 of the volume it plays at unasked. None is 1, and the overall
///     volume of the song still applies on top.
/// </param>
/// <param name="IsEnabled">
///     False leaves the track out of the song: it is not written at all, rather than written silent.
/// </param>
public sealed record TrackRequest(int Channel, int? Instrument = null, double? Volume = null, bool IsEnabled = true);
