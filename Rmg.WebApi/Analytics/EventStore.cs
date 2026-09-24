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
                id      INTEGER PRIMARY KEY AUTOINCREMENT,
                at      TEXT    NOT NULL,
                day     TEXT    NOT NULL,
                visitor TEXT    NOT NULL,
                name    TEXT    NOT NULL,
                ms      INTEGER NULL,
                bytes   INTEGER NULL,
                seconds REAL    NULL,
                seed    INTEGER NULL,
                detail  TEXT    NULL
            );

            CREATE INDEX IF NOT EXISTS events_day ON events (day);
            CREATE INDEX IF NOT EXISTS events_name_day ON events (name, day);
            CREATE INDEX IF NOT EXISTS events_visitor_day ON events (visitor, day);
            """;

        command.ExecuteNonQuery();
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

            using var counting = connection.CreateCommand();
            counting.CommandText = "SELECT COUNT(*) FROM events WHERE visitor = $visitor AND day = $day";
            counting.Parameters.AddWithValue("$visitor", stored.Visitor);
            counting.Parameters.AddWithValue("$day", stored.Day);

            if (Convert.ToInt64(counting.ExecuteScalar()) >= DailyEventsPerVisitor) return false;

            using var writing = connection.CreateCommand();
            writing.CommandText = """
                INSERT INTO events (at, day, visitor, name, ms, bytes, seconds, seed, detail)
                VALUES ($at, $day, $visitor, $name, $ms, $bytes, $seconds, $seed, $detail)
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

            writing.ExecuteNonQuery();
            return true;
        }
    }

    /// <summary>Throws away everything older than <see cref="RetentionDays" />, and says how much went.</summary>
    public int Prune(DateTimeOffset now)
    {
        lock (_writing)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();

            command.CommandText = "DELETE FROM events WHERE day < $oldest";
            command.Parameters.AddWithValue("$oldest", Day(now.AddDays(-RetentionDays)));

            return command.ExecuteNonQuery();
        }
    }

    public static string Day(DateTimeOffset at)
    {
        return at.UtcDateTime.ToString("yyyy-MM-dd");
    }
}
