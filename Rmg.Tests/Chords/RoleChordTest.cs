using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.Chords;

public sealed class RoleChordTest
{
    private const int DrawCount = 5_000;

    private static readonly string[] CadenceShapeNames = ["Seventh", "Seven-sus4", "Sus4", "Triad", "Ninth"];

    /// <summary>The shape a chord is laid out from: voicings move notes by octaves, so the pitch classes tell.</summary>
    private static ChordShape ShapeOf(Chord chord)
    {
        static string Classes(IEnumerable<double> semitones) =>
            string.Join(",", semitones.Select(x => Math.Round(((x % 12) + 12) % 12, 3)).Distinct().Order());

        var classes = Classes(chord.Heights.Select(x => x * 12));
        return ChordShapes.All.First(x => Classes(x.Targets) == classes);
    }

    [Test]
    [Arguments(0.0)]
    [Arguments(2.5)]
    [Arguments(5.0)]
    public async Task HomeChords_StayPlain(double anchor)
    {
        var unconventionality = new HarmonicUnconventionality(anchor, 2, 0, 1);
        var context = new GenerationContext(1);

        var levels = Enumerable.Range(0, DrawCount)
            .Select(_ => ShapeOf(unconventionality.GenerateHomeChord(context)).Unconventionality)
            .ToArray();

        await Assert.That(levels.Max()).IsLessThanOrEqualTo(HarmonicUnconventionality.HomeChordMaxUnconventionality);
    }

    [Test]
    public async Task ConventionalCadences_PlayCadenceShapes_TheSeventhMostOften()
    {
        var unconventionality = new HarmonicUnconventionality(0, 0.5, 1, 1);
        var context = new GenerationContext(1);

        var names = Enumerable.Range(0, DrawCount)
            .Select(_ => ShapeOf(unconventionality.GenerateCadenceChord(context)).Name)
            .ToArray();

        await Assert.That(names.All(CadenceShapeNames.Contains)).IsTrue();
        await Assert.That(names.CountBy(x => x).MaxBy(x => x.Value).Key).IsEqualTo("Seventh");
    }

    [Test]
    public async Task UnconventionalCadences_KeepTheirStrangeness()
    {
        var unconventionality = new HarmonicUnconventionality(4.5, 0.3, 1, 1);
        var context = new GenerationContext(1);

        var levels = Enumerable.Range(0, DrawCount)
            .Select(_ => ShapeOf(unconventionality.GenerateCadenceChord(context)).Unconventionality)
            .ToArray();

        await Assert.That(levels.All(x => x > HarmonicUnconventionality.CadenceShapeMaxUnconventionality)).IsTrue();
    }

    [Test]
    public async Task Songs_PlayRoleChordsInTheHomeAndCadenceBars_AndPoolChordsBetween()
    {
        using var trace = StateTrace.Start();
        for (var seed = 0; seed < 10; seed++)
            SongGenerator.GenerateSong(seed);

        var chordEntries = trace.Entries.Where(x => x.Point == "Chord").ToArray();

        await Assert.That(chordEntries.Length).IsGreaterThan(0);
        foreach (var entry in chordEntries)
        {
            var hasRoleChord = !entry.StateMap.GetStateValue(CompositionStateKinds.RoleChord).IsEmpty;
            await Assert.That(hasRoleChord).IsEqualTo(entry.Bar is 0 or 3).Because($"bar {entry.Bar}");
        }
    }
}
