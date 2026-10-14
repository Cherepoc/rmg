using Rmg.Core.Composition;

namespace Rmg.Tests.Genres;

/// <summary>A song's mood leaning its genre's options (<see cref="Mood.Leans" />): the chord shapes by their darkness, the figures by how a loud section leans them.</summary>
public sealed class MoodLeansTest
{
    /// <summary>An option's weight over another's in a genre at a mood, at the middle conventionality.</summary>
    private static double Ratio(Genre genre, Mood mood, string choice, string option, string against)
    {
        var options = genre.AtMood(mood).Choices[choice];
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

    [Test]
    public async Task AFierceMood_LeansToALoudSectionsFigures_ACalmOneAway()
    {
        var metal = GenrePresets.Genre("metal");
        var (fierce, calm) = (new Mood(0, 1), new Mood(0, -1));
        await Assert.That(Ratio(metal, fierce, "figure.ground", "doubleKick", Figures.Own)).IsGreaterThan(Ratio(metal, calm, "figure.ground", "doubleKick", Figures.Own));
    }
}
