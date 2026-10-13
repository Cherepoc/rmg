using Rmg.Core.Composition;

namespace Rmg.Tests.Chords;

/// <summary>The alternating thumb: the root and the fifth on the beats by turns, the fingers above them off the beats.</summary>
public sealed class AlternatingThumbTest
{
    [Test]
    public async Task TheThumb_AlternatesRootAndFifthOnTheBeats_TheFingersPickAboveOffThem()
    {
        // a C major chord voiced C3 G3 C4 E4
        int[] voiced = [48, 55, 60, 64];
        int At(int place, double position, bool onBeat) => Arpeggios.Pick(ArpeggioPattern.AlternatingThumb, [..voiced], place, position, onBeat, 0, 0);

        await Assert.That(At(0, 0, true)).IsEqualTo(48);
        await Assert.That(At(2, 1, true)).IsEqualTo(55);
        await Assert.That(At(4, 2, true)).IsEqualTo(48);
        await Assert.That(At(6, 3, true)).IsEqualTo(55);
        // off the beats the notes above the fifth, by turns
        int[] fingers = [At(1, 0.5, false), At(3, 1.5, false), At(5, 2.5, false), At(7, 3.5, false)];
        await Assert.That(fingers.All(x => x > 55)).IsTrue();
        await Assert.That(fingers.Distinct().Count()).IsEqualTo(2);
    }
}
