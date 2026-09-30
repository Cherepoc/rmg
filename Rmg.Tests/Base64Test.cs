using Rmg.Core;

namespace Rmg.Tests;

public sealed class Base64Test
{
    [Test]
    [Arguments(0UL, "0")]
    [Arguments(61UL, "z")]
    [Arguments(62UL, "-")]
    [Arguments(63UL, "_")]
    [Arguments(64UL, "10")]
    [Arguments(ulong.MaxValue, "F__________")]
    public async Task ASeed_ReadsBackAsItWasWritten(ulong seed, string text)
    {
        await Assert.That(Base64.FromSeed(seed)).IsEqualTo(text);
        await Assert.That(Base64.ToSeed(text)).IsEqualTo(seed);
    }

    [Test]
    [Arguments("")]
    [Arguments("G__________")]
    [Arguments("123456789012")]
    [Arguments("12.4")]
    public async Task AnythingElse_IsNoSeed(string text)
    {
        await Assert.That(Base64.ToSeed(text)).IsNull();
    }
}
