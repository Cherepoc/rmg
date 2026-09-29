using Microsoft.Data.Sqlite;

namespace Rmg.WebApi.Analytics;

public static class EventStoreSummary
{
    /// <summary>
    ///     Everything the dashboard shows, in one read. The window is counted back from
    ///     <paramref name="now" />, and a day is a UTC day, which is what the rows are keyed by.
    /// </summary>
    public static AnalyticsSummary Summarise(this EventStore store, DateTimeOffset now, int days)
    {
        var since = EventStore.Day(now.AddDays(-(days - 1)));

        using var connection = store.OpenForReading();

        return new AnalyticsSummary(
            days,
            Count(connection, "SELECT COUNT(DISTINCT visitor) FROM events WHERE day >= $since", since),
            Count(connection, $"SELECT COUNT(*) FROM events WHERE name = '{EventNames.PageOpen}' AND day >= $since", since),
            SpreadOf(connection, "ms", EventNames.SoundFontReady, since),
            SpreadOf(connection, "ms", EventNames.AudioReady, since),
            SpreadOf(connection, "seconds", EventNames.Listened, since),
            Funnel(connection, since),
            Daily(connection, since),
            Seeds(connection, since),
            Versions(connection, since),
            Failures(connection, since),
            Count(connection, "SELECT COUNT(*) FROM events WHERE day >= $since", since)
        );
    }

    private static int Count(SqliteConnection connection, string sql, string since)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$since", since);

