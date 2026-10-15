using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.WebApi;

/// <summary>The song's harmony as it plays (<see cref="SongHarmony" />), marked in its file, which the page shows as it plays.</summary>
public sealed class SongHarmonyTest
{
    /// <summary>The markers of a file's first track, its system track, each its position in ticks and its text, as the page reads them.</summary>
    internal static List<(long Ticks, string Text)> Markers(byte[] file)
    {
        // past the header's 14 bytes and the track's own 8
        var (at, end, ticks) = (22, 22 + (file[18] << 24 | file[19] << 16 | file[20] << 8 | file[21]), 0L);
        long Number()
        {
            long value = 0;
            byte b;
            do value = value << 7 | ((b = file[at++]) & 0x7F);
            while ((b & 0x80) != 0);
            return value;
        }

        var markers = new List<(long, string)>();
        while (at < end)
        {
            ticks += Number();
            var status = file[at++];
            // a meta event, its type, its length and its bytes; a system exclusive message, its length and its bytes
            var type = status == 0xFF ? file[at++] : -1;
            var length = (int)Number();
            if (type == 0x06)
                markers.Add((ticks, System.Text.Encoding.UTF8.GetString(file, at, length)));
            at += length;
        }

        return markers;
    }

    /// <summary>
    ///     Every song names its key and mode and a chord from its start, no chord where no pitched part plays, as a drums'
    ///     intro, and a chord wherever one does, in order, a change apart from the one before, and its file marks each where
    ///     it is.
    /// </summary>
    [Test]
    public async Task EverySongsHarmony_NamesAKeyAndAChord_InOrder_MarkedInItsFile()
    {
        foreach (var song in TestCorpus.Range(64).Concat(GenrePresets.All.Select(x => TestCorpus.Get(1, new SongOverrides(Genre: GenrePresets.Genre(x))))))
        {
            var harmony = song.Rendered.Harmony;
            await Assert.That(harmony.Length).IsGreaterThan(0).Because($"seed {song.Seed}");
            // from the start, no chord there where the first pitched note comes after its span, a song that opens on its
            // drums alone
            var first = song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key] is PitchInstrumentTrack && x.Value.Count > 0).Min(x => x.Value[0].Position);
            await Assert.That(harmony[0].Position).IsEqualTo(0).Because($"seed {song.Seed}");
            await Assert.That(harmony[0].Chord == SongHarmony.NoChord).IsEqualTo(harmony.Length > 1 && first >= harmony[1].Position - 1e-9).Because($"seed {song.Seed}");
            foreach (var (before, after) in harmony.Zip(harmony.Skip(1)))
            {
                await Assert.That(after.Position).IsGreaterThan(before.Position).Because($"seed {song.Seed}");
                await Assert.That(after.Key != before.Key || after.Chord != before.Chord).IsTrue().Because($"seed {song.Seed} at {after.Position}");
            }

            foreach (var change in harmony)
            {
                await Assert.That(System.Text.RegularExpressions.Regex.IsMatch(change.Key, "^[A-G][♯♭]? [A-Z][a-z]+( [a-z]+)*$")).IsTrue().Because($"seed {song.Seed}: {change.Key}");
                await Assert.That(System.Text.RegularExpressions.Regex.IsMatch(change.Chord, @"^([A-G][♯♭]?|N\.C\.$)")).IsTrue().Because($"seed {song.Seed}: {change.Chord}");
            }

            using var stream = new MemoryStream();
            song.Rendered.Write(stream, null);
            var markers = Markers(stream.ToArray());
            await Assert.That(markers.Select(x => x.Text)).IsEquivalentTo(harmony.Select(x => $"{x.Key}: {x.Chord}"), CollectionOrdering.Matching).Because($"seed {song.Seed}");
            await Assert.That(markers.Select(x => x.Ticks)).IsEquivalentTo(harmony.Select(x => (long)Math.Round(x.Position * 480)), CollectionOrdering.Matching).Because($"seed {song.Seed}");
        }
    }
}
