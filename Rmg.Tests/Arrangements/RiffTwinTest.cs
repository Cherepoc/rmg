using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Tests.Arrangements;

public sealed class RiffTwinTest
{
    [Test]
    public async Task ATwin_PlaysTheRiffsNotes_ByAStepOfItsOwn_AllTheWayToTheOtherSide()
    {
        var (twins, matched) = (0, 0);
        foreach (var song in TestCorpus.Range(64))
        {
            var absent = song.Absent;
            var riffPan = ((PitchInstrumentTrack)song.Song.TrackDefinitions[song.Song.Track(TrackRole.Riff)]).Pan;
            var twinPan = ((PitchInstrumentTrack)song.Song.TrackDefinitions[song.Song.Track(TrackRole.RiffTwin)]).Pan;
            var twin = song.Song.Notes!.GetValueOrDefault(song.Song.Track(TrackRole.RiffTwin))?.ToArray() ?? [];
            if (absent.Contains(TrackRole.RiffTwin))
            {
                await Assert.That(twin).IsEmpty();
                continue;
            }

            twins++;
            await Assert.That(absent).DoesNotContain(TrackRole.Riff);
            await Assert.That(Math.Abs(riffPan)).IsEqualTo(1);
            await Assert.That(twinPan).IsEqualTo(-riffPan);
            // a riff that plays no note has no twin to measure
            var riff = song.Song.Notes!.GetValueOrDefault(song.Song.Track(TrackRole.Riff))?.ToDictionary(x => Math.Round(x.Position, 6), x => x.Value.State) ?? [];
            // every note where the riff plays one, but where the riff solos, which the twin keeps the riff under: its scale
            // step one of a twin's apart from the riff's, the same in a section
            foreach (var span in song.Map.Sections)
            {
                var notes = twin.Where(x => x.Position >= span.Start && x.Position < span.End && !(riff.TryGetValue(Math.Round(x.Position, 6), out var r) && r.GetStateValue(CompositionStateKinds.LineSolo) != 0)).ToArray();
                foreach (var note in notes)
                    await Assert.That(riff.ContainsKey(Math.Round(note.Position, 6))).IsTrue().Because($"seed {song.Seed}, the twin at {note.Position}");
                matched += notes.Length;
                var apart = notes.Select(x => x.Value.State.GetStateValue(StateKinds.ScaleStep) - riff[Math.Round(x.Position, 6)].GetStateValue(StateKinds.ScaleStep))
                    .Distinct()
                    .ToArray();
                await Assert.That(apart.Length <= 1 && apart.All(x => LineDoubling.TwinSteps.Any(t => t.Step == x))).IsTrue().Because($"seed {song.Seed} at {span.Start}");
            }
        }

        await Assert.That(twins).IsGreaterThan(3);
        await Assert.That(matched).IsGreaterThan(100);
    }

