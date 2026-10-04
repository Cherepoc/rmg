using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Rmg.Core;

namespace Rmg.WebApi.Genres;

/// <summary>
///     The genres songs were asked for in, kept so that a link can carry one by a short key: every genre by the key of its
///     canonical form (<see cref="Canonical" />), written once, never changed, and never let go of, as a link may name it
///     for as long as it is kept. Beside the analytics, in a file of its own, and kept whether anything is counted or not,
///     as it is what makes a link play: it knows a genre, never who asked for it.
/// </summary>
public sealed class GenreStore
{
    public const string FileName = "genres.db";

    /// <summary>The longest name a genre has, in bytes of UTF-8.</summary>
    public const int MaxNameBytes = 63;

    /// <summary>How many characters a key is: 66 bits of the canonical form's SHA-256, six to a character.</summary>
    public const int KeyLength = 11;

    /// <summary>The canonical form's format, written into it, so that one written differently has another key.</summary>
    public const int Format = 1;

    private readonly string _connectionString;

    private GenreStore(string connectionString)
    {
        _connectionString = connectionString;
    }

    public static GenreStore Create(string directory)
    {
        Directory.CreateDirectory(directory);
        var store = new GenreStore(new SqliteConnectionStringBuilder { DataSource = Path.Combine(directory, FileName), Mode = SqliteOpenMode.ReadWriteCreate }.ToString());
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

    /// <summary>The key of a canonical form: the first 66 bits of its SHA-256, in <see cref="Base64" />'s digits.</summary>
    public static string KeyOf(string canonical)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        var key = new char[KeyLength];
        for (var i = 0; i < KeyLength; i++)
        {
            var bit = 6 * i;
            var value = ((hash[bit / 8] << 8 | hash[bit / 8 + 1]) >> (10 - bit % 8)) & 63;
            key[i] = Base64.Digits[value];
        }

        return new string(key);
    }

    /// <summary>Keeps a genre's canonical form, once, and gives its key; none where another genre has the key already.</summary>
    public string? Keep(string canonical)
    {
        var key = KeyOf(canonical);
        using var connection = Open();
        using var insert = connection.CreateCommand();
        insert.CommandText = "INSERT OR IGNORE INTO genres (key, body, created) VALUES ($key, $body, $created)";
        insert.Parameters.AddWithValue("$key", key);
        insert.Parameters.AddWithValue("$body", canonical);
        insert.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("O"));
        insert.ExecuteNonQuery();
        return Find(key) == canonical ? key : null;
    }

    /// <summary>The canonical form of the genre of a key, none for a key it does not know.</summary>
    public string? Find(string key)
    {
        if (key.Length != KeyLength || key.Any(x => !Base64.Digits.Contains(x)))
            return null;
        using var connection = Open();
        using var select = connection.CreateCommand();
        select.CommandText = "SELECT body FROM genres WHERE key = $key";
        select.Parameters.AddWithValue("$key", key);
        return select.ExecuteScalar() as string;
    }
}
