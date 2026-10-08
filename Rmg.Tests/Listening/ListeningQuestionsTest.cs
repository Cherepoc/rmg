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
    private sealed record Question(
        string Id,
        string Text,
        string Decides,
        string? Genre,
        string[] Skip,
        Dictionary<string, Dictionary<string, int>>? GenrePatch,
        Func<CorpusSong, double?> At
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

    /// <summary>A place in the song in seconds from its start, through its tempo.</summary>
    private static double Seconds(CorpusSong song, double beat)
    {
        var tempo = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetStateTimeline(StateKinds.Tempo);
        var (seconds, at) = (0.0, 0.0);
        foreach (var change in tempo.Select(x => x.Position).Where(x => x > 0 && x < beat).Append(beat))
        {
            seconds += (change - at) * 60 / (tempo.GetEffectiveValueAt(at) * Meter.BaseTempo);
            at = change;
        }

        return seconds;
    }

    private static readonly Question[] Questions =
    [
        new("stop-time", "Stop time, the band hitting once a bar under the tune, the hi-hat going on where the tune rests: a band's device, or a hole?",
            "Its chance and where it plays.", "blues", ["Stop time"], null, x => FirstSection(x, TracePoints.StopTime, v => (bool)v!)),
        new("pushes", "Pushes, the band's chord changes an eighth early: a lift, or a stumble?",
            "Its chance by genre.", "rock", ["Pushes"], null, x => FirstSection(x, TracePoints.Push, v => (bool)v!)),
        new("drum-marks", "The drums marking the form, a strike every few bars and a swell into a section: a calm piece's percussion, or a groove gone missing?",
            "How often the marks strike.", "ambient", ["Drum marks"], null, x => (DrumJob)x.Trace.Single(t => t.Point == TracePoints.DrumJob).Value! == DrumJob.Marks ? x.Map.Origin : null),
        new("louder-forms", "A clean guitar turning distorted in the loud sections: a band kicking in, or a jump?",
            "Its chance by genre.", "rock", ["Louder forms"], null, x => x.Trace.Where(t => t.Point == TracePoints.LouderForm).Select(t => x.Song.Notes!.GetValueOrDefault(t.Track)?
                .Where(n => n.Value.State.GetStateValue(CompositionStateKinds.Program) == (int)t.Value! + 1).Select(n => (double?)n.Position).FirstOrDefault()).Min()),
        new("ornaments", "The lines' ornaments, graces, turns and trills: a folk player's, or a warble?",
            "How often a line is ornamented.", "medieval", ["Ornaments"], null,
            x => FirstNote(x, n => !n.State.GetStateValue(CompositionStateKinds.Ornament).IsEmpty)),
        new("pocket", "The band's pocket, the backbeat a little late, the bass a touch ahead: played, or sloppy?",
            "How far the parts play off the beat.", null, ["Timing"], null, x => x.Map.Origin),
        new("solo-answers", "The solo part answering in the melody's gaps: a guitarist between a singer's phrases, or clutter?",
            "Its chance.", null, ["Solo answers"], null, x => FirstSection(x, TracePoints.SoloAnswers, v => (((int, bool Answers))v!).Answers)),
        new("lift-cues", "The drums' cues into a bigger section, the hi-hat opening and the snare on its head: a drummer's, or predictable?",
            "Whether the cues stay, and how often.", null, ["Lift cues"], null, x => Before(x, x.Trace.Where(t => t.Point == TracePoints.LiftCue).Select(t => (double?)(double)t.Value!).Min(), 2)),
        new("doubling", "A line doubled, in octaves, harmonised, the bass with the riff: fuller, or muddy?",
            "Its chances.", "rock", ["Doubling"], null, x => FirstSection(x, TracePoints.LineDoubling, _ => true)),
        new("kick-lock", "The kick striking with the riff, a riff in 16ths a double kick: a groove band's break, or a machine gun?",
            "Its weight by genre, and whether it follows the riff's every note.", "nuMetal", ["Figures"], null, x => Plays(x, FigurePlayer.Ground, "withRiff")),
        new("double-kick", "A double kick in the loud sections, the kick in 16ths: thrash, or a drum machine?",
            "Its weight, and its lean to the loud sections.", "metal", ["Figures"], null, x => Plays(x, FigurePlayer.Ground, "doubleKick")),
        new("gallop", "The riff's gallop, an 8th and two 16ths a beat: a heavy band's, or a stumble?",
            "Its weight in Metal.", "metal", ["Figures"], null, x => Plays(x, FigurePlayer.Riff, "gallop")),
        new("clave", "The claves on the clave's three and two: Latin's, or the claves' own rhythm better?",
            "Its weight in Latin.", "latin", ["Figures"], null, x => Plays(x, FigurePlayer.Claves, "clave32") ?? Plays(x, FigurePlayer.Claves, "clave23")),
        new("tremolo", "The riff tremolo-picked, every note in 16ths: black metal's, or a buzz?",
            "Its weight in Black metal.", "blackMetal", ["Figures"], null, x => Plays(x, FigurePlayer.Riff, "tremolo")),
        new("blast", "A blast beat, the kick and the snare on every 8th: black metal's drive, or a wall of noise?",
            "Its weight and its lean to the loud sections.", "blackMetal", ["Figures"], null, x => Plays(x, FigurePlayer.Ground, "blast")),
        new("echo", "The echo, the synthesizer's delay repeating the rhythm part and the snare: dub's, or a smear?",
            "The delay's level, feedback and darkness, and the echo's weight by genre.", "reggae", [],
            new() { ["strongEffect"] = Only(GenreChoices.StrongEffect, "none") },
            x => ((SongEffects)x.Trace.Single(t => t.Point == TracePoints.SongEffects).Value!).Strong == StrongEffect.Echo ? FirstNote(x, n => n.State.GetStateValue(CompositionStateKinds.Echo) > 0) : null),
        new("metal-arpeggio", "A guitar breaking its chord, a pedal point or upwards, a ninth or a menacing tone laid on it: metal's, or out of place?",
            "Its patterns, its pace and its colours in Metal.", "metal", [], new() { ["arpeggio"] = Only(GenreChoices.Arpeggio, "none") },
            x => FirstNote(x, n => n.State.GetStateValue(CompositionStateKinds.Arpeggio) == (int)ArpeggioPattern.Pedal)),
        new("djent-arpeggio", "Djent's clean arpeggios, mostly dissonant, over and between the riffs: atmosphere, or wrong notes?",
            "Its colours' weights in Djent.", "djent", [], new() { ["colour"] = Only(GenreChoices.Colour, "none") },
            x => FirstNote(x, n => n.State.GetStateValue(CompositionStateKinds.Colour) != 0)),
        new("tone", "Jazz with some parts in a darker tone, a mellow guitar, a dull bass, darker in the quiet sections: warmer, or muffled?",
            "The tone's chances in the presets that give it.", "jazz", [], new() { ["effects"] = new() { ["tone"] = 0 } },
            x => FirstNote(x, n => n.State.GetStateValue(CompositionStateKinds.Tone) > 0)),
        new("mix-filter", "Trance's whole mix filtered, opening up from muffled over its intro, dipping before a louder section, closing at the end: a build, or a muffled mix?",
            "The mix filter's chances in the dance genres, and its depths.", "trance", ["Mix filter"], null,
            x => (MixFilterPlan)x.Trace.Single(t => t.Point == TracePoints.MixFilter).Value! switch { { Opening: true } => 0, { Dips.IsEmpty: false } and var plan => Before(x, plan.Dips[0], 3), _ => null }),
        new("rolling-bass", "Trance's bass rolling in the 16ths after the kick, or on the offbeats: the drive, or a machine gun?",
            "The bass's figures' weights in Trance.", "trance", [], new() { ["figure.bass"] = new() { ["rolling"] = 0, ["offbeat"] = 0 } },
            x => Plays(x, FigurePlayer.Bass, "rolling") ?? Plays(x, FigurePlayer.Bass, "offbeat")),
        new("ending", "Black metal ending as it played, planed or sliding home by ♭II, ♭VI or ♭VII (with), or by a classical V–i, its leading tone raised (without)?",
            "The chord into home and the raised leading tone, by genre.", "blackMetal", [],
            new() { ["cadenceDegree"] = new() { ["fifth"] = 100, ["second"] = 50, ["sixth"] = 50, ["seventh"] = 50 }, ["cadenceRaise"] = new() { ["sections"] = 100 } },
            x => Before(x, x.Map.Sections[^1].End, 4)),
        new("trade", "Jazz's solo trading with the drums, turn by turn, the band laying out for the drums: a jazz band's, or a hole?",
            "The trade's chance by genre.", "jazz", [], new() { ["solos"] = new() { ["trades"] = 0 } },
            x => x.Solos.Where(s => s.Value.TradesWithDrums).Select(s => (double?)x.Map.Sections[s.Key].Start).Min()),
        new("tape", "IDM off a worn tape, its parts' pitch drifting together by a few cents, its lines held a few cents off: warm, or out of tune?",
            "The tape's chances in IDM, Hip hop, Ambient and Synthwave, and its wow's depth.", "idm", [], new() { ["effects"] = new() { ["tape"] = 0 } },
            x => FirstNote(x, n => n.State.GetStateValue(CompositionStateKinds.Wow) > 0)),
        new("breakdown", "Djent's bridge as a breakdown, the drums in half time, the kick with the chugging riff, the melody and the pad out: heavy, or a hole?",
            "The breakdown's chances in the heavy genres.", "djent", [], new() { ["breakdown"] = new() { ["sections"] = 0 } },
            x => Before(x, FirstSection(x, TracePoints.Breakdown, v => (bool)v!), 2)),
        new("riff-intro", "Rock opening on its riff alone, the band coming in after it: a song's signature, or a thin start?",
            "The riff intro's chances in the riff-driven genres.", "rock", [], new() { ["riffIntro"] = new() { ["songs"] = 0 } },
            x => x.Map.Intro is { Kind: IntroKind.Entries, Entries: [{ Part.Role: TrackRole.Riff, Entry: 0 }, ..] } ? 0 : null),
        new("ghosts", "Funk's snare playing ghost notes, soft 16ths around its backbeat: a funk drummer's, or clutter?",
            "The ghost notes' chances by genre, how many and how soft.", "funk", ["Ghosts"], null, x => FirstSection(x, TracePoints.Ghosts, v => (bool)v!)),
        new("twin-solo", "Metal's solo played by two guitars together a third apart, the second on the held notes: a twin-guitar break, or a smear?",
            "The twins' chances by genre, and the second's notes.", "metal", [], new() { ["solos"] = new() { ["twins"] = 0 } },
            x => x.Solos.Where(s => s.Value is { Mode: SoloMode.Together, Soloists.Length: > 1 }).Select(s => (double?)x.Map.Sections[s.Key].Start).Min()),
        new("jazz-passing", "Jazz passing into its chords by the tritone substitute, a half-diminished chord a semitone below or a chromatic slide (with), or by their dominants alone (without): richer, or wrong?",
            "The passing chords' kinds by genre.", "jazz", [],
            new() { ["passingKind"] = new() { ["dominant"] = 100, ["tritone"] = 0, ["diminished"] = 0, ["chromatic"] = 0, ["chromaticBelow"] = 0 } },
            x => FirstSection(x, TracePoints.HarmonicRhythm, v => !((HarmonicRhythm)v!).Passing.IsEmpty)),
        new("root-fifth", "Country's bass alternating the chord's root and its fifth below on the strong beats: a country bass, or a plod?",
            "The alternating bass's chances by genre.", "country", [], new() { ["alternatingBass"] = new() { ["sections"] = 0 } },
            x => FirstSection(x, TracePoints.AlternatingBass, v => (bool)v!)),
        new("quieter-form", "Black metal's distorted guitar turning acoustic or clean in its quieter sections: a calm between the storms, or a jump?",
            "The quieter form's chances by genre.", "blackMetal", [], new() { ["louderForms"] = new() { ["quieter"] = 0 } },
            x => x.Trace.Where(t => t.Point == TracePoints.QuieterForm).Select(t => x.Song.Notes!.GetValueOrDefault(t.Track)?
                .Where(n => n.Value.State.GetStateValue(CompositionStateKinds.Program) == (int)t.Value! + 1).Select(n => (double?)n.Position).FirstOrDefault()).Min())
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
            var measured = TestCorpus.InParallel(Enumerable.Range(0, 128), seed =>
                {
                    var song = TestCorpus.Get(seed, new SongOverrides(Genre: genre));
                    return question.At(song) is { } at ? (Seed: seed, Measure: (ValueTuple<double, double, string[]>?)Measure(question, seed, song, Seconds(song, at))) : (seed, null);
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
                without = new { skip = question.Skip, genre = question.GenrePatch ?? [] },
                seeds = asked.Select(x => new { seed = Base62.FromSeed((ulong)x.Seed), at = Math.Round(x.At, 1), listen = x.Listen }).ToArray()
            });
        }

        var path = QuestionsFile();
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(written, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
    }

    /// <summary>Every question's stages ones a song may leave out, and every question with songs to hear.</summary>
    [Test]
    public async Task EveryQuestion_LeavesOutStagesThatMayBe_AndHasSongs()
    {
        foreach (var question in Questions)
            await Assert.That(question.Skip.All(SongGenerator.SkippableStages.Contains)).IsTrue().Because(question.Id);

        var path = QuestionsFile();
        using var file = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var ids = file.RootElement.EnumerateArray().Select(x => x.GetProperty("id").GetString()).ToImmutableHashSet();
        await Assert.That(ids.SetEquals(Questions.Select(x => x.Id))).IsTrue().Because("listening.json is written again by Write");
        foreach (var question in file.RootElement.EnumerateArray())
            await Assert.That(question.GetProperty("seeds").GetArrayLength()).IsGreaterThan(0).Because(question.GetProperty("id").GetString()!);
    }
}
