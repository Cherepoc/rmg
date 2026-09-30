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
            .Select(_ => DrumRoles.DrawSection(context, Spread, DrumRole.Ground, 1))
            .First(x => !x.IsDefault);
        var role = (DrumRole)changed.GetStateValue(CompositionStateKinds.DrumRole).Value;
        var rhythm = DrumRoles.Parts[DrumRole.Ground].ToStateMap().MergeWith(changed);

        await Assert.That(changed.GetStateValue(CompositionStateKinds.DrumRole).Depth).IsEqualTo(StateDepths.Section);
        await Assert.That(rhythm.Except([CompositionStateKinds.DrumRole])).IsEqualTo(DrumRoles.Parts[role].ToStateMap());
    }

    [Test]
    public async Task APlainSongsDrum_PlaysItsMainRole_AndAWildSong_ReachesForTheOtherRolesMore()
    {
        double OffRoles(double unconventionality)
        {
            var context = new GenerationContext(1);
            return Enumerable.Range(0, 4_000)
                .Count(_ => (DrumRole)DrumRoles.GenerateSong(context, Spread, unconventionality)
                    .GetStateValue(CompositionStateKinds.DrumRole).Value != DrumRole.Ground) / 4_000.0;
        }

        // the plainest song's kick plays its main role, every time
        var context = new GenerationContext(1);
        var kicks = Enumerable.Range(0, 200).Select(_ => (DrumRole)DrumRoles.GenerateSong(context, DrumDefinitions.Kick, 0).GetStateValue(CompositionStateKinds.DrumRole).Value);
        await Assert.That(kicks.All(x => x == DrumRole.Ground)).IsTrue();
        await Assert.That(OffRoles(1)).IsGreaterThan(OffRoles(0) * 3);
    }
}
