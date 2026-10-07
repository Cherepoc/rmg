using System.Collections.Immutable;
using System.Text.Encodings.Web;
using System.Text.Json;
using Rmg.Core;
using Rmg.Core.Composition;
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
    /// <param name="GenrePatch">The choices the song without the device lays over its genre's, by choice and option.</param>
    /// <param name="Plays">Whether the song plays the device, as its trace and notes have it.</param>
    private sealed record Question(
        string Id,
        string Text,
        string Decides,
        string? Genre,
        string[] Skip,
        Dictionary<string, Dictionary<string, int>>? GenrePatch,
        Func<CorpusSong, bool> Plays
    );

    private static bool Any(CorpusSong song, string point, Func<object?, bool> plays) => song.Trace.Any(x => x.Point == point && plays(x.Value));

    private static bool Plays(CorpusSong song, FigurePlayer player, string figure) =>
        Any(song, TracePoints.Figures, v => ((ImmutableDictionary<FigurePlayer, string>)v!).GetValueOrDefault(player) == figure);

    private static readonly Question[] Questions =
    [
        new("stop-time", "Stop time, the band hitting once a bar under the tune: a band's device, or a hole?",
            "Its chance and where it plays.", "blues", ["Stop time"], null, x => Any(x, TracePoints.StopTime, v => (bool)v!)),
        new("pushes", "Pushes, the band's chord changes an eighth early: a lift, or a stumble?",
            "Its chance by genre.", "rock", ["Pushes"], null, x => Any(x, TracePoints.Push, v => (bool)v!)),
        new("drum-marks", "The drums marking the form, a strike every few bars and a swell into a section: a calm piece's percussion, or a groove gone missing?",
            "How often the marks strike.", "ambient", ["Drum marks"], null, x => (DrumJob)x.Trace.Single(t => t.Point == TracePoints.DrumJob).Value! == DrumJob.Marks),
        new("louder-forms", "A clean guitar turning distorted in the loud sections: a band kicking in, or a jump?",
            "Its chance by genre.", "rock", ["Louder forms"], null, x => Any(x, TracePoints.LouderForm, _ => true)),
        new("ornaments", "The lines' ornaments, graces, turns and trills: a folk player's, or a warble?",
            "How often a line is ornamented.", "medieval", ["Ornaments"], null,
            x => x.Song.Notes!.Values.Any(n => n.Any(y => !y.Value.State.GetStateValue(CompositionStateKinds.Ornament).IsEmpty))),
        new("pocket", "The band's pocket, the backbeat a little late, the bass a touch ahead: played, or sloppy?",
            "How far the parts play off the beat.", null, ["Timing"], null, _ => true),
        new("solo-answers", "The solo part answering in the melody's gaps: a guitarist between a singer's phrases, or clutter?",
            "Its chance.", null, ["Solo answers"], null, x => Any(x, TracePoints.SoloAnswers, v => (((int, bool Answers))v!).Answers)),
        new("lift-cues", "The drums' cues into a bigger section, the hi-hat opening and the snare on its head: a drummer's, or predictable?",
            "Whether the cues stay, and how often.", null, ["Lift cues"], null, x => Any(x, TracePoints.LiftCue, _ => true)),
        new("doubling", "A line doubled, in octaves, harmonised, the bass with the riff: fuller, or muddy?",
            "Its chances.", "rock", ["Doubling"], null, x => Any(x, TracePoints.LineDoubling, _ => true)),
        new("kick-lock", "The kick striking with the riff, a riff in 16ths a double kick: a groove band's break, or a machine gun?",
            "Its weight by genre, and whether it follows the riff's every note.", "nuMetal", ["Figures"], null, x => Plays(x, FigurePlayer.Ground, "withRiff")),
        new("double-kick", "A double kick in the loud sections, the kick in 16ths: thrash, or a drum machine?",
            "Its weight, and its lean to the loud sections.", "metal", ["Figures"], null, x => Plays(x, FigurePlayer.Ground, "doubleKick")),
        new("gallop", "The riff's gallop, an 8th and two 16ths a beat: a heavy band's, or a stumble?",
            "Its weight in Metal.", "metal", ["Figures"], null, x => Plays(x, FigurePlayer.Riff, "gallop")),
        new("clave", "The claves on the clave's three and two: Latin's, or the claves' own rhythm better?",
            "Its weight in Latin.", "latin", ["Figures"], null, x => Plays(x, FigurePlayer.Claves, "clave32") || Plays(x, FigurePlayer.Claves, "clave23")),
        new("tremolo", "The riff tremolo-picked, every note in 16ths: black metal's, or a buzz?",
            "Its weight in Black metal.", "blackMetal", ["Figures"], null, x => Plays(x, FigurePlayer.Riff, "tremolo")),
        new("blast", "A blast beat, the kick and the snare on every 8th: black metal's drive, or a wall of noise?",
            "Its weight and its lean to the loud sections.", "blackMetal", ["Figures"], null, x => Plays(x, FigurePlayer.Ground, "blast")),
        new("breakbeat", "A breakbeat's broken kick and snare: an electronica's dusty break, or a stumble?",
            "Its weight in IDM, and a genre's breakbeats elsewhere.", "idm", ["Figures"], null, x => Plays(x, FigurePlayer.Ground, "breakbeat")),
        new("echo", "The echo, the synthesizer's delay repeating the rhythm part and the snare: dub's, or a smear?",
            "The delay's level, feedback and darkness, and the echo's weight by genre.", "reggae", [],
            new() { ["strongEffect"] = new() { ["none"] = 100 } },
            x => ((SongEffects)x.Trace.Single(t => t.Point == TracePoints.SongEffects).Value!).Strong == StrongEffect.Echo),
        new("metal-arpeggio", "A guitar breaking its chord, a pedal point or upwards, a ninth or a menacing tone laid on it: metal's, or out of place?",
            "Its patterns, its pace and its colours in Metal.", "metal", [], new() { ["arpeggio"] = new() { ["none"] = 100 } },
            x => x.Song.Notes!.Values.Any(n => n.Any(v => v.Value.State.GetStateValue(CompositionStateKinds.Arpeggio) == (int)ArpeggioPattern.Pedal))),
        new("djent-arpeggio", "Djent's clean arpeggios, mostly dissonant, over and between the riffs: atmosphere, or wrong notes?",
            "Its colours' weights in Djent.", "djent", [], new() { ["colour"] = new() { ["none"] = 100 } },
            x => x.Song.Notes!.Values.Any(n => n.Any(v => v.Value.State.GetStateValue(CompositionStateKinds.Colour) != 0))),
        new("djent-tension", "Djent's slow synth line over the guitars, its strong beats on a tension against the chord: a menacing glow, or a wrong note?",
            "Its tension chances in Djent, the counter-melody's and the melody's.", "djent", [], new() { ["tension"] = new() { ["counterMelody"] = 0, ["melody"] = 0 } },
            x => x.Song.Notes!.Values.Any(n => n.Any(v => v.Value.State.GetStateValue(CompositionStateKinds.LineTension) > 0)) && x.Song.TrackDefinitions.Values.Any(t => t.Role == TrackRole.CounterMelody)),
        new("tone", "Jazz with some parts in a darker tone, a mellow guitar, a dull bass, darker in the quiet sections: warmer, or muffled?",
            "The tone's chances in the presets that give it.", "jazz", [], new() { ["effects"] = new() { ["tone"] = 0 } },
            x => x.Song.Notes!.Values.Any(n => n.Any(v => v.Value.State.GetStateValue(CompositionStateKinds.Tone) > 0))),
        new("tape", "IDM off a worn tape, its parts' pitch drifting together by a few cents, some held a few cents off: warm, or out of tune?",
            "The tape's chances in IDM, Hip hop, Ambient and Synthwave, and its wow's depth.", "idm", [], new() { ["effects"] = new() { ["tape"] = 0 } },
            x => x.Song.Notes!.Values.Any(n => n.Any(v => v.Value.State.GetStateValue(CompositionStateKinds.Wow) > 0))),
        new("mix-filter", "Trance's whole mix filtered, opening up from muffled over its intro, dipping before a louder section, closing at the end: a build, or a muffled mix?",
            "The mix filter's chances in the dance genres, and its depths.", "trance", ["Mix filter"], null,
            x => ((MixFilterPlan)x.Trace.Single(t => t.Point == TracePoints.MixFilter).Value!) is { Opening: true } or { Dips.IsEmpty: false }),
        new("gate", "The trance gate, a held chord chopped in 16ths: the trance sound, or a stutter?",
            "Its weight in Trance, and the parts it chops.", "trance", [],
            new() { ["strongEffect"] = new() { ["none"] = 100 } },
            x => ((SongEffects)x.Trace.Single(t => t.Point == TracePoints.SongEffects).Value!).Strong == StrongEffect.Gate)
    ];

    /// <summary>The page's questions file, in the repository the tests run from.</summary>
    private static string QuestionsFile()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "Rmg.WebApi", "Client")))
            directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("No repository above the tests."), "Rmg.WebApi", "Client", "listening.json");
    }

    /// <summary>Writes the questions, each with the first of the corpus's seeds that play its device, where the page reads them.</summary>
    [Test]
    [Explicit]
    public async Task Write()
    {
        var written = Questions.Select(question =>
        {
            var genre = question.Genre is null ? null : GenrePresets.Genre(question.Genre);
            var seeds = TestCorpus.InParallel(Enumerable.Range(0, 128), seed => question.Plays(TestCorpus.Get(seed, new SongOverrides(Genre: genre))) ? seed : -1)
                .Where(x => x >= 0).Take(Seeds).Select(x => Base62.FromSeed((ulong)x)).ToArray();
            Console.WriteLine($"{question.Id}: {seeds.Length} seeds");
            return new
            {
                id = question.Id,
                question = question.Text,
                decides = question.Decides,
                genre = question.Genre,
                without = new { skip = question.Skip, genre = question.GenrePatch ?? [] },
                seeds
            };
        }).ToArray();

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
