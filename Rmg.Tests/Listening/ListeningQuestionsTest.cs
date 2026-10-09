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
    private sealed record Question(
        string Id,
        string Text,
        string Decides,
        string? Genre,
        string[] Skip,
        Dictionary<string, Dictionary<string, int>>? GenrePatch,
        Func<CorpusSong, double?> At,
        int[]? Heard = null
    );

    /// <summary>A patch keeping a choice to one option alone, every other of its options at none, as a song without a device's has it.</summary>
    private static Dictionary<string, int> Only(GenreChoice choice, string option) => choice.Options.ToDictionary(x => x.Name, x => x.Name == option ? 100 : 0);

    /// <summary>The first note of the song, of the parts given or any, whose state says it plays the device.</summary>
    private static double? FirstNote(CorpusSong song, Func<RealizedNote, bool> plays, Func<TrackRole, bool>? parts = null) =>
        song.Song.Notes!.Where(x => parts is null || parts(song.Song.TrackDefinitions[x.Key].Role)).SelectMany(x => x.Value).Where(x => plays(x.Value))
            .Select(x => (double?)x.Position).Min();

    /// <summary>Where a section the trace finds playing the device first plays.</summary>
    private static double? FirstSection(CorpusSong song, string point, Func<object?, bool> plays) =>
        song.Trace.Where(x => x.Point == point && plays(x.Value)).Select(x => song.Map.Sections.Where(s => s.SectionId == x.Section).Select(s => (double?)s.Start).Min()).Min();

    private static double? Plays(CorpusSong song, FigurePlayer player, string figure) =>
        FirstSection(song, TracePoints.Figures, v => ((ImmutableDictionary<FigurePlayer, string>)v!).GetValueOrDefault(player) == figure);

    /// <summary>Bars before a place, to hear what leads into it, never before the song's start.</summary>
    private static double? Before(CorpusSong song, double? at, int bars) => at is { } place ? Math.Max(0, place - bars * song.Song.MeterAt(place).BarDuration) : null;


    private static readonly Question[] Questions =
    [
        new("quieter-form", "Black metal's distorted guitar turning acoustic or clean in its quieter sections: a calm between the storms, or a jump?",
            "The quieter form's chances by genre.", "blackMetal", [], new() { ["louderForms"] = new() { ["quieter"] = 0 } },
            x => x.Trace.Where(t => t.Point == TracePoints.QuieterForm).Select(t => x.Song.Notes!.GetValueOrDefault(t.Track)?
                .Where(n => n.Value.State.GetStateValue(CompositionStateKinds.Program) == (int)t.Value! + 1).Select(n => (double?)n.Position).FirstOrDefault()).Min()),
        new("own-modes", "Fusion's planed chords each in a mode of its own, major and minor moving by semitones, rather than all the home's shape: fusion, or aimless?",
            "The planed modes' chance by genre.", "fusion", [], new() { ["planing"] = new() { ["modes"] = 0 } },
            x => x.Trace.Where(t => t.Point == TracePoints.Planing && t.Value is not null
                    && ((ImmutableArray<ChordScale>)x.Trace.Last(c => c.Point == TracePoints.ChordScales && c.Section == t.Section).Value!).Any(c => c.Kind == ChordScaleKind.Own))
                .Select(t => x.Map.Sections.Where(s => s.SectionId == t.Section).Select(s => (double?)s.Start).Min()).Min()),
        new("mediant", "Orchestral's iii or vi turned major, a chromatic mediant, as a film score lifts: a lift, or a wrong chord?",
            "The mediant's chance by genre.", "orchestral", [], new() { ["chordScales"] = new() { ["mediant"] = 0 } },
            x => Before(x, x.Trace.Where(t => t.Point == TracePoints.ChordScales && ((ImmutableArray<ChordScale>)t.Value!).Any(c => c.Kind == ChordScaleKind.Mediant))
                .Select(t => x.Map.Sections.Where(s => s.SectionId == t.Section).Select(s => (double?)s.Start).Min()).Min(), 2)),
        new("riser", "Techno's reverse cymbal swelling into a drop, the band muffled under it or silent: a build, or noise over the band?",
            "The riser's loudness against the band, and whether it rises over a filtered drop.", "techno", ["Risers"], null,
            x => Before(x, x.Trace.Where(t => t.Point == TracePoints.FilteredDrop).Select(t => (double?)t.Value).Min(), 2)),
        new("rudiments", "Jazz's snare dragging into its backbeat and flamming in its fills, soft strokes just before a note: a drummer's touch, or a stumble?",
            "The flams' and drags' chances by genre, and their strokes' loudness.", "jazz", ["Drum ornaments"], null,
            x => x.Song.Notes!.Where(n => x.IsDrum(n.Key)).SelectMany(n => n.Value).Where(n => n.Value.State.GetStateValue(CompositionStateKinds.Ornament) is { IsDefaultOrEmpty: false })
                .Select(n => (double?)n.Position).Min() is { } at ? Before(x, at, 2) : null),
        new("phrase-length", "Orchestral's phrases of four bars and two, the cadence's bars played again before it arrives, and now and then of three: a phrase drawn out, or a bar too many?",
            "The phrase lengths' weights by genre, and whether three bars are kept.", "orchestral", [], new() { ["phraseBars"] = new() { ["threeBars"] = 0, ["sixBars"] = 0 } },
            x => Before(x, x.Map.Sections.Where(s => s.Meter.PhraseBars != Meter.DefaultPhraseBars).Select(s => (double?)s.Start).Min(), 1))
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

    /// <summary>How far apart the places to listen from are tried, in seconds.</summary>
    private const double Step = 2;

    /// <summary>How many of the corpus's songs that play a question's device are measured, of which the most different are asked.</summary>
    private const int Candidates = 24;

    /// <summary>How near the best a place to listen may be and still be taken for being earlier, as a share of the best.</summary>
    private const double NearBest = 0.9;

    /// <summary>A question's genre as its song without the device has it, its choices patched as the page patches them; none for no genre.</summary>
    private static Genre? GenreOf(Question question, bool without)
    {
        if (question.Genre is null)
            return null;
        var preset = GenrePresets.All.Single(x => x.Id == question.Genre);
        if (!without || question.GenrePatch is null)
            return GenrePresets.Genre(preset);
        var choices = new Dictionary<string, Dictionary<string, JsonElement>>(preset.Choices);
        // every option the patch names its amount, the genre's others as they are, as the page lays it (listening.js)
        foreach (var (choice, options) in question.GenrePatch)
        {
            var patched = choices.TryGetValue(choice, out var own) ? new Dictionary<string, JsonElement>(own) : [];
            foreach (var (option, amount) in options)
                patched[option] = JsonDocument.Parse(amount.ToString()).RootElement;
            choices[choice] = patched;
        }
        return GenrePresets.Genre(preset with { Choices = choices });
    }

    /// <summary>How long before the two first differ a song is listened from, in seconds, the page starting it a moment earlier still.</summary>
    private const double LeadIn = 4;

    /// <summary>How many parts a song names to listen to at most, and how much of the most different's difference one makes at least to be named.</summary>
    private const int Listened = 3;

    private const double ListenedShare = 0.25;

    /// <summary>
    ///     A song of a question heard with its device and without, from where the device first plays: how much the two differ
    ///     where they differ the most, at the earliest place nearly as different, that place, in seconds, and the parts that
    ///     differ the most there, to listen to.
    /// </summary>
    private static (double Difference, double At, string[] Listen) Measure(Question question, int seed, CorpusSong with, double first)
    {
        var without = SongGenerator.GenerateSong((ulong)seed, new SongOverrides(Genre: GenreOf(question, true)), question.Skip.ToImmutableHashSet(), CancellationToken.None);
        var difference = new HeardDifference(with.Rendered, Rmg.Core.Rendering.Render.RenderSong(without));
        var places = Enumerable.Range(0, (int)Math.Max(1, Math.Ceiling((with.Rendered.Seconds - Heard - first) / Step)))
            .Select(k => first + k * Step).Select(at => (Difference: difference.Between(at, at + Heard), At: at)).ToArray();
        var best = places.Max(x => x.Difference);
        var (most, at) = places.First(x => x.Difference >= best * NearBest - 1e-9);
        var parts = difference.Parts(at, at + Heard).OrderByDescending(x => x.Value).ToArray();
        // heard from a few seconds before the two first differ there, so that the device comes after what leads into it
        at = Math.Max(0, (difference.FirstDifference(at, at + Heard) ?? at) - LeadIn);
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
                    return question.At(song) is { } at ? (Seed: seed, Measure: (ValueTuple<double, double, string[]>?)Measure(question, seed, song, song.Rendered.SecondsAt(at))) : (seed, null);
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
                without = new { skip = question.Skip, genre = question.GenrePatch ?? new Dictionary<string, Dictionary<string, int>>() },
                seeds = asked.Select(x => new { seed = Base62.FromSeed((ulong)x.Seed), at = Math.Round(x.At, 1), listen = x.Listen }).ToArray()
            });
        }

        var path = QuestionsFile();
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(written, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
    }

    /// <summary>Every question's stages ones a song may leave out, its id short enough for its verdicts, and every question with songs to hear.</summary>
    [Test]
    public async Task EveryQuestion_LeavesOutStagesThatMayBe_AndHasSongs()
    {
        foreach (var question in Questions)
        {
            await Assert.That(question.Skip.All(SongGenerator.SkippableStages.Contains)).IsTrue().Because(question.Id);
            // its verdicts kept whole, the longest choice after its id
            await Assert.That($"{question.Id}:without".Length).IsLessThanOrEqualTo(Rmg.WebApi.Analytics.AnalyticsEndpoints.DetailLength).Because(question.Id);
        }

        var path = QuestionsFile();
        using var file = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var ids = file.RootElement.EnumerateArray().Select(x => x.GetProperty("id").GetString()).ToImmutableHashSet();
        await Assert.That(ids.SetEquals(Questions.Select(x => x.Id))).IsTrue().Because("listening.json is written again by Write");
        foreach (var question in file.RootElement.EnumerateArray())
            await Assert.That(question.GetProperty("seeds").GetArrayLength()).IsGreaterThan(0).Because(question.GetProperty("id").GetString()!);
    }
}
