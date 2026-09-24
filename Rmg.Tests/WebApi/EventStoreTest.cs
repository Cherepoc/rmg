using Rmg.WebApi.Analytics;

namespace Rmg.Tests.WebApi;

public sealed class EventStoreTest : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly EventStore _store;

    public EventStoreTest()
    {
        _store = EventStore.Create(_directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    private bool Write(string name, string visitor = "someone", DateTimeOffset? at = null,
        long? ms = null, double? seconds = null, long? seed = null, string? detail = null)
    {
        var when = at ?? Now;
        return _store.Write(new StoredEvent(when, EventStore.Day(when), visitor, name, ms, null, seconds, seed, detail));
    }

    [Test]
    public async Task NothingWrittenIsNothingCounted()
    {
        var summary = _store.Summarise(Now, 30);

        await Assert.That(summary.Visitors).IsEqualTo(0);
        await Assert.That(summary.Events).IsEqualTo(0);
        await Assert.That(summary.SoundFontMs.Median).IsNull();
        await Assert.That(summary.Daily).IsEmpty();
        await Check.That(summary.Funnel.Select(step => step.Visitors).Distinct()).IsEquivalentTo(new[] { 0 });
    }

    [Test]
    public async Task AVisitorIsCountedOnceHoweverManyEventsTheySend()
    {
        Write(EventNames.PageOpen);
        Write(EventNames.Played);
        Write(EventNames.Played);
        Write(EventNames.PageOpen, "somebody else");

        var summary = _store.Summarise(Now, 30);

        await Assert.That(summary.Visitors).IsEqualTo(2);
        await Assert.That(summary.PageOpens).IsEqualTo(2);
        await Assert.That(summary.Events).IsEqualTo(4);
    }

    [Test]
    public async Task TheFunnelCountsVisitors_NotEvents()
    {
        Write(EventNames.PageOpen);
        Write(EventNames.Played);
        Write(EventNames.Played);

        var played = _store.Summarise(Now, 30).Funnel.Single(step => step.Name == "Pressed play");

        await Assert.That(played.Visitors).IsEqualTo(1);
    }

    [Test]
    public async Task ListeningPastThirtySecondsIsItsOwnStep()
    {
        Write(EventNames.Listened, "brief", seconds: 4);
        Write(EventNames.Listened, "patient", seconds: 300);

        var step = _store.Summarise(Now, 30).Funnel.Single(item => item.Name == "Listened past 30s");

        await Assert.That(step.Visitors).IsEqualTo(1);
    }

    [Test]
    public async Task TheSpreadIsTheMiddleAndTheTwoMarksAboveIt()
    {
        foreach (var ms in Enumerable.Range(1, 100)) Write(EventNames.SoundFontReady, $"visitor {ms}", ms: ms);

        var spread = _store.Summarise(Now, 30).SoundFontMs;

        await Assert.That(spread.Count).IsEqualTo(100);
        await Assert.That(spread.Median).IsEqualTo(51);
        await Assert.That(spread.Upper).IsEqualTo(75);
        await Assert.That(spread.Worst).IsEqualTo(95);
    }

    [Test]
    public async Task ASingleMeasurementIsItsOwnMiddleAndItsOwnWorst()
    {
        Write(EventNames.AudioReady, ms: 4200);

        var spread = _store.Summarise(Now, 30).FirstGestureMs;

        await Assert.That(spread.Count).IsEqualTo(1);
        await Assert.That(spread.Median).IsEqualTo(4200);
        await Assert.That(spread.Worst).IsEqualTo(4200);
    }

    [Test]
    public async Task DaysAreReportedInOrder_AndOnlyTheDaysAsked()
    {
        Write(EventNames.PageOpen, "old", Now.AddDays(-40));
        Write(EventNames.PageOpen, "recent", Now.AddDays(-2));
        Write(EventNames.PageOpen, "today");

        var summary = _store.Summarise(Now, 7);

        await Check.That(summary.Daily.Select(day => day.Day))
            .IsEquivalentTo(new[] { EventStore.Day(Now.AddDays(-2)), EventStore.Day(Now) });
        await Assert.That(summary.Visitors).IsEqualTo(2);
    }

    [Test]
    public async Task SeedsAreRankedByHowLongTheyHeldAttention()
    {
        Write(EventNames.Listened, "one", seconds: 10, seed: 111);
        Write(EventNames.Listened, "two", seconds: 30, seed: 222);
        Write(EventNames.Listened, "three", seconds: 25, seed: 222);

        var seeds = _store.Summarise(Now, 30).Seeds;

        await Assert.That(seeds[0].Seed).IsEqualTo(222);
        await Assert.That(seeds[0].Seconds).IsEqualTo(55);
        await Assert.That(seeds[0].Plays).IsEqualTo(2);
        await Assert.That(seeds[1].Seed).IsEqualTo(111);
    }

    [Test]
    public async Task FailuresAreGroupedByWhatAndWhy()
    {
        Write(EventNames.SoundFontFailed, "a", detail: "TypeError");
        Write(EventNames.SoundFontFailed, "b", detail: "TypeError");
        Write(EventNames.SongFailed, "c", detail: "http 500");

        var failures = _store.Summarise(Now, 30).Failures;

        await Assert.That(failures[0].Detail).IsEqualTo("TypeError");
        await Assert.That(failures[0].Count).IsEqualTo(2);
        await Assert.That(failures).HasCount().EqualTo(2);
    }

    [Test]
    public async Task AVisitorCannotWriteAllDay()
    {
        for (var written = 0; written < _store.DailyEventsPerVisitor; written++)
            await Assert.That(Write(EventNames.MixChanged)).IsTrue();

        await Assert.That(Write(EventNames.MixChanged)).IsFalse();

        // and somebody else is not held back by it
        await Assert.That(Write(EventNames.MixChanged, "a different visitor")).IsTrue();
    }

    [Test]
    public async Task ASharedSongIsASongTakenAway()
    {
        Write(EventNames.Shared, "one", seed: 42);
        Write(EventNames.DownloadedMidi, "two", seed: 42);
        Write(EventNames.Played, "three", seed: 42);

        var step = _store.Summarise(Now, 30).Funnel.Single(item => item.Name == "Took the song away");

        await Assert.That(step.Visitors).IsEqualTo(2);
    }

    [Test]
    public async Task OldEventsAreThrownAway()
    {
        Write(EventNames.PageOpen, "ancient", Now.AddDays(-(_store.RetentionDays + 1)));
        Write(EventNames.PageOpen, "recent", Now.AddDays(-1));

        var dropped = _store.Prune(Now);

        await Assert.That(dropped).IsEqualTo(1);
        await Assert.That(_store.Summarise(Now, _store.RetentionDays).Visitors).IsEqualTo(1);
    }

    [Test]
    public async Task EventsAreKeptForAsLongAsTheSettingsSay()
    {
        var directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try
        {
            var store = EventStore.Create(directory, 7);
            store.Write(new StoredEvent(Now.AddDays(-8), EventStore.Day(Now.AddDays(-8)), "old", EventNames.PageOpen, null, null, null, null, null));
            store.Write(new StoredEvent(Now.AddDays(-6), EventStore.Day(Now.AddDays(-6)), "recent", EventNames.PageOpen, null, null, null, null, null));

            await Assert.That(store.RetentionDays).IsEqualTo(7);
            await Assert.That(store.Prune(Now)).IsEqualTo(1);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Test]
    public async Task EveryEventTheClientSendsIsAKnownName()
    {
        string[] sent =
        [
            EventNames.PageOpen, EventNames.SongGenerated, EventNames.SongFailed, EventNames.SoundFontReady,
            EventNames.SoundFontFailed, EventNames.AudioReady, EventNames.Played, EventNames.Listened,
            EventNames.MixChanged, EventNames.DownloadedMidi, EventNames.ExportedMp3, EventNames.Shared
        ];

        foreach (var name in sent) await Assert.That(EventNames.IsKnown(name)).IsTrue();

        await Assert.That(EventNames.IsKnown("something_else")).IsFalse();
        await Assert.That(EventNames.IsKnown(null)).IsFalse();
    }
}
