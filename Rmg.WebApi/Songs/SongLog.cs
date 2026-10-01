using Rmg.WebApi.Analytics;

namespace Rmg.WebApi.Songs;

/// <summary>
///     Where the server keeps what it measured of the songs asked of it (<see cref="SongMeasurement" />): the analytics,
///     or nowhere, where nothing is counted. A measurement that cannot be written is logged and let go of, as the song
///     itself is what was asked for.
/// </summary>
public sealed class SongLog
{
    private readonly EventStore? _store;
    private readonly ILogger? _logger;

    private SongLog(EventStore? store, ILogger? logger)
    {
        _store = store;
        _logger = logger;
    }

    public static SongLog Off { get; } = new(null, null);

    public static SongLog To(EventStore store, ILogger<SongLog> logger) => new(store, logger);

    public void Write(SongMeasurement song)
    {
        if (_store is null) return;

        try
        {
            _store.Write(song);
        }
        catch (Exception ex)
        {
            _logger!.LogWarning(ex, "A song's measurement could not be written");
        }
    }
}
