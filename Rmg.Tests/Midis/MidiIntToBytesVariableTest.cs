using Rmg.Core;

namespace Rmg.Tests.Midis;

public sealed class MidiIntToBytesVariableTest
{
    [Test]
    public async Task ZeroValue()
    {
        var result = Midi.IntToBytesVariable(0x0);

        byte[] expected =
        [
            0x0,
        ];

        await Assert.That(result).IsEquivalentTo(expected, CollectionOrdering.Matching);
    }
    
    [Test]
    public async Task MaxOneByteValue()
    {
        var result = Midi.IntToBytesVariable(0x7F);

        byte[] expected =
        [
            0x7F,
        ];

        await Assert.That(result).IsEquivalentTo(expected, CollectionOrdering.Matching);
    }
    
    [Test]
    public async Task TwoByteValue()
    {
        var result = Midi.IntToBytesVariable(0xFF);

        byte[] expected =
        [
            0x81,
            0x7F,
        ];

        await Assert.That(result).IsEquivalentTo(expected, CollectionOrdering.Matching);
    }

    /// <summary>A quantity of three bytes and of four, the longest a MIDI file takes, as long rests and tick deltas need.</summary>
    [Test]
    [Arguments(0x80u, new byte[] { 0x81, 0x00 })]
    [Arguments(0x3FFFu, new byte[] { 0xFF, 0x7F })]
    [Arguments(0x4000u, new byte[] { 0x81, 0x80, 0x00 })]
    [Arguments(0x0FFFFFFFu, new byte[] { 0xFF, 0xFF, 0xFF, 0x7F })]
    public async Task LongerValues_TakeTheirBytes_EachButTheLastFlagged(uint value, byte[] expected)
    {
        await Assert.That(Midi.IntToBytesVariable(value)).IsEquivalentTo(expected, CollectionOrdering.Matching);
    }
}
