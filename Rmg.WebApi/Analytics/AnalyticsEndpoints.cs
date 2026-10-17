using Rmg.Core;
using Rmg.Core.Versions;
using Rmg.WebApi.Songs;

namespace Rmg.WebApi.Analytics;

public static class AnalyticsEndpoints
{
    /// <summary>The longest a detail is allowed to be, since it is one word of context and nothing else: a verdict's question id and its choice.</summary>
    public const int DetailLength = 40;

    /// <summary>
    ///     How many visitors' worth of events one network writes in a day at most, an IPv4 address or an IPv6 /64, so that
    ///     a client changing its user agent or its address within its network cannot spend the day's cap for everyone; its
    ///     ratings and verdicts counted apart from what its pages tell by themselves, as a visitor's are, so that an office's
    ///     or a carrier's many listeners behind one address never lose what they tell on purpose to their pages' telemetry.
    /// </summary>
    public const int VisitorsPerNetwork = 8;

    /// <summary>
    ///     The events the pages send, and the summary of them, which is the operator's, served in the admin area only
    ///     (<see cref="Admin.AdminArea" />).
    /// </summary>
    /// <param name="isListening">
    ///     Whether the server listens (<see cref="ServerSettings.IsListening" />), a test instance, whose listening mode's
    ///     verdicts the page's index of questions shows, to anyone, as nothing in them is anyone's.
    /// </param>
    public static void MapAnalytics(this IEndpointRouteBuilder routes, EventStore store, VisitorHasher hasher, bool isListening)
    {
        if (isListening)
            routes.MapGet("/api/listening/verdicts", () => Results.Ok(store.LatestVerdicts()));

        // the events each network wrote today, counted here, as nothing about an address is written down
        var networks = new NetworkCount();
        routes.MapPost("/api/tally", (HttpContext context, EventRequest? request) =>
        {
            // a verdict only where the server listens, as elsewhere its song without the device cannot be made, and only as the
            // page gives it, its question and its choice
            if (!EventNames.IsKnown(request?.Name) || request!.Name == EventNames.Verdict && (!isListening || !IsVerdict(request.Detail))) return Results.NoContent();

            var now = DateTimeOffset.UtcNow;
            var day = EventStore.Day(now);
            if (!networks.Admit(day, context.Connection.RemoteIpAddress, EventNames.IsDeliberate(request.Name), store.DailyEventsPerVisitor * VisitorsPerNetwork))
                return Results.NoContent();

            // the address is hashed and dropped on the floor; nothing here writes it down
            var visitor = hasher.Hash(
                day,
                context.Connection.RemoteIpAddress?.ToString(),
                context.Request.Headers.UserAgent.ToString()
            );

            store.Write(new StoredEvent(
                now,
                day,
                visitor,
                request!.Name!,
                Sane(request.Ms, 0, (long)TimeSpan.FromHours(6).TotalMilliseconds),
                Sane(request.Bytes, 0, 8L * 1024 * 1024 * 1024),
                request.Seconds is >= 0 and <= 86_400 ? Math.Round(request.Seconds.Value, 1) : null,
                request.Seed is null ? null : Base62.ToSeed(request.Seed) is { } seed ? unchecked((long)seed) : null,
                Shorten(request.Detail),
                SongsVersion.IsHeard(request.Version) ? request.Version : null,
                SongSettings.Parse(request.Settings),
                request.Genre is { Length: > 0 and <= Genres.GenreStore.KeyLength } genre && genre.All(Base62.Digits.Contains) ? genre : null
            ));

            // always the same answer, whether it was kept or not: a caller has no business knowing
            return Results.NoContent();
        });

        routes.MapGet($"{Admin.AdminArea.Path}/api/summary", (int? days) =>
        {
            return Results.Ok(store.Summarise(DateTimeOffset.UtcNow, Math.Clamp(days ?? 30, 1, store.RetentionDays)));
        });
    }

    /// <summary>
    ///     The events every network wrote in the day, what it told on purpose and what its pages told apart, forgotten the
    ///     next; no more networks than a day's events, past which a new one is refused as the store would refuse its
    ///     events, so that one rotating its addresses grows it no further.
    /// </summary>
    public sealed class NetworkCount
    {
        private readonly Dictionary<(string, bool), int> _counts = [];
        private string _day = "";

        /// <summary>Whether a network may write one more event today, counting it if it may.</summary>
        public bool Admit(string day, System.Net.IPAddress? address, bool deliberate, int most)
        {
            // a network as the song queue tells a client, an IPv6 home's whole /64 one
            var network = Songs.SongQueue.ClientOf(address);
            lock (_counts)
            {
                if (_day != day)
                {
                    _day = day;
                    _counts.Clear();
                }
                var count = _counts.GetValueOrDefault((network, deliberate));
                if (count >= most || count == 0 && _counts.Count >= 2 * EventStore.DailyEvents)
                    return false;
                _counts[(network, deliberate)] = count + 1;
                return true;
            }
        }
    }

    /// <summary>A verdict's detail as the page writes it, "question:choice", its question a word or two, its choice one of the four, whole within the detail's length, never kept cut.</summary>
    public static bool IsVerdict(string? detail) =>
        detail?.Length <= DetailLength && detail.Split(':') is [{ Length: > 0 } question, "with" or "without" or "same" or "unsure"]
                                       && question.All(x => char.IsAsciiLetterLower(x) || char.IsAsciiDigit(x) || x == '-');

    private static long? Sane(long? value, long lowest, long highest)
    {
        return value >= lowest && value <= highest ? value : null;
    }

    private static string? Shorten(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail)) return null;

        var trimmed = detail.Trim();
        return trimmed.Length <= DetailLength ? trimmed : trimmed[..DetailLength];
    }
}
