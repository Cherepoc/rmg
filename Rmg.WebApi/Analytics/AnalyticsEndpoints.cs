using System.Security.Cryptography;
using System.Text;

namespace Rmg.WebApi.Analytics;

public static class AnalyticsEndpoints
{
    /// <summary>The longest a detail is allowed to be, since it is one word of context and nothing else.</summary>
    private const int DetailLength = 40;

    /// <param name="token">
    ///     What the summary asks for before it answers. Null leaves the summary switched off altogether,
    ///     which is the right way round: traffic figures are nobody's business but the operator's, and a
    ///     dashboard that is public by default would be public by accident.
    /// </param>
    public static void MapAnalytics(this IEndpointRouteBuilder routes, EventStore store, VisitorHasher hasher, string? token)
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
                Sane(request.Seed, int.MinValue, int.MaxValue),
                Shorten(request.Detail)
            ));

            // always the same answer, whether it was kept or not: a caller has no business knowing
            return Results.NoContent();
        });

        if (token is null) return;

        routes.MapGet("/api/tally/summary", (HttpContext context, int? days) =>
        {
            if (!IsAllowed(context, token)) return Results.Unauthorized();

            return Results.Ok(store.Summarise(DateTimeOffset.UtcNow, Math.Clamp(days ?? 30, 1, store.RetentionDays)));
        });
    }

    /// <summary>
    ///     Compares the token without letting how long the comparison took say how much of it was right.
    /// </summary>
    private static bool IsAllowed(HttpContext context, string token)
    {
        var offered = context.Request.Headers.Authorization.ToString();
        const string scheme = "Bearer ";

        if (!offered.StartsWith(scheme, StringComparison.Ordinal)) return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(offered[scheme.Length..]),
            Encoding.UTF8.GetBytes(token)
        );
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
