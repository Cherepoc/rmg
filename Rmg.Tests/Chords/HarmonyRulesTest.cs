using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.Chords;

/// <summary>The harmony's devices kept where they belong: out of the plainest songs, a vamp on its one chord, a section's home in its genre's pool.</summary>
public sealed class HarmonyRulesTest
{
    private static Genre With(GenreChoice choice, string option) =>
        new(ImmutableDictionary<string, ImmutableDictionary<string, ByConvention>>.Empty.Add(choice.Name,
            ImmutableDictionary<string, ByConvention>.Empty.Add(option, new ByConvention(1, 1, 1))));

    /// <summary>The plainest songs never reharmonise an appearance nor hold a section over a pedal.</summary>
    [Test]
    public async Task ThePlainestSongs_NeitherReharmoniseNorHoldAPedal()
    {
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Base: 0))))
        {
            await Assert.That(song.Trace.Single(x => x.Point == TracePoints.Reharmonisation).Value).IsNull().Because($"seed {song.Seed}");
            await Assert.That(song.Trace.Where(x => x.Point == TracePoints.SectionPedal).All(x => x.Value is null)).IsTrue().Because($"seed {song.Seed}");
        }
    }

    /// <summary>
    ///     A vamping section plays its home chord alone, wherever it plays, a later appearance's too, but in the pattern
    ///     leading into a key change, whose chords lead into the new key (<see cref="KeyChange" />).
    /// </summary>
    [Test]
    public async Task AVampingSection_PlaysOneChord_WhereverItPlays()
    {
        var vamping = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: With(GenreChoices.Vamp, "sections")))))
        {
            var vamps = song.Trace.Where(x => x.Point == TracePoints.Vamp && (bool)x.Value!).Select(x => x.Section).ToHashSet();
            var common = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap;
            var keyChanges = ((ImmutableArray<KeyChange>)song.Trace.Single(x => x.Point == TracePoints.KeyChange).Value!).Select(x => x.Position).ToArray();
            bool LeadsIntoAKeyChange(double position, Rmg.Core.Songs.SectionSpan span) =>
                keyChanges.Any(k => k > position + 1e-9 && k <= span.End + 1e-9 && k - position <= span.Meter.PatternDuration + 1e-9);
            foreach (var span in song.Map.Sections.Where(x => vamps.Contains(x.SectionId)))
            {
                vamping++;
                var roots = common.GetStateTimeline(StateKinds.ChordChange).Where(x => span.IsWithin(x.Position) && !LeadsIntoAKeyChange(x.Position, span))
                    .Select(x => common.GetEffectiveStateMapAt(x.Position).GetStateValue(StateKinds.ChordRoot)).Distinct().ToArray();
                await Assert.That(roots.Length).IsLessThanOrEqualTo(1).Because($"seed {song.Seed} at {span.Start}: {string.Join(", ", roots)}");
            }
        }

        await Assert.That(vamping).IsGreaterThan(10);
    }

    /// <summary>A genre's homes named the tonic alone keep every section's home on it.</summary>
    [Test]
    public async Task AGenresSectionHomes_KeepToItsPool()
    {
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: With(GenreChoices.SectionHome, "tonic")))))
        foreach (var home in song.Trace.Where(x => x.Point == TracePoints.SectionHome))
            await Assert.That((int)home.Value!).IsEqualTo(0).Because($"seed {song.Seed}, section {home.Section}");
    }
}
