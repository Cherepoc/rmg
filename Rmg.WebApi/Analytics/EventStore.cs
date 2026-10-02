using Microsoft.Data.Sqlite;

namespace Rmg.WebApi.Analytics;

/// <summary>
///     Where the events are kept, which is one SQLite file beside the app rather than a database server:
///     a handful of rows per visit does not need one, and a file is something a deploy can leave alone.
/// </summary>
public sealed class EventStore
{
    public const string FileName = "analytics.db";

    private readonly string _connectionString;
    private readonly Lock _writing = new();

    // how many songs today's measurements are of, read from the file on the first of a day, so that a flood of
    // requests, refused for next to nothing, cannot fill the disk with rows
    private string? _songsDay;
    private int _songsToday;

    // the day the store last threw away what is older than it keeps, which the first write of every day does again,
    // so that a server that runs for weeks keeps no more than one started today would
    private string? _prunedDay;

    private EventStore(string connectionString, int retentionDays, int dailyEventsPerVisitor)
    {
        _connectionString = connectionString;
        RetentionDays = retentionDays;
        DailyEventsPerVisitor = dailyEventsPerVisitor;
    }

    /// <summary>How long events are kept before they are thrown away, in days.</summary>
    public int RetentionDays { get; }

    /// <summary>How many events one visitor can write in one day, which is well above honest use.</summary>
    public int DailyEventsPerVisitor { get; }

    /// <summary>
    ///     How many songs' measurements are kept in one day, well above what the server makes, as many as two a second
    ///     all day long, and a few megabytes.
    /// </summary>
    public const int DailySongs = 200_000;

    public static EventStore Create(
        string directory,
        int retentionDays = ServerSettings.DefaultRetentionDays,
        int dailyEventsPerVisitor = ServerSettings.DefaultEventsPerVisitorPerDay)
    {
        Directory.CreateDirectory(directory);

        var store = new EventStore(new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(directory, FileName),
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString(), retentionDays, dailyEventsPerVisitor);

        store.Migrate();
        return store;
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        // with WAL, a commit is written but not waited on to reach the disk, which only a checkpoint is: a power cut
        // loses the last few, an app that falls over none, and the file is never corrupt, for an event written in 0.1 ms
        // rather than 7; a setting of the connection, not of the file
        using var synchronous = connection.CreateCommand();
        synchronous.CommandText = "PRAGMA synchronous = NORMAL";
        synchronous.ExecuteNonQuery();

        return connection;
    }

    /// <summary>A connection for the summary to read through. Reads need no lock: the journal is WAL.</summary>
    public SqliteConnection OpenForReading()
    {
        return Open();
    }

    private void Migrate()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();

        // the columns are the measurements the events are allowed to carry, rather than a bag of JSON:
        // the shapes are known in advance, so they may as well be queryable without parsing anything
        command.CommandText = """
            PRAGMA journal_mode = WAL;

            CREATE TABLE IF NOT EXISTS events (
                id                INTEGER PRIMARY KEY AUTOINCREMENT,
                at                TEXT    NOT NULL,
                day               TEXT    NOT NULL,
                visitor           TEXT    NOT NULL,
                name              TEXT    NOT NULL,
                ms                INTEGER NULL,
                bytes             INTEGER NULL,
                seconds           REAL    NULL,
                seed              INTEGER NULL,
                detail            TEXT    NULL,
                version           TEXT    NULL,
                settings          TEXT    NULL,
                unconventionality INTEGER NULL,
                given             INTEGER NULL,
                identity          TEXT    NULL
            );

            CREATE INDEX IF NOT EXISTS events_day ON events (day);
            CREATE INDEX IF NOT EXISTS events_name_day ON events (name, day);
            CREATE INDEX IF NOT EXISTS events_visitor_day ON events (visitor, day);
            CREATE INDEX IF NOT EXISTS events_version_name ON events (version, name);
            """;

        command.ExecuteNonQuery();

