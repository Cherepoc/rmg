using Rmg.Core.Composition;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Tests.Genres;

/// <summary>
///     How far every preset's songs are from its idioms, as its description says them: a measure each, the same for every
///     preset, and the targets a preset's idioms set, so that a target no setting reaches names the mechanism it lacks.
///     The targets are written from the descriptions, a guess each, and listening the judge.
/// </summary>
public sealed class GenreIdiomsReportTest
{
    private const int Count = 32;

    /// <summary>A song's measures, each by its name.</summary>
    internal static Dictionary<string, double> Measure(CorpusSong song)
    {
        var map = song.Map;
        var notes = song.Song.Notes!;
        var minutes = song.Rendered.Seconds / 60;
        // the song's sections in common time, whose beats the drums' measures count
        var common = map.Sections.Where(x => x.Meter.Equals(Meter.FourFour) || x.Meter.HasSameBar(Meter.FourFour)).ToArray();
        int[] Tracks(DrumGroup group) => [..group.Drums.Select(DrumGroups.GetTrackNumber).Where(notes.ContainsKey)];
        bool HitsAt(int[] tracks, double position) => tracks.Any(t => notes[t].Any(x => Math.Abs(x.Position - position) < 0.05));
        // its groove's bars: where the drums play, but a phrase's last, where a fill does
        var drums = song.Song.TrackDefinitions.Where(x => x.Value is PercussionInstrumentTrack && notes.ContainsKey(x.Key)).Select(x => x.Key).ToArray();
        bool DrumsPlay(double start) => drums.Any(t => notes[t].Any(x => x.Position >= start - 1e-9 && x.Position < start + 4 - 1e-9));
        var bars = common.SelectMany(x => Enumerable.Range(0, (int)Math.Round(x.Duration / x.Meter.BarDuration))
                .Where(b => b % Meter.PhraseBarCount != Meter.PhraseBarCount - 1)
                .Select(b => x.Start + b * x.Meter.BarDuration))
            .Where(DrumsPlay)
            .ToArray();
        var (kicks, snares) = (Tracks(DrumGroups.Kick), Tracks(DrumGroups.Snare).Concat(DrumGroups.AllDrums.Where(x => x.Name == "Clap").Select(DrumGroups.GetTrackNumber).Where(notes.ContainsKey)).ToArray());

        var progressions = song.Trace.Where(x => x.Point == TracePoints.Progression).GroupBy(x => x.Section).Select(x => string.Join(" ", (System.Collections.Immutable.ImmutableArray<int>)x.First().Value!)).ToArray();
        var changes = map.Sections.Sum(x => song.ChordChanges.Count(c => c >= x.Start - 1e-9 && c < x.End - 1e-9));
        var phrases = map.Sections.Sum(x => x.Duration / (Meter.PhraseBarCount * x.Meter.BarDuration));

        var melody = notes.GetValueOrDefault(SongTracks.MelodyTrack)?.ToArray() ?? [];
        var drumVelocities = song.Song.TrackDefinitions.Where(x => x.Value is PercussionInstrumentTrack && notes.ContainsKey(x.Key)).SelectMany(x => notes[x.Key]).Select(x => 127 * Render.GetMidiVelocity(x.Value)).ToArray();
        var melodyVelocities = melody.Select(x => 127 * Render.GetMidiVelocity(x.Value)).ToArray();
        var moves = melody.Zip(melody.Skip(1)).Select(x => Math.Abs(x.Second.Value.Pitches[0] - x.First.Value.Pitches[0])).ToArray();
        // a bar of the melody as the one before it, its notes where they fall in it and their pitches
        string BarOf(double start, double length) =>
            string.Join(" ", melody.Where(x => x.Position >= start - 1e-9 && x.Position < start + length - 1e-9).Select(x => $"{Math.Round(x.Position - start, 3)}:{x.Value.Pitches[0]}"));
        var melodyBars = map.Sections.SelectMany(x => Enumerable.Range(1, (int)Math.Round(x.Duration / x.Meter.BarDuration) - 1).Select(b => (Start: x.Start + b * x.Meter.BarDuration, x.Meter.BarDuration))).ToArray();
        var played = melodyBars.Where(x => BarOf(x.Start, x.BarDuration).Length > 0).ToArray();
        var lineScales = song.Trace.Where(x => x.Point == TracePoints.LineScale && x.Track == SongTracks.MelodyTrack).Select(x => (LineScale)x.Value!).ToArray();
        // the bass where a chord changes: its note there on the chord's root, and the one before a step or a half step from it
        var bass = notes.GetValueOrDefault(SongTracks.BassTrack)?.ToArray() ?? [];
        var stateMaps = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap;
        var arrivals = song.ChordChanges.Select(c => (Change: c, Index: Array.FindIndex(bass, x => Math.Abs(x.Position - c) < 1e-6))).Where(x => x.Index > 0).ToArray();
        bool OnRoot(int index, double change) => (bass[index].Value.Pitches.Min() - Realizer.GetChord(stateMaps.GetEffectiveStateMapAt(change)).Chord.GetPitch(0)) % 12 == 0;

        // the sections' phrases of two different bars at most, a chorus's and a verse's
        var structure = (SongStructure)song.Trace.Single(x => x.Point == TracePoints.SongForm).Value!;
        double Repeating(SectionRole role)
        {
            var schemes = song.Trace.Where(x => x.Point == TracePoints.PhraseScheme && structure.Roles.GetValueOrDefault(x.Section) == role).Select(x => (PhraseScheme)x.Value!).ToArray();
            return schemes.Length > 0 ? schemes.Count(x => x.PatternCount <= 2) / (double)schemes.Length : double.NaN;
        }

        double RepeatedIn(SectionRole role)
        {
            var bars = played.Where(x => map.SectionAt(x.Start) is { } span && structure.Roles.GetValueOrDefault(span.SectionId) == role).ToArray();
            return bars.Length > 0 ? bars.Count(x => BarOf(x.Start, x.BarDuration) == BarOf(x.Start - x.BarDuration, x.BarDuration)) / (double)bars.Length : double.NaN;
        }

        return new Dictionary<string, double>
        {
            ["chorus phrases of two bars at most"] = Repeating(SectionRole.Chorus),
            ["verse phrases of two bars at most"] = Repeating(SectionRole.Verse),
            ["chords in 4 bars"] = phrases > 0 ? changes / phrases : double.NaN,
            ["sections sharing a progression"] = progressions.Length > 1 ? progressions.Skip(1).Count(x => x == progressions[0]) / (double)(progressions.Length - 1) : double.NaN,
            ["fills a minute"] = song.Trace.Count(x => x.Point == TracePoints.FillDecision && ((FillDecision)x.Value!).Span > 0) / minutes,
            ["drums over the melody"] = drumVelocities.Length > 0 && melodyVelocities.Length > 0 ? drumVelocities.Average() - melodyVelocities.Average() : double.NaN,
            ["kick on 1"] = bars.Length > 0 ? bars.Count(x => HitsAt(kicks, x)) / (double)bars.Length : double.NaN,
            ["kick on every beat"] = bars.Length > 0 ? bars.Count(x => Enumerable.Range(0, 4).All(b => HitsAt(kicks, x + b))) / (double)bars.Length : double.NaN,
            ["snare on 2 and 4"] = bars.Length > 0 ? bars.Count(x => HitsAt(snares, x + 1) && HitsAt(snares, x + 3)) / (double)bars.Length : double.NaN,
            ["chorus melody bars repeated"] = RepeatedIn(SectionRole.Chorus),
            ["verse melody bars repeated"] = RepeatedIn(SectionRole.Verse),
            ["melody bars repeated"] = played.Length > 0 ? played.Count(x => BarOf(x.Start, x.BarDuration) == BarOf(x.Start - x.BarDuration, x.BarDuration)) / (double)played.Length : double.NaN,
            ["melody pentatonic or blues"] = lineScales.Length > 0 ? lineScales.Count(x => x is LineScale.Pentatonic or LineScale.Blues) / (double)lineScales.Length : double.NaN,
            ["melody mean move"] = moves.Length > 0 ? moves.Average() : double.NaN,
            ["melody leaps past a fifth"] = moves.Length > 0 ? moves.Count(x => x > 7) / (double)moves.Length : double.NaN,
            ["bass leads into a change"] = arrivals.Length > 0 ? arrivals.Count(x => Math.Abs(bass[x.Index].Value.Pitches.Min() - bass[x.Index - 1].Value.Pitches.Min()) is 1 or 2) / (double)arrivals.Length : double.NaN,
            ["bass a half step into a change"] = arrivals.Length > 0 ? arrivals.Count(x => Math.Abs(bass[x.Index].Value.Pitches.Min() - bass[x.Index - 1].Value.Pitches.Min()) == 1) / (double)arrivals.Length : double.NaN,
            ["bass on the root at a change"] = arrivals.Length > 0 ? arrivals.Count(x => OnRoot(x.Index, x.Change)) / (double)arrivals.Length : double.NaN,
            ["riff sections in power chords"] = song.Trace.Where(x => x.Point == TracePoints.RiffPowerChords).Select(x => (bool)x.Value!).DefaultIfEmpty().Average(x => x ? 1.0 : 0),
            ["guitars' notes palm-muted"] = notes.Where(x => x.Key is SongTracks.RiffTrack or SongTracks.RhythmTrack).SelectMany(x => x.Value).Select(x => x.Value.State.GetStateValue(CompositionStateKinds.PalmMute) > 0 ? 1.0 : 0).DefaultIfEmpty(double.NaN).Average(),
            ["solo appearances"] = song.Solos.Count / (double)map.Sections.Length,
            ["ending a fade"] = map.Ending.Kind == EndingKind.Fade ? 1 : 0,
            ["ending a button or a stop"] = map.Ending.Kind is EndingKind.Button or EndingKind.Stop ? 1 : 0,
            ["intro part by part"] = map.Intro.Kind == IntroKind.Entries ? 1 : 0,
            ["tempo"] = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetEffectiveStateMapAt(map.Origin).GetStateValue(Rmg.Core.Events.StateKinds.Tempo) * Meter.BaseTempo
        };
    }

