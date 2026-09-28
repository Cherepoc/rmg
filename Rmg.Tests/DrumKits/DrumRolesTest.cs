using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.DrumKits;

public sealed class DrumRolesTest
{
    private static readonly PercussionInstrumentDefinition Spread = new(
        "Spread",
        [60],
        1,
        roles: [new(1, DrumRole.Ground), new(0.3, DrumRole.Time), new(0.3, DrumRole.Backbeat)]
    );

    [Test]
    public async Task ASectionsRole_AddsTheDifferenceOfItsPart_SoTheRhythmIsTheNewRolesAlone()
    {
        var context = new GenerationContext(1);
        var changed = Enumerable.Range(0, 2_000)
            .Select(_ => DrumRoles.DrawSection(context, Spread, DrumRole.Ground, new RhythmicUnconventionality(1).Tilt))
            .First(x => !x.IsDefault);
        var role = (DrumRole)changed.GetStateValue(CompositionStateKinds.DrumRole).Value;
        var rhythm = DrumRoles.Parts[DrumRole.Ground].ToStateMap().MergeWith(changed);

        await Assert.That(changed.GetStateValue(CompositionStateKinds.DrumRole).Depth).IsEqualTo(StateDepths.Section);
        await Assert.That(rhythm.Except([CompositionStateKinds.DrumRole])).IsEqualTo(DrumRoles.Parts[role].ToStateMap());
    }

    [Test]
    public async Task ADrumOfOneRole_AlwaysPlaysIt_AndAWildSong_ReachesForTheOtherRolesMore()
    {
        double OffRoles(double unconventionality)
        {
            var context = new GenerationContext(1);
            return Enumerable.Range(0, 4_000)
                .Count(_ => (DrumRole)DrumRoles.GenerateSong(context, Spread, new RhythmicUnconventionality(unconventionality).Tilt)
                    .GetStateValue(CompositionStateKinds.DrumRole).Value != DrumRole.Ground) / 4_000.0;
        }

        var kick = DrumRoles.GenerateSong(new GenerationContext(1), DrumDefinitions.Kick, new RhythmicUnconventionality(1).Tilt);
        await Assert.That((DrumRole)kick.GetStateValue(CompositionStateKinds.DrumRole).Value).IsEqualTo(DrumRole.Ground);
        await Assert.That(OffRoles(1)).IsGreaterThan(OffRoles(0) * 3);
    }
}
