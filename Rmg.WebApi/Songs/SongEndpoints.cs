using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.WebApi.Songs;

public static class SongEndpoints
{
    public static void MapSongs(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/songs/generate", GenerateSong);
    }

    /// <summary>
    ///     Writes a song as a MIDI file. What it is made of is reported in the headers, since the body is
    ///     the file itself.
    /// </summary>
    private static IResult GenerateSong(HttpContext context, GenerateSongRequest? request)
    {
        request ??= new GenerateSongRequest();

        if (!request.TryGetChannelTracks(out var channelTracks, out var error))
            return Results.BadRequest(new { error });

        var songSeed = request.Seed ?? Random.Shared.Next();

        MemoryStream stream;
        ImmutableSortedDictionary<byte, int> channelInstruments;
        try
        {
            var renderedSong = Render.RenderSong(SongGenerator.GenerateSong(songSeed));

            // asking for a channel this song does not play is the caller's mistake, not a failed generation
            var generatedInstruments = renderedSong.GetChannelInstruments();
            foreach (var channel in channelTracks.Keys.Where(channel => !generatedInstruments.ContainsKey(channel)))
                return Results.BadRequest(new
                {
                    error = $"Song {songSeed} does not play channel {channel + GenerateSongRequest.FirstChannel}."
                });

            renderedSong = renderedSong.WithChannelInstruments(Instruments(channelTracks));

            // what the song is made of, which is what the channels of the request name, switched off or not
            channelInstruments = renderedSong.GetChannelInstruments();

            renderedSong = renderedSong
                .WithChannelVolumes(Volumes(channelInstruments.Keys, channelTracks, request.SongVolume))
                .WithoutChannels(SwitchedOff(channelTracks));

            stream = new MemoryStream();
            renderedSong.Write(stream);
            stream.Seek(0, SeekOrigin.Begin);
        }
        catch (Exception ex)
        {
            return Results.Problem($"Generating the song with seed {songSeed} failed: {ex.Message}", statusCode: 500);
        }

        // lets the page show and reuse the seed it actually got when it asked for a random one
        context.Response.Headers["X-Song-Seed"] = songSeed.ToString();

        // and show what every channel plays before a note of it is heard
        context.Response.Headers["X-Song-Instruments"] = Describe(channelInstruments);

        return Results.File(stream, "audio/midi", SongFile.GetName(songSeed));
    }

    private static Dictionary<byte, int> Instruments(Dictionary<byte, TrackRequest> channelTracks)
    {
        return channelTracks
            .Where(x => x.Value.Instrument.HasValue)
            .ToDictionary(x => x.Key, x => x.Value.Instrument!.Value);
    }

    /// <summary>
    ///     The volume of every channel of the song, since the volume of the song applies to all of them and
    ///     not only to the channels the request names.
    /// </summary>
    private static Dictionary<byte, double> Volumes(
        IEnumerable<byte> channels,
        Dictionary<byte, TrackRequest> channelTracks,
        double songVolume
    )
    {
        return channels.ToDictionary(
            channel => channel,
            channel => songVolume * (channelTracks.TryGetValue(channel, out var track) ? track.Volume ?? 1 : 1)
        );
    }

    private static List<byte> SwitchedOff(Dictionary<byte, TrackRequest> channelTracks)
    {
        return channelTracks.Where(x => !x.Value.IsEnabled).Select(x => x.Key).ToList();
    }

    /// <summary>"1:40,10:16": the instrument of every channel, with the channels counted from 1.</summary>
    private static string Describe(ImmutableSortedDictionary<byte, int> channelInstruments)
    {
        return string.Join(
            ",",
            channelInstruments.Select(x => $"{x.Key + GenerateSongRequest.FirstChannel}:{x.Value}")
        );
    }
}