    /// <summary>Every preset's idioms as targets, the least and the most of a measure, written from its description.</summary>
    internal static readonly Dictionary<string, (string Measure, double Least, double Most)[]> Targets = new()
    {
        ["hipHop"] =
        [
            ("chords in 4 bars", 0, 2), ("sections sharing a progression", 0.8, 1), ("fills a minute", 0, 2), ("drums over the melody", 10, 127),
            ("kick on 1", 0.9, 1), ("snare on 2 and 4", 0.9, 1), ("melody bars repeated", 0.5, 1), ("tempo", 70, 100)
        ],
        ["electronic"] =
        [
            ("chords in 4 bars", 0, 2), ("sections sharing a progression", 0.6, 1), ("fills a minute", 0, 2), ("drums over the melody", 5, 127),
            ("kick on every beat", 0.85, 1), ("melody bars repeated", 0.5, 1), ("tempo", 117, 135)
        ],
        ["rock"] = [("kick on 1", 0.85, 1), ("snare on 2 and 4", 0.85, 1), ("chords in 4 bars", 2, 4), ("melody pentatonic or blues", 0.5, 1)],
        ["blues"] = [("melody pentatonic or blues", 0.6, 1), ("snare on 2 and 4", 0.8, 1)],
        ["jazz"] = [("chords in 4 bars", 4, 8), ("melody leaps past a fifth", 0.06, 0.2)],
        ["folk"] = [("melody mean move", 0, 2.5), ("fills a minute", 0, 4)]
    };

    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var preset in GenrePresets.All)
        {
            var measured = TestCorpus.InParallel(Enumerable.Range(0, Count), seed => Measure(TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(preset)))));
            double Mean(string measure) => measured.Select(x => x[measure]).Where(double.IsFinite).DefaultIfEmpty(double.NaN).Average();
            var targets = Targets.GetValueOrDefault(preset.Id, []);
            Console.WriteLine($"{preset.Name}: " + string.Join(", ", measured[0].Keys.Select(m =>
            {
                var value = Mean(m);
                var target = targets.FirstOrDefault(t => t.Measure == m);
                var mark = target.Measure is null ? "" : value >= target.Least && value <= target.Most ? " ✓" : $" ✗ ({target.Least}–{target.Most})";
                return $"{m} {value:0.##}{mark}";
            })));
        }

        await Task.CompletedTask;
    }
}
