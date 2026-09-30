using Rmg.Core;

namespace Rmg.Tests;

public sealed class Base62Test
{
    [Test]
    [Arguments(0UL, "0")]
    [Arguments(61UL, "z")]
    [Arguments(62UL, "10")]
    [Arguments(ulong.MaxValue, "LygHa16AHYF")]
    public async Task ASeed_ReadsBackAsItWasWritten(ulong seed, string text)
    {
        await Assert.That(Base62.FromSeed(seed)).IsEqualTo(text);
        await Assert.That(Base62.ToSeed(text)).IsEqualTo(seed);
    }

    [Test]
    [Arguments("")]
    [Arguments("LygHa16AHYG")]
    [Arguments("123456789012")]
    [Arguments("12-4")]
    public async Task AnythingElse_IsNoSeed(string text)
    {
        await Assert.That(Base62.ToSeed(text)).IsNull();
    }
}
