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

    /// <summary>Where a section the trace finds playing the device first plays.</summary>
    private static double? FirstSection(CorpusSong song, string point, Func<object?, bool> plays) =>
        song.Trace.Where(x => x.Point == point && plays(x.Value)).Select(x => song.Map.Sections.Where(s => s.SectionId == x.Section).Select(s => (double?)s.Start).Min()).Min();

    /// <summary>Bars before a place, to hear what leads into it, never before the song's start.</summary>
    private static double? Before(CorpusSong song, double? at, int bars) => at is { } place ? Math.Max(0, place - bars * song.Song.MeterAt(place).BarDuration) : null;

    private static readonly Question[] Questions =
    [
        new("quieter-form", "Black metal's distorted guitar turning acoustic or clean in its quieter sections: a calm between the storms, or a jump?",
            "The quieter form's chances by genre.", "blackMetal", [], new() { ["louderForms"] = new() { ["quieter"] = 0 } },
            x => x.Trace.Where(t => t.Point == TracePoints.QuieterForm).Select(t => x.Song.Notes!.GetValueOrDefault(t.Track)?
                .Where(n => n.Value.State.GetStateValue(CompositionStateKinds.Program) == (int)t.Value! + 1).Select(n => (double?)n.Position).FirstOrDefault()).Min()),
        new("rock-key-lift", "Rock's last section lifted a key: this rock song's climb, or a pop ballad's?",
            "Rock's chance of lifting its last section.", "rock", [], new() { ["keyChanges"] = new() { ["lastSection"] = 0 } }, x => Before(x, FirstKeyChange(x), 2)),
        new("funk-key", "A funk vamp moving key, a lifted last section or new keys every few bars: still the groove, or a song wandering?",
            "Funk's key changes, now the songs' own.", "funk", [], new() { ["keyChanges"] = new() { ["lastSection"] = 0, ["sections"] = 0, ["patterns"] = 0 } },
            x => Before(x, FirstKeyChange(x), 2)),
        new("funk-pushes", "Funk's band landing an eighth early on the change, the kick and the bass off the downbeat: funk, or the one lost?",
            "Funk's push chance, the most of any preset.", "funk", [], new() { ["push"] = new() { ["sections"] = 0 } },
            x => Before(x, FirstSection(x, TracePoints.Push, v => (bool)v!), 2)),
        new("djent-polymeter", "Djent's riff cycling across the bar: the style's sound in nearly every section, or wanting square sections between?",
            "Djent's polymeter chance, 0.6 at the plain end.", "djent", [], new() { ["polymeter"] = new() { ["sections"] = 0 } },
            x => ((ImmutableDictionary<int, Polymeter.Plan>)x.Trace.Single(t => t.Point == TracePoints.Polymeter).Value!).Keys
                .Select(id => x.Map.Sections.Where(s => s.SectionId == id).Select(s => (double?)s.Start).Min()).Min()),
        new("numetal-half-time", "Nu metal's drums in half time, the snare on three: the style's heavy sit, or a drag?",
            "Nu metal's half-time chance, 30 to 40 now.", "nuMetal", [], new() { ["feelChances"] = new() { ["halfTime"] = 10 } },
            x => Before(x, FirstSection(x, TracePoints.TimeFeel, v => (int)v! == 1), 2)),
        new("techno-feel", "A techno section whose clap falls to half or double time: a deliberate shift, or the groove losing its drive?",
            "Techno's half and double time.", "techno", [], new() { ["feelChances"] = new() { ["halfTime"] = 0, ["doubleTime"] = 0 } },
            x => Before(x, FirstSection(x, TracePoints.TimeFeel, v => (int)v! != 0), 2)),
        new("disco-pump", "The pads and bass pumping on every beat: disco, or house?",
            "Disco's pump among its strong effects.", "disco", [], new() { ["strongEffect"] = new() { ["pump"] = 0 } },
            x => x.Trace.Single(t => t.Point == TracePoints.SongEffects).Value is SongEffects { Strong: StrongEffect.Pump } ? 0 : null),
        new("metal-turnaround", "The chord leading a metal section back to its start: this song's harmony, or a pop or jazz turnaround?",
            "Metal's turnaround, now the songs' own.", "metal", [], new() { ["turnaround"] = new() { ["sections"] = 0 } },
            x => FirstSection(x, TracePoints.Turnaround, v => (bool)v!)),
        new("medieval-raise", "The raised, major chord before home at a phrase's end: medieval, or a later classical cadence?",
            "Medieval's cadence raise, now the songs' own.", "medieval", [], new() { ["cadenceRaise"] = new() { ["sections"] = 0 } },
            x => FirstSection(x, TracePoints.CadenceRaise, v => (((int?, bool))v!).Item2)),
        new("ambient-swing", "An ambient piece's arpeggios swung long-short: right, or should they float evenly?",
            "Ambient's swing, now the songs' own.", "ambient", [], new() { ["feelChances"] = new() { ["swing"] = 0 } },
            x => ((Swing)x.Trace.Single(t => t.Point == TracePoints.Swing).Value!).Delay > 0 ? 0 : null),
        new("trap-reharm", "The last hook's loop changing its chords: this trap song, or its beat lost?",
            "Trap's reharmonisation, now the songs' own.", "trap", [], new() { ["harmonyDevices"] = new() { ["reharmonised"] = 0 } },
            x => x.Trace.Where(t => t.Point == TracePoints.Reharmonisation && t.Value is not null)
                .Select(t => ((ValueTuple<int, ImmutableArray<double>>)t.Value!).Item2.Select(p => (double?)p).Min()).Min() is { } at ? Before(x, at, 2) : null)
    ];

    /// <summary>Where the song first changes key.</summary>
    private static double? FirstKeyChange(CorpusSong song) =>
        ((ImmutableArray<KeyChange>)song.Trace.Single(t => t.Point == TracePoints.KeyChange).Value!).Select(x => (double?)x.Position).Min();

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
        var without = SongGenerator.GenerateSong(TestCorpus.SongSeed(seed), new SongOverrides(Genre: GenreOf(question, true)), question.Skip.ToImmutableHashSet(), CancellationToken.None);
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
        foreach (var question in Questions)
        {
            await Assert.That(question.Skip.All(SongGenerator.SkippableStages.Contains)).IsTrue().Because(question.Id);
            // its verdicts kept whole, the longest choice after its id
            await Assert.That($"{question.Id}:without".Length).IsLessThanOrEqualTo(Rmg.WebApi.Analytics.AnalyticsEndpoints.DetailLength).Because(question.Id);
            // and a verdict the server takes
            await Assert.That(Rmg.WebApi.Analytics.AnalyticsEndpoints.IsVerdict($"{question.Id}:with")).IsTrue().Because(question.Id);
        }

        var path = QuestionsFile();
        using var file = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var ids = file.RootElement.EnumerateArray().Select(x => x.GetProperty("id").GetString()).ToImmutableHashSet();
        await Assert.That(ids.SetEquals(Questions.Select(x => x.Id))).IsTrue().Because("listening.json is written again by Write");
        foreach (var question in file.RootElement.EnumerateArray())
            await Assert.That(question.GetProperty("seeds").GetArrayLength()).IsGreaterThan(0).Because(question.GetProperty("id").GetString()!);
    }
}
