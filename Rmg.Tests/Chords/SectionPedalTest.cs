using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Chords;

/// <summary>A pedal under a section: the bass holding one note, its home or its dominant, under the changing chords of its first phrase, or of its last for the dominant.</summary>
public sealed class SectionPedalTest
{
    [Test]
    public async Task APedalledPhrase_HoldsOneNoteInTheBass_UnderChangingChords()
    {
        int phrases = 0, held = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed,
                     new SongOverrides(Facets: ImmutableDictionary<Facet, double>.Empty.Add(Facet.Progression, 0.9)))))
        {
            var pedalling = song.Trace.Where(x => x.Point == TracePoints.SectionPedal && x.Value is not null).ToDictionary(x => x.Section, x => (int)x.Value!);
            var bass = song.Song.Notes![song.Song.Track(TrackRole.Bass)];
            foreach (var span in song.Song.Map!.Sections.Where(x => pedalling.ContainsKey(x.SectionId)))
            {
                var phrase = Meter.PhraseBarCount * span.Meter.BarDuration;
                var (start, end) = pedalling[span.SectionId] == 0 ? (span.Start, Math.Min(span.End, span.Start + phrase)) : (Math.Max(span.Start, span.End - phrase), span.End);
                var classes = bass.Where(x => x.Position >= start - 1e-9 && x.Position < end - 1e-9).Select(x => x.Value.Pitches[0].Mod(12)).ToArray();
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
