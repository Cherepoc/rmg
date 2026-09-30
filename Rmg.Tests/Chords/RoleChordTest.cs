using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.Chords;

public sealed class RoleChordTest
{
    private const int DrawCount = 5_000;

    private static readonly string[] CadenceShapeNames = ["Seventh", "Seven-sus4", "Sus4", "Triad", "Ninth", "Nine-sus4"];

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
    [Arguments(0.5)]
    public async Task HomeChords_StayPlain_ButInTheWildestSongs(double chords)
    {
        var unconventionality = new HarmonicUnconventionality(chords);
        var context = new GenerationContext(1);

        var levels = Enumerable.Range(0, DrawCount)
            .Select(_ => ShapeOf(unconventionality.GenerateHomeChord(context)).Unconventionality)
            .ToArray();

        await Assert.That(levels.Max()).IsLessThanOrEqualTo(1);
    }

    [Test]
    public async Task TheWildestHomeChords_AreStrange()
    {
        var unconventionality = new HarmonicUnconventionality(1);
        var context = new GenerationContext(1);

        var levels = Enumerable.Range(0, DrawCount).Select(_ => unconventionality.GenerateHomeChord(context).Shape.Unconventionality).ToArray();

        await Assert.That(levels.All(x => x >= 3)).IsTrue();
    }

    [Test]
    public async Task ThePlainestCadences_AreTriads_AndMiddlingOnes_CadenceShapes_TheSeventhMostOften()
    {
        var context = new GenerationContext(1);
        var plainest = Enumerable.Range(0, DrawCount).Select(_ => new HarmonicUnconventionality(0).GenerateCadenceChord(context).Shape.Name).ToArray();
        var middling = Enumerable.Range(0, DrawCount)
            .Select(_ => new HarmonicUnconventionality(0.5).GenerateCadenceChord(context).Shape)
            .Where(x => x.Unconventionality <= 2)
            .Select(x => x.Name)
            .ToArray();

        await Assert.That(plainest.All(x => x == "Triad")).IsTrue();
        await Assert.That(middling.All(CadenceShapeNames.Contains)).IsTrue();
        await Assert.That(middling.CountBy(x => x).MaxBy(x => x.Value).Key).IsEqualTo("Seventh");
    }

    [Test]
    public async Task UnconventionalCadences_KeepTheirStrangeness()
    {
        var unconventionality = new HarmonicUnconventionality(1);
        var context = new GenerationContext(1);

        var levels = Enumerable.Range(0, DrawCount)
            .Select(_ => unconventionality.GenerateCadenceChord(context).Shape.Unconventionality)
            .ToArray();

        await Assert.That(levels.All(x => x >= 3)).IsTrue();
    }

    [Test]
    public async Task Songs_PlayRoleChordsInTheHomeAndCadenceChords_AndPoolChordsBetween()
    {
        var entries = 0;
        foreach (var song in TestCorpus.Range(8))
        {
            var rhythms = song.Trace.Where(x => x.Point == TracePoints.HarmonicRhythm).ToDictionary(x => x.Section, x => (HarmonicRhythm)x.Value!);
            foreach (var entry in song.Trace.Where(x => x.Point == TracePoints.Chord))
            {
                entries++;
                // the note's place in the pattern: its bar's start and its place in the bar
                var rhythm = rhythms[entry.Section];
                var chord = rhythm.IndexAt(entry.Bar % Rmg.Core.Composition.Progressions.BarCount * rhythm.Meter.BarDuration + entry.Position);
                var hasRoleChord = !entry.StateMap.GetStateValue(CompositionStateKinds.RoleChord).IsEmpty;
                await Assert.That(hasRoleChord).IsEqualTo(chord == 0 || chord == rhythm.Count - 1).Because($"bar {entry.Bar}, chord {chord} of {rhythm.Count}");
            }
        }

        await Assert.That(entries).IsGreaterThan(0);
    }
}
