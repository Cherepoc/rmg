using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Tests.Genres;

/// <summary>
///     How often every preset plays each of the engine's devices, beside the songs with no genre: its harmony's colours, its
///     form's devices and its parts' expression, so that a genre playing a device its style does not use shows.
/// </summary>
public sealed class GenreDevicesReportTest
{
    private const int Count = 32;

    /// <summary>A song's share of each device, by its name.</summary>
    private static Dictionary<string, double> Measure(CorpusSong song)
    {
        double Sections(string point, Func<object?, bool> plays) =>
            song.Trace.Where(x => x.Point == point).Select(x => plays(x.Value) ? 1.0 : 0).DefaultIfEmpty(double.NaN).Average();
        var scales = song.Trace.Where(x => x.Point == TracePoints.ChordScales).SelectMany(x => (ImmutableArray<ChordScale>)x.Value!).ToArray();
        double Chords(ChordScaleKind kind) => scales.Length > 0 ? scales.Count(x => x.Kind == kind) / (double)scales.Length : double.NaN;
        var chords = song.Song.Notes!.GetValueOrDefault(SongTracks.ChordsTrack)?.Where(x => x.Value.Pitches.Length >= 3).ToArray() ?? [];
        var lines = new[] { SongTracks.MelodyTrack, SongTracks.CounterMelodyTrack, SongTracks.RiffTrack, SongTracks.SoloTrack }
            .SelectMany(x => song.Song.Notes!.GetValueOrDefault(x) ?? EventTimeline.Create<RealizedNote>(0, [])).ToArray();
        double Lines(Func<RealizedNote, bool> plays) => lines.Length > 0 ? lines.Count(x => plays(x.Value)) / (double)lines.Length : double.NaN;
        var lifts = song.Trace.Count(x => x.Point == TracePoints.LiftCue);
        return new Dictionary<string, double>
        {
            ["chords applied dominants"] = Chords(ChordScaleKind.Applied),
            ["chords borrowed"] = Chords(ChordScaleKind.Borrowed),
            ["chords in a scale of their own"] = Chords(ChordScaleKind.Own),
            ["chords of four notes or more"] = chords.Length > 0 ? chords.Count(x => x.Value.Pitches.Select(p => p % 12).Distinct().Count() >= 4) / (double)chords.Length : double.NaN,
            ["chords parts arpeggiated"] = Sections(TracePoints.Arpeggio, x => (ArpeggioPattern)x! != ArpeggioPattern.None),
            ["sections with a passing chord"] = Sections(TracePoints.HarmonicRhythm, x => ((HarmonicRhythm)x!).Passing.Length > 0),
            ["sections turned around"] = Sections(TracePoints.Turnaround, x => (bool)x!),
            ["sections planing"] = Sections(TracePoints.Planing, x => x is not null),
            ["sections with an inner line"] = Sections(TracePoints.InnerLine, x => (bool)x!),
            ["sections over a pedal"] = Sections(TracePoints.SectionPedal, x => x is not null),
            ["songs reharmonising"] = song.Trace.Single(x => x.Point == TracePoints.Reharmonisation).Value is null ? 0 : 1,
            ["songs changing key"] = ((ImmutableArray<KeyChange>)song.Trace.Single(x => x.Point == TracePoints.KeyChange).Value!).IsEmpty ? 0 : 1,
            ["sections in stop time"] = Sections(TracePoints.StopTime, x => (bool)x!),
            ["sections pushing"] = Sections(TracePoints.Push, x => (bool)x!),
            ["lifts dropped"] = lifts > 0 ? song.Trace.Count(x => x.Point == TracePoints.Drop) / (double)lifts : double.NaN,
            ["sections of 16 bars or more"] = song.Song.Map!.Sections.Average(x => x.Duration >= 16 * x.Meter.BarDuration - 1e-9 ? 1.0 : 0),
            ["sections of 32 bars"] = song.Song.Map!.Sections.Average(x => x.Duration >= 32 * x.Meter.BarDuration - 1e-9 ? 1.0 : 0),
            ["appearances building or thinning"] = song.Trace.Where(x => x.Point == TracePoints.Texture)
                .Select(x => ((int, TextureKind Kind, ImmutableArray<ImmutableHashSet<TrackRole>>))x.Value!).Average(x => x.Kind is TextureKind.Build or TextureKind.Thin ? 1.0 : 0),
            ["verses without a melody"] = song.Trace.Where(x => x.Point == TracePoints.SongForm).Select(x => (SongStructure)x.Value!).Single() is var structure
                && song.Song.Map!.Sections.Select((span, i) => (span, i)).Where(x => structure.Roles.GetValueOrDefault(x.span.SectionId) == SectionRole.Verse).ToArray() is { Length: > 0 } verses
                ? verses.Average(x => song.Resting(x.span).Contains(TrackRole.Melody) ? 1.0 : 0)
                : double.NaN,
            ["songs with a solo"] = song.Solos.Count > 0 ? 1 : 0,
            ["line notes bent or slid"] = Lines(x => x.State.GetStateValue(CompositionStateKinds.Bend) > 0),
            ["bass's changes of pitch slid into"] = song.Song.Notes!.GetValueOrDefault(SongTracks.BassTrack)?.ToArray() is { Length: > 1 } bass
                && bass.Skip(1).Where((x, i) => x.Value.Pitches[0] != bass[i].Value.Pitches[0]).ToArray() is { Length: > 0 } changes
                ? changes.Count(x => x.Value.State.GetStateValue(CompositionStateKinds.Bend) == (int)BendKind.Slide) / (double)changes.Length
                : double.NaN,
            ["line notes with a vibrato"] = Lines(x => x.State.GetStateValue(CompositionStateKinds.Vibrato) > 0 || x.State.GetStateValue(CompositionStateKinds.WideVibrato) > 0),
            ["line notes slurred"] = Lines(x => x.State.GetStateValue(CompositionStateKinds.Legato) > 0),
            ["unconventionality"] = song.Song.Draws!.Unconventionality.Base
        };
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var columns = new List<(string Name, Dictionary<string, double>[] Songs)>
        {
            ("none", TestCorpus.InParallel(Enumerable.Range(0, Count), seed => Measure(TestCorpus.Get(seed))))
        };
        columns.AddRange(GenrePresets.All.Select(preset =>
            (preset.Name, TestCorpus.InParallel(Enumerable.Range(0, Count), seed => Measure(TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(preset))))))));

        Console.WriteLine($"{"",-32}" + string.Join("", columns.Select(x => $"{x.Name,10}")));
        foreach (var measure in columns[0].Songs[0].Keys)
            Console.WriteLine($"{measure,-32}" + string.Join("", columns.Select(c =>
                c.Songs.Select(x => x[measure]).Where(double.IsFinite).DefaultIfEmpty(double.NaN).Average() is var value && measure == "unconventionality"
                    ? $"{value,10:0.00}"
                    : $"{value,10:P0}")));

        await Task.CompletedTask;
    }
}
