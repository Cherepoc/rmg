using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Rendering;

/// <summary>Legato: a line's step slurred, the note before held into it, never in the plainest songs.</summary>
public sealed class LegatoTest
{
    private static SongOverrides Sound(double value) => new(Facets: ImmutableDictionary<Facet, double>.Empty.Add(Facet.Sound, value));

    [Test]
    public async Task ASlurredNote_StepsFromTheNoteBefore_WhichReachesIt_AndAPlainSongSlursNone()
    {
        int slurred = 0, lineNotes = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, Sound(0.5))))
        foreach (var track in new[] { SongTracks.MelodyTrack, SongTracks.CounterMelodyTrack, SongTracks.RiffTrack })
        {
            if (!song.Song.Notes!.TryGetValue(track, out var notes))
                continue;
            var items = notes.ToArray();
            lineNotes += items.Length;
            for (var i = 1; i < items.Length; i++)
            {
                if (items[i].Value.State.GetStateValue(CompositionStateKinds.Legato) == 0)
                    continue;
                slurred++;
                await Assert.That(Math.Abs(items[i].Value.Pitches[0] - items[i - 1].Value.Pitches[0])).IsBetween(1, SongStages.SlurStep);
                await Assert.That(items[i - 1].Position + items[i - 1].Value.Duration).IsEqualTo(items[i].Position).Within(1e-6);
            }
        }

        Console.WriteLine($"{slurred} of {lineNotes} line notes slurred");
        await Assert.That(slurred).IsGreaterThan(lineNotes / 50);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 8), seed => TestCorpus.Get(seed, Sound(0))))
            await Assert.That(song.Song.Notes!.Values.SelectMany(x => x).All(x => x.Value.State.GetStateValue(CompositionStateKinds.Legato) == 0)).IsTrue();
    }
}
