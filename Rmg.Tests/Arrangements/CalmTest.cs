using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Arrangements;

/// <summary>A calm section (<see cref="SectionForm" />): its guitars but the one breaking its chords resting, and what sounds there on a guitar.</summary>
public sealed class CalmTest
{
    /// <summary>The calm sections of a genre's songs, with the songs.</summary>
    private static IEnumerable<(CorpusSong Song, SectionSpan Span)> CalmSections(string genre, int count) =>
        TestCorpus.InParallel(Enumerable.Range(0, count), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre))))
            .SelectMany(song => song.Map.Sections.Where(s => song.Trace.Any(t => t.Point == TracePoints.SectionForm && t.Section == s.SectionId && (SectionFormKind)t.Value! == SectionFormKind.Calm))
                .Select(span => (song, span)));

    [Test]
    [Arguments("metal")]
    [Arguments("blackMetal")]
    [Arguments("thrash")]
    [Arguments("doom")]
    public async Task ACalmSection_PlaysNoRiff_NorItsTwin_NorASolo(string genre)
    {
        var sections = 0;
        foreach (var (song, span) in CalmSections(genre, 32))
        {
            sections++;
            foreach (var role in new[] { TrackRole.Riff, TrackRole.RiffTwin }.Where(x => !song.Absent.Contains(x)))
                await Assert.That(song.Song.Notes!.GetValueOrDefault(song.Track(role))?.Any(n => n.IsWithin(span.Start, span.End)) ?? false).IsFalse()
                    .Because($"seed {song.Seed}, its {role} in the calm section at {span.Start}");
            await Assert.That(song.Trace.Any(t => t.Point == TracePoints.Solo && t.Section == span.SectionId)).IsFalse().Because($"seed {song.Seed}, a solo over the calm section at {span.Start}");
        }

        await Assert.That(sections).IsGreaterThan(3);
    }

    [Test]
    [Explicit]
    [Arguments("metal")]
    [Arguments("blackMetal")]
    [Arguments("thrash")]
    [Arguments("doom")]
    public async Task Report(string genre)
    {
        var rows = new List<(TrackRole Role, double Rate, double Shortest)>();
        var sections = 0;
        foreach (var (song, span) in CalmSections(genre, 64))
        {
            sections++;
            // but in its last bar, where a solo after it may lead in
            var end = span.End - span.Meter.BarDuration;
            foreach (var (track, definition) in song.Song.TrackDefinitions.Where(x => x.Value is PitchInstrumentTrack))
            {
                var notes = song.Song.Notes!.GetValueOrDefault(track)?.Where(n => n.IsWithin(span.Start, end)).ToArray() ?? [];
                var programs = notes.Select(n => InstrumentsOverTime.ProgramOf(n.Value.State, ((PitchInstrumentTrack)definition).InstrumentCode)).Distinct().ToArray();
                if (notes.Length < 2 || !programs.Any(p => p is >= 24 and <= 31))
                    continue;
                var onsets = notes.Select(n => n.Position).Distinct().Order().ToArray();
                rows.Add((definition.Role, onsets.Length / (end - span.Start), onsets.Zip(onsets.Skip(1), (a, b) => b - a).Min()));
            }
        }

        Console.WriteLine($"{genre}: {sections} calm sections");
        foreach (var group in rows.GroupBy(x => x.Role))
            Console.WriteLine($"{genre}: {group.Key} sounding on a guitar in {group.Count()}, {group.Average(x => x.Rate):0.00} onsets a beat, " +
                              $"its shortest gap under an 8th in {group.Count(x => x.Shortest < SectionForm.CalmShortest - 1e-9)}");
        await Task.CompletedTask;
    }
}
