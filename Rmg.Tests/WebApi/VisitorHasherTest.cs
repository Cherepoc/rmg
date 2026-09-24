using Rmg.WebApi.Analytics;

namespace Rmg.Tests.WebApi;

public sealed class VisitorHasherTest : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public VisitorHasherTest()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    [Test]
    public async Task SameVisitorOnTheSameDayIsTheSameVisitor()
    {
        var hasher = VisitorHasher.Create(_directory);

        var first = hasher.Hash("2026-09-24", "203.0.113.7", "Firefox");
        var second = hasher.Hash("2026-09-24", "203.0.113.7", "Firefox");

        await Assert.That(first).IsEqualTo(second);
    }

    [Test]
    public async Task TheSameVisitorTomorrowIsSomebodyElse()
    {
        var hasher = VisitorHasher.Create(_directory);

        var today = hasher.Hash("2026-09-24", "203.0.113.7", "Firefox");
        var tomorrow = hasher.Hash("2026-09-25", "203.0.113.7", "Firefox");

        await Assert.That(today).IsNotEqualTo(tomorrow);
    }

    [Test]
    [Arguments("203.0.113.8", "Firefox")]
    [Arguments("203.0.113.7", "Chrome")]
    public async Task ADifferentAddressOrBrowserIsADifferentVisitor(string address, string userAgent)
    {
        var hasher = VisitorHasher.Create(_directory);

        var one = hasher.Hash("2026-09-24", "203.0.113.7", "Firefox");
        var other = hasher.Hash("2026-09-24", address, userAgent);

        await Assert.That(one).IsNotEqualTo(other);
    }

    [Test]
    public async Task RestartingDoesNotTurnEverybodyIntoSomebodyNew()
    {
        var before = VisitorHasher.Create(_directory).Hash("2026-09-24", "203.0.113.7", "Firefox");
        var after = VisitorHasher.Create(_directory).Hash("2026-09-24", "203.0.113.7", "Firefox");

        await Assert.That(before).IsEqualTo(after);
    }

    [Test]
    public async Task TwoServersDoNotAgreeOnWhoAVisitorIs()
    {
        var other = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(other);

        try
        {
            var here = VisitorHasher.Create(_directory).Hash("2026-09-24", "203.0.113.7", "Firefox");
            var there = VisitorHasher.Create(other).Hash("2026-09-24", "203.0.113.7", "Firefox");

            await Assert.That(here).IsNotEqualTo(there);
        }
        finally
        {
            Directory.Delete(other, true);
        }
    }

    [Test]
    public async Task TheHashIsShortAndSaysNothingAboutWhereItCameFrom()
    {
        var hash = VisitorHasher.Create(_directory).Hash("2026-09-24", "203.0.113.7", "Firefox");

        await Assert.That(hash.Length).IsEqualTo(16);
        await Assert.That(hash).DoesNotContain("203");
    }
}
