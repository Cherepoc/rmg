using System.Collections.Concurrent;
using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Tests;

/// <summary>
///     The songs the tests look at, generated once per seed with the default settings and shared by every test, as they
///     are immutable: each with its rendering and the trace of its generation. A seed is the corpus's, its song the seed
///     moved by <see cref="Offset" />, so that the suite can run on other songs now and then.
/// </summary>
internal static class TestCorpus
{
    /// <summary>
    ///     How far the corpus's seeds are moved, from the setting RMG_CORPUS_OFFSET: none where it is not set, the songs
    ///     every calibration was measured on; another, other songs, which meet the rules with songs they never met. A seed
    ///     a test names is then the corpus's, its song made again with the same offset.
    /// </summary>
    public static ulong Offset { get; } = Environment.GetEnvironmentVariable("RMG_CORPUS_OFFSET") is { Length: > 0 } offset
        ? ulong.Parse(offset)
        : 0;

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

    /// <summary>
    ///     The work of every seed, in the seeds' order, as <see cref="InParallel{T}" /> does it, but handed over a core's
    ///     worth at a time as it is read, so that a test reading songs made afresh once holds a few of them rather than all:
    ///     a song with its trace is some 3 MB, and the suite's tests run eight at a time.
    /// </summary>
    public static IEnumerable<T> Streamed<T>(IEnumerable<int> seeds, Func<int, T> work) =>
        seeds.Chunk(Environment.ProcessorCount).SelectMany(chunk => InParallel(chunk, work));

    private static readonly ConcurrentDictionary<(int, Meter), Lazy<CorpusSong>> MeterSongs = new();

    /// <summary>The song of the seed with its bars in the meter given instead of its own.</summary>
    public static CorpusSong Get(int seed, Meter meter)
    {
        return MeterSongs.GetOrAdd((seed, meter), x => new Lazy<CorpusSong>(() => Generate(x.Item1, new SongOverrides(Meter: x.Item2)))).Value;
    }

    /// <summary>The song of the seed with what the overrides set in place of its own draws, made afresh every time.</summary>
    public static CorpusSong Get(int seed, SongOverrides overrides) => Generate(seed, overrides);

    /// <summary>Facets given a song, as <see cref="SongOverrides.Facets" /> takes them.</summary>
    public static ImmutableDictionary<Facet, double> FacetsOf(params (Facet Facet, double Value)[] facets) => facets.ToImmutableDictionary(x => x.Facet, x => x.Value);

    /// <summary>A genre naming one choice alone, its options given played always, at every end, the songs' own everywhere else.</summary>
    public static Genre Always(GenreChoice choice, params string[] options) =>
        new(ImmutableDictionary<string, ImmutableDictionary<string, Rmg.Core.Probabilities.ByConvention>>.Empty.Add(choice.Name,
            options.ToImmutableDictionary(x => x, _ => new Rmg.Core.Probabilities.ByConvention(1, 1, 1))));

    /// <summary>A genre of every section's pattern so many phrases long, and nothing else, as a long pattern's test asks songs in.</summary>
    public static Genre PhrasesOnly(int phrases) => new(ImmutableDictionary<string, ImmutableDictionary<string, Rmg.Core.Probabilities.ByConvention>>.Empty
        .Add(GenreChoices.Phrases.Name, ImmutableDictionary<string, Rmg.Core.Probabilities.ByConvention>.Empty.Add(GenreChoices.PhrasesName(phrases), new(1, 1, 1))));

    /// <summary>
    ///     The song with its lines' ornaments left off, as a test of a line's own notes renders it: an ornament's quick
    ///     notes are played over the line, not of it (<see cref="Ornaments" />).
    /// </summary>
    public static Song Unornamented(Song song) => new(song.Duration, song.Meter, song.TrackDefinitions, song.TrackEventStateTimelineMap, song.Map,
        song.Notes?.ToImmutableSortedDictionary(x => x.Key, x => EventTimeline.Create(x.Value.Duration,
            x.Value.Select(n => (n.Value with { State = n.Value.State.With(CompositionStateKinds.Ornament, []) }).ToTimelineItem(n.Position)))), song.Draws);

    // a flow of execution with nothing of the caller's, no trace among it, captured on a thread of its own
    private static readonly ExecutionContext CleanContext = CaptureClean();