        // what the server measured of every song asked of it, which is nothing about who asked
        using var songs = connection.CreateCommand();
        songs.CommandText = """
            CREATE TABLE IF NOT EXISTS songs (
                id                INTEGER PRIMARY KEY AUTOINCREMENT,
                at                TEXT    NOT NULL,
                day               TEXT    NOT NULL,
                outcome           TEXT    NOT NULL,
                wait_ms           INTEGER NOT NULL,
                make_ms           INTEGER NULL,
                render_ms         INTEGER NULL,
                running           INTEGER NOT NULL,
                waiting           INTEGER NOT NULL,
                version           TEXT    NOT NULL,
                unconventionality INTEGER NULL,
                notes             INTEGER NULL
            );

            CREATE INDEX IF NOT EXISTS songs_day ON songs (day);
            """;
        songs.ExecuteNonQuery();
    }

    /// <summary>
    ///     Writes one event, unless this visitor has already written more today than anybody honestly
    ///     would. Answers whether it was kept, which the endpoint does not pass on: a caller being
    ///     quietly ignored is not something worth telling it.
    /// </summary>
    public bool Write(StoredEvent stored)
    {
        // SQLite takes one writer at a time, and this is the only thing writing
        lock (_writing)
        {
            using var connection = Open();
            if (_prunedDay != stored.Day)
                PruneOlder(connection, stored.At);

            using var counting = connection.CreateCommand();
            counting.CommandText = "SELECT COUNT(*) FROM events WHERE visitor = $visitor AND day = $day";
            counting.Parameters.AddWithValue("$visitor", stored.Visitor);
            counting.Parameters.AddWithValue("$day", stored.Day);

            if (Convert.ToInt64(counting.ExecuteScalar()) >= DailyEventsPerVisitor) return false;

            using var writing = connection.CreateCommand();
            writing.CommandText = """
                INSERT INTO events (at, day, visitor, name, ms, bytes, seconds, seed, detail, version, settings, unconventionality, given, identity)
                VALUES ($at, $day, $visitor, $name, $ms, $bytes, $seconds, $seed, $detail, $version, $settings, $unconventionality, $given, $identity)
                """;

            writing.Parameters.AddWithValue("$at", stored.At.UtcDateTime.ToString("O"));
            writing.Parameters.AddWithValue("$day", stored.Day);
            writing.Parameters.AddWithValue("$visitor", stored.Visitor);
            writing.Parameters.AddWithValue("$name", stored.Name);
            writing.Parameters.AddWithValue("$ms", (object?)stored.Ms ?? DBNull.Value);
            writing.Parameters.AddWithValue("$bytes", (object?)stored.Bytes ?? DBNull.Value);
            writing.Parameters.AddWithValue("$seconds", (object?)stored.Seconds ?? DBNull.Value);
            writing.Parameters.AddWithValue("$seed", (object?)stored.Seed ?? DBNull.Value);
            writing.Parameters.AddWithValue("$detail", (object?)stored.Detail ?? DBNull.Value);
            writing.Parameters.AddWithValue("$version", (object?)stored.Version ?? DBNull.Value);
            writing.Parameters.AddWithValue("$settings", (object?)stored.Settings?.Format() ?? DBNull.Value);
            writing.Parameters.AddWithValue("$unconventionality", (object?)stored.Settings?.Unconventionality.Value ?? DBNull.Value);
            writing.Parameters.AddWithValue("$given", (object?)stored.Settings?.Unconventionality.IsGiven ?? DBNull.Value);
            writing.Parameters.AddWithValue("$identity", (object?)stored.Settings?.Identity ?? DBNull.Value);

            writing.ExecuteNonQuery();
            return true;
        }
    }

    /// <summary>
    ///     Writes what the server measured of a song, unless as many songs have been written today as are kept
    ///     (<see cref="DailySongs" />). Answers whether it was kept.
    /// </summary>
    public bool Write(SongMeasurement song)
    {
        lock (_writing)
        {
            using var connection = Open();
            var day = Day(song.At);
            if (_prunedDay != day)
                PruneOlder(connection, song.At);

            if (_songsDay != day)
            {
                using var counting = connection.CreateCommand();
                counting.CommandText = "SELECT COUNT(*) FROM songs WHERE day = $day";
                counting.Parameters.AddWithValue("$day", day);
                _songsToday = Convert.ToInt32(counting.ExecuteScalar());
                _songsDay = day;
            }

            if (_songsToday >= DailySongs) return false;

            using var writing = connection.CreateCommand();
            writing.CommandText = """
                INSERT INTO songs (at, day, outcome, wait_ms, make_ms, render_ms, running, waiting, version, unconventionality, notes)
                VALUES ($at, $day, $outcome, $wait, $make, $render, $running, $waiting, $version, $unconventionality, $notes)
                """;

            writing.Parameters.AddWithValue("$at", song.At.UtcDateTime.ToString("O"));
            writing.Parameters.AddWithValue("$day", day);
            writing.Parameters.AddWithValue("$outcome", SongOutcomes.Name(song.Outcome));
            writing.Parameters.AddWithValue("$wait", song.WaitMs);
            writing.Parameters.AddWithValue("$make", (object?)song.MakeMs ?? DBNull.Value);
            writing.Parameters.AddWithValue("$render", (object?)song.RenderMs ?? DBNull.Value);
            writing.Parameters.AddWithValue("$running", song.Running);
            writing.Parameters.AddWithValue("$waiting", song.Waiting);
            writing.Parameters.AddWithValue("$version", song.Version);
            writing.Parameters.AddWithValue("$unconventionality", (object?)song.Unconventionality ?? DBNull.Value);
            writing.Parameters.AddWithValue("$notes", (object?)song.Notes ?? DBNull.Value);

            writing.ExecuteNonQuery();
            _songsToday++;
            return true;
        }
    }

    /// <summary>Throws away everything older than <see cref="RetentionDays" />, and says how much went.</summary>
    public int Prune(DateTimeOffset now)
    {
        lock (_writing)
        {
            using var connection = Open();
            return PruneOlder(connection, now);
        }
    }

    private int PruneOlder(SqliteConnection connection, DateTimeOffset now)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM events WHERE day < $oldest; DELETE FROM songs WHERE day < $oldest";
        command.Parameters.AddWithValue("$oldest", Day(now.AddDays(-RetentionDays)));
        var pruned = command.ExecuteNonQuery();
        _prunedDay = Day(now);
        return pruned;
    }

    public static string Day(DateTimeOffset at)
    {
        return at.UtcDateTime.ToString("yyyy-MM-dd");
    }
}
