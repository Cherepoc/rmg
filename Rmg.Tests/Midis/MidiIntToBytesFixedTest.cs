using Rmg.Core;

namespace Rmg.Tests.Midis;

public sealed class MidiIntToBytesFixedTest
{
    [Test]
    public async Task OneByte()
    {
        var result = Midi.IntToBytesFixed(0xF1F2, 1);

        byte[] expected =
        [
            0xF2,
        ];

        await Assert.That(result).IsEquivalentTo(expected);
    }
    
    [Test]
    public async Task TwoByte()
    {
        var result = Midi.IntToBytesFixed(0xF1F2F3, 2);

        byte[] expected =
        [
            0xF2,
            0xF3,
        ];

        await Assert.That(result).IsEquivalentTo(expected);
    }
    
    [Test]
    public async Task ThreeByte()
    {
        var result = Midi.IntToBytesFixed(0xF1F2F3F4, 3);

        byte[] expected =
        [
            0xF2,
            0xF3,
            0xF4,
        ];

        await Assert.That(result).IsEquivalentTo(expected);
    }
    
    [Test]
    public async Task FourByte()
    {
        var result = Midi.IntToBytesFixed(0x00F2F3F4, 4);

        byte[] expected =
        [
            0x00,
            0xF2,
            0xF3,
            0xF4,
        ];

        await Assert.That(result).IsEquivalentTo(expected);
    }
}