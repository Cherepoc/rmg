using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Tests.Genres;

/// <summary>
///     How often every preset plays each of the engine's devices, beside the songs with no genre: its harmony's colours, its
///     form's devices and its parts' expression, so that a genre playing a device its style does not use shows; a value
///     marked * the preset inherits, its choice unnamed, the songs' own.
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
        var sections = song.Song.Map!.Sections.ToArray();
        var polymeters = (System.Collections.IDictionary)song.Trace.Single(x => x.Point == TracePoints.Polymeter).Value!;
        var tempos = (ImmutableArray<double>)song.Trace.Single(x => x.Point == TracePoints.SectionTempo).Value!;
        var meter = (Meter)song.Trace.Single(x => x.Point == TracePoints.Meter).Value!;
        // a piano's chords held a beat or longer, which its part rolls or strikes together
        var held = song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key] is PitchInstrumentTrack).SelectMany(x => x.Value.Where(n =>
            n.Value.Pitches.Length >= 3 && n.Value.Duration >= 1 - 1e-9 && n.Value.State.GetStateValue(CompositionStateKinds.Program) is var stated
            && (stated > 0 ? stated - 1 : ((PitchInstrumentTrack)song.Song.TrackDefinitions[x.Key]).InstrumentCode) is >= 0 and <= 7)).ToArray();
        // the pitched parts' sections, and those in an instrument switched to
        var played = song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key] is PitchInstrumentTrack)
            .SelectMany(x => sections.Select((span, i) => (x.Key, i, Notes: x.Value.Where(n => n.Position >= span.Start - 1e-9 && n.Position < span.End - 1e-9).ToArray())))
            .Where(x => x.Notes.Length > 0).ToArray();
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
            ["songs swung"] = song.Swing.Delay > 0 ? 1 : 0,
            ["songs swung in 16ths"] = song.Swing.Delay > 0 && song.Swing.Period < 1 ? 1 : 0,
            ["songs with a guitar driven when loud"] = song.Trace.Any(x => x.Point == TracePoints.LouderForm) ? 1 : 0,
            ["line notes bent or slid"] = Lines(x => x.State.GetStateValue(CompositionStateKinds.Bend) > 0),
            ["bass's changes of pitch slid into"] = song.Song.Notes!.GetValueOrDefault(SongTracks.BassTrack)?.ToArray() is { Length: > 1 } bass
                && bass.Skip(1).Where((x, i) => x.Value.Pitches[0] != bass[i].Value.Pitches[0]).ToArray() is { Length: > 0 } changes
                ? changes.Count(x => x.Value.State.GetStateValue(CompositionStateKinds.Bend) == (int)BendKind.Slide) / (double)changes.Length
                : double.NaN,
            ["line notes with a vibrato"] = Lines(x => x.State.GetStateValue(CompositionStateKinds.Vibrato) > 0 || x.State.GetStateValue(CompositionStateKinds.WideVibrato) > 0),
            ["line notes slurred"] = Lines(x => x.State.GetStateValue(CompositionStateKinds.Legato) > 0),
            ["sections polymetric"] = sections.Average(x => polymeters.Contains(x.SectionId) ? 1.0 : 0),
            ["appearances at a tempo of their own"] = tempos.Average(x => Math.Abs(x - 1) > 1e-9 ? 1.0 : 0),
            ["sections in a meter of their own"] = sections.Average(x => x.Meter.HasSameBar(meter) ? 0 : 1.0),
            ["sections the solo part answers"] = Sections(TracePoints.SoloAnswers, x => (((int, bool Answers))x!).Answers),
            ["lines in octaves"] = Sections(TracePoints.Octave, x => (((int, TrackRole, double, bool InOctaves))x!).InOctaves),
            ["sections harmonising the melody"] = sections.Length == 0 ? double.NaN
                : song.Trace.Where(x => x.Point == TracePoints.LineDoubling).Count(x => (((int, TrackRole Source, TrackRole, int))x.Value!).Source == TrackRole.Melody) / (double)sections.Length,
            ["songs repeating the last section"] = (bool)song.Trace.Single(x => x.Point == TracePoints.LastRepeat).Value! ? 1 : 0,
            ["parts' sections switched"] = played.Length > 0
                ? played.Count(x => x.Notes.Any(n => n.Value.State.GetStateValue(CompositionStateKinds.Program) is var p && p > 0 && p - 1 != ((PitchInstrumentTrack)song.Song.TrackDefinitions[x.Key]).InstrumentCode)) / (double)played.Length
                : double.NaN,
            ["songs with a strong effect"] = ((SongEffects)song.Trace.Single(x => x.Point == TracePoints.SongEffects).Value!).Strong != StrongEffect.None ? 1 : 0,
            ["held piano chords rolled"] = held.Length > 0 ? held.Count(x => x.Value.State.GetStateValue(CompositionStateKinds.Strum) > 0) / (double)held.Length : double.NaN,
            ["reverb sent"] = song.Song.Notes!.Values.SelectMany(x => x).Select(x => x.Value.State.GetStateValue(CompositionStateKinds.Reverb) - 1.0).Where(x => x >= 0).DefaultIfEmpty(double.NaN).Average(),
            ["unconventionality"] = song.Song.Draws!.Unconventionality.Base
        };
    }

    /// <summary>The genre choice each measure is a genre's by, which a preset that does not name it inherits; none for a measure of no choice.</summary>
    private static readonly Dictionary<string, string> ChoiceOf = new()
    {
        ["chords applied dominants"] = "chordScales", ["chords borrowed"] = "chordScales", ["chords in a scale of their own"] = "chordScales",
        ["chords of four notes or more"] = "chordShape", ["chords parts arpeggiated"] = "arpeggio", ["sections with a passing chord"] = "passingChords",
        ["sections turned around"] = "turnaround", ["sections planing"] = "planing", ["sections with an inner line"] = "harmonyDevices",
        ["sections over a pedal"] = "harmonyDevices", ["songs reharmonising"] = "harmonyDevices", ["songs changing key"] = "keyChanges",
        ["sections in stop time"] = "stopTime", ["sections pushing"] = "push", ["lifts dropped"] = "drop", ["sections of 16 bars or more"] = "sectionLength",
        ["sections of 32 bars"] = "sectionLength", ["appearances building or thinning"] = "texture", ["verses without a melody"] = "voice",
        ["songs with a solo"] = "solos", ["songs with a guitar driven when loud"] = "louderForms", ["songs swung"] = "feelChances", ["songs swung in 16ths"] = "feelValues", ["line notes bent or slid"] = "effects", ["bass's changes of pitch slid into"] = "glide",
        ["line notes with a vibrato"] = "effects", ["line notes slurred"] = "articulation", ["sections polymetric"] = "polymeter",
        ["appearances at a tempo of their own"] = "sectionChanges", ["sections in a meter of their own"] = "sectionChanges",
        ["sections the solo part answers"] = "soloAnswers", ["lines in octaves"] = "doubling", ["sections harmonising the melody"] = "doubling",
        ["songs repeating the last section"] = "lastRepeat", ["parts' sections switched"] = "instrumentSwitches", ["songs with a strong effect"] = "strongEffect",
        ["held piano chords rolled"] = "articulation", ["reverb sent"] = "space"
    };

    /// <summary>The measures that are numbers rather than shares.</summary>
    private static readonly HashSet<string> Numbers = ["reverb sent", "unconventionality"];

    [Test]
    [Explicit]
    public async Task Report()
    {
        var columns = new List<(string Name, Dictionary<string, double>[] Songs)>
        {
            ("none", TestCorpus.InParallel(Enumerable.Range(0, Count), seed => Measure(TestCorpus.Get(seed))))
        };
        columns.AddRange(GenrePresets.All.Select(preset =>
            (preset.Name + "|" + preset.Id, TestCorpus.InParallel(Enumerable.Range(0, Count), seed => Measure(TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(preset))))))));

        bool Inherits(string column, string measure) =>
            column.Split('|') is [_, var id] && ChoiceOf.TryGetValue(measure, out var choice) && !GenrePresets.All.Single(x => x.Id == id).Choices.ContainsKey(choice);
        Console.WriteLine($"{"",-36}" + string.Join("", columns.Select(x => $"{x.Name.Split('|')[0],11}")));
        foreach (var measure in columns[0].Songs[0].Keys)
            Console.WriteLine($"{measure,-36}" + string.Join("", columns.Select(c =>
                (c.Songs.Select(x => x[measure]).Where(double.IsFinite).DefaultIfEmpty(double.NaN).Average() is var value && Numbers.Contains(measure)
                    ? $"{value:0.00}"
                    : $"{value:P0}") + (Inherits(c.Name, measure) ? "*" : " ") is var cell ? $"{cell,11}" : "")));

        await Task.CompletedTask;
    }
}