    private static ExecutionContext CaptureClean()
    {
        // the caller's flow not passed to the thread, so that a trace it has started is not captured with it
        ExecutionContext? context = null;
        var thread = new Thread(() => context = ExecutionContext.Capture());
        using (ExecutionContext.SuppressFlow())
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

    private static CorpusSong Generate(int seed, SongOverrides overrides) => Generate(seed, SongSeed(seed), overrides);

    /// <summary>The seed the song of a corpus's seed is made from, moved by the offset, for a test that makes the song itself.</summary>
    public static ulong SongSeed(int seed) => unchecked((ulong)seed + Offset);

    /// <param name="corpusSeed">The corpus's seed of the song, which a test names it by.</param>
    /// <param name="seed">The song's own seed, the corpus's moved by the offset.</param>
    private static CorpusSong Generate(int corpusSeed, ulong seed, SongOverrides overrides)
    {
        return Isolated(() =>
            {
                // the entries and their values, not what every layer contributed, which would make the songs several
                // times slower to generate: a test that explains a state traces a song of its own
                using var trace = StateTrace.Start(explains: false);
                var song = SongGenerator.GenerateSong(seed, overrides, CancellationToken.None);
                return new CorpusSong(corpusSeed, song, [..trace.Entries]);
            }
        );
    }
}

/// <summary>A song's tracks found by their parts, as the tests ask for them, never by their numbers.</summary>
internal static class SongRoles
{
    /// <summary>The track that plays the part in the song.</summary>
    public static int Track(this Song song, TrackRole role) => song.TrackDefinitions.Single(x => x.Value.Role == role).Key;

    /// <summary>Whether the track is one of the song's drums.</summary>
    public static bool IsDrum(this Song song, int track) => song.TrackDefinitions[track].Role == TrackRole.Drum;
}

/// <summary>A song of the corpus, its rendering, and what its generation's trace recorded.</summary>
internal sealed record CorpusSong(int Seed, Song Song, ImmutableArray<StateTraceEntry> Trace)
{
    /// <summary>Its rendering, made when first read, as many tests read the song's state alone.</summary>
    public RenderedSong Rendered => field ??= Render.RenderSong(Song);

    public SongMap Map => Song.Map!;

    /// <summary>
    ///     The sections where the song plays them, and before them a hook intro, its chorus's first phrase quoted
    ///     (<see cref="IntroKind.Hook" />), or an intro of entries' window, each as the span its source would be there
    ///     (<see cref="SongMap.Plays" />), but the ending.
    /// </summary>
    public SectionSpan[] PlayedSpans => field ??= [..Map.Plays.Where(x => !x.IsEnding).Select(x => x.Span)];

    /// <summary>The span a position plays in as a section, a hook intro's and an intro of entries' window among them (<see cref="SongMap.Plays" />); none for an intro of another kind or the ending.</summary>
    public SectionSpan? SpanAt(double position) => Map.PlayingAt(position) is { IsEnding: false } play ? play.Span : null;

    /// <summary>The parts the song leaves out (<see cref="SongParts" />).</summary>
    public ImmutableHashSet<TrackRole> Absent => field ??= (ImmutableHashSet<TrackRole>)Trace.Single(x => x.Point == TracePoints.SongParts).Value!;

    /// <summary>Whether the song has drums, which a few songs leave out (<see cref="SongParts" />).</summary>
    public bool PlaysDrums => !Absent.Contains(TrackRole.Drum);

    /// <summary>The sections' polymeters, by their ids (<see cref="Polymeter" />).</summary>
    public ImmutableDictionary<int, Polymeter.Plan> PolymeterPlans => (ImmutableDictionary<int, Polymeter.Plan>)Trace.Single(x => x.Point == TracePoints.Polymeter).Value!;

    /// <summary>Each soloist the song draws, by its part (<see cref="TracePoints.Soloist" />).</summary>
    public Dictionary<TrackRole, Soloist> Soloists => Trace.Where(x => x.Point == TracePoints.Soloist).Select(x => ((TrackRole Role, Soloist Soloist))x.Value!).ToDictionary(x => x.Role, x => x.Soloist);

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
    public ImmutableHashSet<TrackRole> Resting(SectionSpan span) => Arrangements[IndexOf(span)];

    /// <summary>A span's place among the song's sections, a played one's, a hook intro's, as the section it plays as.</summary>
    private int IndexOf(SectionSpan span) => Array.IndexOf(Spans, span) is >= 0 and var index ? index
        : Map.PlayingAt(span.Start)?.Index ?? throw new ArgumentException($"No section of the song plays at {span.Start}.", nameof(span));

    // the song's sections, and what the trace says of each, read once
    private SectionSpan[] Spans => field ??= [..Map.Sections];

    private ImmutableHashSet<TrackRole>[] Arrangements => field ??= [..Trace.Where(x => x.Point == TracePoints.Arrangement).Select(x => (ImmutableHashSet<TrackRole>)x.Value!)];

    private (int Index, TextureKind Kind, ImmutableArray<ImmutableHashSet<TrackRole>> PhraseSilent)[] Textures => field ??=
        [..Trace.Where(x => x.Point == TracePoints.Texture).Select(x => ((int, TextureKind, ImmutableArray<ImmutableHashSet<TrackRole>>))x.Value!)];

    /// <summary>How a section brings its parts in where the song plays it (<see cref="Rmg.Core.Composition.Texture" />).</summary>
    public (int Index, TextureKind Kind, ImmutableArray<ImmutableHashSet<TrackRole>> PhraseSilent) Texture(SectionSpan span) => Textures[IndexOf(span)];

