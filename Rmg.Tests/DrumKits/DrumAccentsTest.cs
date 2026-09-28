using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Tests.DrumKits;

public sealed class DrumAccentsTest
{
    private static double Share(DrumSound[] sounds, int rank, Tilt energy)
    {
        var context = new GenerationContext(1);
        return Enumerable.Range(0, 20_000).Count(_ => !DrumAccents.Draw(context, sounds, rank, energy).IsDefault) / 20_000.0;
    }

    [Test]
    public async Task TheOpenHiHat_AccentsTheWeakBeats_AndTheBell_TheStrong_AtANotesDepth()
    {
        var hiHat = DrumDefinitions.HiHat.Sounds.ToArray();
        var ride = DrumDefinitions.Ride.Sounds.ToArray();

        await Assert.That(Share(hiHat, 2, Tilt.None)).IsGreaterThan(Share(hiHat, 0, Tilt.None) * 4);
        await Assert.That(Share(ride, 0, Tilt.None)).IsGreaterThan(Share(ride, 2, Tilt.None) * 4);
        // the open hi-hat, the third sound, as the note's own stroke
        var context = new GenerationContext(1);
        var accent = Enumerable.Range(0, 1_000).Select(_ => DrumAccents.Draw(context, hiHat, 2, Tilt.None)).First(x => !x.IsDefault);
        await Assert.That(accent.GetStateValue(StateKinds.DrumStroke)).IsEqualTo(new LayerValue<int>(StateDepths.Note, 2));
    }

    [Test]
    public async Task ALoudSection_AccentsMore_ThanAQuietOne()
    {
        var hiHat = DrumDefinitions.HiHat.Sounds.ToArray();

        await Assert.That(Share(hiHat, 2, Tilt.Of(SectionEnergy.HighOdds, 0.5))).IsGreaterThan(Share(hiHat, 2, Tilt.Of(SectionEnergy.HighOdds, -0.5)));
    }
}
