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
        long? ms = null, double? seconds = null, long? seed = null, string? detail = null, string? version = null)
    {
        var when = at ?? Now;
        return _store.Write(new StoredEvent(when, EventStore.Day(when), visitor, name, ms, null, seconds, seed, detail, version));
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
            store.Write(new StoredEvent(Now.AddDays(-8), EventStore.Day(Now.AddDays(-8)), "old", EventNames.PageOpen, null, null, null, null, null, null));
            store.Write(new StoredEvent(Now.AddDays(-6), EventStore.Day(Now.AddDays(-6)), "recent", EventNames.PageOpen, null, null, null, null, null, null));

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
            EventNames.MixChanged, EventNames.DownloadedMidi, EventNames.ExportedMp3, EventNames.Shared,
            EventNames.Rated
        ];

        foreach (var name in sent) await Assert.That(EventNames.IsKnown(name)).IsTrue();

        await Assert.That(EventNames.IsKnown("something_else")).IsFalse();
        await Assert.That(EventNames.IsKnown(null)).IsFalse();
    }

    [Test]
    public async Task ListeningIsSummedByVersion_AVisitorsSongOnce()
    {
        Write(EventNames.SongGenerated, seed: 1, version: "0.5.000");
        Write(EventNames.Played, seed: 1, version: "0.5.000");
        Write(EventNames.Listened, seconds: 20, seed: 1, version: "0.5.000");
        Write(EventNames.Listened, seconds: 15, seed: 1, version: "0.5.000");
        Write(EventNames.Listened, seconds: 10, seed: 2, version: "0.5.000");
        Write(EventNames.Listened, seconds: 40, seed: 1, version: "0.5.001", visitor: "somebody else");
        Write(EventNames.Played, seed: 3);

        var versions = _store.Summarise(Now, 30).Versions;

        await Assert.That(versions.Select(x => x.Version)).IsEquivalentTo(new[] { "0.5.001", "0.5.000" });
        var first = versions.Single(x => x.Version == "0.5.000");
        await Assert.That(first.Songs).IsEqualTo(1);
        await Assert.That(first.Plays).IsEqualTo(1);
        await Assert.That(first.Listens).IsEqualTo(2);
        await Assert.That(first.ListensPast30s).IsEqualTo(1);
    }

    [Test]
    public async Task VersionsAreOrderedByTheirNumbers()
    {
        Write(EventNames.Played, version: "0.5.999");
        Write(EventNames.Played, version: "0.5.1000");
        Write(EventNames.Played, version: "0.6.000");

        var versions = _store.Summarise(Now, 30).Versions.Select(x => x.Version).ToArray();

        await Assert.That(versions).IsEquivalentTo(new[] { "0.6.000", "0.5.1000", "0.5.999" });
    }

    [Test]
    public async Task AFileFromBeforeTheVersion_GetsItsColumn_AndKeepsItsEvents()
    {
        var directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(directory);

        try
        {
            using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={Path.Combine(directory, EventStore.FileName)}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE events (
                        id INTEGER PRIMARY KEY AUTOINCREMENT, at TEXT NOT NULL, day TEXT NOT NULL, visitor TEXT NOT NULL,
                        name TEXT NOT NULL, ms INTEGER NULL, bytes INTEGER NULL, seconds REAL NULL, seed INTEGER NULL,
                        detail TEXT NULL
                    );
                    INSERT INTO events (at, day, visitor, name) VALUES ('2026-09-24T12:00:00Z', '2026-09-24', 'old', 'page_open');
                    """;
                command.ExecuteNonQuery();
            }

            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            var store = EventStore.Create(directory);
            store.Write(new StoredEvent(Now, EventStore.Day(Now), "new", EventNames.Played, null, null, null, 1, null, "0.5.000"));

            var summary = store.Summarise(Now, 30);
            await Assert.That(summary.Events).IsEqualTo(2);
            await Assert.That(summary.Versions.Single().Version).IsEqualTo("0.5.000");
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(directory, true);
        }
    }

    [Test]
    public async Task ARatingChanged_CountsAsItStandsNow_WhoeverAndWheneverChangedIt()
    {
        // liked yesterday, as a visitor of yesterday's hash, and turned into a dislike today, as another
        Write(EventNames.Rated, seed: 1, detail: "none>up", version: "0.5.000", visitor: "yesterday", at: Now.AddDays(-1));
        Write(EventNames.Rated, seed: 1, detail: "up>down", version: "0.5.000", visitor: "today");
        // liked, and taken back the day after
        Write(EventNames.Rated, seed: 2, detail: "none>up", version: "0.5.000", visitor: "yesterday", at: Now.AddDays(-1));
        Write(EventNames.Rated, seed: 2, detail: "up>none", version: "0.5.000", visitor: "today");
        // liked by two
        Write(EventNames.Rated, seed: 3, detail: "none>up", version: "0.5.000");
        Write(EventNames.Rated, seed: 3, detail: "none>up", version: "0.5.000", visitor: "somebody else");

        var summary = _store.Summarise(Now, 30);
        var version = summary.Versions.Single(x => x.Version == "0.5.000");

        await Assert.That(version.Likes).IsEqualTo(2);
        await Assert.That(version.Dislikes).IsEqualTo(1);
        await Assert.That(summary.RatedVersion).IsEqualTo("0.5.000");
        await Assert.That(summary.Rated).IsEquivalentTo(new[] { new RatedSeed(3, 2, 0), new RatedSeed(1, 0, 1) });
    }

    [Test]
    public async Task ARatingBeforeTheWindow_StillCounts()
    {
        Write(EventNames.Played, version: "0.5.000");
        Write(EventNames.Rated, seed: 1, detail: "none>up", version: "0.5.000", at: Now.AddDays(-20));

        var version = _store.Summarise(Now, 7).Versions.Single();

        await Assert.That(version.Likes).IsEqualTo(1);
    }

    [Test]
    [Arguments("up")]
    [Arguments("up>up")]
    [Arguments("up>sideways")]
    [Arguments("none>up>down")]
    public async Task AnythingButAChangeOfRating_IsNotCounted(string detail)
    {
        Write(EventNames.Played, version: "0.5.000");
        Write(EventNames.Rated, seed: 1, detail: detail, version: "0.5.000");

        var summary = _store.Summarise(Now, 30);

        await Assert.That(summary.Versions.Single().Likes).IsEqualTo(0);
        await Assert.That(summary.Rated).IsEmpty();
        await Assert.That(summary.RatedVersion).IsNull();
    }

    [Test]
    public async Task AChangeFromARatingNoLongerKept_TakesNoMoreThanThereIs()
    {
        Write(EventNames.Rated, seed: 1, detail: "up>down", version: "0.5.000");

        var rated = _store.Summarise(Now, 30).Rated.Single();

        await Assert.That(rated.Likes).IsEqualTo(0);
        await Assert.That(rated.Dislikes).IsEqualTo(1);
    }
}
