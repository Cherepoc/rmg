using Rmg.Core.Rendering;

namespace Rmg.Tests.Rendering;

public sealed class RenderFixNoteOffsetTest
{
    [Test]
    [Arguments(5)]
    [Arguments(17)]
    [Arguments(65)]
    [Arguments(77)]
    public async Task Count1ReturnsMinOctaveOffset(int noteOffset)
    {
        var result = Render.FixNoteOffset(5, 1, noteOffset);
        const int expectedOffset = 65;
        await Assert.That(result).IsEqualTo(expectedOffset);
    }
    
    [Test]
    [Arguments(17)]
    [Arguments(53)]
    [Arguments(65)]
    [Arguments(101)]
    [Arguments(113)]
    public async Task Count2FirstPeriod(int noteOffset)
    {
        var result = Render.FixNoteOffset(5, 2, noteOffset);
        const int expectedOffset = 65;
        await Assert.That(result).IsEqualTo(expectedOffset);
    }
    
    [Test]
    [Arguments(17)]
    [Arguments(29)]
    [Arguments(65)]
    [Arguments(77)]
    [Arguments(113)]
    public async Task Count2SecondPeriod(int noteOffset)
    {
        var result = Render.FixNoteOffset(4, 2, noteOffset);
        const int expectedOffset = 65;
        await Assert.That(result).IsEqualTo(expectedOffset);
    }
    
    [Test]
    [Arguments(-7)]
    [Arguments(53)]
    [Arguments(65)]
    [Arguments(125)]
    public async Task Count3FirstPeriod(int noteOffset)
    {
        var result = Render.FixNoteOffset(5, 3, noteOffset);
        const int expectedOffset = 65;
        await Assert.That(result).IsEqualTo(expectedOffset);
    }
    
    [Test]
    [Arguments(-7)]
    [Arguments(29)]
    [Arguments(65)]
    [Arguments(101)]
    public async Task Count3SecondPeriod(int noteOffset)
    {
        var result = Render.FixNoteOffset(4, 3, noteOffset);
        const int expectedOffset = 65;
        await Assert.That(result).IsEqualTo(expectedOffset);
    }
    
    [Test]
    [Arguments(5)]
    [Arguments(65)]
    [Arguments(77)]
    [Arguments(137)]
    public async Task Count3ThirdPeriod(int noteOffset)
    {
        var result = Render.FixNoteOffset(3, 3, noteOffset);
        const int expectedOffset = 65;
        await Assert.That(result).IsEqualTo(expectedOffset);
    }
}