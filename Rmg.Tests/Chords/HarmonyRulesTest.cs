using Rmg.Core;
using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Tests.Chords;

/// <summary>The harmony's devices kept where they belong: out of the plainest songs, a vamp on its one chord, a section's home in its genre's pool, a dominant before a chord it resolves to, a loop on the first section's harmony.</summary>
public sealed class HarmonyRulesTest
{
    private static Genre With(GenreChoice choice, string option) => TestCorpus.Always(choice, option);

    /// <summary>The plainest songs never reharmonise an appearance nor hold a section over a pedal.</summary>
    [Test]
    public async Task ThePlainestSongs_NeitherReharmoniseNorHoldAPedal()
    {
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Base: 0))))
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
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: With(GenreChoices.Vamp, "sections")))))
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

    /// <summary>
    ///     A section's home at the contrast, the classical form's, its fifth in a major key and its relative major in a minor
    ///     one, a genre's naming it alone, the tonic where it keeps it.
    /// </summary>
    [Test]
    public async Task AContrastHome_IsAMajorKeysFifth_AndAMinorKeysRelativeMajor()
    {
        var (major, minor) = (0, 0);
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: With(GenreChoices.SectionHome, "contrast")))))
        foreach (var home in song.Trace.Where(x => x.Point == TracePoints.SectionHome && (int)x.Value! != 0))
        {
            var scale = (Scale)song.Trace.Last(x => x.Point == TracePoints.SectionScale && x.Section == home.Section).Value!;
            var isMinor = scale.Offsets[2] - scale.Offsets[0] == 3;
            // a step from the tonic, from -3 to 3: a minor key's third up, a major key's fifth, a fourth down
            await Assert.That((int)home.Value!).IsEqualTo(isMinor ? 2 : -3).Because($"seed {song.Seed}, section {home.Section}, {scale.Name}");
            (major, minor) = isMinor ? (major, minor + 1) : (major + 1, minor);
        }

        Console.WriteLine($"sections at the contrast: {major} in a major key, {minor} in a minor one");
        await Assert.That(major).IsGreaterThan(5);
        await Assert.That(minor).IsGreaterThan(5);
    }

    /// <summary>A genre's homes named the tonic alone keep every section's home on it.</summary>
    [Test]
    public async Task AGenresSectionHomes_KeepToItsPool()
    {
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: With(GenreChoices.SectionHome, "tonic")))))
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
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre))))
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
    ///     A dominant of a scale of its own, an applied one, a passing one or its tritone substitute, as the band plays it
    ///     in a reharmonised appearance after the stages that change chords, leads into a diminished chord of the appearance
    ///     only where its section first played it so, a borrowed chord's resolution in an exotic scale of its own harmony;
    ///     the section before's last chord is its own section's, which the reharmonisation never changed.
    /// </summary>
    [Test]
    public async Task ADominant_AsPlayed_NeverLeadsIntoADiminishedChord()
    {
        var (dominants, reharmonised) = (0, 0);
        var all = ImmutableDictionary<string, Rmg.Core.Probabilities.ByConvention>.Empty;
        var genre = new Genre(ImmutableDictionary<string, ImmutableDictionary<string, Rmg.Core.Probabilities.ByConvention>>.Empty
            .Add(GenreChoices.ChordScales.Name, all.Add("applied", new(1, 1, 1)))
            .Add(GenreChoices.HarmonyDevices.Name, all.Add("reharmonised", new(1, 1, 1))));
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre))))
        {
            if (song.Trace.Single(x => x.Point == TracePoints.Reharmonisation).Value is not (int later, ImmutableArray<double>))
                continue;
            reharmonised++;
            var timeline = song.Song.TrackEventStateTimelineMap;
            var common = timeline.CommonStateTimelineMap;
            // every note of the parts that play the chord, the bass's and the chords', as the realizer reads them
            var notes = song.Song.TrackDefinitions.Where(x => (x.Value.Role.PlaysChords() || x.Value.Role == TrackRole.Bass) && timeline.TrackTimelineMap.ContainsKey(x.Key))
                .SelectMany(x => Realizer.GetNoteStates(timeline.TrackTimelineMap[x.Key], x.Value, common)).OrderBy(x => x.Position).ToArray();
            var changes = common.GetStateTimeline(StateKinds.ChordChange).Select(x => x.Position).ToArray();
            // a change's chord, by the first note struck in it, its notes above its root as pitch classes, and whether it is
            // in a scale of its own or moved by semitones; none where no part strikes it
            (HashSet<int> Classes, bool Own)? ChordAt(double at)
            {
                var index = Array.FindLastIndex(changes, x => x <= at + 1e-9);
                var end = index + 1 < changes.Length ? changes[index + 1] : double.MaxValue;
                if (index < 0 || notes.FirstOrDefault(x => x.Position >= changes[index] - 1e-9 && x.Position < end - 1e-9) is not { Value: { } state })
                    return null;
                var (chord, _, classes) = Realizer.GetChordNotes(state);
                var own = state.GetStateValue(StateKinds.ScaleAlterations) is { IsSet: true } alterations && alterations.Value != StepAlterations.None
                          || state.GetStateValue(StateKinds.ChordShift) != 0;
                return (classes.Select(x => (x - chord.Root).Mod(12)).ToHashSet(), own);
            }

            bool Diminished((HashSet<int> Classes, bool Own)? chord) => chord is { } c && c.Classes.IsSupersetOf([3, 6]) && !c.Classes.Contains(7);
            var sections = song.Map.Sections;
            for (var i = 0; i + 1 < changes.Length; i++)
            {
                if (ChordAt(changes[i]) is not { Own: true } dominant || !dominant.Classes.IsSupersetOf([4, 7]) || ChordAt(changes[i + 1]) is not { } next
                    || !sections[later].IsWithin(changes[i]) || !sections[later].IsWithin(changes[i + 1]))
                    continue;
                var span = sections[later];
                dominants++;
                // the same place in the section's first appearance, as drawn
                var first = sections.First(x => x.SectionId == span.SectionId);
                var (was, then) = (first.Start + (changes[i] - span.Start), first.Start + (changes[i + 1] - span.Start));
                await Assert.That(Diminished(next) && !(ChordAt(was) is { Own: true } d && d.Classes.IsSupersetOf([4, 7]) && Diminished(ChordAt(then))))
                    .IsFalse().Because($"seed {song.Seed}, at {changes[i + 1]}");
            }
        }

        await Assert.That(dominants).IsGreaterThan(20);
        await Assert.That(reharmonised).IsGreaterThan(4);
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
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre))))
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
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Base: 0, Genre: unlessPlain))))
            await Assert.That(song.Trace.Where(x => x.Point == TracePoints.Loop).All(x => !(bool)x.Value!)).IsTrue().Because($"seed {song.Seed}");
    }
}
