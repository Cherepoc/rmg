using Rmg.Core;
using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Tests.Chords;

/// <summary>A section planing: every chord the home's shape moved, its cadence raising nothing by semitones, each by a small move or a fourth, a wild song's alone.</summary>
public sealed class PlaningTest
{
    private static SongOverrides Progression(double value) => new(Facets: TestCorpus.FacetsOf((Facet.Progression, value)));

    [Test]
    public async Task APlanedSectionsChords_AreItsHomesShape_MovedBySmallStepsOrAFourth_NeverFarFromHome_TheLastEndingHome()
    {
        var (planed, lastPlaned) = (0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, Progression(1))))
        {
            var progressions = song.Trace.Where(x => x.Point == TracePoints.Progression).Select(x => (x.Section, Roots: (ImmutableArray<int>)x.Value!)).ToArray();
            foreach (var entry in song.Trace.Where(x => x.Point == TracePoints.Planing && x.Value is not null))
            {
                planed++;
                var shifts = (ImmutableArray<int>)entry.Value!;
                await Assert.That(shifts[0]).IsEqualTo(0);
                await Assert.That(shifts.All(x => Math.Abs(x) <= Planing.Reach)).IsTrue();
                await Assert.That(shifts.Zip(shifts.Skip(1), (a, b) => Math.Abs(b - a)).All(x => x is 1 or 2 or 3 or 5)).IsTrue();
                await Assert.That(progressions.Last(x => x.Section == entry.Section).Roots.All(x => x == 0)).IsTrue();
                // every chord the home's shape, and no cadence's raise
                var shapes = song.Trace.Where(x => x.Point == TracePoints.Chord && x.Section == entry.Section).Select(x => x.StateMap.GetStateValue(CompositionStateKinds.RoleChord)).ToArray();
                await Assert.That(shapes.Length).IsGreaterThan(0);
                await Assert.That(shapes.All(x => !x.IsEmpty && x.SequenceEqual(shapes[0]))).IsTrue().Because($"seed {song.Seed}");
                await Assert.That(((int? Raisable, bool Raised))song.Trace.Last(x => x.Point == TracePoints.CadenceRaise && x.Section == entry.Section).Value!).IsEqualTo((null, false)).Because($"seed {song.Seed}");
                // the song's last section, which ends the song, sliding home at its end
                if (entry.Section == song.Map.Sections[^1].SectionId)
                {
                    lastPlaned++;
                    await Assert.That(shifts[^1]).IsEqualTo(0).Because($"seed {song.Seed}");
                }
            }
        }

        await Assert.That(planed).IsGreaterThan(5);
        await Assert.That(lastPlaned).IsGreaterThan(0);
    }

    /// <summary>
    ///     A planed section's chords keep the home's structure, in the section's scale, or each take a mode of its own, the
    ///     first the home's; a calibration of how often, in Fusion's, and of how often a chord's mode differs from the one
    ///     before.
    /// </summary>
    [Test]
    public async Task APlanedSectionsChords_KeepTheHomesStructure_OrEachTakeAModeOfItsOwn()
    {
        var (planed, inModes, chords, changed) = (0, 0, 0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Genres.GenrePresets.Genre("fusion")))))
        foreach (var entry in song.Trace.Where(x => x.Point == TracePoints.Planing && x.Value is not null))
        {
            planed++;
            var scales = (ImmutableArray<ChordScale>)song.Trace.Last(x => x.Point == TracePoints.ChordScales && x.Section == entry.Section).Value!;
            await Assert.That(scales[0]).IsEqualTo(ChordScale.Section).Because($"seed {song.Seed}");
            await Assert.That(scales.All(x => x.Kind is ChordScaleKind.Section or ChordScaleKind.Own)).IsTrue().Because($"seed {song.Seed}");
            if (scales.All(x => x == ChordScale.Section))
                continue;
            inModes++;
            chords += scales.Length - 1;
            changed += scales.Zip(scales.Skip(1)).Count(x => x.First != x.Second);
        }

        Console.WriteLine($"of {planed} planed sections, {inModes / (double)planed:P1} in modes, their chords' modes changing {changed / (double)chords:P1} of the moves");
        await Assert.That(planed).IsGreaterThan(20);
        // measured 73% to 74% in modes, 76% to 79% of the moves changing, at offsets 0 and a million
        await Assert.That(inModes / (double)planed).IsBetween(0.4, 0.95);
        await Assert.That(changed / (double)chords).IsGreaterThan(0.4);
    }

    /// <summary>
    ///     A report of the melody across a change of chord, in planed sections and in the others: how often its move is the
    ///     root's, in semitones, as a line transposed with its chord would move, and how often it keeps its note, a common
    ///     tone.
    /// </summary>
    [Test]
    [Explicit]
    public async Task ReportTheMelodyAcrossPlanedChanges()
    {
        var counts = new Dictionary<bool, (int Changes, int WithTheRoot, int Kept)>();
        foreach (var song in new[] { "fusion", "jazz" }.SelectMany(genre => TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Genres.GenrePresets.Genre(genre))))))
        {
            var planed = song.Trace.Where(x => x.Point == TracePoints.Planing && x.Value is not null).Select(x => x.Section).ToHashSet();
            var melody = song.Song.Notes!.GetValueOrDefault(song.Song.Track(TrackRole.Melody))?.ToArray() ?? [];
            foreach (var (before, after) in melody.Zip(melody.Skip(1)))
            {
                int Root(RealizedNote note) => Realizer.GetChord(note.State).Chord.GetPitch(0);
                var (from, to) = (Root(before.Value), Root(after.Value));
                if (from == to || after.Position - before.Position > 2)
                    continue;
                if (song.Map.Sections.FirstOrDefault(x => after.Position >= x.Start - 1e-9 && after.Position < x.End - 1e-9) is not { } span)
                    continue;
                var key = planed.Contains(span.SectionId);
                var (changes, withTheRoot, kept) = counts.GetValueOrDefault(key);
                var moved = after.Value.Pitches.Max() - before.Value.Pitches.Max();
                counts[key] = (changes + 1, withTheRoot + ((moved - (to - from)).Mod(12) == 0 && moved != 0 ? 1 : 0), kept + (moved == 0 ? 1 : 0));
            }
        }

        foreach (var (key, (changes, withTheRoot, kept)) in counts)
            Console.WriteLine($"{(key ? "planed" : "other")} sections: {changes} changes, the melody moving with the root {withTheRoot / (double)changes:P1}, keeping its note {kept / (double)changes:P1}");
        await Task.CompletedTask;
    }

    [Test]
    public async Task AMiddlingSong_NeverPlanes()
    {
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, Progression(0.5))))
            await Assert.That(song.Trace.Where(x => x.Point == TracePoints.Planing).All(x => x.Value is null)).IsTrue();
    }
}
