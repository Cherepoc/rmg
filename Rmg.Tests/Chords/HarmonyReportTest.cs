using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;

namespace Rmg.Tests.Chords;

/// <summary>
///     How conventional the harmony plays, by how conventional the song's or the section's harmony is, in fifths of its
///     range: the levels of the chords the chords track plays, a chord of a section's pattern once, apart for the home
///     chord, the cadence and the chords between; the songs' scales, how often a section plays another, and how often
///     songs change key and sections play a pentatonic melody.
/// </summary>
public sealed class HarmonyReportTest
{
    private const int SongCount = 256;

    private sealed record SectionChords(int Band, ImmutableArray<(string Place, int Level, bool? IsClose)> Chords);

    private sealed record SongHarmony(int Band, string Scale, int Sections, int OtherScales, int Pentatonic, int KeyChanges, ImmutableArray<SectionChords> SectionChords,
        ImmutableArray<(int Band, ImmutableArray<int> Roots)> Progressions, ImmutableArray<(int Band, bool IsReset)> ChordBars,
        ImmutableArray<(int Band, bool IsRaised)> Raises);

    // a place on the unconventionality's scale, in fifths: a section's chords facet for its chords, and the song's scale
    // facet for its scales, its key change and its pentatonic melodies
    private static int ChordsBand(HarmonicUnconventionality harmony) => Math.Clamp((int)(harmony.Chords * 5), 0, 4);

    private static int Band(double facet) => Math.Clamp((int)(facet * 5), 0, 4);

    [Test]
    [Explicit]
    public async Task Report()
    {
        var songs = TestCorpus.Measure(SongCount, Measure);

        Console.WriteLine("chords' levels 0-5 by the section's chords facet, in fifths:");
        foreach (var place in new[] { "home", "between", "cadence" })
        foreach (var band in songs.SelectMany(x => x.SectionChords).GroupBy(x => x.Band).OrderBy(x => x.Key))
        {
            var levels = band.SelectMany(x => x.Chords).Where(x => x.Place == place).Select(x => x.Level).ToArray();
            Console.WriteLine($"  {place,-8} {band.Key}/5, {levels.Length,5} chords: {string.Join(" ", Enumerable.Range(0, ChordShapes.MaxUnconventionality + 1).Select(l => $"{l}:{levels.Count(x => x == l) / (double)Math.Max(1, levels.Length):P0}"))}");
        }

        Console.WriteLine("chords' voicings and register by the section's chords facet, in fifths:");
        foreach (var band in songs.SelectMany(x => x.SectionChords).GroupBy(x => x.Band).OrderBy(x => x.Key))
        {
            var voiced = band.SelectMany(x => x.Chords).Where(x => x.IsClose is not null).ToArray();
            var bars = songs.SelectMany(x => x.ChordBars).Where(x => x.Band == band.Key).ToArray();
            Console.WriteLine($"  {band.Key}/5, {voiced.Length,5} chords: close {voiced.Count(x => x.IsClose == true) / (double)Math.Max(1, voiced.Length):P0}; " +
                              $"{bars.Length} bars, afresh {bars.Count(x => x.IsReset) / (double)Math.Max(1, bars.Length):P1}");
        }

        Console.WriteLine("progressions' roots, in steps above the home, by the section's progression facet, in fifths:");
        foreach (var band in songs.SelectMany(x => x.Progressions).GroupBy(x => x.Band).OrderBy(x => x.Key))
        {
            var roots = band.SelectMany(x => x.Roots.Skip(1).SkipLast(1)).ToArray();
            Console.WriteLine($"  {band.Key}/5, {band.Count()} sections, {roots.Length} chords between home and cadence: " +
                              $"{string.Join(" ", roots.GroupBy(x => x).OrderBy(x => x.Key).Select(x => $"{x.Key}:{x.Count() / (double)roots.Length:P0}"))}");
        }

        Console.WriteLine("cadences raising the seventh, where the scale allows it, by the section's progression facet, in fifths:");
        foreach (var band in songs.SelectMany(x => x.Raises).GroupBy(x => x.Band).OrderBy(x => x.Key))
            Console.WriteLine($"  {band.Key}/5, {band.Count()} raisable cadences: raised {band.Count(x => x.IsRaised) / (double)band.Count():P0}");

        Console.WriteLine("by the song's scale facet, in fifths:");
        foreach (var band in songs.GroupBy(x => x.Band).OrderBy(x => x.Key))
        {
            var of = band.ToArray();
            var scales = string.Join(", ", of.GroupBy(x => x.Scale).OrderByDescending(x => x.Count()).Select(x => $"{x.Key} {x.Count()}"));
            Console.WriteLine($"  {band.Key}/5, {of.Length} songs: sections in another scale {of.Sum(x => x.OtherScales) / (double)of.Sum(x => x.Sections):P0}, " +
                              $"pentatonic {of.Sum(x => x.Pentatonic) / (double)of.Sum(x => x.Sections):P0}, key change {of.Count(x => x.KeyChanges > 0) / (double)of.Length:P0}, {of.Sum(x => x.KeyChanges) / (double)of.Length:F2} changes a song; scales {scales}");
        }

        await Task.CompletedTask;
    }

