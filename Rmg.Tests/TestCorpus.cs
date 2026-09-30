using System.Collections.Concurrent;
using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Tests;

/// <summary>
///     The songs the tests look at, generated once per seed with the default settings and shared by every test, as they
///     are immutable: each with its rendering and the trace of its generation.
/// </summary>
internal static class TestCorpus
{
    private static readonly ConcurrentDictionary<int, Lazy<CorpusSong>> Songs = new();

    public static CorpusSong Get(int seed)
    {
        return Songs.GetOrAdd(seed, x => new Lazy<CorpusSong>(() => Generate(x))).Value;
    }

    /// <summary>The songs of seeds 0 to <paramref name="count" /> - 1, in order, those not yet made made in parallel.</summary>
    public static CorpusSong[] Range(int count) => InParallel(Enumerable.Range(0, count), Get);

    /// <summary>
    ///     A measure of every song of seeds 0 to <paramref name="count" /> - 1, in the seeds' order, each song made and
    ///     measured in parallel with the others, so that a report adds its measures up in the same order every run.
    /// </summary>
    public static T[] Measure<T>(int count, Func<CorpusSong, T> measure) => InParallel(Enumerable.Range(0, count), seed => measure(Get(seed)));

    /// <summary>
    ///     The work of every seed, in the seeds' order, done in parallel, each in a flow of execution of its own, so that
    ///     a trace it starts is its own, such as a test's that explains the states of songs it makes itself.
    /// </summary>
    public static T[] InParallel<T>(IEnumerable<int> seeds, Func<int, T> work) =>
        seeds.AsParallel().AsOrdered().Select(seed => Isolated(() => work(seed))).ToArray();

    private static readonly ConcurrentDictionary<(int, Meter), Lazy<CorpusSong>> MeterSongs = new();

    /// <summary>The song of the seed with its bars in the meter given instead of its own.</summary>
    public static CorpusSong Get(int seed, Meter meter)
    {
        return MeterSongs.GetOrAdd((seed, meter), x => new Lazy<CorpusSong>(() => Generate(x.Item1, new SongOverrides(Meter: x.Item2)))).Value;
    }

    /// <summary>The song of the seed with what the overrides set in place of its own draws, made afresh every time.</summary>
    public static CorpusSong Get(int seed, SongOverrides overrides) => Generate(seed, overrides);

    // a flow of execution with nothing of the caller's, no trace among it, captured on a thread of its own
    private static readonly ExecutionContext CleanContext = CaptureClean();

    private static ExecutionContext CaptureClean()
    {
        ExecutionContext? context = null;
        var thread = new Thread(() => context = ExecutionContext.Capture());
        thread.Start();
        thread.Join();
        return context!;
    }

    /// <summary>The work done here and now, in a flow of its own, apart from any trace the caller has started.</summary>
    private static T Isolated<T>(Func<T> work)
    {
        var result = default(T);
        ExecutionContext.Run(CleanContext, _ => result = work(), null);
        return result!;
    }

    private static CorpusSong Generate(int seed) => Generate(seed, SongOverrides.None);

    private static CorpusSong Generate(int seed, SongOverrides overrides)
    {
        return Isolated(() =>
            {
                // the entries and their values, not what every layer contributed, which would make the songs several
                // times slower to generate: a test that explains a state traces a song of its own
                using var trace = StateTrace.Start(explains: false);
                var song = SongGenerator.GenerateSong((ulong)seed, ProgressionSettings.Default, overrides);
                return new CorpusSong(seed, song, Render.RenderSong(song), [..trace.Entries]);
            }
        );
    }
}

/// <summary>A song of the corpus, its rendering, and what its generation's trace recorded.</summary>
internal sealed record CorpusSong(int Seed, Song Song, RenderedSong Rendered, ImmutableArray<StateTraceEntry> Trace)
{
    public SongMap Map => Song.Map!;

    /// <summary>Whether the song has drums, which a few songs leave out (<see cref="SongParts" />).</summary>
    public bool PlaysDrums => !((ImmutableHashSet<TrackRole>)Trace.Single(x => x.Point == TracePoints.SongParts).Value!).Contains(TrackRole.Drum);

    public void Deconstruct(out Song song, out double origin)
    {
        song = Song;
        origin = Origin;
    }

    /// <summary>How the song swings, which moves where a note decided at a place is rendered.</summary>
    public Swing Swing => Render.GetSwing(Song);

    /// <summary>Where the song's chords change, as its sections have them.</summary>
    public double[] ChordChanges => [..Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetStateTimeline(StateKinds.ChordChange).Select(x => x.Position)];

