using System.Text.Json;
using System.Text.Json.Serialization;
using Rmg.WebApi.Analytics;
using Rmg.WebApi.Songs;
using Rmg.WebApi.SoundFonts;

namespace Rmg.WebApi;

/// <summary>
///     Every type the API reads or writes as JSON, written out at compile time rather than discovered by
///     reflection when a request arrives, which is quicker and keeps the server ready for a trimmed or native
///     build, where a type missing from here would fail on its first request.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(GenerateSongRequest))]
[JsonSerializable(typeof(EventRequest))]
[JsonSerializable(typeof(IEnumerable<SoundFont>))]
[JsonSerializable(typeof(AnalyticsSummary))]
[JsonSerializable(typeof(ListeningVerdicts))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(VersionResponse))]
[JsonSerializable(typeof(Admin.SongDescriptionResponse))]
[JsonSerializable(typeof(IReadOnlyList<Genres.GenreChoiceResponse>))]
[JsonSerializable(typeof(Genres.GenrePreviewRequest))]
[JsonSerializable(typeof(Genres.GenrePreviewResponse))]
[JsonSerializable(typeof(Genres.GenrePreviewsResponse))]
[JsonSerializable(typeof(IReadOnlyDictionary<string, IReadOnlyDictionary<string, System.Text.Json.JsonElement>>))]
[JsonSerializable(typeof(SongOptions))]
[JsonSerializable(typeof(SongReport))]
internal sealed partial class AppJsonContext : JsonSerializerContext;

/// <summary>What a request the API will not serve is told, as <c>{ "error": "…" }</c>.</summary>
public sealed record ErrorResponse(string Error);
