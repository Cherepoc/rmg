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
        new("idm-key", "IDM changing key as the songs do, now and then its last section lifted a step: a turn its electronic neighbours never take, or a lift?",
            "IDM's key changes.", "idm", [], new() { ["keyChanges"] = new() { ["lastSection"] = 0, ["sections"] = 0, ["patterns"] = 0 } },
            x => Before(x, LastKeyChange(x), 1)),
        new("fusion-lift", "Fusion's last section lifted a key up, a pop ballad's gesture: a climax, or not what a fusion head does when it returns?",
            "Fusion's last section's lift.", "fusion", [], new() { ["keyChanges"] = new() { ["lastSection"] = 0 } },
            x => Before(x, LastKeyChange(x), 1)),
        new("techno-comp", "Techno's rhythm part, a clavinet, an electric piano or a muted guitar comping: a texture of its own, or a funk band in the club?",
            "Whether Techno plays a rhythm part.", "techno", [], new() { ["parts"] = new() { ["rhythm"] = 0 } },
            x => x.Absent.Contains(TrackRole.Rhythm) ? null : FirstSection(x, TracePoints.Arrangement, v => ((ImmutableHashSet<TrackRole>)v!).Contains(TrackRole.Rhythm))),
        new("techno-bass", "Techno's bass rolling, three 16ths after every kick, or on the 8th between the kicks: the club's bass line, or busy?",
            "The rolling and offbeat bass in Techno.", "techno", [], new() { ["figure.bass"] = new() { ["rolling"] = 0, ["offbeat"] = 0 } },
            x => FirstSection(x, TracePoints.Figures, v => ((ImmutableDictionary<FigurePlayer, string>)v!).ContainsKey(FigurePlayer.Bass))),
        new("djent-colour", "Djent's broken chords coloured, a major seventh, a flat second, a sharp fourth or a ninth laid on them: the genre's tension, or out of tune?",
            "The colours' weight in Djent.", "djent", [], new() { ["colour"] = new() { ["majorSeventh"] = 0, ["flatSecond"] = 0, ["sharpFourth"] = 0, ["ninth"] = 0 } },
            x => FirstColoured(x, colour => colour != ChordColour.None)),
        new("metal-violin", "Metal's melody on a violin, as a folk or symphonic metal band's: a colour, or out of place in a metal band?",
            "The violin's weight in Metal's melody.", "metal", [], new() { ["instrument.melody"] = new() { ["violin"] = 0 } },
            x => x.Song.TrackDefinitions[x.Track(TrackRole.Melody)] is PitchInstrumentTrack { InstrumentCode: var code } && code == GeneralMidi.Programs.IndexOf("Violin")
                ? x.Song.Notes!.GetValueOrDefault(x.Track(TrackRole.Melody))?.Select(n => (double?)n.Position).FirstOrDefault() : null),
        new("bossa-root-fifth", "Bossa nova's bass alternating root and fifth on its strong beats, as a surdo does, rather than walking into its chords: the style, or plain?",
            "The root and fifth's chance in Bossa nova.", "bossa", [], new() { ["alternatingBass"] = new() { ["sections"] = 0 } },
            x => FirstSection(x, TracePoints.AlternatingBass, v => (bool)v!)),
        new("chiptune-octave", "Chiptune's bass bouncing in octaves, the sound chip's commonest bass: the console's sound, or a gimmick?",
            "The octave bass's chance in Chiptune.", "chiptune", [], new() { ["octaveBass"] = new() { ["sections"] = 0 } },
            x => FirstSection(x, TracePoints.OctaveBass, v => (bool)v!)),
        new("punk-riff-intro", "A Punk song opening on its guitar riff alone, the band crashing in after it: a punk opening, or a slow start?",
            "The riff intro's chance in Punk.", "punk", [], new() { ["riffIntro"] = new() { ["songs"] = 0 } },
            x => x.Map.Intro is { Kind: IntroKind.Entries, Entries: [{ Part.Role: TrackRole.Riff, Entry: 0 }, ..] } ? 0 : null),
        new("orchestral-switch", "Orchestral's theme handed to another instrument when its section returns: orchestration, or a part that changes its mind?",
            "Instrument switches' chance in Orchestral.", "orchestral", [], new() { ["instrumentSwitches"] = new() { ["sections"] = 0 } },
            x => Before(x, x.Trace.Where(t => t.Point == TracePoints.InstrumentOverTime && x.Song.TrackDefinitions[t.Track] is PitchInstrumentTrack part
                    && (((int, TrackRole, int, ArticulationMode, int))t.Value!).Item3 != part.InstrumentCode)
                .Select(t => x.Map.Sections.Where(s => s.SectionId == t.Section).Select(s => (double?)s.Start).Min()).Min(), 1))
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

    /// <summary>How far apart the places to listen from are tried, in seconds.</summary>
    private const double Step = 2;

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
    }
}
