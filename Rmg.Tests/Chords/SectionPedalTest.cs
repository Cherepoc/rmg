using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Chords;

/// <summary>A pedal under a section: the bass holding one note, its home or its dominant, under the first phrase's changing chords.</summary>
public sealed class SectionPedalTest
{
    [Test]
    public async Task APedalledPhrase_HoldsOneNoteInTheBass_UnderChangingChords()
    {
        int phrases = 0, held = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed,
                     new SongOverrides(Facets: ImmutableDictionary<Facet, double>.Empty.Add(Facet.Progression, 0.9)))))
        {
            var pedalling = song.Trace.Where(x => x.Point == TracePoints.SectionPedal && x.Value is not null).Select(x => x.Section).ToHashSet();
            var bass = song.Song.Notes![SongTracks.BassTrack];
            foreach (var span in song.Song.Map!.Sections.Where(x => pedalling.Contains(x.SectionId)))
            {
                var end = Math.Min(span.End, span.Start + Meter.PhraseBarCount * span.Meter.BarDuration);
                var classes = bass.Where(x => x.Position >= span.Start - 1e-9 && x.Position < end - 1e-9).Select(x => x.Value.Pitches[0].Mod(12)).ToArray();
                if (classes.Length == 0)
                    continue;
                phrases++;
                // one note held, but where the band pushes into the next phrase
                if (classes.GroupBy(x => x).Max(x => x.Count()) >= 0.9 * classes.Length)
                    held++;
            }
        }

        await Assert.That(phrases).IsGreaterThan(5);
        await Assert.That(held / (double)phrases).IsGreaterThan(0.9);
    }
}