    /// <summary>The parts a section leaves out where the song plays it, by its span's place among the song's (<see cref="Arrangement" />).</summary>
    public ImmutableHashSet<TrackRole> Resting(SectionSpan span) =>
        (ImmutableHashSet<TrackRole>)Trace.Where(x => x.Point == TracePoints.Arrangement).ElementAt(Array.IndexOf(Map.Sections.ToArray(), span)).Value!;

    /// <summary>How a section brings its parts in where the song plays it (<see cref="Rmg.Core.Composition.Texture" />).</summary>
    public (int Index, TextureKind Kind, ImmutableArray<ImmutableHashSet<TrackRole>> PhraseSilent) Texture(SectionSpan span) =>
        ((int, TextureKind, ImmutableArray<ImmutableHashSet<TrackRole>>))Trace.Where(x => x.Point == TracePoints.Texture).ElementAt(Array.IndexOf(Map.Sections.ToArray(), span)).Value!;

    /// <summary>The sections the song plays as solos, by their places among its sections.</summary>
    public ImmutableDictionary<int, SoloPlan> Solos =>
        Trace.Where(x => x.Point == TracePoints.Solo).Select(x => ((int Index, SoloPlan Plan))x.Value!).ToImmutableDictionary(x => x.Index, x => x.Plan);

    /// <summary>Whether the song plays a section as a solo where it plays it.</summary>
    public bool IsSolo(SectionSpan span) => Solos.ContainsKey(Array.IndexOf(Map.Sections.ToArray(), span));

    /// <summary>Whether a section's drums play where the song plays it, rather than rest for a breakdown.</summary>
    public bool HasDrums(SectionSpan span) => !Resting(span).Contains(TrackRole.Drum);

    /// <summary>
    ///     Where the drums mark a line, as the song puts them: at every pattern from the first section on, the first after
    ///     the intro only if it has one, and the last before the ending only if it has one; but for a line into a section
    ///     whose drums rest, or in one, where an intro's window into the first section ends on its own line.
    /// </summary>
    public double[] FillLines()
    {
        // every pattern's start in its section's meter, the song's first only after an intro, and the ending's line where
        // it has a final chord
        var patterns = Map.Sections
            .SelectMany(span => Enumerable.Range(0, (int)Math.Round(span.Duration / span.Meter.PatternDuration)).Select(p => span.Start + p * span.Meter.PatternDuration))
            .Where(x => Map.Intro.Duration > 0 || x > Origin + 1e-9)
            .Concat(FormLayers.HasFinalChord(Map.Ending.Kind) ? [Map.Sections[^1].End] : []);
        var windowEnd = Map.Intro.Kind == IntroKind.Entries && !Map.Intro.Window.IsBefore ? Origin + Map.Intro.Window.Bars * Map.Meter.BarDuration : double.NaN;
        // and every bar of a drum solo
        var soloBars = Solos.Where(x => x.Value.IsDrumSolo).Select(x => Map.Sections[x.Key])
            .SelectMany(span => Enumerable.Range(1, (int)Math.Round(span.Duration / span.Meter.BarDuration) - 1).Select(bar => span.Start + bar * span.Meter.BarDuration));
        return
        [
            ..patterns
                .Where(x => x >= Map.Sections[^1].End - 1e-9
                            || HasDrums(Map.Sections.Last(s => s.Start <= x + 1e-9))
                            || Math.Abs(x - windowEnd) < 1e-9 && HasDrums(Map.Sections[0]))
                .Concat(soloBars)
                .Distinct()
                .Order()
        ];
    }

    /// <summary>Where the first section starts, after any bars of an intro.</summary>
    public double Origin => Map.Origin;

    /// <summary>The rendered notes of a pitched track, in order.</summary>
    public TimelineItem<RenderedNote>[] Notes(int track)
    {
        var program = ((PitchInstrumentTrack)Song.TrackDefinitions[track]).InstrumentCode;
        return Rendered.Tracks
            .Where(x => !x.IsPercussionInstrument && x.PitchInstrumentCode == program)
            .SelectMany(x => x.NoteTimeline)
            .OrderBy(x => x.Position)
            .ToArray();
    }

    /// <summary>The rendered notes of the drums, in order.</summary>
    public TimelineItem<RenderedNote>[] Drums =>
        Rendered.Tracks.Where(x => x.IsPercussionInstrument).SelectMany(x => x.NoteTimeline).OrderBy(x => x.Position).ToArray();
}
