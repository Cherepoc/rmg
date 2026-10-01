using System.Net;
using Rmg.WebApi.Songs;

namespace Rmg.Tests.WebApi;

public sealed class SongQueueTest
{
    [Test]
    public async Task AsManyAsItMakesAtOnce_AreGivenTheirTurns_AndTheNextWaits()
    {
        var queue = new SongQueue(2, 10, 10);

        var first = await queue.Enter("a", CancellationToken.None);
        var second = await queue.Enter("b", CancellationToken.None);
        var third = queue.Enter("c", CancellationToken.None);

        await Assert.That(first.Refusal).IsNull();
        await Assert.That(second.Refusal).IsNull();
        await Assert.That(await IsWaiting(third)).IsTrue();

        first.Dispose();
        await Assert.That((await third).Refusal).IsNull();
    }

    [Test]
    public async Task AClientAskingForMany_DelaysAnotherByOneSong_NotByAllOfItsOwn()
    {
        var queue = new SongQueue(1, 10, 10);
        var making = await queue.Enter("a", CancellationToken.None);
        var many = Enumerable.Range(0, 5).Select(_ => queue.Enter("a", CancellationToken.None)).ToArray();
        var other = queue.Enter("b", CancellationToken.None);

        making.Dispose();
        (await many[0]).Dispose();

        await Assert.That(await IsGiven(other)).IsTrue();
        await Assert.That(await IsWaiting(many[1])).IsTrue();
    }

    [Test]
    public async Task AClient_MayKeepOnlySoManyWaiting_AndAllTogether_OnlySoMany()
    {
        var queue = new SongQueue(1, 3, 2);
        await queue.Enter("a", CancellationToken.None);
        _ = queue.Enter("a", CancellationToken.None);
        _ = queue.Enter("a", CancellationToken.None);

        await Assert.That((await queue.Enter("a", CancellationToken.None)).Refusal).IsEqualTo(SongQueue.Refusal.Client);

        _ = queue.Enter("b", CancellationToken.None);
        await Assert.That((await queue.Enter("c", CancellationToken.None)).Refusal).IsEqualTo(SongQueue.Refusal.Full);
    }

    [Test]
    public async Task OneThatGivesUpWaiting_LeavesItsLine_AndTakesNoTurn()
    {
        var queue = new SongQueue(1, 1, 1);
        var making = await queue.Enter("a", CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var givingUp = queue.Enter("b", cancellation.Token);

        await cancellation.CancelAsync();
        await Assert.That(async () => await givingUp).Throws<OperationCanceledException>();

        // its place is free again, and the turn goes to the one waiting in it
        var next = queue.Enter("c", CancellationToken.None);
        making.Dispose();
        await Assert.That((await next).Refusal).IsNull();
    }

    [Test]
    public async Task ATurnLetGoOfTwice_FreesOneSongOnly()
    {
        var queue = new SongQueue(1, 10, 10);
        var making = await queue.Enter("a", CancellationToken.None);
        var first = queue.Enter("b", CancellationToken.None);
        var second = queue.Enter("c", CancellationToken.None);

        making.Dispose();
        making.Dispose();

        await Assert.That(await IsGiven(first)).IsTrue();
        await Assert.That(await IsWaiting(second)).IsTrue();
    }

    // a turn is handed over on another thread, so one given is waited for, generously, and one waiting is still so a
    // moment later
    // a turn arriving within ten seconds: given in milliseconds, its continuation may wait for a thread while the rest of
    // the suite keeps every core busy
    private static async Task<bool> IsGiven(Task<SongQueue.Turn> turn)
    {
        return await Task.WhenAny(turn, Task.Delay(10_000)) == turn;
    }

    private static async Task<bool> IsWaiting(Task<SongQueue.Turn> turn)
    {
        return await Task.WhenAny(turn, Task.Delay(200)) != turn;
    }

    [Test]
    [Arguments("203.0.113.7", "203.0.113.7")]
    [Arguments("::ffff:203.0.113.7", "203.0.113.7")]
    [Arguments("2001:db8:1:2:aaaa:bbbb:cccc:dddd", "2001:db8:1:2::/64")]
    [Arguments("2001:db8:1:2::1", "2001:db8:1:2::/64")]
    public async Task AClient_IsItsAddress_OrItsIPv6Network(string address, string client)
    {
        await Assert.That(SongQueue.ClientOf(IPAddress.Parse(address))).IsEqualTo(client);
    }

    [Test]
    public async Task ARequestOfNoAddress_IsOneClient()
    {
        await Assert.That(SongQueue.ClientOf(null)).IsEqualTo("unknown");
    }
}
