using System.Security.Cryptography;
using System.Text;

namespace Rmg.WebApi.Analytics;

/// <summary>
///     Turns a request into a visitor, without keeping anything that says who that visitor is.
///     <para>
///         The address and the browser are hashed with a salt that changes every day, and neither is ever
///         written down. Two visits on the same day count as one visitor; the same person tomorrow is a
///         new one, because yesterday's salt cannot be arrived at from today's. Nothing is stored on the
///         visitor's machine either, which together is what keeps this the side of the line that needs no
///         banner asking to be allowed.
///     </para>
///     <para>
///         The salt comes from a secret kept on the server rather than from a number made up at startup,
///         so a restart in the middle of a day does not turn everyone into somebody new.
///     </para>
/// </summary>
public sealed class VisitorHasher
{
    private const string SecretFileName = "visitors.key";
    private const int SecretLength = 32;

    private readonly byte[] _secret;

    private VisitorHasher(byte[] secret)
    {
        _secret = secret;
    }

    /// <summary>Reads the secret beside the database, and makes one the first time there is none.</summary>
    public static VisitorHasher Create(string directory)
    {
        var path = Path.Combine(directory, SecretFileName);

        // a secret of its whole length, a file cut short by a crash as it was written made again
        if (File.Exists(path) && File.ReadAllBytes(path) is { Length: SecretLength } kept) return new VisitorHasher(kept);

        var secret = RandomNumberGenerator.GetBytes(SecretLength);
        // readable by nobody else from the moment it exists: whoever holds it could ask whether a given address visited on a
        // given day
        var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        using (var file = new FileStream(path, options))
            file.Write(secret);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        return new VisitorHasher(secret);
    }

    public string Hash(string day, string? address, string? userAgent)
    {
        var salt = HMACSHA256.HashData(_secret, Encoding.UTF8.GetBytes(day));
        var identity = Encoding.UTF8.GetBytes($"{address}\n{userAgent}");

        // shortened, because it only ever has to tell one visitor from another within a single day
        return Convert.ToHexStringLower(HMACSHA256.HashData(salt, identity))[..16];
    }
}
