using Rmg.Core;
using Rmg.Core.Versions;
using Rmg.WebApi.Songs;

namespace Rmg.WebApi.Analytics;

public static class AnalyticsEndpoints
{
    /// <summary>The longest a detail is allowed to be, since it is one word of context and nothing else.</summary>
    private const int DetailLength = 40;

    /// <summary>
    ///     The events the pages send, and the summary of them, which is the operator's, served in the admin area only
    ///     (<see cref="Admin.AdminArea" />).
    /// </summary>
    public static void MapAnalytics(this IEndpointRouteBuilder routes, EventStore store, VisitorHasher hasher)
    {
        routes.MapPost("/api/tally", (HttpContext context, EventRequest? request) =>
        {
            if (!EventNames.IsKnown(request?.Name)) return Results.NoContent();

            var now = DateTimeOffset.UtcNow;
            var day = EventStore.Day(now);

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
                request.Seed is null ? null : Base64.ToSeed(request.Seed) is { } seed ? unchecked((long)seed) : null,
                Shorten(request.Detail),
                SongsVersion.IsNumber(request.Version) ? request.Version : null,
                SongSettings.Parse(request.Settings)
            ));

            // always the same answer, whether it was kept or not: a caller has no business knowing
            return Results.NoContent();
        });

        routes.MapGet($"{Admin.AdminArea.Path}/api/summary", (int? days) =>
        {
            return Results.Ok(store.Summarise(DateTimeOffset.UtcNow, Math.Clamp(days ?? 30, 1, store.RetentionDays)));
        });
    }

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
