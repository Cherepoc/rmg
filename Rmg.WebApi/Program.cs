using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Rendering;

var builder = WebApplication.CreateBuilder(args);


// Configure Kestrel for Linux
builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenAnyIP(5000);
    }
);

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// API endpoints
app.MapGet("/midi/generate", () =>
{
    try
    {
        var songPattern = SongGenerator.GenerateSong();
        var renderedSong = Render.RenderSong(songPattern);

        var stream = new MemoryStream();
        renderedSong.Write(stream);
        stream.Seek(0, SeekOrigin.Begin);

        return Results.File(stream, "audio/midi", "generated_song.mid");
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.Run();
