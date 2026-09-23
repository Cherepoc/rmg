using Microsoft.AspNetCore.StaticFiles;
using Rmg.WebApi.Songs;
using Rmg.WebApi.SoundFonts;

var builder = WebApplication.CreateBuilder(args);

// binds 0.0.0.0, so the page is reachable from other machines on the network
builder.WebHost.ConfigureKestrel(options => { options.ListenAnyIP(5000); });

var app = builder.Build();

var soundFonts = SoundFontLibrary.Create(app.Environment.WebRootPath);
app.Logger.LogInformation("Offering the soundfonts in {Directory}", soundFonts.Directory);

app.UseDefaultFiles();

// soundfonts have no registered media type, so static files would refuse to serve them unasked
var contentTypes = new FileExtensionContentTypeProvider();
foreach (var extension in SoundFontLibrary.Extensions) contentTypes.Mappings[extension] = "application/octet-stream";

app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = contentTypes });

app.MapSoundFonts(soundFonts);
app.MapSongs();

app.Run();
