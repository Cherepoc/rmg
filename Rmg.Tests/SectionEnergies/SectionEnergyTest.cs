using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.SectionEnergies;

public sealed class SectionEnergyTest
{
    private static double Explained(StateMap stateMap, string layer) =>
        stateMap.Explain(CompositionStateKinds.Energy).Where(x => x.Layer == layer).Sum(x => (double)x.Value);

    private static Dictionary<int, StateMap> Traced(params int[] sectionIds)
    {
        using var trace = StateTrace.Start();
        return SectionEnergy.GenerateSections(new GenerationContext(1), sectionIds).ToDictionary();
    }

    [Test]
    public async Task TheSectionThatRecursMost_HasTheWholeRecurrenceStep()
    {
        // 0 plays twice, 1 four times, 2 once: the mean is 7/3, and 1 is furthest from it
        var energies = Traced(0, 1, 0, 1, 2, 1, 1);

        await Assert.That(Explained(energies[1], "Section recurrence")).IsEqualTo(SectionEnergy.Recurrence).Within(1e-9);
        await Assert.That(Explained(energies[0], "Section recurrence")).IsLessThan(0);
        await Assert.That(Explained(energies[2], "Section recurrence")).IsLessThan(Explained(energies[0], "Section recurrence"));
    }

    [Test]
    public async Task TheArc_RisesWithTheSectionsAveragePlace()
    {
        var energies = Traced(0, 1, 2, 3);

        await Assert.That(Explained(energies[0], "Section arc")).IsEqualTo(-SectionEnergy.Arc).Within(1e-9);
        await Assert.That(Explained(energies[3], "Section arc")).IsEqualTo(SectionEnergy.Arc).Within(1e-9);
        await Assert.That(Explained(energies[1], "Section arc")).IsLessThan(Explained(energies[2], "Section arc"));
    }

    [Test]
    public async Task SectionsThatRecurAlike_HaveNoRecurrenceStep()
    {
        var energies = Traced(0, 1, 0, 1);

        await Assert.That(Explained(energies[0], "Section recurrence")).IsEqualTo(0);
        await Assert.That(Explained(energies[1], "Section recurrence")).IsEqualTo(0);
    }

    [Test]
    public async Task APlainSection_FollowsItsEnergy_MoreThanAWildOne()
    {
        var plain = SectionEnergy.Tilt(0.5, new RhythmicUnconventionality(0).Coupling);
        var wild = SectionEnergy.Tilt(0.5, new RhythmicUnconventionality(1).Coupling);

        await Assert.That(plain.Odds).IsEqualTo(Math.Pow(SectionEnergy.HighOdds, 0.5)).Within(1e-9);
        await Assert.That(wild.Odds).IsGreaterThan(1);
        await Assert.That(wild.Odds).IsLessThan(plain.Odds);
    }
}
