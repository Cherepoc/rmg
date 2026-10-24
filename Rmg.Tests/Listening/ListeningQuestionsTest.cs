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
    /// <param name="GenrePatch">
    ///     The options the song without the device lays over its genre's, by choice and option, the genre's others as they
    ///     are: an amount or its three ends; none for none.
    /// </param>
    /// <param name="At">Where the song first plays the device, in beats, as its trace and notes have it; none where it does not.</param>
    /// <param name="In">The sections the device plays in, where alone it is listened to; every section for none.</param>
    /// <param name="With">The settings the songs with the device are given, by the page's name, as its steps; none for none.</param>
    /// <param name="Without">The settings the songs without it are given, as <paramref name="With" />.</param>
    /// <param name="Places">
    ///     Where the device plays, in beats, each heard from the bar before it, alike in the two songs; none
    ///     for a device that plays throughout what it is heard in, heard from a pattern's start.
    /// </param>
    /// <param name="WithPatch">The options the song with the device lays over its genre's, as <paramref name="GenrePatch" />: a device the preset lacks added; none for none.</param>
    private sealed record Question(
        string Id,
        string Text,
        string Decides,
        string? Genre,
        string[] Skip,
        Dictionary<string, Dictionary<string, object>>? GenrePatch,
        Func<CorpusSong, double?> At,
        Func<CorpusSong, SectionSpan, bool>? In = null,
        Dictionary<string, int>? With = null,
        Dictionary<string, int>? Without = null,
        Func<CorpusSong, double[]>? Places = null,
        Dictionary<string, Dictionary<string, object>>? WithPatch = null
    );

    private static readonly Question[] Questions =
    [
        new("soul-reharmonised", "A Soul song's later chorus over new chords around its melody, the tune the same as sung before: a lift, or the chorus lost?",
            "How often a later appearance is reharmonised (harmonyDevices.reharmonised, Soul's 10-20-30), against never.", "soul", [],
            new() { ["harmonyDevices"] = new() { ["reharmonised"] = 0 } },
            x => Reharmonised(x) is { } r ? r.Substitutes.Min() : null, Places: x => Reharmonised(x) is { } r ? [..r.Substitutes] : []),
        new("folk-tune", "A Folk tune a little faster and fuller, about four notes a bar rather than three: a tune to follow, or busier than the style?",
            "Folk's melody rhythm (rhythm.melody, speed 60 and fullness 65), against the songs' own.", "folk", [],
            null,
            x => Sung(x).Select(v => (double?)v.Start).Min(), (x, span) => Sung(x).Contains(span),
            WithPatch: new() { ["rhythm.melody"] = new() { ["speed"] = 60, ["fullness"] = 65 } }),
        new("piano-double-stops", "A Cinematic piano melody struck with a second note under it on every bar's downbeat, as a pianist voices a tune: fuller, or cluttered?",
            "Whether a melody on a chord instrument plays double stops on its downbeats (lineChords.melody, 25 at every end), against never; not told apart, cut (#64).", "filmPiano", [],
            new() { ["lineChords"] = new() { ["melody"] = 0 } },
            x => DoubleStops(x).Select(v => (double?)v).Min(), Places: DoubleStops),
        new("funk-articulation", "A Funk part switching to its instrument's other sound for a while, a bass slapped on its accents or a guitar muted for a bar: a player's touch, or a sound jumping?",
            "Whether a section's part plays its instrument's variant (articulationModes, the songs' own: in bars 0-4-100, on accents 0-3-100), against never; not told apart, cut (#59).", "funk", [],
            new() { ["articulationModes"] = new() { ["none"] = 100, ["bars"] = 0, ["accents"] = 0 } },
            x => Variants(x).Select(v => (double?)v).Min(), Places: Variants)
    ];

    /// <summary>The appearance a song reharmonises, by its place, and where its substitutes start; none where it reharmonises none.</summary>
    private static (int Index, ImmutableArray<double> Substitutes)? Reharmonised(CorpusSong song) =>
        song.Trace.Where(x => x.Point == TracePoints.Reharmonisation).Select(x => x.Value).OfType<(int, ImmutableArray<double>)>().Select(x => ((int, ImmutableArray<double>)?)x).FirstOrDefault();

    /// <summary>Whether a melody note sounds in a span.</summary>
    private static bool IsSung(CorpusSong song, SectionSpan span) => song.Song.Notes!.GetValueOrDefault(song.Track(TrackRole.Melody))?.Any(n => n.IsWithin(span.Start, span.End)) ?? false;

    /// <summary>A song's appearances its melody sounds in.</summary>
    private static SectionSpan[] Sung(CorpusSong song) => [..song.Map.Sections.Where(x => IsSung(song, x))];

    /// <summary>Where a song's melody stacks its notes on the band's chord, a double stop, in beats.</summary>
    private static double[] DoubleStops(CorpusSong song) =>
        [..song.Song.Notes!.GetValueOrDefault(song.Track(TrackRole.Melody))?.Where(x => x.Value.State.GetStateValue(CompositionStateKinds.LineVoices) > 1).Select(x => x.Position) ?? []];

    /// <summary>Where a song's parts play their instrument's variant, as each section that articulates traces it, in beats.</summary>
    private static double[] Variants(CorpusSong song) =>
    [
        ..song.Trace.Where(x => x.Point == TracePoints.InstrumentOverTime)
            .Select(x => (x.Track, Value: ((int Index, TrackRole, int, ArticulationMode Mode, int Variant))x.Value!))
            .Where(x => x.Value.Mode != ArticulationMode.None)
            .SelectMany(x => song.Song.Notes!.GetValueOrDefault(x.Track)?.Where(n => song.Map.PlayAt(n.Position)?.Index == x.Value.Index
                                                                                  && n.Value.State.GetStateValue(CompositionStateKinds.Program) == x.Value.Variant + 1) ?? [])
            .Select(x => x.Position)
    ];

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
    ///     A question's patch of one side as the page lays it: its options, and of a choice its genre does not name, every
    ///     other option at the songs' own amounts, as a choice named switches off the options it leaves out, so that the two
    ///     songs differ by the device alone; none for no patch.
    /// </summary>
    private static Dictionary<string, Dictionary<string, object>>? PatchOf(Question question, bool without)
    {
        if ((without ? question.GenrePatch : question.WithPatch) is not { } sidePatch)
            return null;
        var named = question.Genre is null ? [] : GenrePresets.All.Single(x => x.Id == question.Genre).Choices;
        var patch = new Dictionary<string, Dictionary<string, object>>();
        foreach (var (choice, options) in sidePatch)
        {
            var completed = options.ToDictionary(x => x.Key, x => x.Value);
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

    /// <summary>A question's genre as its song of one side has it, its choices patched as the page patches them; none for no genre.</summary>
    private static Genre? GenreOf(Question question, bool without)
    {
        if (question.Genre is null)
            return null;
        var preset = GenrePresets.All.Single(x => x.Id == question.Genre);
        if (PatchOf(question, without) is not { } patch)
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

    /// <summary>A question's songs of one side as the page asks for them: its genre, patched for the side without, and the settings the side gives.</summary>
    private static SongOverrides OverridesOf(Question question, bool without)
    {
        var settings = (without ? question.Without : question.With) ?? [];
        if (settings.Keys.Except(["darkness", "intensity"]).FirstOrDefault() is { } other)
            throw new InvalidOperationException($"{question.Id}: the setting {other} is not one a question gives.");
        double? Mood(string name) => settings.TryGetValue(name, out var step) ? Rmg.Core.Composition.Mood.OfAmount(Rmg.WebApi.Songs.StepScale.ToAmount(step)) : null;
        return new SongOverrides(Genre: GenreOf(question, without), Darkness: Mood("darkness"), Intensity: Mood("intensity"));
    }

    /// <summary>How many bars before a place of a device's the two songs' difference is looked for from, as a build or a pickup leads into it.</summary>
    private const int LookBack = 4;

    /// <summary>How many parts a song names to listen to at most, and how much of the most different's difference one makes at least to be named.</summary>
    private const int Listened = 3;

    private const double ListenedShare = 0.25;

    /// <summary>
    ///     A song of a question heard with its device and without, at a place of its device's, or, of a device playing
    ///     throughout, at a pattern's start, as a figure is stated in its first bar and played again in the rest, which
    ///     alone may differ, in the sections the device plays in: how much the two differ where they differ the most, at
    ///     the earliest place nearly as different, where to listen and where to start, the bar before, in seconds, and the
    ///     parts that differ the most there, to listen to; and how many of its places nothing differs within a bar of;
    ///     none where no place is the device's.
    /// </summary>
    private static ((double Difference, double At, double From, string[] Listen)? Heard, int Missed) Measure(Question question, int seed, CorpusSong with, double first)
    {
        var without = SongGenerator.GenerateSong(TestCorpus.SongSeed(seed), OverridesOf(question, true), question.Skip.ToImmutableHashSet(), CancellationToken.None);
        var difference = new HeardDifference(with.Rendered, Rmg.Core.Rendering.Render.RenderSong(without));
        double Seconds(double position) => with.Rendered.SecondsAt(Math.Max(0, position));
        double BarBefore(double position) => position - with.Map.BarBefore(position);
        // a place listened at where the two songs' difference holding it starts, after a stretch alike in both, a build,
        // a pickup or an approach into it among the device's, from the bar before, so that what leads in is the same in both
        // and the device what is heard next; a place differing all the bars before it is inside another's difference, and
        // one where nothing differs within a bar of it one its trace has wrong, or where it is not heard
        (double At, double From)? HeardAt(double position)
        {
            var bar = with.Map.BarBefore(position);
            var look = position - LookBack * bar;
            if (difference.FirstDifference(Seconds(look), Seconds(position + with.Map.MeterAt(position).BarDuration)) is not { } at
                || at <= Seconds(look) + 1e-6 && look > 1e-9)
                return null;
            return (at, Math.Max(0, at - (Seconds(position) - Seconds(position - bar))));
        }
        bool IsMissed(double position) =>
            difference.FirstDifference(Seconds(BarBefore(position)), Seconds(position + with.Map.MeterAt(position).BarDuration)) is null;
        (double At, double From)[] places;
        var missed = 0;
        if (question.Places is { } placesOf)
        {
            var all = placesOf(with).Where(x => x >= first - 1e-9).Distinct().Order().ToArray();
            places = [..all.Select(HeardAt).OfType<(double, double)>().Distinct()];
            missed = all.Count(IsMissed);
        }
        else
            places =
            [
                ..with.Map.Sections
                    .Where(x => x.End > first + 1e-9 && (question.In?.Invoke(with, x) ?? true))
                    .SelectMany(x => x.Patterns.Where(p => p.End > first + 1e-9))
                    .Select(p => (Seconds(p.Start), Seconds(BarBefore(p.Start))))
            ];
        var measured = places.Select(x => (Difference: difference.Between(x.At, x.At + Heard), x.At, x.From)).ToArray();
        if (measured.Length == 0)
            return (null, missed);
        var best = measured.Max(x => x.Difference);
        var (most, at, from) = measured.First(x => x.Difference >= best * NearBest - 1e-9);
        var parts = difference.Parts(at, at + Heard).OrderByDescending(x => x.Value).ToArray();
        // a part once, at its largest share, which its second take or its other instrument would name again
        return ((most, at, from, [..parts.Where(x => x.Value >= parts.FirstOrDefault().Value * ListenedShare).DistinctBy(x => x.Key.Split(" (")[0]).Take(Listened).Select(x => x.Key)]), missed);
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
            var overrides = OverridesOf(question, false);
            // the first songs that play the device, each measured where it plays
            var playing = TestCorpus.InParallel(Enumerable.Range(0, 128), seed =>
                {
                    var song = TestCorpus.Get(seed, overrides);
                    return question.At(song) is { } at ? (Seed: seed, Measure: Measure(question, seed, song, at)) : (seed, (null, 0));
                })
                .Where(x => x.Measure.Heard is not null || x.Measure.Missed > 0).Take(Candidates).ToArray();
            // a song with no place heard after a stretch alike is never asked
            var missed = playing.Sum(x => x.Measure.Missed);
            if (missed > 0 || playing.Any(x => x.Measure.Heard is null))
                Console.WriteLine($"{question.Id}: {missed} places with nothing differing a bar either side, {playing.Count(x => x.Measure.Heard is null)} songs of {playing.Length} with no place heard after a stretch alike");
            var measured = playing.Where(x => x.Measure.Heard is not null).Select(x => (x.Seed, x.Measure.Heard!.Value.Difference, x.Measure.Heard.Value.At, x.Measure.Heard.Value.From, x.Measure.Heard.Value.Listen)).ToArray();
            // a song the device plays in but that sounds the same without it, as another device plays it there too, never asked
            var asked = measured.Where(x => x.Difference > 0).OrderByDescending(x => x.Difference).Take(Seeds).OrderBy(x => x.Seed).ToArray();
            Console.WriteLine($"{question.Id}: {measured.Length} songs measured, {measured.Count(x => x.Difference <= 0)} the same without, the first {Seeds} differing {measured.Take(Seeds).Select(x => x.Difference).DefaultIfEmpty().Average():F2} on the average, " +
                              $"those asked {asked.Select(x => x.Difference).DefaultIfEmpty().Average():F2}, from {asked.Select(x => x.Difference).DefaultIfEmpty().Min():F2}");
            if (asked.Length < Seeds)
                throw new InvalidOperationException($"{question.Id}: fewer than {Seeds} songs sound other with its device than without.");
            written.Add(new
            {
                id = question.Id,
                question = question.Text,
                decides = question.Decides,
                genre = question.Genre,
                // an object of choices always, as the page reads it, empty where the song without the device patches none
                with = new { genre = PatchOf(question, false) ?? new Dictionary<string, Dictionary<string, object>>(), settings = question.With ?? [] },
                without = new { skip = question.Skip, genre = PatchOf(question, true) ?? new Dictionary<string, Dictionary<string, object>>(), settings = question.Without ?? [] },
                seeds = asked.Select(x => new { seed = Base62.FromSeed(TestCorpus.SongSeed(x.Seed)), at = Math.Round(x.At, 1), from = Math.Round(x.From, 1), listen = x.Listen }).ToArray()
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
            foreach (var (choice, options) in (PatchOf(question, false) ?? []).Concat(PatchOf(question, true) ?? []))
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
            await Assert.That(Json(entry.GetProperty("with").GetProperty("genre"))).IsEqualTo(Json(PatchOf(asked, false) ?? new Dictionary<string, Dictionary<string, object>>())).Because(asked.Id);
            await Assert.That(Json(entry.GetProperty("without").GetProperty("genre"))).IsEqualTo(Json(PatchOf(asked, true) ?? new Dictionary<string, Dictionary<string, object>>())).Because(asked.Id);
            await Assert.That(Json(entry.GetProperty("with").GetProperty("settings"))).IsEqualTo(Json(asked.With ?? [])).Because(asked.Id);
            await Assert.That(Json(entry.GetProperty("without").GetProperty("settings"))).IsEqualTo(Json(asked.Without ?? [])).Because(asked.Id);
        }
    }
}
