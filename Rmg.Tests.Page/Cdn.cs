using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Playwright;

namespace Rmg.Tests.Page;

/// <summary>
///     The page's modules from the CDN (cdn.jsdelivr.net), served to every page from a cache rather than fetched by each:
///     a module holds back the page's load, so that one slow fetch would fail a test, and their URLs name their versions,
///     so that a copy kept on disk stays right. Fetched once a run where the disk has none, and again where that failed.
/// </summary>
internal static class Cdn
{
    public const string Pattern = "https://cdn.jsdelivr.net/**";

    private static readonly string Directory = Path.Combine(Path.GetTempPath(), "rmg-page-tests-cdn");

    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(60) };

    private static readonly ConcurrentDictionary<string, Lazy<Task<(byte[] Body, string Type)>>> Files = new();

    public static async Task Serve(IRoute route)
    {
        var file = Files.GetOrAdd(route.Request.Url, url => new Lazy<Task<(byte[] Body, string Type)>>(() => Load(url)));
        (byte[] Body, string Type) loaded;
        try
        {
            loaded = await file.Value;
        }
        catch
        {
            // a failed fetch not kept, so that the next page fetches it again, and this one's request failed at once
            // rather than left to hang until the page gives up
            Files.TryRemove(new KeyValuePair<string, Lazy<Task<(byte[] Body, string Type)>>>(route.Request.Url, file));
            try
            {
                await route.AbortAsync("failed");
            }
            catch (PlaywrightException)
            {
                // let go of by the page as it closed
            }

            return;
        }

        var (body, type) = loaded;
        try
        {
            await route.FulfillAsync(new RouteFulfillOptions
            {
                Status = 200,
                BodyBytes = body,
                ContentType = type,
                Headers = new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = "*" }
            });
        }
        catch (PlaywrightException)
        {
            // let go of by the page as it closed
        }
    }

    private static async Task<(byte[], string)> Load(string url)
    {
        var name = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)))[..32];
        var (path, typePath) = (Path.Combine(Directory, name), Path.Combine(Directory, name + ".type"));
        // an empty type, a run's broken off as it wrote it, as none
        if (File.Exists(path) && File.Exists(typePath) && await File.ReadAllTextAsync(typePath) is { Length: > 0 } kept && !string.IsNullOrWhiteSpace(kept))
            return (await File.ReadAllBytesAsync(path), kept);

        using var response = await Client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsByteArrayAsync();
        var type = response.Content.Headers.ContentType?.ToString() ?? throw new InvalidOperationException($"{url} came with no content type.");
        System.IO.Directory.CreateDirectory(Directory);
        // each written whole under a name of its own and moved into place, the type last, so that neither is ever read half
        // written, by this run broken off or by another run beside it
        var temporary = Path.Combine(Directory, $"{name}.{Guid.NewGuid():N}");
        await File.WriteAllBytesAsync(temporary, body);
        File.Move(temporary, path, true);
        await File.WriteAllTextAsync(temporary, type);
        File.Move(temporary, typePath, true);
        return (body, type);
    }
}
