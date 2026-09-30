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

    public static IEnumerable<CorpusSong> Range(int count) => Enumerable.Range(0, count).Select(Get);

    private static readonly ConcurrentDictionary<(int, Meter), Lazy<CorpusSong>> MeterSongs = new();

    /// <summary>The song of the seed with its bars in the meter given instead of its own.</summary>
    public static CorpusSong Get(int seed, Meter meter)
    {
        return MeterSongs.GetOrAdd((seed, meter), x => new Lazy<CorpusSong>(() => Generate(x.Item1, x.Item2))).Value;
    }

    private static CorpusSong Generate(int seed, Meter? meter = null)
    {
        // the song's own trace, apart from any the calling test has started, which it would clash with
        Task<CorpusSong> task;
        using (ExecutionContext.SuppressFlow())
            task = Task.Run(() =>
                {
                    using var trace = StateTrace.Start();
                    var song = meter is null ? SongGenerator.GenerateSong(seed) : SongGenerator.GenerateSong(seed, ProgressionSettings.Default, meter);
                    return new CorpusSong(seed, song, Render.RenderSong(song), [..trace.Entries]);
                }
            );
        return task.GetAwaiter().GetResult();
    }
}

/// <summary>A song of the corpus, its rendering, and what its generation's trace recorded.</summary>
internal sealed record CorpusSong(int Seed, Song Song, RenderedSong Rendered, ImmutableArray<StateTraceEntry> Trace)
{
    public SongMap Map => Song.Map!;

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

    /// <summary>Whether a section's drums play where the song plays it, rather than rest for a breakdown.</summary>
    public bool HasDrums(SectionSpan span) => !Resting(span).Contains(TrackRole.Drum);

    /// <summary>
    ///     Where the drums mark a line, as the song puts them: at every pattern from the first section on, the first after
    ///     the intro only if it has one, and the last before the ending only if it has one; but for a line into a section
    ///     whose drums rest, or in one, where an intro's window into the first section ends on its own line.
    /// </summary>
    public double[] FillLines()
    {
        var first = Map.Intro.Duration > 0 ? 0 : 1;
        var last = (int)Math.Round((Map.Sections[^1].End - Origin) / Map.Meter.PatternDuration) - (FormLayers.HasFinalChord(Map.Ending.Kind) ? 0 : 1);
        var windowEnd = Map.Intro.Kind == IntroKind.Entries && !Map.Intro.Window.IsBefore ? Origin + Map.Intro.Window.Bars * Map.Meter.BarDuration : double.NaN;
        return
        [
            ..Enumerable.Range(first, last - first + 1)
                .Select(x => Origin + x * Map.Meter.PatternDuration)
                .Where(x => x >= Map.Sections[^1].End - 1e-9
                            || HasDrums(Map.Sections.Last(s => s.Start <= x + 1e-9))
                            || Math.Abs(x - windowEnd) < 1e-9 && HasDrums(Map.Sections[0]))
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
