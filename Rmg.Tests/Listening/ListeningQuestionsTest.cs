using System.Collections.Immutable;
using System.Text.Encodings.Web;
using System.Text.Json;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Listening;

/// <summary>
///     The questions the page's listening mode asks (<c>Client/listening.json</c>): each a device heard with and without,
///     in a genre where it plays, the corpus seeds where the trace finds it playing.
/// </summary>
public sealed class ListeningQuestionsTest
{
    /// <summary>How many songs each question is heard over.</summary>
    private const int Seeds = 6;

    /// <param name="Id">How a verdict names the question, a word or two.</param>
    /// <param name="Text">What the listener is asked.</param>
    /// <param name="Decides">What the answer decides.</param>
    /// <param name="Genre">The preset the songs are made in, none for the songs' own.</param>
    /// <param name="Skip">The stages the song without the device leaves out.</param>
    /// <param name="GenrePatch">The options the song without the device lays over its genre's, by choice and option, the genre's others as they are.</param>
    /// <param name="At">Where the song first plays the device, in beats, as its trace and notes have it, a little before it where it grows; none where it does not.</param>
    /// <param name="Heard">The corpus seeds asked before, left out where the question is asked again over other songs.</param>
    /// <param name="In">The sections the device plays in, where alone it is listened to; every section for none.</param>
    private sealed record Question(
        string Id,
        string Text,
        string Decides,
        string? Genre,
        string[] Skip,
        Dictionary<string, Dictionary<string, int>>? GenrePatch,
        Func<CorpusSong, double?> At,
        int[]? Heard = null,
        Func<CorpusSong, SectionSpan, bool>? In = null
    );

    /// <summary>Where a section the trace finds playing the device first plays.</summary>
    private static double? FirstSection(CorpusSong song, string point, Func<object?, bool> plays) =>
        song.Trace.Where(x => x.Point == point && plays(x.Value)).Select(x => song.Map.Sections.Where(s => s.SectionId == x.Section).Select(s => (double?)s.Start).Min()).Min();

    /// <summary>Bars before a place, to hear what leads into it, never before the song's start.</summary>
    private static double? Before(CorpusSong song, double? at, int bars) => at is { } place ? Math.Max(0, place - bars * song.Song.MeterAt(place).BarDuration) : null;

