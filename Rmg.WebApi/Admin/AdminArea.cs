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

    /// <param name="password">The password, or null to serve nothing under <see cref="Path" />.</param>
    public static void UseAdmin(this WebApplication app, string? password)
    {
        app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments(Path))
            {
                await next();
                return;
            }

            if (password is null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            if (!IsAllowed(context, password))
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

        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(credentials), Encoding.UTF8.GetBytes($"{User}:{password}"));
    }
}
