using System.Collections.Immutable;
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
        // the drums keeping time, as their song gives them the role
        var times = song.Song.TrackDefinitions.Where(x => x.Value is PercussionInstrumentTrack d && DrumRoles.Of(d.StateMap) == DrumRole.Time && notes.ContainsKey(x.Key)).Select(x => x.Key).ToArray();

        // the time drums' hits in a bar, where they fall in it, which a steady time plays alike bar after bar
        string TimeOf(double start) => string.Join(" ", times.SelectMany(t => notes[t].Where(x => x.Position >= start - 1e-9 && x.Position < start + 4 - 1e-9))
            .Select(x => Math.Round((x.Position - start) * 4) / 4).Distinct().Order());
        var timeBars = bars.Where(x => bars.Contains(x - 4) && TimeOf(x).Length > 0).ToArray();

        var progressions = song.Trace.Where(x => x.Point == TracePoints.Progression).GroupBy(x => x.Section).Select(x => string.Join(" ", (System.Collections.Immutable.ImmutableArray<int>)x.First().Value!)).ToArray();
        // every move of a section's chord roots, in scale steps up, 0 to 6, as the root motions name them (Progressions.Motions)
        var motions = song.Trace.Where(x => x.Point == TracePoints.Progression).GroupBy(x => x.Section).Select(x => (System.Collections.Immutable.ImmutableArray<int>)x.First().Value!)
            .SelectMany(x => x.Zip(x.Skip(1), (a, b) => ((b - a) % 7 + 7) % 7)).ToArray();
        double Motion(int steps) => motions.Length > 0 ? motions.Count(x => x == steps) / (double)motions.Length : double.NaN;
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

        // the melody's span in each section's appearance, its tenth highest note over its tenth lowest, on average
        double MelodyRange()
        {
            if (!notes.TryGetValue(SongTracks.MelodyTrack, out var melodyNotes))
                return double.NaN;
            var spans = map.Sections.Select(span => melodyNotes.Where(x => x.Position >= span.Start && x.Position < span.End).Select(x => x.Value.Pitches[0]).Order().ToArray())
                .Where(x => x.Length >= 10).Select(x => x[^(x.Length / 10 + 1)] - x[x.Length / 10]).ToArray();
            return spans.Length > 0 ? spans.Average() : double.NaN;
        }

        // the share of the appearances a part the song plays rests in
        double Rests(TrackRole part) => song.Song.TrackDefinitions.Values.Any(x => x.Role == part)
            ? song.Trace.Where(x => x.Point == TracePoints.Arrangement).Select(x => ((ImmutableHashSet<TrackRole>)x.Value!).Contains(part) ? 1.0 : 0).DefaultIfEmpty(double.NaN).Average()
            : double.NaN;
        return new Dictionary<string, double>
        {
            ["chorus phrases of two bars at most"] = Repeating(SectionRole.Chorus),
            ["verse phrases of two bars at most"] = Repeating(SectionRole.Verse),
            ["chords in 4 bars"] = phrases > 0 ? changes / phrases : double.NaN,
            ["roots down a fifth (ii-V, V-I)"] = Motion(3),
            ["roots down a fourth (I-V, IV-I)"] = Motion(4),
            ["roots down a third (vi-IV, i-bVI)"] = Motion(5),
            ["roots up a step (V-vi, bVI-bVII)"] = Motion(1),
            ["roots down a step (bVII-bVI)"] = Motion(6),
            ["sections on one chord"] = song.Trace.Where(x => x.Point == TracePoints.Progression).GroupBy(x => x.Section)
                .Select(x => ((System.Collections.Immutable.ImmutableArray<int>)x.First().Value!).Distinct().Count() == 1 ? 1.0 : 0).DefaultIfEmpty(double.NaN).Average(),
            ["sections with a passing chord"] = song.Trace.Where(x => x.Point == TracePoints.HarmonicRhythm).Select(x => ((HarmonicRhythm)x.Value!).Passing.Length > 0 ? 1.0 : 0).DefaultIfEmpty(double.NaN).Average(),
            ["sections sharing a progression"] = progressions.Length > 1 ? progressions.Skip(1).Count(x => x == progressions[0]) / (double)(progressions.Length - 1) : double.NaN,
            ["fills a minute"] = song.Trace.Count(x => x.Point == TracePoints.FillDecision && ((FillDecision)x.Value!).Span > 0) / minutes,
            ["drums over the melody"] = drumVelocities.Length > 0 && melodyVelocities.Length > 0 ? drumVelocities.Average() - melodyVelocities.Average() : double.NaN,
            ["kick on 1"] = bars.Length > 0 ? bars.Count(x => HitsAt(kicks, x)) / (double)bars.Length : double.NaN,
            ["kick hits a bar"] = bars.Length > 0 ? bars.Average(x => kicks.Sum(t => notes[t].Count(n => n.Position >= x - 1e-9 && n.Position < x + 4 - 1e-9))) : double.NaN,
            ["kick on every beat"] = bars.Length > 0 ? bars.Count(x => Enumerable.Range(0, 4).All(b => HitsAt(kicks, x + b))) / (double)bars.Length : double.NaN,
            ["one drop (kick and snare on 3 alone)"] = bars.Length > 0 ? bars.Count(x => HitsAt(kicks, x + 2) && HitsAt(snares, x + 2) && !HitsAt(kicks, x) && !HitsAt(snares, x + 1) && !HitsAt(snares, x + 3)) / (double)bars.Length : double.NaN,
            ["chords on the offbeat 8ths"] = notes.GetValueOrDefault(SongTracks.ChordsTrack) is { Count: > 0 } chordNotes
                ? chordNotes.Average(x => Math.Abs(x.Position % 1 - 0.5) < 0.05 ? 1.0 : 0)
                : double.NaN,
            ["time in the tresillo (1 and-of-2 4 alone)"] = bars.Length > 0 ? bars.Count(x => HitsAt(times, x) && HitsAt(times, x + 1.5) && HitsAt(times, x + 3) && !HitsAt(times, x + 1) && !HitsAt(times, x + 2)) / (double)bars.Length : double.NaN,
            ["chords stabbed (a 16th or shorter)"] = notes.GetValueOrDefault(SongTracks.ChordsTrack) is { Count: > 0 } chordStabs
                ? chordStabs.Average(x => x.Value.Duration <= 0.25 + 1e-9 ? 1.0 : 0)
                : double.NaN,
            ["tom hits a minute"] = Tracks(DrumGroups.Toms).Sum(t => notes[t].Count) / minutes,
            ["time as the bar before"] = timeBars.Length > 0 ? timeBars.Count(x => TimeOf(x) == TimeOf(x - 4)) / (double)timeBars.Length : double.NaN,
            ["hi-hat foot on 2 and 4"] = bars.Length > 0 ? bars.Count(x => new[] { 1, 3 }.All(b => notes.Values.Any(n => n.Any(y => Math.Abs(y.Position - (x + b)) < 0.05 && y.Value.Pitches.Contains(44))))) / (double)bars.Length : double.NaN,
            ["snares cross-stick"] = snares.SelectMany(t => notes[t]).ToArray() is { Length: > 0 } struck ? struck.Count(x => x.Value.Pitches.Contains(37)) / (double)struck.Length : double.NaN,
            ["snare on 2 and 4"] = bars.Length > 0 ? bars.Count(x => HitsAt(snares, x + 1) && HitsAt(snares, x + 3)) / (double)bars.Length : double.NaN,
            ["chorus melody bars repeated"] = RepeatedIn(SectionRole.Chorus),
            ["verse melody bars repeated"] = RepeatedIn(SectionRole.Verse),
            ["melody bars repeated"] = played.Length > 0 ? played.Count(x => BarOf(x.Start, x.BarDuration) == BarOf(x.Start - x.BarDuration, x.BarDuration)) / (double)played.Length : double.NaN,
            ["melody pentatonic or blues"] = lineScales.Length > 0 ? lineScales.Count(x => x is LineScale.Pentatonic or LineScale.Blues) / (double)lineScales.Length : double.NaN,
            ["melody's range a section"] = MelodyRange(),
            ["melody mean move"] = moves.Length > 0 ? moves.Average() : double.NaN,
            ["melody leaps past a fifth"] = moves.Length > 0 ? moves.Count(x => x > 7) / (double)moves.Length : double.NaN,
            ["bass leads into a change"] = arrivals.Length > 0 ? arrivals.Count(x => Math.Abs(bass[x.Index].Value.Pitches.Min() - bass[x.Index - 1].Value.Pitches.Min()) is 1 or 2) / (double)arrivals.Length : double.NaN,
            ["bass a half step into a change"] = arrivals.Length > 0 ? arrivals.Count(x => Math.Abs(bass[x.Index].Value.Pitches.Min() - bass[x.Index - 1].Value.Pitches.Min()) == 1) / (double)arrivals.Length : double.NaN,
            ["bass on the root at a change"] = arrivals.Length > 0 ? arrivals.Count(x => OnRoot(x.Index, x.Change)) / (double)arrivals.Length : double.NaN,
            ["riff sections in power chords"] = song.Trace.Where(x => x.Point == TracePoints.RiffPowerChords).Select(x => (bool)x.Value!).DefaultIfEmpty().Average(x => x ? 1.0 : 0),
            ["guitars' notes palm-muted"] = notes.Where(x => x.Key is SongTracks.RiffTrack or SongTracks.RhythmTrack).SelectMany(x => x.Value).Select(x => x.Value.State.GetStateValue(CompositionStateKinds.PalmMute) > 0 ? 1.0 : 0).DefaultIfEmpty(double.NaN).Average(),
            ["solo appearances"] = song.Solos.Count / (double)map.Sections.Length,
            ["sections in stop time"] = song.Trace.Where(x => x.Point == TracePoints.StopTime).Select(x => (bool)x.Value! ? 1.0 : 0).DefaultIfEmpty(double.NaN).Average(),
            ["sections the drums rest"] = Rests(TrackRole.Drum),
            ["sections the pad rests"] = Rests(TrackRole.Pad),
            ["changing key"] = ((ImmutableArray<KeyChange>)song.Trace.Single(x => x.Point == TracePoints.KeyChange).Value!).IsEmpty ? 0 : 1,
            ["bass notes an octave from the one before"] = bass.Length > 1 ? bass.Skip(1).Select((x, i) => Math.Abs(x.Value.Pitches.Min() - bass[i].Value.Pitches.Min()) == 12 ? 1.0 : 0).Average() : double.NaN,
            ["sections pushing"] = song.Trace.Where(x => x.Point == TracePoints.Push).Select(x => (bool)x.Value! ? 1.0 : 0).DefaultIfEmpty(double.NaN).Average(),
            ["lifts dropped"] = song.Trace.Count(x => x.Point == TracePoints.LiftCue) is var lifts and > 0 ? song.Trace.Count(x => x.Point == TracePoints.Drop) / (double)lifts : double.NaN,
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
            ("kick on 1", 0.9, 1), ("snare on 2 and 4", 0.9, 1), ("melody bars repeated", 0.2, 1), ("tempo", 70, 100)
        ],
        ["electronic"] =
        [
            ("chords in 4 bars", 0, 2), ("sections sharing a progression", 0.6, 1), ("fills a minute", 0, 2), ("drums over the melody", 5, 127),
            ("kick on every beat", 0.85, 1), ("melody bars repeated", 0.2, 1), ("lifts dropped", 0.5, 1), ("tempo", 117, 135)
        ],
        ["techno"] = [("chords in 4 bars", 0, 2), ("kick on every beat", 0.85, 1), ("drums over the melody", 10, 127), ("tempo", 125, 140)],
        ["synthwave"] = [("snare on 2 and 4", 0.8, 1), ("kick on every beat", 0, 0.3), ("tempo", 85, 118), ("ending a fade", 0.5, 1)],
        ["rock"] = [("kick on 1", 0.85, 1), ("snare on 2 and 4", 0.85, 1), ("chords in 4 bars", 2, 4), ("melody pentatonic or blues", 0.5, 1)],
        ["latin"] = [("time in the tresillo (1 and-of-2 4 alone)", 0.5, 1), ("sections sharing a progression", 0.3, 1), ("melody bars repeated", 0.08, 1), ("solo appearances", 0.08, 0.3),
            ("ending a button or a stop", 0.5, 1)],
        ["reggae"] = [("one drop (kick and snare on 3 alone)", 0.6, 1), ("chords on the offbeat 8ths", 0.7, 1), ("tempo", 60, 90), ("sections sharing a progression", 0.4, 1),
            ("chords stabbed (a 16th or shorter)", 0.3, 1), ("ending a fade", 0.4, 1)],
        ["blues"] = [("melody pentatonic or blues", 0.6, 1), ("snare on 2 and 4", 0.8, 1)],
        ["jazz"] = [("chords in 4 bars", 4, 8), ("melody leaps past a fifth", 0.06, 0.2), ("time as the bar before", 0.8, 1)],
        ["folk"] = [("melody mean move", 0, 2.5), ("fills a minute", 0, 4)],
        ["pop"] = [("snare on 2 and 4", 0.75, 1), ("chords in 4 bars", 2, 5), ("changing key", 0.2, 0.6), ("tempo", 95, 125)],
        ["metal"] = [("kick hits a bar", 4, 16), ("riff sections in power chords", 0.7, 1), ("guitars' notes palm-muted", 0.15, 1), ("tempo", 120, 180)],
        ["funk"] = [("sections sharing a progression", 0.4, 1), ("melody bars repeated", 0.1, 1), ("chords stabbed (a 16th or shorter)", 0.15, 1), ("lifts dropped", 0, 0)]
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