    private static readonly Question[] Questions =
    [
        new("chiptune-speed", "Chiptune's chords cycled on one voice, a chord held long now cycling three times at the most, its notes slower: a chip's chord on every chord now, or a broken chord where it buzzed?",
            "The frame-rate arpeggio's cycles, at most Articulations.FrameCycles 3 a chord, against whole chords; heard worse on the long-held chords at two frames a note on 15 October 2026, asked again over the same songs.", "chiptune", [], new() { ["articulation"] = new() { ["frames"] = 0 } },
            x => Before(x, x.Song.Notes!.GetValueOrDefault(x.Track(TrackRole.Chords))?.Where(n => n.Value.Duration / Meter.BeatsPerSecond(x.Song.TrackEventStateTimelineMap.CommonStateTimelineMap
                .GetStateTimeline(StateKinds.Tempo).GetEffectiveValueAt(n.Position)) < Articulations.FrameSeconds + 1e-6).Select(n => (double?)n.Position).FirstOrDefault(), 1)),
        new("cinematic-pad", "Cinematic piano's strings holding the chords under the piano: a film score's bed, or a piano piece smothered?",
            "Cinematic piano's pad, its strings at 70-80-80, against the piano, its bass and its counter-melody alone.", "filmPiano", [], new() { ["parts"] = new() { ["pad"] = 0 } },
            x => x.Song.Notes!.ContainsKey(x.Track(TrackRole.Pad)) ? (double?)x.Map.Sections[0].Start : null),
        new("lyrical-beat", "Lyrical guitar's slow swung beat under its guitars: a downtempo groove, or the haze broken up?",
            "Lyrical guitar's drums, 95-75-40, against none, as most of its records play.", "lyrical", [], new() { ["parts"] = new() { ["drum"] = 0 } },
            x => x.PlaysDrums ? (double?)x.Map.Sections[0].Start : null),
        new("dnb-two-step", "Drum and bass's kick on 1 and the 3-and around the snare's 2 and 4, the two-step: rolling, or stiff?",
            "The two-step, Drum and bass's figure.ground 50-60-60 beside its breakbeat, against those alone.", "dnb", [], new() { ["figure.ground"] = new() { ["twoStep"] = 0 } },
            x => FirstSection(x, TracePoints.Figures, v => ((ImmutableDictionary<FigurePlayer, string>)v!).GetValueOrDefault(FigurePlayer.Ground) == "twoStep"),
            In: (song, span) => song.Trace.Any(t => t.Point == TracePoints.Figures && t.Section == span.SectionId && ((ImmutableDictionary<FigurePlayer, string>)t.Value!).GetValueOrDefault(FigurePlayer.Ground) == "twoStep")),
        new("soul-every-beat", "Soul's snare on every beat, as a Motown record drives: a stomp, or a march?",
            "The snare on every beat, Soul's figure.backbeat 20-30-30, against its backbeat.", "soul", [], new() { ["figure.backbeat"] = new() { ["everyBeat"] = 0 } },
            x => FirstSection(x, TracePoints.Figures, v => ((ImmutableDictionary<FigurePlayer, string>)v!).GetValueOrDefault(FigurePlayer.Backbeat) == "everyBeat"),
            In: (song, span) => song.Trace.Any(t => t.Point == TracePoints.Figures && t.Section == span.SectionId && ((ImmutableDictionary<FigurePlayer, string>)t.Value!).GetValueOrDefault(FigurePlayer.Backbeat) == "everyBeat")),
        new("disco-pulse", "Disco's bass pulsing in even 8ths on the root: the dance floor's drive, or a bass that never moves?",
            "The bass's 8ths, Disco's figure.bass 40-50-50, against its own line.", "disco", [], new() { ["figure.bass"] = new() { ["eighths"] = 0 } },
            x => FirstSection(x, TracePoints.Figures, v => ((ImmutableDictionary<FigurePlayer, string>)v!).GetValueOrDefault(FigurePlayer.Bass) == "eighths"),
            In: (song, span) => song.Trace.Any(t => t.Point == TracePoints.Figures && t.Section == span.SectionId && ((ImmutableDictionary<FigurePlayer, string>)t.Value!).GetValueOrDefault(FigurePlayer.Bass) == "eighths")),
        new("house-kick-out", "House taking its kick and its bass out for a beat or a bar before a downbeat, the hats playing on: a lift, or a hole?",
            "The kick-out, House's kickOut.lines 20-30-40, the bass out with it, against the kick playing through.", "electronic", [], new() { ["kickOut"] = new() { ["lines"] = 0 } },
            x => Before(x, ((ImmutableArray<(double From, double To)>)x.Trace.Single(t => t.Point == TracePoints.KickOuts).Value!).Select(k => (double?)k.From).FirstOrDefault(), 1)),
        new("ballad-breakdown", "The Power ballad's last chorus played twice, its first pass built up from the voice alone, the drums next, the band by its end: a ballad's climax, or a stall before it?",
            "The breakdown chorus, the Power ballad's lastRepeat.breakdown 30-40-40, against its last chorus repeated whole.", "ballad", [], new() { ["lastRepeat"] = new() { ["breakdown"] = 0 } },
            x => x.Trace.Single(t => t.Point == TracePoints.BreakdownChorus).Value is true ? Before(x, x.Map.Sections[^2].Start, 1) : null),
        new("folk-waltz", "Folk in three, its bass on 1 and its chords on 2 and 3: a waltz, or an oompah band?",
            "The waltz figure, Folk's chords' 50-60-60 and bass's 20-30-30 in a bar of three, against their own rhythms.", "folk", [],
            new() { ["figure.chords"] = new() { ["waltz"] = 0 }, ["figure.bass"] = new() { ["waltz"] = 0 } },
            x => FirstSection(x, TracePoints.Figures, v => ((ImmutableDictionary<FigurePlayer, string>)v!).GetValueOrDefault(FigurePlayer.Chords) == "waltz"),
            In: (song, span) => song.Trace.Any(t => t.Point == TracePoints.Figures && t.Section == span.SectionId && ((ImmutableDictionary<FigurePlayer, string>)t.Value!).GetValueOrDefault(FigurePlayer.Chords) == "waltz")),
        new("latin-tumbao", "Latin's bass on the tumbao, each bar's 2-and and 4 ahead of the chord, where it played its own line: a salsa's bass, or one that never lands?",
            "The tumbao, Latin's figure.bass 100 beside its own rhythm's 20-30-40, against its own alone; asked once Latin was researched and retuned.", "latin", [], new() { ["figure.bass"] = new() { ["tumbao"] = 0 } },
            x => FirstSection(x, TracePoints.Figures, v => ((ImmutableDictionary<FigurePlayer, string>)v!).GetValueOrDefault(FigurePlayer.Bass) == "tumbao"),
            In: (song, span) => song.Trace.Any(t => t.Point == TracePoints.Figures && t.Section == span.SectionId && ((ImmutableDictionary<FigurePlayer, string>)t.Value!).GetValueOrDefault(FigurePlayer.Bass) == "tumbao"))
    ];