    /// <summary>
    ///     A twin plays only in the sections its twin lead draws, and there, where the genre tunes its riff down, the riff
    ///     and its twin play as many octaves up as it does (<see cref="CompositionStateKinds.LineRaise" />).
    /// </summary>
    [Test]
    public async Task ATwin_PlaysOnlyWhereItsSectionTwinLeads_ItsRiffUpAsFarAsTheGenreTunesItDown()
    {
        var genre = Rmg.Tests.Genres.GenrePresets.Genre("metal");
        var raise = Math.Max(0, -Rmg.Core.Composition.Registers.OctavesOf(genre, TrackRole.Riff));
        var (leading, raised) = (0, 0);
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre,
                     Parts: ImmutableDictionary<TrackRole, bool>.Empty.Add(TrackRole.Riff, true).Add(TrackRole.RiffTwin, true)))))
        {
            var leads = song.Trace.Where(x => x.Point == TracePoints.TwinLead).ToDictionary(x => x.Section, x => (bool)x.Value!);
            var twin = song.Song.Notes!.GetValueOrDefault(song.Song.Track(TrackRole.RiffTwin))?.ToArray() ?? [];
            var riff = song.Song.Notes!.GetValueOrDefault(song.Song.Track(TrackRole.Riff))?.ToArray() ?? [];
            foreach (var play in song.Map.Plays)
            {
                var notes = twin.Where(x => x.IsWithin(play.Span.Start, play.Span.End)).ToArray();
                await Assert.That(notes.Length == 0 || leads[play.Span.SectionId]).IsTrue().Because($"seed {song.Seed} at {play.Span.Start}");
                if (notes.Length == 0)
                    continue;
                leading++;
                // the riff's notes the twin harmonises, up with it
                var harmonised = notes.Select(x => Math.Round(x.Position, 6)).ToHashSet();
                foreach (var note in riff.Where(x => harmonised.Contains(Math.Round(x.Position, 6)) && x.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 0))
                {
                    await Assert.That(note.Value.State.GetStateValue(CompositionStateKinds.LineRaise)).IsEqualTo(raise).Because($"seed {song.Seed} at {note.Position}");
                    raised++;
                }
            }
        }

        Console.WriteLine($"{leading} appearances twin-led, {raised} riff notes under a twin, raised {raise}");
        await Assert.That(leading).IsGreaterThan(5);
        await Assert.That(raised).IsGreaterThan(0);
    }

    private static SongOverrides Melody(double melody, bool? twin) => new(
        Facets: TestCorpus.FacetsOf((Facet.Melody, melody)),
        Parts: twin is null
            ? ImmutableDictionary<TrackRole, bool>.Empty.Add(TrackRole.Riff, true)
            : ImmutableDictionary<TrackRole, bool>.Empty.Add(TrackRole.Riff, true).Add(TrackRole.RiffTwin, twin.Value));

    [Test]
    public async Task APlainSong_HarmonisesNoRiff_AndOneGivenATwin_InThirdsAndSixthsAlone()
    {
        var drawn = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, Melody(0, null)));
        await Assert.That(drawn.Count(x => !x.Absent.Contains(TrackRole.RiffTwin))).IsEqualTo(0);

        var given = TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, Melody(0, true)));
        await Assert.That(given.SelectMany(Intervals).Distinct().Order()).IsEquivalentTo(new[] { -5, 2 }, CollectionOrdering.Matching);
    }

    [Test]
    public async Task AWildSong_HarmonisesItsRiff_ByWiderIntervalsToo()
    {
        var given = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, Melody(1, true)));
        var intervals = given.SelectMany(Intervals).ToArray();
        Console.WriteLine($"of {intervals.Length} twinned sections, {intervals.Count(x => x is not (2 or -5)) / (double)intervals.Length:P1} wider than a third");

        // measured 59% to 62% of 411 to 433 twinned sections, at offsets 0 and a million
        await Assert.That(intervals.Count(x => x is not (2 or -5))).IsGreaterThan(intervals.Length / 3);
    }

    /// <summary>The steps a twin plays apart from the riff in each section it plays, where the riff does not solo.</summary>
    private static IEnumerable<int> Intervals(CorpusSong song)
    {
        // a riff that plays no note has no twin to measure
        var riff = song.Song.Notes!.GetValueOrDefault(song.Song.Track(TrackRole.Riff))?.ToDictionary(x => Math.Round(x.Position, 6), x => x.Value.State) ?? [];
        var twin = song.Song.Notes!.GetValueOrDefault(song.Song.Track(TrackRole.RiffTwin))?.ToArray() ?? [];
        return song.Map.Sections.SelectMany(span => twin
            .Where(x => x.Position >= span.Start && x.Position < span.End && riff.TryGetValue(Math.Round(x.Position, 6), out var r) && r.GetStateValue(CompositionStateKinds.LineSolo) == 0)
            .Select(x => x.Value.State.GetStateValue(StateKinds.ScaleStep) - riff[Math.Round(x.Position, 6)].GetStateValue(StateKinds.ScaleStep))
            .Distinct());
    }

    /// <summary>
    ///     How many songs with a riff have a twin, and the steps it plays apart in its sections, by fifths of the song's
    ///     melody facet.
    /// </summary>
    [Test]
    [Explicit]
    public async Task Report()
    {
        var songs = TestCorpus.Range(512).Where(x => !x.Absent.Contains(TrackRole.Riff)).ToArray();
        foreach (var band in songs.GroupBy(x => Math.Clamp((int)(x.Unconventionality[Facet.Melody] * 5), 0, 4)).OrderBy(x => x.Key))
        {
            var twins = band.Where(x => !x.Absent.Contains(TrackRole.RiffTwin)).ToArray();
            var intervals = twins.SelectMany(Intervals).ToArray();
            var shares = intervals.GroupBy(x => x).OrderBy(x => x.Key).Select(x => $"{x.Key:+0;-0} {x.Count() / (double)intervals.Length:P0}");
            Console.WriteLine($"  melody {band.Key}/5: {twins.Length} of {band.Count()} songs with a riff have a twin ({twins.Length / (double)band.Count():P0}); its sections apart by {string.Join(", ", shares)}");
        }

        await Task.CompletedTask;
    }

    /// <summary>
    ///     The twin rests in a section that chugs, which may play its riff in power chords, a third against a power chord
    ///     being mud, as it rests under a riff in power chords.
    /// </summary>
    [Test]
    public async Task TheTwin_RestsInASectionThatChugs()
    {
        var checkedSections = 0;
        // Metal's songs given the riff and its twin, so that every chugging section has a twin to rest
        var withTwin = new SongOverrides(Genre: Rmg.Tests.Genres.GenrePresets.Genre("metal"), Parts: ImmutableDictionary<TrackRole, bool>.Empty.Add(TrackRole.Riff, true).Add(TrackRole.RiffTwin, true));
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, withTwin)))
        {
            var chugs = song.Trace.Where(x => x.Point == TracePoints.Chugs && (bool)x.Value!).Select(x => x.Section).ToHashSet();
            var twin = song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key].Role == TrackRole.RiffTwin).SelectMany(x => x.Value).ToArray();
            foreach (var span in song.Map.Sections.Where(x => chugs.Contains(x.SectionId) && !song.IsSolo(x)))
            {
                checkedSections++;
                await Assert.That(twin.Count(x => x.Position >= span.Start - 1e-9 && x.Position < span.End - 1e-9)).IsEqualTo(0).Because($"seed {song.Seed} at {span.Start}");
            }
        }

        await Assert.That(checkedSections).IsGreaterThan(10);
    }
}