    /// <summary>The sections the song plays as solos, by their places among its sections.</summary>
    public ImmutableDictionary<int, SoloPlan> Solos => field ??=
        Trace.Where(x => x.Point == TracePoints.Solo).Select(x => ((int Index, SoloPlan Plan))x.Value!).ToImmutableDictionary(x => x.Index, x => x.Plan);

    /// <summary>Whether the song plays a section as a solo where it plays it.</summary>
    public bool IsSolo(SectionSpan span) => Solos.ContainsKey(IndexOf(span));

    /// <summary>Whether a section's drums play where the song plays it, rather than rest for a breakdown.</summary>
    public bool HasDrums(SectionSpan span) => !Resting(span).Contains(TrackRole.Drum);

    /// <summary>The phrase an appearance's drums come in at, a build bringing them in late, as its texture has it; 0 where they play from its start.</summary>
    public int DrumsFrom(int index) => Textures
        .Where(x => x.Index == index).Select(x => x.PhraseSilent.Select((parts, phrase) => parts.Contains(TrackRole.Drum) ? phrase + 1 : 0).DefaultIfEmpty(0).Max()).Single();

    /// <summary>
    ///     Where the drums mark a line, as the song puts them: at every phrase from the first section on, the first after
    ///     the intro only if it has one, and the last before the ending only if it has one; but for a line into a section
    ///     whose drums rest, or in one, where an intro's window into the first section ends on its own line, and within a
    ///     drum solo, which plays its own.
    /// </summary>
    public double[] FillLines()
    {
        // every phrase's start in its section's meter, the song's first only after an intro, and the ending's line where
        // it has a final chord
        var patterns = Map.Sections
            .SelectMany(span => span.Phrases.Select(p => p.Start))
            .Where(x => Map.Intro.Duration > 0 || x > Origin + 1e-9)
            .Concat(FormLayers.HasFinalChord(Map.Ending.Kind) ? [Map.Sections[^1].End] : []);
        var windowEnd = Map.Intro.Kind == IntroKind.Entries && !Map.Intro.Window.IsBefore ? Origin + Map.Intro.Window.Bars * Map.Meter.BarDuration : double.NaN;
        // the window's end a line of its own, a phrase in, though a longer pattern goes on past it
        patterns = patterns.Concat(double.IsNaN(windowEnd) ? [] : [windowEnd]);
        var drumSolos = Solos.Where(x => x.Value.IsDrumSolo).Select(x => Map.Sections[x.Key]).ToArray();
        // a line at a phrase a build's drums have come in by, with the fill they come in on
        bool IsAfterDrumsComeIn(double line)
        {
            var index = Array.FindLastIndex(Spans, s => s.Start <= line + 1e-9);
            var span = Map.Sections[index];
            return (int)Math.Floor((line - span.Start) / span.Meter.PhraseDuration + 1e-9) >= DrumsFrom(index);
        }

        // none in a song without drums
        if (!PlaysDrums)
            return [];
        return
        [
            ..patterns
                .Where(x => x >= Map.Sections[^1].End - 1e-9
                            || HasDrums(Map.Sections.Last(s => s.Start <= x + 1e-9)) && IsAfterDrumsComeIn(x)
                            || Math.Abs(x - windowEnd) < 1e-9 && HasDrums(Map.Sections[0]))
                .Where(x => !drumSolos.Any(span => x > span.Start + 1e-9 && x < span.End - 1e-9))
                .Distinct()
                .Order()
        ];
    }

    /// <summary>The track that plays the part in the song.</summary>
    public int Track(TrackRole role) => Song.Track(role);

    /// <summary>How unconventional the song is, by each facet, as it drew it.</summary>
    public Unconventionality Unconventionality => (Unconventionality)Trace.Single(t => t.Point == TracePoints.SongUnconventionality).Value!;

    /// <summary>Whether the track is one of the song's drums.</summary>
    public bool IsDrum(int track) => Song.IsDrum(track);

    /// <summary>The meter a section of the song is in, its phrases' bars with it.</summary>
    public Meter MeterOf(int section) => Map.Sections.First(x => x.SectionId == section).Meter;

    /// <summary>Where the first section starts, after any bars of an intro.</summary>
    public double Origin => Map.Origin;

    /// <summary>The rendered notes of a pitched track, its part's, in order.</summary>
    public TimelineItem<RenderedNote>[] Notes(int track)
    {
        var role = Song.TrackDefinitions[track].Role;
        return Rendered.Tracks
            .Where(x => !x.IsPercussionInstrument && !x.IsDouble && x.Role == role)
            .SelectMany(x => x.NoteTimeline)
            .OrderBy(x => x.Position)
            .ToArray();
    }

    /// <summary>The rendered notes of the drums, in order.</summary>
    public TimelineItem<RenderedNote>[] Drums =>
        Rendered.Tracks.Where(x => x.IsPercussionInstrument).SelectMany(x => x.NoteTimeline).OrderBy(x => x.Position).ToArray();
}
