using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Rmg.WebApi;
using Rmg.WebApi.Analytics;
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

var app = builder.Build();

// first, so that everything after it sees the request as the client made it, not as the proxy relayed it
app.UseForwardedHeaders();

var soundFonts = SoundFontLibrary.Create(
    app.Environment.WebRootPath, settings.SoundFontDirectory, settings.DefaultSoundFont);

app.Logger.LogInformation("Offering the soundfonts in {Directory}", soundFonts.Directory);

if (soundFonts.IsPreferredMissing())
    app.Logger.LogWarning(
        "DefaultSoundFont is {Preferred}, which is not in {Directory}, so the first one there is used instead",
        settings.DefaultSoundFont, soundFonts.Directory);

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

app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = contentTypes });

app.MapSoundFonts(soundFonts);
app.MapSongs();

if (settings.IsCounting)
{
    // kept beside the app rather than in it, so a deploy replaces the app and leaves the figures alone
    var events = EventStore.Create(settings.StateDirectory, settings.RetentionDays, settings.EventsPerVisitorPerDay);
    var pruned = events.Prune(DateTimeOffset.UtcNow);

    app.Logger.LogInformation(
        "Keeping analytics in {Directory} for {Days} days; {Pruned} older events were dropped",
        settings.StateDirectory, settings.RetentionDays, pruned);

    if (settings.DashboardToken is null)
        app.Logger.LogInformation("No DashboardToken, so the analytics summary is not served at all");

    app.MapAnalytics(events, VisitorHasher.Create(settings.StateDirectory), settings.DashboardToken);
}
else
{
    app.Logger.LogInformation("AnalyticsEnabled is false, so nothing is counted and nothing is written");
}

app.Run();

return 0;
