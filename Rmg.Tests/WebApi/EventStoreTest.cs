using Rmg.WebApi.Analytics;
using Rmg.WebApi.Songs;

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
        long? ms = null, double? seconds = null, long? seed = null, string? detail = null, string? version = null, SongSettings? settings = null)
    {
        var when = at ?? Now;
        return _store.Write(new StoredEvent(when, EventStore.Day(when), visitor, name, ms, null, seconds, seed, detail, version, settings));
    }

    [Test]
    public async Task ASongsSettings_AreKept_WithItsUnconventionalityAndWhetherItWasGiven()
    {
        var settings = SongSettingsTest.Mixed;
        Write(EventNames.Rated, seed: 5, detail: "none>up", version: "0.5.000", settings: settings);
        Write(EventNames.Rated, seed: 6, detail: "none>up", version: "0.5.000");

        using var connection = _store.OpenForReading();
        using var reading = connection.CreateCommand();
        reading.CommandText = "SELECT settings, unconventionality, given FROM events ORDER BY seed";
        using var rows = reading.ExecuteReader();

        await Assert.That(rows.Read()).IsTrue();
        await Assert.That(rows.GetString(0)).IsEqualTo(settings.Format());
        await Assert.That(rows.GetInt32(1)).IsEqualTo(settings.Unconventionality.Value);
        await Assert.That(rows.GetBoolean(2)).IsEqualTo(settings.Unconventionality.IsGiven);
        await Assert.That(rows.Read()).IsTrue();
        await Assert.That(rows.IsDBNull(0) && rows.IsDBNull(1) && rows.IsDBNull(2)).IsTrue();
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
        await Assert.That(summary.Server.MakeMs.Median).IsNull();
        await Assert.That(summary.Server.Outcomes).IsEmpty();
        await Assert.That(summary.Server.FifthsVersion).IsNull();
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

        await Assert.That(seeds[0].Seed).IsEqualTo("3U");
        await Assert.That(seeds[0].Seconds).IsEqualTo(55);
        await Assert.That(seeds[0].Plays).IsEqualTo(2);
        await Assert.That(seeds[1].Seed).IsEqualTo("1l");
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
    public async Task TheFirstWriteOfADay_ThrowsAwayWhatIsTooOld_AsAServerRunsOn()
    {
        Write(EventNames.PageOpen, "ancient", Now.AddDays(-(_store.RetentionDays + 1)));
        Write(EventNames.PageOpen, "today");

        await Assert.That(_store.Prune(Now)).IsEqualTo(0);
        await Assert.That(_store.Summarise(Now, _store.RetentionDays).Visitors).IsEqualTo(1);
    }

    [Test]
    public async Task EventsAreKeptForAsLongAsTheSettingsSay()
    {
        var directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try
        {
            var store = EventStore.Create(directory, 7);
            store.Write(new StoredEvent(Now.AddDays(-8), EventStore.Day(Now.AddDays(-8)), "old", EventNames.PageOpen, null, null, null, null, null, null, null));
            store.Write(new StoredEvent(Now.AddDays(-6), EventStore.Day(Now.AddDays(-6)), "recent", EventNames.PageOpen, null, null, null, null, null, null, null));

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
        await Assert.That(summary.Rated).IsEquivalentTo(new[] { new RatedSeed("3", "", 2, 0), new RatedSeed("1", "", 0, 1) });
    }

    [Test]
    public async Task ASeedAtAGivenUnconventionality_IsAnotherSong_AndSongsAreRatedByFifths()
    {
        SongSettings Heard(int unconventionality, bool isGiven) => SongSettingsTest.Mixed with
        {
            Unconventionality = new Setting(isGiven, unconventionality),
            Tempo = SongSettingsTest.Mixed.Tempo with { IsGiven = false },
            Key = SongSettingsTest.Mixed.Key with { IsGiven = false },
            Meter = SongSettingsTest.Mixed.Meter with { IsGiven = false },
            Facets = [..SongSettingsTest.Mixed.Facets.Select(x => x with { IsGiven = false })],
            Parts = [..SongSettingsTest.Mixed.Parts.Select(x => x with { Plays = 0 })],
            DrumPieces = DrumPieceSettings.Drawn
        };
        // seed 1 drawn at 10, liked, and asked for at 120, disliked; seed 2 drawn at 20, liked; seed 3 from before
        // the settings were kept, liked
        Write(EventNames.Rated, seed: 1, detail: "none>up", version: "0.5.000", settings: Heard(5, false));
        Write(EventNames.Rated, seed: 1, detail: "none>down", version: "0.5.000", settings: Heard(60, true));
        Write(EventNames.Rated, seed: 2, detail: "none>up", version: "0.5.000", settings: Heard(10, false));
        Write(EventNames.Rated, seed: 3, detail: "none>up", version: "0.5.000");

        var summary = _store.Summarise(Now, 30);

        await Assert.That(summary.Rated).IsEquivalentTo(new[] { new RatedSeed("1", "", 1, 0), new RatedSeed("2", "", 1, 0), new RatedSeed("3", "", 1, 0), new RatedSeed("1", Heard(60, true).Identity, 0, 1) });
        await Assert.That(summary.RatedFifths).IsEquivalentTo(new[] { new RatedFifth(false, 0, 2, 2, 0), new RatedFifth(true, 4, 1, 0, 1) });
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

    private static SongMeasurement Song(SongOutcome outcome, DateTimeOffset? at = null) => outcome == SongOutcome.Made
        ? new SongMeasurement(at ?? Now, outcome, 12, 340, 25, 3, 1, "0.6.000", 40, 5000)
        : new SongMeasurement(at ?? Now, outcome, 12, null, null, 3, 1, "0.6.000", null, null);

    [Test]
    public async Task ASongsMeasurements_AreKept_WithHowItEnded()
    {
        foreach (var outcome in Enum.GetValues<SongOutcome>()) await Assert.That(_store.Write(Song(outcome))).IsTrue();

        using var connection = _store.OpenForReading();
        using var reading = connection.CreateCommand();
        reading.CommandText = "SELECT outcome, wait_ms, make_ms, render_ms, running, waiting, version, unconventionality, notes FROM songs ORDER BY id";
        using var rows = reading.ExecuteReader();

        await Assert.That(rows.Read()).IsTrue();
        await Assert.That(rows.GetString(0)).IsEqualTo("made");
        await Assert.That(new[] { rows.GetInt64(1), rows.GetInt64(2), rows.GetInt64(3), rows.GetInt64(4), rows.GetInt64(5), rows.GetInt64(7), rows.GetInt64(8) })
            .IsEquivalentTo(new long[] { 12, 340, 25, 3, 1, 40, 5000 });
        await Assert.That(rows.GetString(6)).IsEqualTo("0.6.000");

        var others = new List<string>();
        while (rows.Read())
        {
            others.Add(rows.GetString(0));
            await Assert.That(rows.IsDBNull(2) && rows.IsDBNull(3) && rows.IsDBNull(7) && rows.IsDBNull(8)).IsTrue();
        }

        await Assert.That(others).IsEquivalentTo(new[] { "cancelled_waiting", "cancelled_making", "refused_client", "refused_full", "failed" });
    }

    [Test]
    public async Task OldSongsAreThrownAway_AsOldEventsAre()
    {
        _store.Write(Song(SongOutcome.Made, Now.AddDays(-(_store.RetentionDays + 1))));
        _store.Write(Song(SongOutcome.Made, Now.AddDays(-1)));

        await Assert.That(_store.Prune(Now)).IsEqualTo(1);
    }

    [Test]
    public async Task ADaysSongs_AreKept_OnlyAsManyAsADayKeeps()
    {
        // a day all but full, filled in one go rather than written one by one
        using (var connection = _store.OpenForReading())
        using (var filling = connection.CreateCommand())
        {
            filling.CommandText = """
                WITH RECURSIVE n(i) AS (SELECT 1 UNION ALL SELECT i + 1 FROM n WHERE i < $count)
                INSERT INTO songs (at, day, outcome, wait_ms, running, waiting, version)
                SELECT $at, $day, 'refused_full', 0, 0, 0, '0.6.000' FROM n
                """;
            filling.Parameters.AddWithValue("$count", EventStore.DailySongs - 1);
            filling.Parameters.AddWithValue("$at", Now.UtcDateTime.ToString("O"));
            filling.Parameters.AddWithValue("$day", EventStore.Day(Now));
            filling.ExecuteNonQuery();
        }

        await Assert.That(_store.Write(Song(SongOutcome.Made))).IsTrue();
        await Assert.That(_store.Write(Song(SongOutcome.Made))).IsFalse();
        await Assert.That(_store.Write(Song(SongOutcome.Made, Now.AddDays(1)))).IsTrue();
    }

    [Test]
    public async Task TheServersSongs_AreSummarised_ByHowTheyEnded_TheirVersion_TheirFifth_AndTheDay()
    {
        SongMeasurement Made(string version, long makeMs, int unconventionality, DateTimeOffset? at = null) =>
            new(at ?? Now, SongOutcome.Made, 5, makeMs, 20, 1, 2, version, unconventionality, 3000);

        _store.Write(Made("0.5.999", 100, 10));
        _store.Write(Made("0.5.1000", 200, 5));
        _store.Write(Made("0.5.1000", 400, 60));
        _store.Write(Made("0.5.1000", 300, 62, Now.AddDays(-1)));
        _store.Write(Song(SongOutcome.RefusedFull) with { Waiting = 9 });
        _store.Write(Song(SongOutcome.CancelledMaking));
        _store.Write(Song(SongOutcome.CancelledWaiting));

        var server = _store.Summarise(Now, 30).Server;

        await Assert.That(server.MakeMs.Count).IsEqualTo(4);
        await Assert.That(server.MakeMs.Median).IsEqualTo(300);
        await Assert.That(server.Outcomes[0]).IsEqualTo(new OutcomeCount("made", 4));
        await Assert.That(server.Outcomes.Sum(x => x.Count)).IsEqualTo(7);

        // 0.5.1000 is the later, whatever text would say
        await Assert.That(server.Versions.Count).IsEqualTo(2);
        await Assert.That(server.Versions[0]).IsEqualTo(new VersionSpeed("0.5.1000", 3, 300, 400));
        await Assert.That(server.Versions[1]).IsEqualTo(new VersionSpeed("0.5.999", 1, 100, 100));
        await Assert.That(server.FifthsVersion).IsEqualTo("0.5.1000");
        await Assert.That(server.Fifths.Count).IsEqualTo(2);
        await Assert.That(server.Fifths[0]).IsEqualTo(new FifthSpeed(0, 1, 200, 200));
        await Assert.That(server.Fifths[1]).IsEqualTo(new FifthSpeed(4, 2, 400, 400));

        await Assert.That(server.Daily.Count).IsEqualTo(2);
        await Assert.That(server.Daily[0]).IsEqualTo(new ServerDay(EventStore.Day(Now.AddDays(-1)), 1, 0, 0, 0, 2));
        await Assert.That(server.Daily[1]).IsEqualTo(new ServerDay(EventStore.Day(Now), 3, 2, 1, 0, 9));
    }
}