        return Convert.ToInt32(command.ExecuteScalar());
    }

    /// <summary>
    ///     The middle, the three quarter mark and the nineteen in twenty mark of one measurement. Read out
    ///     in full and worked out here: a few thousand numbers is nothing, and SQLite has no percentile of
    ///     its own without an extension this would rather not need.
    /// </summary>
    private static Spread SpreadOf(SqliteConnection connection, string column, string name, string since)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {column} FROM events WHERE name = $name AND day >= $since AND {column} IS NOT NULL ORDER BY {column}";
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$since", since);

        var values = new List<double>();
        using (var reader = command.ExecuteReader())
            while (reader.Read())
                values.Add(reader.GetDouble(0));

        if (values.Count == 0) return new Spread(null, null, null, 0);

        return new Spread(At(values, 0.5), At(values, 0.75), At(values, 0.95), values.Count);
    }

    /// <summary>The value a given part of the way through a sorted list, taking the nearer of two.</summary>
    private static double At(List<double> sorted, double part)
    {
        var index = (int)Math.Round(part * (sorted.Count - 1), MidpointRounding.AwayFromZero);
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }

    /// <summary>
    ///     How far down the page visitors got, counted in visitors rather than events, so somebody who
    ///     pressed play four times is one person who pressed play.
    /// </summary>
    private static List<FunnelStep> Funnel(SqliteConnection connection, string since)
    {
        (string Label, string Sql)[] steps =
        [
            ("Opened the page", $"name = '{EventNames.PageOpen}'"),
            ("Got a song", $"name = '{EventNames.SongGenerated}'"),
            ("Got a soundfont", $"name = '{EventNames.SoundFontReady}'"),
            ("Touched the page", $"name = '{EventNames.AudioReady}'"),
            ("Pressed play", $"name = '{EventNames.Played}'"),
            ("Listened past 30s", $"name = '{EventNames.Listened}' AND seconds >= 30"),
            ("Took the song away", $"name IN ('{EventNames.DownloadedMidi}', '{EventNames.ExportedMp3}', '{EventNames.Shared}')")
        ];

        return steps
            .Select(step => new FunnelStep(
                step.Label,
                Count(connection, $"SELECT COUNT(DISTINCT visitor) FROM events WHERE {step.Sql} AND day >= $since", since)))
            .ToList();
    }

    private static List<DayCount> Daily(SqliteConnection connection, string since)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT day,
                   COUNT(DISTINCT visitor),
                   COUNT(DISTINCT CASE WHEN name = '{EventNames.SongGenerated}' THEN id END),
                   COUNT(DISTINCT CASE WHEN name = '{EventNames.Played}' THEN id END)
            FROM events
            WHERE day >= $since
            GROUP BY day
            ORDER BY day
            """;
        command.Parameters.AddWithValue("$since", since);

        var days = new List<DayCount>();
        using var reader = command.ExecuteReader();
        while (reader.Read()) days.Add(new DayCount(reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3)));

        return days;
    }

    private static List<SeedListening> Seeds(SqliteConnection connection, string since)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT seed, SUM(seconds), COUNT(*)
            FROM events
            WHERE name = '{EventNames.Listened}' AND seed IS NOT NULL AND day >= $since
            GROUP BY seed
            ORDER BY SUM(seconds) DESC
            LIMIT 10
            """;
        command.Parameters.AddWithValue("$since", since);

        var seeds = new List<SeedListening>();
        using var reader = command.ExecuteReader();
        while (reader.Read()) seeds.Add(new SeedListening(reader.GetInt64(0), Math.Round(reader.GetDouble(1), 1), reader.GetInt32(2)));

        return seeds;
    }

    /// <summary>How many of the latest songs' versions the dashboard shows.</summary>
    private const int VersionsShown = 10;

    /// <summary>
    ///     How each songs' version was listened to, so that a change to the songs shows in how they are heard. A
    ///     listen is a visitor's song, its stretches of playing added up, so pausing does not count it twice.
    /// </summary>
    private static List<VersionListening> Versions(SqliteConnection connection, string since)
    {
        using var counting = connection.CreateCommand();
        counting.CommandText = $"""
            SELECT version,
                   MIN(day),
                   COUNT(CASE WHEN name = '{EventNames.SongGenerated}' THEN 1 END),
                   COUNT(CASE WHEN name = '{EventNames.Played}' THEN 1 END)
            FROM events
            WHERE version IS NOT NULL AND day >= $since
            GROUP BY version
            """;
        counting.Parameters.AddWithValue("$since", since);

        var counts = new List<(string Version, string FirstDay, int Songs, int Plays)>();
        using (var reader = counting.ExecuteReader())
            while (reader.Read())
                counts.Add((reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetInt32(3)));

        using var listening = connection.CreateCommand();
        listening.CommandText = $"""
            SELECT version, SUM(seconds)
            FROM events
            WHERE name = '{EventNames.Listened}' AND version IS NOT NULL AND seed IS NOT NULL AND day >= $since
            GROUP BY version, visitor, seed
            """;
        listening.Parameters.AddWithValue("$since", since);

        var listens = new Dictionary<string, List<double>>();
        using (var reader = listening.ExecuteReader())
            while (reader.Read())
            {
                var version = reader.GetString(0);
                if (!listens.TryGetValue(version, out var seconds)) listens[version] = seconds = [];
                seconds.Add(reader.GetDouble(1));
            }

        return counts
            .OrderByDescending(x => x.Version, VersionOrder)
            .Take(VersionsShown)
            .Select(x =>
            {
                var seconds = listens.GetValueOrDefault(x.Version, []).Order().ToList();
                return new VersionListening(
                    x.Version,
                    x.FirstDay,
                    x.Songs,
                    x.Plays,
                    seconds.Count,
                    seconds.Count(s => s >= 30),
                    seconds.Count == 0 ? null : Math.Round(At(seconds, 0.5), 1)
                );
            })
            .ToList();
    }

    /// <summary>Versions by their numbers, so that 0.5.1000 comes after 0.5.999, where text would put it before.</summary>
    private static readonly Comparer<string> VersionOrder = Comparer<string>.Create((first, second) =>
    {
        var firstParts = first.Split('.').Select(long.Parse).ToArray();
        var secondParts = second.Split('.').Select(long.Parse).ToArray();
        return firstParts.Zip(secondParts, (x, y) => x.CompareTo(y)).FirstOrDefault(x => x != 0);
    });

    private static List<Failure> Failures(SqliteConnection connection, string since)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT name, COALESCE(detail, 'no reason given'), COUNT(*)
            FROM events
            WHERE name IN ('{EventNames.SongFailed}', '{EventNames.SoundFontFailed}') AND day >= $since
            GROUP BY name, detail
            ORDER BY COUNT(*) DESC
            LIMIT 10
            """;
        command.Parameters.AddWithValue("$since", since);

        var failures = new List<Failure>();
        using var reader = command.ExecuteReader();
        while (reader.Read()) failures.Add(new Failure(reader.GetString(0), reader.GetString(1), reader.GetInt32(2)));

        return failures;
    }
}
