using System.Security.Cryptography;
using System.Text;

namespace Rmg.WebApi.Admin;

/// <summary>
///     The operator's pages and what they ask the server for, under <see cref="Path" />: served only to whoever gives the
///     credentials, the user <see cref="User" /> and the server's password, which the browser asks for once and sends on
///     with every request under it; with no password, not served at all, as figures and tools of the operator's that
///     were open by default would be open by accident.
/// </summary>
public static class AdminArea
{
    public const string Path = "/admin";

    public const string User = "admin";

    /// <summary>
    ///     How long a wrong password is answered after, every offered password checked one at a time for everyone, so that
    ///     guessing it goes a guess a second however many are asked at once, should the area face the internet without a
    ///     proxy's own limit.
    /// </summary>
    public static readonly TimeSpan WrongPasswordPause = TimeSpan.FromSeconds(1);

    private static readonly SemaphoreSlim Guessing = new(1, 1);

    /// <param name="password">The password, or null to serve nothing under <see cref="Path" />.</param>
    public static void UseAdmin(this WebApplication app, string? password)
    {
        app.Use(async (context, next) =>
        {
            // the path as the static files read it, a run of slashes in front one, so that //admin is the admin area too
            var path = new PathString("/" + (context.Request.Path.Value ?? "").TrimStart('/', '\\'));
            if (!path.StartsWithSegments(Path, StringComparison.OrdinalIgnoreCase))
            {
                await next();
                return;
            }

            if (password is null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            // every request offering credentials checked one at a time, a wrong one holding the turn for the pause whether
            // its asker waits or not, so that a right guess sent beside wrong ones waits its turn as they do; a browser's
            // first request, which offers none, answered at once
            var allowed = false;
            if (context.Request.Headers.Authorization.Count > 0)
            {
                await Guessing.WaitAsync(context.RequestAborted);
                try
                {
                    allowed = IsAllowed(context, password);
                    if (!allowed)
                        await Task.Delay(WrongPasswordPause, CancellationToken.None);
                }
                finally
                {
                    Guessing.Release();
                }
            }

            if (!allowed)
            {
                context.Response.Headers.WWWAuthenticate = "Basic realm=\"RMG admin\", charset=\"UTF-8\"";
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            await next();
        });
    }

    /// <summary>
    ///     Whether the request gives the credentials, compared without letting how long the comparison took say how much of
    ///     them was right.
    /// </summary>
    private static bool IsAllowed(HttpContext context, string password)
    {
        var offered = context.Request.Headers.Authorization.ToString();
        const string scheme = "Basic ";
        if (!offered.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
            return false;

        string credentials;
        try
        {
            credentials = Encoding.UTF8.GetString(Convert.FromBase64String(offered[scheme.Length..].Trim()));
        }
        catch (FormatException)
        {
            return false;
        }

        // their hashes compared, of one length whatever was offered, so that not even the length is told
        return CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(credentials)), SHA256.HashData(Encoding.UTF8.GetBytes($"{User}:{password}")));
    }
}