    private static SongHarmony Measure(CorpusSong song)
    {
        var (harmony, scale) = ((HarmonicUnconventionality, Scale))song.Trace.Single(x => x.Point == TracePoints.SongHarmony).Value!;
        var sectionHarmony = song.Trace.Where(x => x.Point == TracePoints.SectionHarmony).ToDictionary(x => x.Section, x => (HarmonicUnconventionality)x.Value!);
        var rhythms = song.Trace.Where(x => x.Point == TracePoints.HarmonicRhythm).ToDictionary(x => x.Section, x => (HarmonicRhythm)x.Value!);
        var scales = song.Trace.Where(x => x.Point == TracePoints.SectionScale).Select(x => (Scale)x.Value!).ToArray();
        var pentatonic = song.Trace.Count(x => x.Point == TracePoints.LineScale && x.Track == SongTracks.MelodyTrack && (LineScale)x.Value! == LineScale.Pentatonic);
        var keyChanges = ((ImmutableArray<KeyChange>)song.Trace.Single(x => x.Point == TracePoints.KeyChange).Value!).Length;

        // a chord of every section's pattern once, as the chords track plays it: the role chord where the bar has one,
        // the pool's pick otherwise
        var chords = song.Trace
            .Where(x => x.Point == TracePoints.Chord && x.Track == SongTracks.ChordsTrack)
            .GroupBy(x => x.Section)
            .Select(section =>
            {
                var rhythm = rhythms[section.Key];
                var played = section
                    .GroupBy(x => rhythm.IndexAt(x.Bar % Meter.PatternBarCount * rhythm.Meter.BarDuration + x.Position))
                    .Select(x =>
                    {
                        var entry = x.First();
                        var role = entry.StateMap.GetStateValue(CompositionStateKinds.RoleChord);
                        var chord = role.IsEmpty ? CompositionStateKinds.ChordPool.Pick(entry.StateMap) : role[0];
                        var place = x.Key == 0 ? "home" : x.Key == rhythm.Count - 1 ? "cadence" : "between";
                        // laid out as its shape is, rather than inverted or spread by octaves, for a shape a voicing may lay out
                        var isClose = chord.IsVoicingFixed ? (bool?)null : chord.Heights.SequenceEqual(chord.Shape.Targets.Order().Select(h => h / 12));
                        return (place, chord.Shape.Unconventionality, isClose);
                    });
                return new SectionChords(ChordsBand(sectionHarmony[section.Key]), [..played]);
            });

        var facets = song.Trace.Where(x => x.Point == TracePoints.SectionUnconventionality).ToDictionary(x => x.Section, x => (Unconventionality)x.Value!);
        var progressions = song.Trace.Where(x => x.Point == TracePoints.Progression)
            .Select(x => (Math.Clamp((int)(facets[x.Section][Facet.Progression] * 5), 0, 4), (ImmutableArray<int>)x.Value!));

        var songFacets = (Unconventionality)song.Trace.Single(x => x.Point == TracePoints.SongUnconventionality).Value!;
        // the chords' bars that start afresh in a register of their own, rather than led from the chord before
        var bars = song.Song.Notes![SongTracks.ChordsTrack]
            .Where(x => song.Map.SectionAt(x.Position) is not null)
            .GroupBy(x => (int)Math.Floor(x.Position / song.Map.Meter.BarDuration + 1e-9))
            .Select(x => (Band: ChordsBand(sectionHarmony[song.Map.SectionAt(x.First().Position)!.SectionId]), IsReset: x.First().Value.State.GetStateValue(StateKinds.ChordVoicingReset) > 0));

        var raises = song.Trace.Where(x => x.Point == TracePoints.CadenceRaise)
            .Select(x => (Section: x.Section, Value: ((int? Raisable, bool IsRaised))x.Value!))
            .Where(x => x.Value.Raisable is not null)
            .Select(x => (Band(facets[x.Section][Facet.Progression]), x.Value.IsRaised));

        return new SongHarmony(Band(songFacets[Facet.Scale]), scale.Name, scales.Length, scales.Count(x => x != scale), pentatonic, keyChanges, [..chords], [..progressions], [..bars], [..raises]);
    }
}
