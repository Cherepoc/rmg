using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Tests.RhythmicUnconventionalities;

/// <summary>
///     How plain the songs come out at a base supplied as 0.25 and as 0.5, and as drawn: the share of songs that keep to
///     each convention, and the facets' spread, so that a centre for the drawn base can be chosen by what it does.
/// </summary>
public sealed class ConventionalityLevelsReportTest
{
    private sealed record Plainness(
        ImmutableDictionary<Facet, double> Facets,
        double Base,
        bool FourFour,
        bool Straight,
        bool Unswung,
        bool InAForm,
        bool MajorOrMinor,
        bool SongMajorOrMinor,
        bool OneScale,
        bool OneKey,
        bool OneMeter,
        bool OneTempo,
        bool NoPolymeter,
        bool KitOnly,
        double Triads,
        double ChordLevel,
        double Improvisation
    );

    private static Plainness Measure(CorpusSong song)
    {
        var trace = song.Trace;
        T Value<T>(string point) => (T)trace.First(x => x.Point == point).Value!;
        var (_, scale) = Value<(HarmonicUnconventionality, Scale)>(TracePoints.SongHarmony);
        var chords = trace.Where(x => x.Point == TracePoints.Chord && x.Track == SongTracks.ChordsTrack)
            .Select(x =>
            {
                var role = x.StateMap.GetStateValue(CompositionStateKinds.RoleChord);
                return (role.IsEmpty ? CompositionStateKinds.ChordPool.Pick(x.StateMap) : role[0]).Shape;
            })
            .ToArray();
        var structure = Value<SongStructure>(TracePoints.SongForm);
        var draws = song.Song.Draws!;
        return new Plainness(
            draws.Unconventionality.Facets,
            draws.Unconventionality.Base,
            song.Map.Meter == Meter.FourFour,
            trace.Where(x => x.Point == TracePoints.Feel).All(x => (int)x.Value! == 0),
            Value<Swing>(TracePoints.Swing).Delay == 0,
            structure.Roles.Values.All(x => x != SectionRole.Free),
            (scale == Core.Composition.Scales.Major || scale == Core.Composition.Scales.NaturalMinor) && trace.Where(x => x.Point == TracePoints.SectionScale).All(x => (Scale)x.Value! == scale),
            scale == Core.Composition.Scales.Major || scale == Core.Composition.Scales.NaturalMinor,
            trace.Where(x => x.Point == TracePoints.SectionScale).All(x => (Scale)x.Value! == scale),
            Value<System.Collections.IEnumerable>(TracePoints.KeyChange).Cast<object>().Any() == false,
            Value<ImmutableDictionary<int, Meter>>(TracePoints.SectionMeter).Values.All(x => x == song.Map.Meter),
            Value<ImmutableArray<double>>(TracePoints.SectionTempo).All(x => Math.Abs(x - 1) < 1e-9),
            Value<System.Collections.IDictionary>(TracePoints.Polymeter).Count == 0,
            draws.DrumSetup == DrumSetup.Kit,
            chords.Length == 0 ? 1 : chords.Count(x => x.Unconventionality == 0) / (double)chords.Length,
            chords.Length == 0 ? 0 : chords.Average(x => x.Unconventionality),
            Value<double>(TracePoints.MelodyImprovisation)
        );
    }

    private static void Describe(string what, Plainness[] songs)
    {
        string Share(Func<Plainness, bool> plain) => $"{songs.Count(plain) / (double)songs.Length,4:P0}";
        Console.WriteLine($"{what,-10} base {songs.Average(x => x.Base):F2} ({songs.Min(x => x.Base):F2}-{songs.Max(x => x.Base):F2}); facets mean {songs.Average(x => x.Facets.Values.Average()):F2}, " +
                          $"a facet above 0.75 in {Share(x => x.Facets.Values.Any(f => f > 0.75))} of songs, below 0.25 in {Share(x => x.Facets.Values.Any(f => f < 0.25))}");
        Console.WriteLine($"           4/4 {Share(x => x.FourFour)}, straight {Share(x => x.Straight)}, unswung {Share(x => x.Unswung)}, a form {Share(x => x.InAForm)}, " +
                          $"major or minor {Share(x => x.MajorOrMinor)} (the song's {Share(x => x.SongMajorOrMinor)}, one scale {Share(x => x.OneScale)}), one key {Share(x => x.OneKey)}, one meter {Share(x => x.OneMeter)}, one tempo {Share(x => x.OneTempo)}, " +
                          $"no polymeter {Share(x => x.NoPolymeter)}, kit only {Share(x => x.KitOnly)}");
        Console.WriteLine($"           triads {songs.Average(x => x.Triads):P0} of chords, chord level {songs.Average(x => x.ChordLevel):F2}, improvisation {songs.Average(x => x.Improvisation):F2}; " +
                          $"plain in all ten {Share(x => x.FourFour && x.Straight && x.Unswung && x.InAForm && x.MajorOrMinor && x.OneKey && x.OneMeter && x.OneTempo && x.NoPolymeter && x.KitOnly)}");
        foreach (var facet in Enum.GetValues<Facet>())
            Console.Write($"{facet} {songs.Average(x => x.Facets[facet]):F2}  ");
        Console.WriteLine();
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        const int count = 256;
        Describe("drawn", TestCorpus.Measure(count, Measure));
        foreach (var @base in new[] { 0.25, 0.5 })
            Describe($"base {@base}", TestCorpus.InParallel(Enumerable.Range(0, count), seed => Measure(TestCorpus.Get(seed, new SongOverrides(Base: @base)))));
        await Task.CompletedTask;
    }
}
