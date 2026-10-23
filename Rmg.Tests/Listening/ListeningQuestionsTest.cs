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
    ///     are: an amount or its three ends; a device a preset may take, the song
    ///     without it the song with it added.
    /// </param>
    /// <param name="At">Where the song first plays the device, in beats, as its trace and notes have it, a little before it where it grows; none where it does not.</param>
    /// <param name="In">The sections the device plays in, where alone it is listened to; every section for none.</param>
    /// <param name="With">The settings the songs with the device are given, by the page's name, as its steps; none for none.</param>
    /// <param name="Without">The settings the songs without it are given, as <paramref name="With" />.</param>
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
        Dictionary<string, int>? Without = null
    );

    /// <summary>A song's pitched solos' segments, by their sections' places, as the trace records them.</summary>
    private static (int Index, SoloLines.Segment[] Plan)[] SoloPlans(CorpusSong song) =>
        [..song.Trace.Where(x => x.Point == TracePoints.SoloGestures).Select(x => ((int Index, bool, SoloLines.Segment[] Plan))x.Value!).Select(x => (x.Index, x.Plan))];

    /// <summary>The appearances a song plays its pitched solos in, by their places among its sections, not every appearance of their sections.</summary>
    private static HashSet<int> Soloed(CorpusSong song) => [..SoloPlans(song).Select(x => x.Index)];

    /// <summary>Where a song first plays a solo segment that takes a test, in beats; none where it plays none.</summary>
    private static double? FirstSegment(CorpusSong song, Func<SoloLines.Segment, bool> takes) =>
        SoloPlans(song).SelectMany(x => x.Plan).Where(takes).Select(x => (double?)x.Start).Min();

    private static readonly Question[] Questions =
    [
        new("funk-rhythm", "A Funk soloist riffing on the band's rhythm, a note or two struck short on its onsets: in the groove, or a soloist gone quiet?",
            "How often a soloist riffs on the band's rhythm (soloStyle.rhythm, Funk's 30-40-50), against never.", "funk", [],
            new() { ["soloStyle"] = new() { ["rhythm"] = 0 } },
            x => FirstSegment(x, s => s.Gesture == SoloGesture.Rhythm), (x, span) => Soloed(x).Contains(x.Map.Sections.IndexOf(span))),
        new("surf-drop", "A Surf guitar's last bar before a louder section slid down the neck in palm-muted 16ths onto it: the surf drop, or a gimmick?",
            "Whether a lift into a louder section takes a surf drop (surfDrop.lifts, Surf's 30-40-50), against none.", "surf", [],
            new() { ["surfDrop"] = new() { ["lifts"] = 0 } },
            x => Drops(x).Select(d => (double?)d.From - 2).Min(), (x, span) => Drops(x).Any(d => Math.Abs(d.To - span.End) < 1e-6)),
        new("jazz-chromatic", "A Jazz soloist's run approaching its note on the beat from a half step away: a jazz line, or a wrong note?",
            "How often a soloist's run approaches its note on the beat chromatically (soloStyle.chromatic, Jazz's 40-50-60), against never.", "jazz", [],
            new() { ["soloStyle"] = new() { ["chromatic"] = 0 } },
            x => FirstSegment(x, s => s.Gesture == SoloGesture.Run), (x, span) => Soloed(x).Contains(x.Map.Sections.IndexOf(span))),
        new("idm-64ths", "An IDM hi-hat rolling in 64ths, a drill splintering the beat: the style's, or a buzz?",
            "How often IDM's hat rolls drill in 64ths (hatRollRate.sixtyFourths, 0-20-60), against its 32nds and triplets alone.", "idm", [],
            new() { ["hatRollRate"] = new() { ["sixtyFourths"] = 0 } },
            x => x.Trace.Where(e => e.Point == TracePoints.HatRolls).SelectMany(e => (ImmutableArray<(double From, double To, int Rate)>)e.Value!).Where(r => r.Rate == 16).Select(r => (double?)r.From - 2).Min()),
        new("rock-turnaround", "A Rock riff turning round into its next cycle, a walk up or down on its last beat: a riff band's, or a stumble?",
            "How often a riff turns round where its pattern starts again (riff.turnaround, Rock's 30-40-40), against never.", "rock", [],
            new() { ["riff"] = new() { ["turnaround"] = 0 } },
            x => x.Trace.Where(e => e.Point == TracePoints.LineFill && e.Track == x.Track(TrackRole.Riff)).Select(e => (double?)(((double, double[]))e.Value!).Item1 - 2).Min()),
        new("techno-kickout", "A Techno kick dropping out for a line before the next section, the band going on without it: a DJ's tension, or a hole?",
            "How often the kick drops out for a phrase's last line (kickOut.lines, Techno's 20-30-40), against never.", "techno", [],
            new() { ["kickOut"] = new() { ["lines"] = 0 } },
            x => KickOuts(x).Select(k => (double?)k.From - 2).Min(), (x, span) => KickOuts(x).Any(k => k.From >= span.Start - 1e-9 && k.From < span.End - 1e-9)),
        new("soul-reharmonised", "A Soul song's later chorus over new chords under the same melody: a lift, or the chorus lost?",
            "How often a later appearance is reharmonised (harmonyDevices.reharmonised, Soul's 10-20-30), against never.", "soul", [],
            new() { ["harmonyDevices"] = new() { ["reharmonised"] = 0 } },
            x => Reharmonised(x) is { } r ? r.Substitutes.Min() : null, (x, span) => Reharmonised(x) is { } r && x.Map.Sections.IndexOf(span) == r.Index),
        new("metal-verse-riff", "A Metal verse sung over the intro's riff: the band's main riff carrying the song, or a verse with nothing of its own?",
            "How often a verse plays over the intro's riff (riffSection.verses, Metal's 30-40-40), against a verse of its own.", "metal", [],
            new() { ["riffSection"] = new() { ["verses"] = 0 } },
            x => FirstOf(x, OverRiff(x)), (x, span) => OverRiff(x).Contains(span.SectionId)),
        new("metal-riff-section", "A Metal song opening on its main riff and bringing it back between a chorus and a verse, and at its end: a riff band's song, or the same bars again?",
            "How often a song is built on a riff section returning (riffSection.songs, Metal's 50-60-40), against the songs' own form.", "metal", [],
            new() { ["riffSection"] = new() { ["songs"] = 0 } },
            x => RiffReturns(x).Select(r => (double?)r.Start).Min(), (x, span) => RiffReturns(x).Any(r => Math.Abs(r.Start - span.Start) < 1e-6)),
        new("darkjazz-half-time", "Dark jazz's drums in half time, the backbeat on 3, at an already slow tempo: the style's slow menace, or a pulse too slow to follow?",
            "How often a Dark jazz section's drums play in half time (feelChances.halfTime, 0-70-80), against in time.", "darkJazz", [],
            new() { ["feelChances"] = new() { ["halfTime"] = 0 } },
            x => FirstOf(x, HalfTime(x)), (x, span) => HalfTime(x).Contains(span.SectionId))
    ];

    /// <summary>Where a song first plays a section of those, in beats; none where it plays none.</summary>
    private static double? FirstOf(CorpusSong song, HashSet<int> sections) => song.Map.Sections.FirstOrDefault(x => sections.Contains(x.SectionId))?.Start;

    /// <summary>A song's surf drops, each its bar's start and end.</summary>
    private static (double From, double To)[] Drops(CorpusSong song) =>
        [..song.Trace.Where(x => x.Point == TracePoints.SurfDrop).Select(x => ((double From, double To))x.Value!)];

    /// <summary>A song's kick-outs, each from where the kick drops out to where it plays again.</summary>
    private static (double From, double To)[] KickOuts(CorpusSong song) =>
        [..song.Trace.Where(x => x.Point == TracePoints.KickOuts).SelectMany(x => (ImmutableArray<(double From, double To)>)x.Value!)];

    /// <summary>The appearance a song reharmonises, by its place, and where its substitutes start; none where it reharmonises none.</summary>
    private static (int Index, ImmutableArray<double> Substitutes)? Reharmonised(CorpusSong song) =>
        song.Trace.Where(x => x.Point == TracePoints.Reharmonisation).Select(x => x.Value).OfType<(int, ImmutableArray<double>)>().Select(x => ((int, ImmutableArray<double>)?)x).FirstOrDefault();

    /// <summary>A song's form, as it drew it.</summary>
    private static SongStructure FormOf(CorpusSong song) => (SongStructure)song.Trace.Single(x => x.Point == TracePoints.SongForm).Value!;

    /// <summary>The intro's section, where the song opens on one.</summary>
    private static int? Intro(CorpusSong song) => FormOf(song).Roles.Where(x => x.Value == SectionRole.Intro).Select(x => (int?)x.Key).SingleOrDefault();

    /// <summary>The verses a song plays over its intro's riff, by their sections.</summary>
    private static HashSet<int> OverRiff(CorpusSong song) =>
        [..song.Trace.Where(x => x.Point == TracePoints.LinesFrom && (int)x.Value! == Intro(song) && FormOf(song).Roles.GetValueOrDefault(x.Section) == SectionRole.Verse).Select(x => x.Section)];

    /// <summary>A riff section's returns, the intro's section played again after the song opens on it.</summary>
    private static SectionSpan[] RiffReturns(CorpusSong song) => Intro(song) is { } intro ? [..song.Map.Sections.Where(x => x.SectionId == intro).Skip(1)] : [];

    /// <summary>The sections whose drums play in half time.</summary>
    private static HashSet<int> HalfTime(CorpusSong song) => [..song.Trace.Where(x => x.Point == TracePoints.TimeFeel && (int)x.Value! == 1).Select(x => x.Section)];


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

    /// <summary>A question's songs of one side as the page asks for them: its genre, patched for the side without, and the settings the side gives.</summary>
    private static SongOverrides OverridesOf(Question question, bool without)
    {
        var settings = (without ? question.Without : question.With) ?? [];
        if (settings.Keys.Except(["darkness", "intensity"]).FirstOrDefault() is { } other)
            throw new InvalidOperationException($"{question.Id}: the setting {other} is not one a question gives.");
        double? Mood(string name) => settings.TryGetValue(name, out var step) ? Rmg.Core.Composition.Mood.OfAmount(Rmg.WebApi.Songs.StepScale.ToAmount(step)) : null;
        return new SongOverrides(Genre: GenreOf(question, without), Darkness: Mood("darkness"), Intensity: Mood("intensity"));
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
        var without = SongGenerator.GenerateSong(TestCorpus.SongSeed(seed), OverridesOf(question, true), question.Skip.ToImmutableHashSet(), CancellationToken.None);
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
            var overrides = OverridesOf(question, false);
            // the first songs that play the device, each measured where it plays
            var measured = TestCorpus.InParallel(Enumerable.Range(0, 128), seed =>
                {
                    var song = TestCorpus.Get(seed, overrides);
                    return question.At(song) is { } at ? (Seed: seed, Measure: Measure(question, seed, song, at)) : (seed, null);
                })
                .Where(x => x.Measure is not null).Take(Candidates).Select(x => (x.Seed, Difference: x.Measure!.Value.Item1, At: x.Measure.Value.Item2, Listen: x.Measure.Value.Item3)).ToArray();
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
                with = new { settings = question.With ?? [] },
                without = new { skip = question.Skip, genre = PatchOf(question) ?? new Dictionary<string, Dictionary<string, object>>(), settings = question.Without ?? [] },
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
            await Assert.That(Json(entry.GetProperty("with").GetProperty("settings"))).IsEqualTo(Json(asked.With ?? [])).Because(asked.Id);
            await Assert.That(Json(entry.GetProperty("without").GetProperty("settings"))).IsEqualTo(Json(asked.Without ?? [])).Because(asked.Id);
        }
    }
}
