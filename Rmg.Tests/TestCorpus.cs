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

    private static CorpusSong Generate(int seed)
    {
        // the song's own trace, apart from any the calling test has started, which it would clash with
        Task<CorpusSong> task;
        using (ExecutionContext.SuppressFlow())
            task = Task.Run(() =>
                {
                    using var trace = StateTrace.Start();
                    var song = SongGenerator.GenerateSong(seed);
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
