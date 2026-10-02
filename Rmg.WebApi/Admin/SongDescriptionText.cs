using System.Globalization;
using System.Text;
using Rmg.Core.Songs;

namespace Rmg.WebApi.Admin;

/// <summary>
///     A song's description as text for an agent to name the song by, or a person to read: a line at a glance first,
///     which carries most of a song's character, then its sound, its form section by section in a table, its sections
///     comparable at a glance and a chord progression heard before given as the section it was heard in, and its melody.
/// </summary>
public static class SongDescriptionText
{
    public static string ToMarkdown(SongDescription song, string seed, string? settings, string songsVersion)
    {
        var text = new StringBuilder();
        var form = string.Join(" – ", song.Sections.Select(x => x.Name));
        text.AppendLine($"# Song {seed} (songs {songsVersion})");
        if (settings is not null)
            text.AppendLine($"Settings: {settings}");
        text.AppendLine();
        text.AppendLine($"At a glance: {Length(song.Seconds)}, {Number(song.Bpm)} bpm, {song.TimeSignature}{(song.Feel == "straight" ? "" : $" in {song.Feel}")}, {song.Key}; " +
                        $"{form}; {song.Unconventionality.Word} ({Number(song.Unconventionality.Value)} of 1).");

        text.AppendLine();
        text.AppendLine("## Sound");
        text.AppendLine($"- Tempo: {Number(song.Bpm)} bpm{Changes(song.Sections.Where(x => x.Bpm is not null).Select(x => $"{Number(x.Bpm!.Value)} bpm in {x.Name} ({x.Place})"))}");
        text.AppendLine($"- Meter: {song.TimeSignature}; feel: {song.Feel}; swing: {song.Swing}");
        text.AppendLine($"- Key: {song.Key}{Changes(song.KeyChanges.Select(x => $"{(x.Semitones > 0 ? "up" : "down")} {Math.Abs(x.Semitones)} semitones to {x.Key} at section {x.Section}"))}");
        text.AppendLine($"- Parts: {string.Join("; ", song.Parts.Select(x => $"{x.Role} on {x.Instrument}{(x.Others.IsEmpty ? "" : $", also {string.Join(", ", x.Others)}")}"))}");
        if (song.Drums is { } drums)
            text.AppendLine($"- Drums: {drums.Setup}, {drums.Kit} kit: {string.Join(", ", drums.Pieces)}");
        if (song.Fastest is { } fastest)
            text.AppendLine($"- Fastest: {Run(fastest)}, first in {song.Sections[fastest.Section - 1].Name} ({fastest.Section})");
        text.AppendLine($"- How experimental, 0 the plainest and 1 the wildest: {song.Unconventionality.Word} ({Number(song.Unconventionality.Value)}); " +
                        string.Join(", ", song.Unconventionality.Facets.Select(x => $"{x.Facet.ToLowerInvariant()} {x.Word} ({Number(x.Value)})")));

        text.AppendLine();
        text.AppendLine($"## Form: {song.Sections.Length} sections, {song.Bars} bars");
        text.AppendLine($"Starts with {song.Intro}; ends with {song.Ending}.");
        text.AppendLine();
        text.AppendLine("| # | Section | Bars | Energy | Density | Chords | Parts | Notes |");
        text.AppendLine("|---|---|---|---|---|---|---|---|");
        var heard = new Dictionary<string, int>();
        foreach (var section in song.Sections)
        {
            var chords = Chords(section);
            var shown = chords == "" ? "—" : heard.TryGetValue(chords, out var place) ? $"as in {place}" : chords;
            heard.TryAdd(chords, section.Place);
            var notes = new[]
            {
                section.Key is null ? null : $"in {section.Key}",
                section.TimeSignature is null ? null : $"in {section.TimeSignature}",
                section.Bpm is null ? null : $"{Number(section.Bpm.Value)} bpm",
                section.Feel is null ? null : $"in {section.Feel}",
                section.Drums is null ? null : $"drums {section.Drums}",
                section.Texture == "steady" ? null : section.Texture,
                section.Soloists.IsEmpty ? null : $"{string.Join(" and ", section.Soloists)} soloing",
                section.Burst is null ? null : $"a burst: {Run(section.Burst)}"
            }.OfType<string>();
            text.AppendLine($"| {section.Place} | {section.Name} | {section.Bars} | {section.Energy} | {section.Density} | {shown} | {string.Join(", ", section.Parts)} | {string.Join("; ", notes)} |");
        }

        if (song.Melody is { } melody)
        {
            text.AppendLine();
            text.AppendLine("## Melody");
            text.AppendLine($"- From {melody.Lowest} to {melody.Highest}, {Number(melody.NotesPerBar)} notes a bar where it plays, varying its tune {melody.Improvisation} where a section comes back");
        }

        return text.ToString();
    }

    /// <summary>A section's chords as numerals, and by name where the chords' part plays them.</summary>
    private static string Chords(SectionDescription section)
    {
        if (section.Chords.IsEmpty)
            return "";
        var numerals = string.Join(" ", section.Chords.Select(x => x.Numeral));
        return section.Chords.All(x => x.Name is null) ? numerals : $"{numerals} ({string.Join(" ", section.Chords.Select(x => x.Name ?? "–"))})";
    }

    private static string Run(RunDescription run) => $"{string.Join(" and ", run.Parts)} in {run.Value}, {Number(run.NotesPerSecond)} notes a second";

    private static string Changes(IEnumerable<string> changes)
    {
        var all = changes.ToArray();
        return all.Length == 0 ? "" : $", changing: {string.Join("; ", all)}";
    }

    private static string Length(double seconds) => $"{(int)seconds / 60}:{(int)seconds % 60:00}";

    private static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
