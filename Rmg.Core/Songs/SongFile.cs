namespace Rmg.Core.Songs;

public static class SongFile
{
    /// <returns>The file name a song with <paramref name="songSeed" /> is stored and downloaded under.</returns>
    public static string GetName(int songSeed)
    {
        return $"song-{songSeed}.mid";
    }

    /// <summary>The name of a song whose unconventionality may be given, as a step from 0 to 127, which names it too.</summary>
    public static string GetName(int songSeed, int? unconventionality)
    {
        return unconventionality is { } step ? $"song-{songSeed}-u{step}.mid" : GetName(songSeed);
    }
}
