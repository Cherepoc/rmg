using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Genres;

/// <summary>Every genre's sound as its songs play it: each part's instruments, its switches among them, and every drum the kit strikes, by how many songs play it.</summary>
public sealed class InstrumentsReportTest
{
    private static readonly Dictionary<int, string> Drums = new()
    {
        [35] = "kick2", [36] = "kick", [37] = "sideStick", [38] = "snare", [39] = "clap", [40] = "eSnare", [41] = "lowFloorTom", [42] = "hat", [43] = "floorTom",
        [44] = "pedalHat", [45] = "lowTom", [46] = "openHat", [47] = "lowMidTom", [48] = "hiMidTom", [49] = "crash", [50] = "highTom", [51] = "ride", [52] = "china",
        [53] = "rideBell", [54] = "tambourine", [55] = "splash", [56] = "cowbell", [57] = "crash2", [58] = "vibraslap", [59] = "ride2", [60] = "hiBongo", [61] = "loBongo",
        [62] = "muteConga", [63] = "openConga", [64] = "lowConga", [65] = "hiTimbale", [66] = "loTimbale", [67] = "hiAgogo", [68] = "loAgogo", [69] = "cabasa", [70] = "maracas",
        [71] = "shortWhistle", [72] = "longWhistle", [73] = "shortGuiro", [74] = "longGuiro", [75] = "claves", [76] = "hiWoodBlock", [77] = "loWoodBlock", [78] = "muteCuica",
        [79] = "openCuica", [80] = "muteTriangle", [81] = "openTriangle"
    };

    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var name in GenrePresets.All.Select(x => x.Id))
        {
            var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(name))));
            string Share(IEnumerable<(string Key, int Song)> heard) => string.Join(", ", heard.Distinct().GroupBy(x => x.Key).OrderByDescending(x => x.Count()).Select(x => $"{x.Key} {x.Count()}"));
            var parts = songs.SelectMany(song => song.Rendered.Tracks.Where(x => !x.IsPercussionInstrument && x.NoteTimeline.Count > 0)
                    .SelectMany(track => track.ProgramChanges.Select(x => x.Program).Prepend(track.PitchInstrumentCode).Distinct().Select(program => (track.Role, Program: GeneralMidi.Programs[program], song.Seed))))
                .GroupBy(x => x.Role);
            Console.WriteLine($"## {name}");
            foreach (var role in parts.OrderBy(x => x.Key))
                Console.WriteLine($"  {role.Key}: {Share(role.Select(x => (x.Program, x.Seed)))}");
            var drums = songs.SelectMany(song => song.Rendered.Tracks.Where(x => x.IsPercussionInstrument).SelectMany(x => x.NoteTimeline).Select(n => (Drums.GetValueOrDefault(n.Value.Offset, $"key{n.Value.Offset}"), song.Seed)));
            Console.WriteLine($"  drums: {Share(drums)}");
        }

        await Task.CompletedTask;
    }
}
