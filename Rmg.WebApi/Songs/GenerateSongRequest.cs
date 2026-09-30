namespace Rmg.WebApi.Songs;

/// <param name="Seed">
///     Seed of the song. None asks for a random one, which the response reports back, so a song heard once
///     can be asked for again.
/// </param>
/// <param name="Volume">
///     How loud the song plays, from 0 to 1 of the volume it plays at unasked. None is 1. It applies to
///     every track, on top of whatever volume the track has of its own.
/// </param>
/// <param name="Tracks">
///     What to play each track with, in place of what the generator picked. Every channel left out is
///     played as it was generated.
/// </param>
/// <param name="Unconventionality">
///     How far the song strays from convention, in steps from 0 for the plainest to 127 for the wildest, used as
///     given. None lets the song draw its own, which keeps well away from both ends. A seed and a step
///     name a song as a seed alone does for a drawn one.
/// </param>
public sealed record GenerateSongRequest(
    int? Seed = null,
    double? Volume = null,
    IReadOnlyList<TrackRequest>? Tracks = null,
    int? Unconventionality = null
)
{
    public const int FirstChannel = 1;
    public const int LastChannel = 16;
    public const int LastInstrument = 127;

    /// <summary>The wildest step of an unconventionality, which is 1: its steps are MIDI's, 0 to 127.</summary>
    public const int LastUnconventionality = 127;

    /// <summary>The unconventionality asked for, from 0 to 1; none for the song's own.</summary>
    public double? UnconventionalityValue => Unconventionality / (double)LastUnconventionality;

    /// <summary>The step nearest to an unconventionality, which is how a drawn one is reported.</summary>
    public static int ToStep(double unconventionality)
    {
        return (int)Math.Round(unconventionality * LastUnconventionality);
    }

    /// <summary>How loud the song is to play, which is 1 unless it says otherwise.</summary>
    public double SongVolume => Volume ?? 1;

    /// <summary>
    ///     Reads the tracks into the channels a written song counts from 0, and checks the rest of the request.
    /// </summary>
    /// <param name="channelTracks">The track asked for on every channel named.</param>
    /// <param name="error">Why the tracks cannot be read, if they cannot.</param>
    public bool TryGetChannelTracks(out Dictionary<byte, TrackRequest> channelTracks, out string? error)
    {
        channelTracks = [];

        if (!TryReadVolume(Volume, "Song", out error)) return false;

        if (Unconventionality is < 0 or > LastUnconventionality)
        {
            error = $"Unconventionality {Unconventionality} is not a step from 0 to {LastUnconventionality}.";
            return false;
        }

        foreach (var track in Tracks ?? [])
        {
            if (track.Channel is < FirstChannel or > LastChannel)
            {
                error = $"{track.Channel} is not a MIDI channel. "
                    + $"Channels are {FirstChannel} to {LastChannel}.";
                return false;
            }

            if (track.Instrument is < 0 or > LastInstrument)
            {
                error = $"{track.Instrument} is not a General MIDI instrument. "
                    + $"Instruments are 0 to {LastInstrument}.";
                return false;
            }

            if (!TryReadVolume(track.Volume, $"Channel {track.Channel}", out error)) return false;

            // two ways to play one channel is a request that cannot be answered, not one to answer halfway
            if (!channelTracks.TryAdd((byte)(track.Channel - FirstChannel), track))
            {
                error = $"Channel {track.Channel} appears more than once.";
                return false;
            }
        }

        return true;
    }

    private static bool TryReadVolume(double? volume, string what, out string? error)
    {
        error = null;
        if (volume is null or >= 0 and <= 1) return true;

        error = $"{what} volume {volume} is not between 0 and 1.";
        return false;
    }
}
