using Rmg.Core;

namespace Rmg.Tests;

public sealed class Base62Test
{
    [Test]
    [Arguments(0, "0")]
    [Arguments(61, "z")]
    [Arguments(62, "10")]
    [Arguments(int.MaxValue, "2LKcb1")]
    [Arguments(-1, "4gfFC3")]
    public async Task ASeed_ReadsBackAsItWasWritten(int seed, string text)
    {
        await Assert.That(Base62.FromSeed(seed)).IsEqualTo(text);
        await Assert.That(Base62.ToSeed(text)).IsEqualTo(seed);
    }

    [Test]
    [Arguments("")]
    [Arguments("4gfFC4")]
    [Arguments("1234567")]
    [Arguments("12-4")]
    public async Task AnythingElse_IsNoSeed(string text)
    {
        await Assert.That(Base62.ToSeed(text)).IsNull();
    }
}