    /// <summary>Where the song's chords first break in a colour of those given, a bar before it.</summary>
    private static double? FirstColoured(CorpusSong song, Func<ChordColour, bool> colours) =>
        Before(song, song.Song.Notes!.GetValueOrDefault(song.Track(TrackRole.Chords))?.Where(x => colours((ChordColour)x.Value.State.GetStateValue(CompositionStateKinds.Colour)))
            .Select(x => (double?)x.Position).FirstOrDefault(), 1);

    /// <summary>Where the song last changes key.</summary>
    private static double? LastKeyChange(CorpusSong song) =>
        ((ImmutableArray<KeyChange>)song.Trace.Single(t => t.Point == TracePoints.KeyChange).Value!).Select(x => (double?)x.Position).Max();

    /// <summary>The page's questions file, in the repository the tests run from.</summary>
    private static string QuestionsFile()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "Rmg.WebApi", "Client")))
            directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("No repository above the tests."), "Rmg.WebApi", "Client", "listening.json");
    }

    /// <summary>How long a listener hears a song from where to listen, in seconds, which its difference is measured over.</summary>
    private const double Heard = 16;

    /// <summary>How many of the corpus's songs that play a question's device are measured, of which the most different are asked.</summary>
    private const int Candidates = 24;

    /// <summary>How near the best a place to listen may be and still be taken for being earlier, as a share of the best.</summary>
    private const double NearBest = 0.9;

    /// <summary>
    ///     A question's patch as the page lays it: its options, and of a choice its genre does not name, every other option
    ///     at the songs' own amounts, as a choice named switches off the options it leaves out, so that the song without
    ///     differs by the device alone; none for no patch.
    /// </summary>
    private static Dictionary<string, Dictionary<string, object>>? PatchOf(Question question)
    {
        if (question.GenrePatch is null)
            return null;
        var named = question.Genre is null ? [] : GenrePresets.All.Single(x => x.Id == question.Genre).Choices;
        var patch = new Dictionary<string, Dictionary<string, object>>();
        foreach (var (choice, options) in question.GenrePatch)
        {
            var completed = options.ToDictionary(x => x.Key, x => (object)x.Value);
            if (!named.ContainsKey(choice))
                foreach (var option in GenreChoices.All.Single(x => x.Name == choice).Options.Where(x => !options.ContainsKey(x.Name)))
                {
                    int[] ends = [..new[] { option.Own.Plain, option.Own.Tuned, option.Own.Wild }.Select(x => x * 100).Select(x => Math.Abs(x - Math.Round(x)) < 1e-9
                        ? (int)Math.Round(x) : throw new InvalidOperationException($"{question.Id}: {choice}'s {option.Name} is no whole percent, which a genre could give."))];
                    completed[option.Name] = ends.Distinct().Count() == 1 ? ends[0] : ends;
                }
            patch[choice] = completed;
        }
        return patch;
    }

    /// <summary>A question's genre as its song without the device has it, its choices patched as the page patches them; none for no genre.</summary>
    private static Genre? GenreOf(Question question, bool without)
    {
        if (question.Genre is null)
            return null;
        var preset = GenrePresets.All.Single(x => x.Id == question.Genre);
        if (!without || PatchOf(question) is not { } patch)
            return GenrePresets.Genre(preset);
        var choices = new Dictionary<string, Dictionary<string, JsonElement>>(preset.Choices);
        // every option the patch names its amount, the genre's others as they are, as the page lays it (listening.js)
        foreach (var (choice, options) in patch)
        {
            var patched = choices.TryGetValue(choice, out var own) ? new Dictionary<string, JsonElement>(own) : [];
            foreach (var (option, amount) in options)
                patched[option] = JsonSerializer.SerializeToElement(amount);
            choices[choice] = patched;
        }
        return GenrePresets.Genre(preset with { Choices = choices });
    }

    /// <summary>How long before a pattern's start a song is listened from, in seconds, the page starting it a moment earlier still.</summary>
    private const double LeadIn = 4;

    /// <summary>How many parts a song names to listen to at most, and how much of the most different's difference one makes at least to be named.</summary>
    private const int Listened = 3;

    private const double ListenedShare = 0.25;

    /// <summary>
    ///     A song of a question heard with its device and without, from where the device first plays, at a pattern's start, as
    ///     a figure is stated in its first bar and played again in the rest, which alone may differ, in the sections the
    ///     device plays in: how much the two differ where they differ the most, at the earliest pattern nearly as different,
    ///     where to listen from, in seconds, and the parts that differ the most there, to listen to; none where no pattern
    ///     is the device's.
    /// </summary>
    private static (double Difference, double At, string[] Listen)? Measure(Question question, int seed, CorpusSong with, double first)
    {
        var without = SongGenerator.GenerateSong(TestCorpus.SongSeed(seed), new SongOverrides(Genre: GenreOf(question, true)), question.Skip.ToImmutableHashSet(), CancellationToken.None);
        var difference = new HeardDifference(with.Rendered, Rmg.Core.Rendering.Render.RenderSong(without));
        var places = with.Map.Sections
            .Where(x => x.End > first + 1e-9 && (question.In?.Invoke(with, x) ?? true))
            .SelectMany(x => x.Patterns.Where(p => p.End > first + 1e-9))
            .Select(p => with.Rendered.SecondsAt(p.Start))
            .Select(at => (Difference: difference.Between(at, at + Heard), At: at))
            .ToArray();
        if (places.Length == 0)
            return null;
        var best = places.Max(x => x.Difference);
        var (most, at) = places.First(x => x.Difference >= best * NearBest - 1e-9);
        var parts = difference.Parts(at, at + Heard).OrderByDescending(x => x.Value).ToArray();
        // heard from a few seconds before the pattern, so that the device comes after what leads into it
        at = Math.Max(0, at - LeadIn);
        // a part once, at its largest share, which its second take or its other instrument would name again
        return (most, at, [..parts.Where(x => x.Value >= parts.FirstOrDefault().Value * ListenedShare).DistinctBy(x => x.Key.Split(" (")[0]).Take(Listened).Select(x => x.Key)]);
    }

    /// <summary>
    ///     Writes the questions where the page reads them, each with the songs of the corpus's that play its device whose
    ///     device is heard the most, by how much the song with it and without it differ (<see cref="HeardDifference" />),
    ///     and where to listen in each; and says how much more than the first songs that play it, which it asked before.
    /// </summary>
    [Test]
    [Explicit]
    public async Task Write()
    {
        var written = new List<object>();
        foreach (var question in Questions)
        {
            var genre = GenreOf(question, false);
            // the first songs that play the device, each measured where it plays
            var measured = TestCorpus.InParallel(Enumerable.Range(0, 128).Except(question.Heard ?? []), seed =>
                {
                    var song = TestCorpus.Get(seed, new SongOverrides(Genre: genre));
                    return question.At(song) is { } at ? (Seed: seed, Measure: Measure(question, seed, song, at)) : (seed, null);
                })
                .Where(x => x.Measure is not null).Take(Candidates).Select(x => (x.Seed, Difference: x.Measure!.Value.Item1, At: x.Measure.Value.Item2, Listen: x.Measure.Value.Item3)).ToArray();
            var asked = measured.OrderByDescending(x => x.Difference).Take(Seeds).OrderBy(x => x.Seed).ToArray();
            Console.WriteLine($"{question.Id}: {measured.Length} songs measured, the first {Seeds} differing {measured.Take(Seeds).Select(x => x.Difference).DefaultIfEmpty().Average():F2} on the average, " +
                              $"those asked {asked.Select(x => x.Difference).DefaultIfEmpty().Average():F2}, from {asked.Select(x => x.Difference).DefaultIfEmpty().Min():F2}");
            if (asked.Length == 0 || asked.Any(x => x.Difference <= 0))
                throw new InvalidOperationException($"{question.Id}: a song asked sounds the same with its device and without.");
            written.Add(new
            {
                id = question.Id,
                question = question.Text,
                decides = question.Decides,
                genre = question.Genre,
                // an object of choices always, as the page reads it, empty where the song without the device patches none
                without = new { skip = question.Skip, genre = PatchOf(question) ?? new Dictionary<string, Dictionary<string, object>>() },
                seeds = asked.Select(x => new { seed = Base62.FromSeed(TestCorpus.SongSeed(x.Seed)), at = Math.Round(x.At, 1), listen = x.Listen }).ToArray()
            });
        }

        var path = QuestionsFile();
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(written, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
    }

    /// <summary>Every question's stages ones a song may leave out, its id short enough for its verdicts, and every question with songs to hear.</summary>
    [Test]
    public async Task EveryQuestion_LeavesOutStagesThatMayBe_AndHasSongs()
    {
        // a round of ten at most, as a listener answers no more at a sitting
        await Assert.That(Questions.Length).IsLessThanOrEqualTo(10);
        foreach (var question in Questions)
        {
            await Assert.That(question.Skip.All(SongGenerator.SkippableStages.Contains)).IsTrue().Because(question.Id);
            // its verdicts kept whole, the longest choice after its id
            await Assert.That($"{question.Id}:without".Length).IsLessThanOrEqualTo(Rmg.WebApi.Analytics.AnalyticsEndpoints.DetailLength).Because(question.Id);
            // and a verdict the server takes
            await Assert.That(Rmg.WebApi.Analytics.AnalyticsEndpoints.IsVerdict($"{question.Id}:with")).IsTrue().Because(question.Id);
            // and its patch switching off no option it does not name: a choice its genre leaves to the songs named whole
            var named = question.Genre is null ? [] : GenrePresets.All.Single(x => x.Id == question.Genre).Choices;
            foreach (var (choice, options) in PatchOf(question) ?? [])
                if (!named.ContainsKey(choice))
                    await Assert.That(options.Keys.ToHashSet().SetEquals(GenreChoices.All.Single(x => x.Name == choice).Options.Select(x => x.Name))).IsTrue().Because($"{question.Id}: {choice}");
        }

        var path = QuestionsFile();
        using var file = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var ids = file.RootElement.EnumerateArray().Select(x => x.GetProperty("id").GetString()).ToImmutableHashSet();
        await Assert.That(ids.SetEquals(Questions.Select(x => x.Id))).IsTrue().Because("listening.json is written again by Write");
        foreach (var question in file.RootElement.EnumerateArray())
            await Assert.That(question.GetProperty("seeds").GetArrayLength()).IsGreaterThan(0).Because(question.GetProperty("id").GetString()!);
        // and every question asking as the code has it: its genre, the stages it leaves out and its patch, which a change
        // to the code makes Write written again for
        foreach (var asked in Questions)
        {
            var entry = file.RootElement.EnumerateArray().Single(x => x.GetProperty("id").GetString() == asked.Id);
            string Json(object? value) => JsonSerializer.Serialize(value);
            await Assert.That(entry.GetProperty("genre").ValueKind == JsonValueKind.Null ? null : entry.GetProperty("genre").GetString()).IsEqualTo(asked.Genre).Because(asked.Id);
            await Assert.That(Json(entry.GetProperty("without").GetProperty("skip"))).IsEqualTo(Json(asked.Skip)).Because(asked.Id);
            await Assert.That(Json(entry.GetProperty("without").GetProperty("genre"))).IsEqualTo(Json(PatchOf(asked) ?? new Dictionary<string, Dictionary<string, object>>())).Because(asked.Id);
        }
    }
}
