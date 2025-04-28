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

        await Assert.That(result).IsEquivalentTo(expected);
    }
    
    [Test]
    public async Task MaxOneByteValue()
    {
        var result = Midi.IntToBytesVariable(0x7F);

        byte[] expected =
        [
            0x7F,
        ];

        await Assert.That(result).IsEquivalentTo(expected);
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

        await Assert.That(result).IsEquivalentTo(expected);
    }
}