using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Rmg.Core;

namespace Rmg.WebApi.Genres;

/// <summary>
///     The genres songs were asked for in, kept so that a link can carry one by a short key: every genre by the key of its
///     canonical form (<see cref="Canonical" />), written once, never changed, and never let go of, as a link may name it
///     for as long as it is kept; as many new ones a day as honest use asks at most (<see cref="DailyNew" />). Beside the analytics, in a file of its own, and kept whether anything is counted or not,
///     as it is what makes a link play: it knows a genre, never who asked for it.
/// </summary>
public sealed class GenreStore
{
    public const string FileName = "genres.db";

    /// <summary>The longest name a genre has, in bytes of UTF-8.</summary>
    public const int MaxNameBytes = 63;

    /// <summary>How many characters a key is: the first 64 bits of the canonical form's SHA-256, in base 62.</summary>
    public const int KeyLength = Base62.SeedLength;

    /// <summary>The canonical form's format, written into it, so that one written differently has another key.</summary>
    public const int Format = 1;

    /// <summary>How many genres not kept before are kept in one day, well above honest use, so that the file cannot be grown without bound.</summary>
    public const int DailyNew = 10_000;

    private readonly string _connectionString;
    private readonly Lock _keeping = new();

    // how many new genres today's day has kept, counted once from the file and then as they are kept
    private (string Day, long Count)? _today;

    private readonly int _dailyNew;

    private GenreStore(string connectionString, int dailyNew)
    {
        _connectionString = connectionString;
        _dailyNew = dailyNew;
    }

    public static GenreStore Create(string directory, int dailyNew = DailyNew)
    {
        Directory.CreateDirectory(directory);
        var store = new GenreStore(new SqliteConnectionStringBuilder { DataSource = Path.Combine(directory, FileName), Mode = SqliteOpenMode.ReadWriteCreate }.ToString(), dailyNew);
        using var connection = store.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;
            CREATE TABLE IF NOT EXISTS genres (key TEXT PRIMARY KEY, body TEXT NOT NULL, created TEXT NOT NULL);
            """;
        command.ExecuteNonQuery();
        return store;
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    /// <summary>
    ///     A genre written one way only, whatever order it came in: its format, its name, and its choices and their options
    ///     by their names in order, every amount a whole number, one alike at every end or three.
    /// </summary>
    /// <param name="choices">Its choices, every option's amounts as a request gives them, already read as valid.</param>
    public static string Canonical(string name, IReadOnlyDictionary<string, IReadOnlyDictionary<string, JsonElement>> choices)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("format", Format);
            writer.WriteString("name", name);
            writer.WriteStartObject("choices");
            foreach (var (choice, options) in choices.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                writer.WriteStartObject(choice);
                foreach (var (option, amount) in options.OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    int[] ends = amount.ValueKind == JsonValueKind.Array ? [..amount.EnumerateArray().Select(x => x.GetInt32())] : [amount.GetInt32()];
                    if (ends.Length == 3 && ends[0] == ends[1] && ends[1] == ends[2])
                        ends = [ends[1]];
                    if (ends.Length == 1)
                        writer.WriteNumber(option, ends[0]);
                    else
                    {
                        writer.WriteStartArray(option);
                        foreach (var end in ends)
                            writer.WriteNumberValue(end);
                        writer.WriteEndArray();
                    }
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>The key of a canonical form: the first 64 bits of its SHA-256, in <see cref="Base62" />'s digits, zeros in front.</summary>
    public static string KeyOf(string canonical) =>
        Base62.Write(BinaryPrimitives.ReadUInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))), KeyLength);

    /// <summary>
    ///     Keeps a genre's canonical form, once, and gives its key; none where another genre has the key already, or where
    ///     the day has kept as many new genres as it keeps (<see cref="DailyNew" />) and this one is new.
    /// </summary>
    public string? Keep(string canonical)
    {
        var key = KeyOf(canonical);
        if (Find(key) is { } known)
            return known == canonical ? key : null;

        var now = DateTimeOffset.UtcNow;
        var day = now.ToString("yyyy-MM-dd");
        lock (_keeping)
        {
            using var connection = Open();
            if (_today?.Day != day)
            {
                using var counting = connection.CreateCommand();
                counting.CommandText = "SELECT COUNT(*) FROM genres WHERE created >= $day";
                counting.Parameters.AddWithValue("$day", day);
                _today = (day, Convert.ToInt64(counting.ExecuteScalar()));
            }

            if (_today.Value.Count >= _dailyNew)
                return null;

            using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT OR IGNORE INTO genres (key, body, created) VALUES ($key, $body, $created)";
            insert.Parameters.AddWithValue("$key", key);
            insert.Parameters.AddWithValue("$body", canonical);
            insert.Parameters.AddWithValue("$created", now.ToString("O"));
            _today = (day, _today.Value.Count + insert.ExecuteNonQuery());
        }

        return Find(key) == canonical ? key : null;
    }

    /// <summary>The canonical form of the genre of a key, none for a key it does not know.</summary>
    public string? Find(string key)
    {
        if (key.Length != KeyLength || key.Any(x => !Base62.Digits.Contains(x)))
            return null;
        using var connection = Open();
        using var select = connection.CreateCommand();
        select.CommandText = "SELECT body FROM genres WHERE key = $key";
        select.Parameters.AddWithValue("$key", key);
        return select.ExecuteScalar() as string;
    }
}
