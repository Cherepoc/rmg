using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;
using Rmg.Core.Versions;
using Rmg.WebApi.Analytics;

namespace Rmg.WebApi.Songs;

public static class SongEndpoints
{
    public static void MapSongs(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/songs/generate", GenerateSong);

        // what a song may be given: its tempos, in beats a minute, by their steps, and its meters, in the order a request
        // names them by
        routes.MapGet("/api/songs/options", () => Results.Ok(new SongOptions(
            [..Enumerable.Range(0, TempoScale.Steps).Select(x => TempoScale.Multiplier(x) * Meter.BaseTempo)],
            [..Meter.Options.Select(x => $"{x.Meter.TimeSignature.Numerator}/{x.Meter.TimeSignature.Denominator}")]
        )));

        // what the page shows as RMG's version
        routes.MapGet("/api/version", () => Results.Ok(new VersionResponse(SongsVersion.Number, SongsVersion.Commit)));
    }

    private static readonly JsonSerializerOptions HeaderJson = new(JsonSerializerDefaults.Web);

    /// <summary>
    ///     Writes a song as a MIDI file. What it is made of is reported in the headers, since the body is the file
    ///     itself. A song waits its turn (<see cref="SongQueue" />), and one turned away is told to ask again in a moment.
    ///     How long it waited and took, and how it ended, is kept (<see cref="SongLog" />) once its turn is given back, so
    ///     that no core waits on the disk.
    /// </summary>
    private static async Task<IResult> GenerateSong(HttpContext context, SongQueue queue, SongLog log, GenerateSongRequest? request)
    {
        request ??= new GenerateSongRequest();

        if (!request.TryRead(out var seed, out var overrides, out var mix, out var error))
            return Results.BadRequest(new ErrorResponse(error!));

        var arrived = Stopwatch.GetTimestamp();
        var (running, waiting) = queue.Load;
        long waited;
        long? made = null, rendered = null;
        int? unconventionality = null, notes = null;
        void Record(SongOutcome outcome) => log.Write(new SongMeasurement(
            DateTimeOffset.UtcNow, outcome, waited, made, rendered, running, waiting, SongsVersion.Number, unconventionality, notes));

        SongQueue.Turn turn;
        try
        {
            turn = await queue.Enter(SongQueue.ClientOf(context.Connection.RemoteIpAddress), context.RequestAborted);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            waited = Milliseconds(arrived);
            Record(SongOutcome.CancelledWaiting);
            throw;
        }

        waited = Milliseconds(arrived);
        if (turn.Refusal is { } refusal)
        {
            Record(refusal == SongQueue.Refusal.Client ? SongOutcome.RefusedClient : SongOutcome.RefusedFull);
            context.Response.Headers.RetryAfter = "1";
            return refusal == SongQueue.Refusal.Client
                ? Results.Problem("You have as many songs waiting as you may.", statusCode: StatusCodes.Status429TooManyRequests)
                : Results.Problem("The server is busy making other songs.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var songSeed = seed ?? Seeds.Random();

        IResult result;
        SongOutcome ended;
        using (turn)
        {
            try
            {
                // a song nobody waits for any more is given up, its core going to the next
                var started = Stopwatch.GetTimestamp();
                var song = SongGenerator.GenerateSong(songSeed, overrides, context.RequestAborted);
                made = Milliseconds(started);
                notes = song.Notes?.Values.Sum(x => x.Count);

                started = Stopwatch.GetTimestamp();
                var renderedSong = Render.RenderSong(song, mix, context.RequestAborted);
                var report = SongReport.Of(song, renderedSong, overrides);
                var stream = new MemoryStream();
                renderedSong.Write(stream, SongsVersion.Label(songSeed));
                stream.Seek(0, SeekOrigin.Begin);
                rendered = Milliseconds(started);
                unconventionality = report.Unconventionality.Value;

                ended = SongOutcome.Made;
                result = Respond(context, songSeed, report, stream);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                ended = SongOutcome.CancelledMaking;
                result = Results.Empty;
            }
            catch (Exception ex)
            {
                ended = SongOutcome.Failed;
                result = Results.Problem($"Song {Base64.FromSeed(songSeed)}: {ex.Message}", statusCode: 500);
            }
        }

        Record(ended);
        return result;
    }

    private static long Milliseconds(long since) => (long)Stopwatch.GetElapsedTime(since).TotalMilliseconds;

    private static IResult Respond(HttpContext context, ulong songSeed, SongReport report, MemoryStream stream)
    {
        // lets the page show and reuse the seed it actually got when it asked for a random one
        context.Response.Headers["X-Song-Seed"] = Base64.FromSeed(songSeed);

        // and which songs' version it is, which the page reports with what it tells of the song
        context.Response.Headers["X-Song-Version"] = SongsVersion.Number;

        // and everything the song drew or was given, and which channel plays which part, for the page to show and
        // set before a note of it is heard
        context.Response.Headers["X-Song-Settings"] = JsonSerializer.Serialize(report, HeaderJson);

        return Results.File(stream, "audio/midi", SongFile.GetName(songSeed));
    }
}

/// <summary>What a song may be given, in the order a request names them by.</summary>
/// <param name="Tempos">The tempos, in beats a minute, by their steps (<see cref="TempoScale" />).</param>
/// <param name="Meters">The meters, as time signatures.</param>
public sealed record SongOptions(ImmutableArray<double> Tempos, ImmutableArray<string> Meters);

/// <summary>RMG's version: the songs' number, which goes up when the songs change, and the commit it is built from.</summary>
public sealed record VersionResponse(string Version, string? Commit);
