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

    private sealed record SectionChords(int Band, ImmutableArray<(string Place, int Level)> Chords);

    private sealed record SongHarmony(int Band, string Scale, int Sections, int OtherScales, int Pentatonic, bool KeyChange, ImmutableArray<SectionChords> SectionChords);

    // a harmony's place on the unconventionality's scale, in fifths: its chords facet for its chords, and its anchor over
    // the chords' levels for the scales, the key change and the pentatonic melodies, which it leans until they go by
    // facets of their own
    private static int ChordsBand(HarmonicUnconventionality harmony) => Math.Clamp((int)(harmony.Chords * 5), 0, 4);

    private static int AnchorBand(HarmonicUnconventionality harmony) => Math.Clamp((int)(harmony.Anchor / ChordShapes.MaxUnconventionality * 5), 0, 4);

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

        Console.WriteLine("by the song's harmonic anchor, in fifths:");
        foreach (var band in songs.GroupBy(x => x.Band).OrderBy(x => x.Key))
        {
            var of = band.ToArray();
            var scales = string.Join(", ", of.GroupBy(x => x.Scale).OrderByDescending(x => x.Count()).Select(x => $"{x.Key} {x.Count()}"));
            Console.WriteLine($"  {band.Key}/5, {of.Length} songs: sections in another scale {of.Sum(x => x.OtherScales) / (double)of.Sum(x => x.Sections):P0}, " +
                              $"pentatonic {of.Sum(x => x.Pentatonic) / (double)of.Sum(x => x.Sections):P0}, key change {of.Count(x => x.KeyChange) / (double)of.Length:P0}; scales {scales}");
        }

        await Task.CompletedTask;
    }

    private static SongHarmony Measure(CorpusSong song)
    {
        var (harmony, scale) = ((HarmonicUnconventionality, Scale))song.Trace.Single(x => x.Point == TracePoints.SongHarmony).Value!;
        var sectionHarmony = song.Trace.Where(x => x.Point == TracePoints.SectionHarmony).ToDictionary(x => x.Section, x => (HarmonicUnconventionality)x.Value!);
        var rhythms = song.Trace.Where(x => x.Point == TracePoints.HarmonicRhythm).ToDictionary(x => x.Section, x => (HarmonicRhythm)x.Value!);
        var scales = song.Trace.Where(x => x.Point == TracePoints.SectionScale).Select(x => (Scale)x.Value!).ToArray();
        var pentatonic = song.Trace.Count(x => x.Point == TracePoints.Pentatonic && (bool)x.Value!);
        var keyChange = song.Trace.Single(x => x.Point == TracePoints.KeyChange).Value is not null;

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
                        return (place, chord.Shape.Unconventionality);
                    });
                return new SectionChords(ChordsBand(sectionHarmony[section.Key]), [..played]);
            });

        return new SongHarmony(AnchorBand(harmony), scale.Name, scales.Length, scales.Count(x => x != scale), pentatonic, keyChange, [..chords]);
    }
}
