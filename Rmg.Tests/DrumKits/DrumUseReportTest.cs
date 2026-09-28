using Rmg.Core.Composition;

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
        await Task.CompletedTask;
    }
}
