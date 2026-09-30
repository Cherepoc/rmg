using Rmg.Core;
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
        var ratings = Ratings(connection);
        var ratedVersion = ratings.Where(x => x.Likes + x.Dislikes > 0).Select(x => x.Version).Distinct().OrderByDescending(x => x, VersionOrder).FirstOrDefault();

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
            Versions(connection, since, ratings),
            ratedVersion,
            RatedSeeds(ratings, ratedVersion),
            RatedFifths(ratings, ratedVersion),
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
        while (reader.Read()) seeds.Add(new SeedListening(Base62.FromSeed(unchecked((ulong)reader.GetInt64(0))), Math.Round(reader.GetDouble(1), 1), reader.GetInt32(2)));

        return seeds;
    }

    /// <summary>How many of the latest songs' versions the dashboard shows.</summary>
    private const int VersionsShown = 10;

    /// <summary>
    ///     How each songs' version was listened to, so that a change to the songs shows in how they are heard. A
    ///     listen is a visitor's song, its stretches of playing added up, so pausing does not count it twice.
    /// </summary>
    private static List<VersionListening> Versions(SqliteConnection connection, string since, List<SongRating> ratings)
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
                    seconds.Count == 0 ? null : Math.Round(At(seconds, 0.5), 1),
                    ratings.Where(r => r.Version == x.Version).Sum(r => r.Likes),
                    ratings.Where(r => r.Version == x.Version).Sum(r => r.Dislikes)
                );
            })
            .ToList();
    }

    /// <summary>
    ///     A song's likes and dislikes in a version: its seed, and what of its settings names it too (<see cref="Songs.SongSettings.Identity" />),
    ///     empty for one its seed alone names; how plain or experimental it is, where its ratings say, and whether that
    ///     was given.
    /// </summary>
    private sealed record SongRating(string Version, long Seed, string Identity, int? Unconventionality, bool IsGiven, int Likes, int Dislikes);

    /// <summary>How many rated songs the dashboard shows.</summary>
    private const int RatedSeedsShown = 20;

    /// <summary>
    ///     Every song's likes and dislikes, from every rating kept, not only the window's: a rating is a change, such as
    ///     "up>down", from what the browser had to what it has, so a song's likes are the changes to a like less the
    ///     changes from one, whoever and whenever they came from. A visitor is a hash of a day, so it could not tell
    ///     that a dislike today takes back yesterday's like; the change can. A change whose rating before it is no
    ///     longer kept would take more than there is, so a count stops at none.
    /// </summary>
    private static List<SongRating> Ratings(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT version, seed, detail, unconventionality, given, identity
            FROM events
            WHERE name = '{EventNames.Rated}' AND version IS NOT NULL AND seed IS NOT NULL AND detail IS NOT NULL
            ORDER BY id
            """;

        var counts = new Dictionary<(string Version, long Seed, string Identity), (int? Unconventionality, bool IsGiven, int Likes, int Dislikes)>();
        using (var reader = command.ExecuteReader())
            while (reader.Read())
            {
                if (!TryReadChange(reader.GetString(2), out var from, out var to)) continue;

                // a rating with no settings kept has no unconventionality, and is of a song its seed alone names
                int? unconventionality = reader.IsDBNull(3) ? null : reader.GetInt32(3);
                var isGiven = !reader.IsDBNull(4) && reader.GetBoolean(4);
                var key = (reader.GetString(0), reader.GetInt64(1), reader.IsDBNull(5) ? "" : reader.GetString(5));
                var (known, _, likes, dislikes) = counts.GetValueOrDefault(key);
                counts[key] = (unconventionality ?? known, isGiven, likes + Count(to, "up") - Count(from, "up"), dislikes + Count(to, "down") - Count(from, "down"));
            }

        return counts
            .Select(x => new SongRating(x.Key.Version, x.Key.Seed, x.Key.Identity, x.Value.Unconventionality, x.Value.IsGiven, Math.Max(0, x.Value.Likes), Math.Max(0, x.Value.Dislikes)))
            .ToList();

        static int Count(string rating, string counted) => rating == counted ? 1 : 0;
    }

    /// <summary>A rating's change, "from>to", each "up", "down" or "none", and never the same; anything else is not one.</summary>
    private static bool TryReadChange(string detail, out string from, out string to)
    {
        string[] ratings = ["up", "down", "none"];
        var parts = detail.Split('>');
        from = parts[0];
        to = parts.Length == 2 ? parts[1] : "";

        return parts.Length == 2 && ratings.Contains(from) && ratings.Contains(to) && from != to;
    }

    /// <summary>The songs of a version rated, the most liked first and the most disliked last.</summary>
    private static List<RatedSeed> RatedSeeds(List<SongRating> ratings, string? version)
    {
        var rated = ratings
            .Where(x => x.Version == version && x.Likes + x.Dislikes > 0)
            .Select(x => new RatedSeed(Base62.FromSeed(unchecked((ulong)x.Seed)), x.Identity, x.Likes, x.Dislikes))
            .OrderByDescending(x => x.Likes - x.Dislikes)
            .ThenByDescending(x => x.Likes)
            .ThenBy(x => x.Seed, StringComparer.Ordinal)
            .ThenBy(x => x.Identity, StringComparer.Ordinal)
            .ToList();

        // the ends are what is worth hearing again, so a long list keeps both of them and drops its middle
        return rated.Count <= RatedSeedsShown
            ? rated
            : [..rated.Take(RatedSeedsShown / 2), ..rated.TakeLast(RatedSeedsShown / 2)];
    }

    /// <summary>
    ///     How a version's songs were liked by how plain or wild they are, in fifths of the unconventionality, those asked
    ///     for with one apart from those that drew their own, since a song asked for at an end is far from what songs
    ///     draw. A song whose ratings do not say how plain or wild it is is left out.
    /// </summary>
    private static List<RatedFifth> RatedFifths(List<SongRating> ratings, string? version)
    {
        return ratings
            .Where(x => x.Version == version && x.Unconventionality is not null && x.Likes + x.Dislikes > 0)
            .GroupBy(x => (x.IsGiven, Fifth: Math.Min(4, x.Unconventionality!.Value * 5 / 127)))
            .Select(x => new RatedFifth(x.Key.IsGiven, x.Key.Fifth, x.Count(), x.Sum(r => r.Likes), x.Sum(r => r.Dislikes)))
            .OrderBy(x => x.IsGiven)
            .ThenBy(x => x.Fifth)
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
