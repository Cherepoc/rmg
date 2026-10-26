using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Tests.MelodyRhythms;

/// <summary>A melody's phrase ending on a goal (<see cref="LinePattern.Goals" />): its held last note on its chord's root or third.</summary>
public sealed class MelodyGoalTest
{
    private static SongOverrides Melody(double melody) => new(Facets: TestCorpus.FacetsOf((Facet.Melody, melody)));

    /// <summary>
    ///     A melody's phrase ends, its held notes but a solo's and those over a reharmonised chord, whose tune was placed over
    ///     the chord it replaced (<see cref="Reharmonisation" />), in the songs given: how many, and how many on the chord's
    ///     root, its third and another note.
    /// </summary>
    private static (int Ends, int Roots, int Thirds, int ChordTones) Ends(IEnumerable<CorpusSong> songs)
    {
        var (ends, roots, thirds, tones) = (0, 0, 0, 0);
        foreach (var song in songs)
        {
            var changes = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetStateTimeline(StateKinds.ChordChange).Select(x => x.Position).ToArray();
            var substituted = song.Trace.Where(x => x.Point == TracePoints.Reharmonisation && x.Value is not null)
                .SelectMany(x => (((int Appearance, ImmutableArray<double> At))x.Value!).At).ToArray();
            bool Reharmonised(double position) => changes.LastOrDefault(x => x <= position + 1e-9, double.NaN) is var start && substituted.Any(x => Math.Abs(x - start) < 1e-6);
            // but a solo's, which ends its phrases its own way
            foreach (var note in song.Song.Notes![song.Song.Track(TrackRole.Melody)].Where(x => x.Value.State.GetStateValue(StateKinds.HeldDuration) > 0 && x.Value.Pitches.Length == 1
                                                                                  && x.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 0 && !Reharmonised(x.Position)))
            {
                var (chord, _, classes) = Realizer.GetChordNotes(note.Value.State);
                var pitch = note.Value.Pitches[0] % 12;
                ends++;
                roots += pitch == ((chord.GetPitch(0) % 12) + 12) % 12 ? 1 : 0;
                // its third where the chord has one, never a sus chord's second or fourth
                thirds += pitch == ((chord.GetPitch(2) % 12) + 12) % 12 && classes.Contains(pitch) ? 1 : 0;
                tones += classes.Contains(pitch) ? 1 : 0;
            }
        }

        return (ends, roots, thirds, tones);
    }

    /// <summary>A plain melody's every phrase ends on its chord's root or third, a wild one's wherever it lands.</summary>
    [Test]
    public async Task APlainMelodysPhrases_EndOnTheChordsRootOrThird()
    {
        var (ends, roots, thirds, _) = Ends(TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, Melody(0))));
        await Assert.That(ends).IsGreaterThan(100);
        await Assert.That(roots + thirds).IsEqualTo(ends);
    }

    /// <summary>How a melody's phrases end, by its melody facet: on the chord's root, its third, any of its notes.</summary>
    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var melody in new double?[] { 0, 0.25, null, 0.75, 1 })
        {
            var (ends, roots, thirds, tones) = Ends(TestCorpus.InParallel(Enumerable.Range(0, 64), seed => melody is { } given ? TestCorpus.Get(seed, Melody(given)) : TestCorpus.Get(seed)));
            Console.WriteLine($"melody {melody?.ToString() ?? "drawn"}: {ends} phrase ends, on the root {roots / (double)ends:P0}, the third {thirds / (double)ends:P0}, the chord {tones / (double)ends:P0}");
        }

        await Task.CompletedTask;
    }
}
