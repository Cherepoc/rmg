using System.Text.Json;
using System.Text.Json.Serialization;
using Rmg.WebApi.Analytics;
using Rmg.WebApi.Songs;
using Rmg.WebApi.SoundFonts;

namespace Rmg.WebApi;

/// <summary>
///     Every type the API reads or writes as JSON, written out at compile time rather than discovered by
///     reflection when a request arrives. A native build has no reflection to discover them with, and a type
///     missing from here fails there on its first request, while the ordinary build would never notice.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(GenerateSongRequest))]
[JsonSerializable(typeof(EventRequest))]
[JsonSerializable(typeof(IEnumerable<SoundFont>))]
[JsonSerializable(typeof(AnalyticsSummary))]
[JsonSerializable(typeof(ErrorResponse))]
internal sealed partial class AppJsonContext : JsonSerializerContext;

/// <summary>What a request the API will not serve is told, as <c>{ "error": "…" }</c>.</summary>
public sealed record ErrorResponse(string Error);
