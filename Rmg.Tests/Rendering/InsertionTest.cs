using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Rendering;

/// <summary>The effect a song plays one part through (<see cref="Insertions" />), written as Roland GS messages.</summary>
public sealed class InsertionTest
{
    /// <summary>The written song's GS messages, each its address and its data.</summary>
    private static List<byte[]> GsMessages(RenderedSong song)
    {
        using var stream = new MemoryStream();
        song.Write(stream, null);
        var bytes = stream.ToArray();
        var messages = new List<byte[]>();
        for (var i = 0; i + 4 < bytes.Length; i++)
            if (bytes[i] == 0x41 && bytes[i + 1] == 0x10 && bytes[i + 2] == 0x42 && bytes[i + 3] == 0x12)
            {
                var end = Array.IndexOf(bytes, (byte)0xf7, i);
                // the address's and the data's sum and its checksum a multiple of 128
                messages.Add(bytes[(i + 4)..(end - 1)]);
                if (bytes[(i + 4)..end].Sum(x => x) % 128 != 0)
                    throw new InvalidOperationException("A GS message's checksum is wrong.");
            }

        return messages;
    }

    [Test]
    public async Task AFunkSongsWah_IsOnAGuitarOrAClavinetComping_EveryChannelOfItsPartAssigned()
    {
        var played = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("funk")))))
        {
            var messages = GsMessages(song.Rendered);
            if (song.Song.Draws!.Insertion is not { } insertion)
            {
                await Assert.That(messages.Any(x => x[0] == 0x40 && x[1] == 0x03)).IsFalse();
                continue;
            }

            played++;
            var part = (PitchInstrumentTrack)song.Song.TrackDefinitions.Values.Single(x => x.Role == insertion.Part);
            await Assert.That(Insertions.Suits(insertion.Kind, part.Role, part.InstrumentCode)).IsTrue();
            var (msb, lsb) = Insertions.TypeOf(insertion.Kind);
            await Assert.That(messages.Any(x => x is [0x40, 0x03, 0x00, var m, var l] && m == msb && l == lsb)).IsTrue();
            var assigned = messages.Where(x => x is [0x40, _, 0x22, 1]).Count();
            await Assert.That(assigned).IsEqualTo(song.Rendered.Tracks.Count(x => x.Role == insertion.Part));
        }

        await Assert.That(played).IsGreaterThan(8);
        // and a drawn song in the middle plays none, its type unset and no part assigned
        foreach (var seed in Enumerable.Range(0, 8))
            await Assert.That(GsMessages(TestCorpus.Get(seed).Rendered).Where(x => x is [0x40, 0x03, 0x00, ..] or [0x40, _, 0x22, ..])).IsEmpty();
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var genre in new[] { "", "funk", "disco", "synthwave", "soul", "rock" })
        {
            var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => genre == "" ? TestCorpus.Get(seed) : TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre))));
            var kinds = songs.Select(x => x.Song.Draws!.Insertion).Where(x => x is not null).Select(x => x!.Value).ToArray();
            Console.WriteLine($"{(genre == "" ? "drawn" : genre)}: " + string.Join(", ", kinds.GroupBy(x => (x.Kind, x.Part)).OrderByDescending(x => x.Count()).Select(x => $"{x.Key.Kind} on {x.Key.Part} {x.Count() / (double)songs.Length:P0}")));
        }

        await Task.CompletedTask;
    }
}
