namespace Rmg.WebApi.Analytics;

/// <summary>
///     What the page reports. The names are a closed set, and every event carries at most the four
///     measurements below, so a public endpoint can only ever write shapes this server already knows.
/// </summary>
public static class EventNames
{
    public const string PageOpen = "page_open";
    public const string SongGenerated = "song_generated";
    public const string SongFailed = "song_failed";
    public const string SoundFontReady = "soundfont_ready";
    public const string SoundFontFailed = "soundfont_failed";
    public const string AudioReady = "audio_ready";
    public const string Played = "play";
    public const string Listened = "listened";
    public const string MixChanged = "mix_changed";
    public const string DownloadedMidi = "download_mid";
    public const string ExportedMp3 = "export_mp3";
    public const string Shared = "shared";

    private static readonly HashSet<string> Known =
    [
        PageOpen, SongGenerated, SongFailed, SoundFontReady, SoundFontFailed, AudioReady,
        Played, Listened, MixChanged, DownloadedMidi, ExportedMp3, Shared
    ];

    public static bool IsKnown(string? name)
    {
        return name is not null && Known.Contains(name);
    }
}

/// <param name="Name">One of <see cref="EventNames" />. Anything else is refused.</param>
/// <param name="Ms">How long something took, in milliseconds.</param>
/// <param name="Bytes">How large something was.</param>
/// <param name="Seconds">How long something was listened to.</param>
/// <param name="Seed">The song it was about, which is the only thing here that identifies anything.</param>
/// <param name="Detail">One short word of context: where a soundfont came from, which control was moved.</param>
public sealed record EventRequest(
    string? Name,
    long? Ms = null,
    long? Bytes = null,
    double? Seconds = null,
    long? Seed = null,
    string? Detail = null
);

/// <summary>An event as it is kept: the request, with the day and the visitor it came from.</summary>
public sealed record StoredEvent(
    DateTimeOffset At,
    string Day,
    string Visitor,
    string Name,
    long? Ms,
    long? Bytes,
    double? Seconds,
    long? Seed,
    string? Detail
);
