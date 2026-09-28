using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.DrumKits;

/// <summary>
///     How much every drum plays over the corpus: in how many songs, in how many sections it grooves (more than eight
///     notes there, so a fill or a landing alone does not count), and how many notes in all.
/// </summary>
public sealed class DrumUseReportTest
{
    [Test]
    [Explicit]
    public async Task Report()
    {
        var songs = TestCorpus.Range(200).ToArray();
        var sections = songs.Sum(x => x.Map.Sections.Length);
        foreach (var drum in DrumGroups.AllDrums)
        {
            var track = DrumGroups.GetTrackNumber(drum);
            var inSongs = songs.Count(s => s.Song.Notes!.TryGetValue(track, out var n) && n.Count > 0);
            var inSections = songs.Sum(s => s.Map.Sections.Count(span =>
                s.Song.Notes!.TryGetValue(track, out var n) && n.Count(x => x.Position >= span.Start && x.Position < span.End) > 8));
            var notes = songs.Sum(s => s.Song.Notes!.TryGetValue(track, out var n) ? n.Count : 0);
            Console.WriteLine($"{drum.Name,-18} songs {inSongs,3}, grooving sections {inSections,4} of {sections}, notes {notes}");
        }

        // by section: which groups groove there, and how many drums
        bool Grooves(CorpusSong song, SectionSpan span, PercussionInstrumentDefinition drum) =>
            song.Song.Notes!.TryGetValue(DrumGroups.GetTrackNumber(drum), out var n) && n.Count(x => x.Position >= span.Start && x.Position < span.End) > 8;
        var all = songs.SelectMany(s => s.Map.Sections.Select(span => (s, span))).ToArray();
        foreach (var group in DrumGroups.All)
            Console.WriteLine($"{group.Name,-12} grooves in {all.Count(x => group.Drums.Any(d => Grooves(x.s, x.span, d))) / (double)all.Length:P0} of the sections");
        Console.WriteLine($"Drums grooving in a section: {all.Average(x => DrumGroups.AllDrums.Count(d => Grooves(x.s, x.span, d))):F2}");

        // by bar: how many drums play in it, and how often that changes from the bar before within a section
        var perBar = all.Select(x => Enumerable.Range(0, (int)(x.span.Duration / Meter.BarDuration)).Select(bar =>
                DrumGroups.AllDrums.Select(DrumGroups.GetTrackNumber).Where(t => x.s.Song.Notes!.TryGetValue(t, out var n) &&
                    n.Any(note => note.Position >= x.span.Start + bar * Meter.BarDuration && note.Position < x.span.Start + (bar + 1) * Meter.BarDuration)).ToHashSet())
            .ToArray()).ToArray();
        var bars = perBar.SelectMany(x => x).ToArray();
        var changes = perBar.Sum(x => x.Zip(x.Skip(1)).Count(p => !p.First.SetEquals(p.Second)));
        Console.WriteLine($"Drums playing in a bar: {bars.Average(x => x.Count):F2}; the drums change from the bar before in {changes / (double)perBar.Sum(x => x.Length - 1):P0} of the bars");
        await Task.CompletedTask;
    }
}
