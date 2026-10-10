using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Genres;

/// <summary>Every genre's sound as its songs play it: each part's instruments, its switches among them, and every drum the kit strikes, by how many songs play it.</summary>
public sealed class InstrumentsReportTest
{
    /// <summary>A drum's sound by its note, named by the pieces that strike it, as the core's drums have them.</summary>
    private static readonly Dictionary<int, string> Drums = DrumPieces.All.SelectMany(x => x.Notes.Select(note => (Note: note, x.Name)))
        .GroupBy(x => x.Note).ToDictionary(x => x.Key, x => $"{string.Join("/", x.Select(y => y.Name).Distinct())} {x.Key}");

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
