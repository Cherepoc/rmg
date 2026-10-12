using Rmg.Core;
using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.Chords;

/// <summary>The harmony's devices kept where they belong: out of the plainest songs, a vamp on its one chord, a section's home in its genre's pool, a dominant before a chord it resolves to, a loop on the first section's harmony.</summary>
public sealed class HarmonyRulesTest
{
    private static Genre With(GenreChoice choice, string option) => TestCorpus.Always(choice, option);

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

    /// <summary>
    ///     A dominant made of a chord, the next chord's applied dominant, a passing dominant or its tritone substitute, never
    ///     leads into a diminished chord, which has no fifth to resolve to.
    /// </summary>
    [Test]
    [Arguments("chordScales", "applied")]
    [Arguments("passingChords", "chords")]
    public async Task ADominant_NeverLeadsIntoADiminishedChord(string choice, string option)
    {
        var dominants = 0;
        var genre = TestCorpus.Always(GenreChoices.All.Single(x => x.Name == choice), option);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre))))
        {
            T Last<T>(string point, int section) => (T)song.Trace.Last(x => x.Point == point && x.Section == section).Value!;
            foreach (var section in song.Trace.Where(x => x.Point == TracePoints.Progression).Select(x => x.Section).Distinct())
            {
                var (scale, home) = (Last<Scale>(TracePoints.SectionScale, section), Last<int>(TracePoints.SectionHome, section));
                var (roots, scales) = (Last<ImmutableArray<int>>(TracePoints.Progression, section), Last<ImmutableArray<ChordScale>>(TracePoints.ChordScales, section));
                int Interval(int chord, int step) => (ChordScales.PitchOf(scale, home + roots[chord] + step, scales[chord]) - ChordScales.PitchOf(scale, home + roots[chord], scales[chord])).Mod(12);
                for (var chord = 0; chord + 1 < roots.Length; chord++)
                {
                    if (scales[chord].Kind != ChordScaleKind.Applied || Interval(chord, 2) != 4 || Interval(chord, 4) != 7)
                        continue;
                    dominants++;
                    await Assert.That(ChordScales.IsDiminished(scale, home, roots[chord + 1], scales[chord + 1])).IsFalse().Because($"seed {song.Seed}, section {section}, chord {chord}");
                }
            }
        }

        await Assert.That(dominants).IsGreaterThan(20);
    }

    /// <summary>
    ///     A looping section plays the song's first section's harmony, its chords, where they change and their scales, the
    ///     song's last only where that harmony is at home on the tonic; and the plainest songs never loop.
    /// </summary>
    [Test]
    public async Task ALoopingSection_PlaysTheFirstSectionsHarmony_AndThePlainestNeverLoop()
    {
        var looping = 0;
        var genre = With(GenreChoices.Loop, "sections");
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre))))
        {
            T Last<T>(string point, int section) => (T)song.Trace.Last(x => x.Point == point && x.Section == section).Value!;
            var first = song.Map.Sections[0].SectionId;
            foreach (var section in song.Trace.Where(x => x.Point == TracePoints.Loop && (bool)x.Value!).Select(x => x.Section))
            {
                looping++;
                await Assert.That(Last<ImmutableArray<int>>(TracePoints.Progression, section).SequenceEqual(Last<ImmutableArray<int>>(TracePoints.Progression, first))).IsTrue().Because($"seed {song.Seed}, section {section}");
                await Assert.That(Last<HarmonicRhythm>(TracePoints.HarmonicRhythm, section).Changes.SequenceEqual(Last<HarmonicRhythm>(TracePoints.HarmonicRhythm, first).Changes)).IsTrue().Because($"seed {song.Seed}, section {section}");
                await Assert.That(Last<ImmutableArray<ChordScale>>(TracePoints.ChordScales, section).SequenceEqual(Last<ImmutableArray<ChordScale>>(TracePoints.ChordScales, first))).IsTrue().Because($"seed {song.Seed}, section {section}");
                if (section == song.Map.Sections[^1].SectionId)
                    await Assert.That(Last<int>(TracePoints.SectionHome, first)).IsEqualTo(0).Because($"seed {song.Seed}");
            }
        }

        await Assert.That(looping).IsGreaterThan(20);
        // a genre looping but at its plain end
        var unlessPlain = new Genre(ImmutableDictionary<string, ImmutableDictionary<string, ByConvention>>.Empty
            .Add(GenreChoices.Loop.Name, ImmutableDictionary<string, ByConvention>.Empty.Add("sections", new ByConvention(0, 1, 1))));
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Base: 0, Genre: unlessPlain))))
            await Assert.That(song.Trace.Where(x => x.Point == TracePoints.Loop).All(x => !(bool)x.Value!)).IsTrue().Because($"seed {song.Seed}");
    }
}
