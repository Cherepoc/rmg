using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Rmg.WebApi;
using Rmg.WebApi.Admin;
using Rmg.WebApi.Analytics;
using Rmg.WebApi.Genres;
using Rmg.WebApi.Songs;
using Rmg.WebApi.SoundFonts;

var builder = WebApplication.CreateBuilder(args);

// appsettings.json holds the settings and their defaults; the environment beats it, as RMG_PORT and
// the like, which keeps a secret out of a file that is in the repository; the command line beats both
builder.Configuration.AddEnvironmentVariables("RMG_").AddCommandLine(args);

if (!ServerSettings.TryRead(
        builder.Configuration,
        Path.Combine(builder.Environment.ContentRootPath, "state"),
        out var settings,
        out var error))
{
    Console.Error.WriteLine(error);
    return 1;
}

builder.WebHost.ConfigureKestrel(options =>
{
    switch (settings!.Interfaces)
    {
        case Interfaces.Loopback: options.ListenLocalhost(settings.Port); break;
        case Interfaces.One: options.Listen(settings.Address!, settings.Port); break;
        default: options.ListenAnyIP(settings.Port); break;
    }

    // a request is a song's settings or a genre, a few kilobytes, never the 30 megabytes Kestrel would read by default
    options.Limits.MaxRequestBodySize = MaxRequestBytes;
});

// Behind a reverse proxy the scheme, host and client address of a request are whatever the proxy says
// they are. Only the proxy is believed, because anything that can reach the port could forge these: the
// loopback address is trusted out of the box, which covers a proxy on this same machine, and any other
// has to be named in KnownProxies.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                               | ForwardedHeaders.XForwardedProto
                               | ForwardedHeaders.XForwardedHost;

    foreach (var proxy in settings!.KnownProxies) options.KnownProxies.Add(proxy);
});

// the JSON the API reads and writes is described at compile time, which a native build cannot do without
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default));

// a song is a burst of work for one core, so as many are made at once as there are cores, a few more wait their turn,
// a few a client, taking their turns client by client, and the rest are turned away at once rather than slowing every
// song down
builder.Services.AddSingleton(new SongQueue(Environment.ProcessorCount, 4 * Environment.ProcessorCount, 4));

// the genres songs were asked for in, which a link names by a key, kept beside the analytics whether anything is
// counted or not, as links play by them
builder.Services.AddSingleton(Rmg.WebApi.Genres.GenreStore.Create(settings!.StateDirectory));

// kept beside the app rather than in it, so a deploy replaces the app and leaves the figures alone, and with them what
// the server measured of every song asked of it
var events = settings!.IsCounting
    ? EventStore.Create(settings.StateDirectory, settings.RetentionDays, settings.EventsPerVisitorPerDay)
    : null;
builder.Services.AddSingleton(provider => events is null
    ? SongLog.Off
    : SongLog.To(events, provider.GetRequiredService<ILogger<SongLog>>()));

// a song holds its thread until it is made, and the pool starts only as many threads at once as there are cores, so
// with every core making one, the rest of the server, a page, a soundfont or the next song let out of the queue, would
// wait for the pool to notice and add a thread; it gets as many again of its own
ThreadPool.GetMinThreads(out var workerThreads, out var completionThreads);
ThreadPool.SetMinThreads(workerThreads + Environment.ProcessorCount, completionThreads);

var app = builder.Build();

// first, so that everything after it sees the request as the client made it, not as the proxy relayed it
app.UseForwardedHeaders();

var soundFonts = SoundFontLibrary.Create(
    app.Environment.WebRootPath, settings!.SoundFontDirectory, settings.DefaultSoundFont);

app.Logger.LogInformation("Offering the soundfonts in {Directory}", soundFonts.Directory);

if (soundFonts.IsPreferredMissing())
    app.Logger.LogWarning(
        "DefaultSoundFont is {Preferred}, which is not in {Directory}, so the first one there is used instead",
        settings.DefaultSoundFont, soundFonts.Directory);

// the operator's pages and what they ask for, before anything is served from them
if (settings.DashboardToken is null)
    app.Logger.LogInformation("No DashboardToken, so the admin area is not served at all");
app.UseAdmin(settings.DashboardToken);

app.UseDefaultFiles();

// soundfonts have no registered media type, so static files would refuse to serve them unasked
var contentTypes = new FileExtensionContentTypeProvider();
foreach (var extension in SoundFontLibrary.Extensions) contentTypes.Mappings[extension] = "application/octet-stream";

// a licence is there to be read, so it is served as text rather than handed over as a download
foreach (var extension in SoundFontLibrary.LicenseExtensions)
    contentTypes.Mappings[extension] = "text/plain; charset=utf-8";

// a soundfont directory of its own is not under the web root, so it is served in its own right, and
// before the web root, or anything left in the old place would answer for it and be served instead
if (settings.SoundFontDirectory is not null)
    app.UseStaticFiles(new StaticFileOptions
    {
        ContentTypeProvider = contentTypes,
        FileProvider = new PhysicalFileProvider(soundFonts.Directory),
        RequestPath = $"/{SoundFontLibrary.DirectoryName}"
    });

// The bundles live in a folder named by a hash of what is in them, so what is at an address there never
// changes: a browser keeps it for good and never asks again. The pages are what point at the current
// folder, so a browser has to ask about those every time, which the ETag keeps cheap. A soundfont never
// changes under the same name either, and is left to the usual caching.
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = contentTypes,
    OnPrepareResponse = context =>
    {
        var path = context.Context.Request.Path;
        if (path.StartsWithSegments($"/{SoundFontLibrary.DirectoryName}")) return;

        context.Context.Response.Headers.CacheControl = path.StartsWithSegments("/assets")
            ? "public, max-age=31536000, immutable"
            : "no-cache";
    }
});

app.MapSoundFonts(soundFonts);
app.MapSongs(settings.IsListening);
app.MapSongDescriptions();
app.MapGenres();

if (events is not null)
{
    var pruned = events.Prune(DateTimeOffset.UtcNow);

    app.Logger.LogInformation(
        "Keeping analytics in {Directory} for {Days} days; {Pruned} older events and songs were dropped",
        settings.StateDirectory, settings.RetentionDays, pruned);

    app.MapAnalytics(events, VisitorHasher.Create(settings.StateDirectory), settings.IsListening);
}
else if (settings.IsListening)
{
    // a listening server is there for its verdicts, which only analytics keep
    app.Logger.LogCritical("A listening server keeps its verdicts, so AnalyticsEnabled must be true where it listens");
    return 1;
}
else
{
    app.Logger.LogInformation("AnalyticsEnabled is false, so nothing is counted and nothing is written");
}

app.Run();

return 0;

/// <summary>The server's own constants.</summary>
internal static partial class Program
{
    /// <summary>The longest request body the server reads: a genre of every choice, several times over.</summary>
    internal const long MaxRequestBytes = 1 << 20;
}
