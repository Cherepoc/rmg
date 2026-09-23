namespace Rmg.Core.Songs;

public static class SongFile
{
    /// <returns>The file name a song with <paramref name="songSeed" /> is stored and downloaded under.</returns>
    public static string GetName(int songSeed)
    {
        return $"song-{songSeed}.mid";
    }
}
