using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Rendering;

/// <summary>The room a song's reverb is (<see cref="GenreChoices.Room" />), written as a Roland GS message.</summary>
public sealed class RoomTest
{
    /// <summary>The reverb macro the written song sets, none where it sets none.</summary>
    private static int? WrittenRoom(RenderedSong song)
    {
        using var stream = new MemoryStream();
        song.Write(stream, null);
        var bytes = stream.ToArray();
        byte[] address = [0x41, 0x10, 0x42, 0x12, 0x40, 0x01, 0x30];
        for (var i = 0; i + address.Length + 2 < bytes.Length; i++)
            if (bytes.AsSpan(i, address.Length).SequenceEqual(address))
            {
                var macro = bytes[i + address.Length];
                // the address's and the data's sum and its checksum a multiple of 128
                return (0x40 + 0x01 + 0x30 + macro + bytes[i + address.Length + 1]) % 128 == 0 ? macro : -1;
            }

        return null;
    }

    [Test]
    public async Task AGenresSong_SetsOneOfItsRooms_AndADrawnOne_ThePlayersOwn()
    {
        foreach (var seed in Enumerable.Range(0, 8))
        {
            var song = TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("ambient")));
            await Assert.That(song.Song.Draws!.Room).IsNotNull();
            await Assert.That(GenreChoices.RoomNameOf(song.Song.Draws.Room!.Value)).IsIn("hall", "plate");
            await Assert.That(WrittenRoom(song.Rendered)).IsEqualTo(song.Song.Draws.Room);
            await Assert.That(WrittenRoom(TestCorpus.Get(seed).Rendered)).IsNull();
        }
    }
}
