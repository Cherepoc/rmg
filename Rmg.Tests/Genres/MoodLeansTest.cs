using Rmg.Core.Composition;

namespace Rmg.Tests.Genres;

/// <summary>A song's mood leaning its genre's options (<see cref="Mood.Leans" />): the chord shapes by their darkness, the figures by how a loud section leans them.</summary>
public sealed class MoodLeansTest
{
    /// <summary>An option's weight over another's in a genre at a mood, at the middle conventionality.</summary>
    private static double Ratio(Genre genre, Mood mood, string choice, string option, string against)
    {
        var options = genre.AtMood(mood, 0.5).Choices[choice];
        return options[option].Tuned / options[against].Tuned;
    }

    [Test]
    public async Task ADarkMood_LeansToTheDarkChordShapes_ABrightOneToTheBright()
    {
        var (dark, bright) = (new Mood(1, 0), new Mood(-1, 0));
        var jazz = GenrePresets.Genre("jazz");
        await Assert.That(Ratio(jazz, dark, "chordShape", "alteredDominant", "seventh")).IsGreaterThan(Ratio(jazz, bright, "chordShape", "alteredDominant", "seventh"));
        await Assert.That(Ratio(jazz, dark, "chordShape", "sixNine", "seventh")).IsLessThan(Ratio(jazz, bright, "chordShape", "sixNine", "seventh"));
    }

    /// <summary>
    ///     A genre given a mood past its span reaching the songs' own scales that lean its way, none leaning the other way;
    ///     inside its span its pool as it names it.
    /// </summary>
    [Test]
    public async Task AMoodPastAGenresSpan_ReachesTheSongsScalesLeaningItsWay()
    {
        var genre = GenrePresets.Genre("blackMetal");
        var own = genre.Choices[GenreChoices.Scale.Name].Where(x => x.Value.Tuned > 0).Select(x => x.Key).ToHashSet();
        var (from, to, _) = genre.Range(GenreChoices.Darkness, 0.5)!.Value;
        double DarknessOf(string name) => Rmg.Core.Composition.Scales.All.Single(x => GenreChoices.Name(x.Name) == name).Darkness;

        var inside = genre.AtMood(new Mood(Mood.OfAmount((from + to) / 2), 0), 0.5).Choices[GenreChoices.Scale.Name];
        await Assert.That(inside.Where(x => x.Value.Tuned > 0).Select(x => x.Key).ToHashSet().SetEquals(own)).IsTrue();
        var brightest = genre.AtMood(new Mood(-1, 0), 0.5).Choices[GenreChoices.Scale.Name].Where(x => x.Value.Tuned > 0).Select(x => x.Key).ToArray();
        await Assert.That(brightest).Contains("major");
        await Assert.That(brightest.Where(x => !own.Contains(x)).All(x => DarknessOf(x) < 0)).IsTrue();
    }

    [Test]
    public async Task AFierceMood_LeansToALoudSectionsFigures_ACalmOneAway()
    {
        var metal = GenrePresets.Genre("metal");
        var (fierce, calm) = (new Mood(0, 1), new Mood(0, -1));
        await Assert.That(Ratio(metal, fierce, "figure.ground", "doubleKick", Figures.Own)).IsGreaterThan(Ratio(metal, calm, "figure.ground", "doubleKick", Figures.Own));
    }

    /// <summary>A value leaned as a range's lean is: fills the more the fiercer, the songs' own amount too.</summary>
    [Test]
    public async Task AMood_LeansAValue_FillsByIntensity()
    {
        double Value(Mood mood, string choice, string option) => Genre.None.AtMood(mood, 0.5).Choices[choice][option].Tuned;
        await Assert.That(Value(new Mood(0, 1), "fills", "amount")).IsGreaterThan(Value(new Mood(0, -1), "fills", "amount"));
    }

    /// <summary>
    ///     A report: the same songs' drum fills at the calmest and the fiercest mood (measured 19, 30 and 44 hits a song), and
    ///     their melody's average pitch at the brightest and the darkest, which a register leaned in octaves moved too far.
    /// </summary>
    [Test]
    [Explicit]
    public async Task Report()
    {
        double Fills(double intensity) => TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Intensity: intensity)))
            .Average(x => x.Trace.Count(t => t.Point == TracePoints.Fill && t.Value is HitKind.Fill));
        double Pitch(double darkness) => TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Darkness: darkness)))
            .SelectMany(x => x.Song.Notes!.TryGetValue(x.Track(Rmg.Core.Songs.TrackRole.Melody), out var melody) ? melody.AsEnumerable() : []).Average(x => x.Value.Pitches[0]);
        Console.WriteLine($"fill hits a song: {Fills(-1):F0} calm, {Fills(0):F0} middling, {Fills(1):F0} fierce; the melody's pitch: {Pitch(-1):F1} bright, {Pitch(0):F1} middling, {Pitch(1):F1} dark");
        await Task.CompletedTask;
    }
}
