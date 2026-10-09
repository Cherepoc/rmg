using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Genres;

/// <summary>
///     What each preset's pitched parts play, every part's notes by the instrument playing them, a switched one's among
///     them, and the palm-muted notes by theirs, so that an instrument heard more than its genre wants shows.
/// </summary>
public sealed class GenreInstrumentsReportTest
{
    private const int Count = 64;

    [Test]
    [Arguments("none")]
    [Arguments("rock")]
    [Arguments("metal")]
    [Explicit]
    public async Task Report(string id)
    {
        var genre = id == "none" ? Genre.None : GenrePresets.Genre(id);
        var notes = TestCorpus.InParallel(Enumerable.Range(0, Count), seed =>
        {
            var song = TestCorpus.Get(seed, new SongOverrides(Genre: genre)).Song;
            return song.Notes!.Where(x => song.TrackDefinitions[x.Key] is PitchInstrumentTrack).SelectMany(x =>
            {
                var track = (PitchInstrumentTrack)song.TrackDefinitions[x.Key];
                return x.Value.Select(n => (track.Role, Program: InstrumentsOverTime.ProgramOf(n.Value.State, track.InstrumentCode),
                    Muted: n.Value.State.GetStateValue(CompositionStateKinds.PalmMute) > 0));
            }).ToArray();
        }).SelectMany(x => x).ToArray();

        var all = notes.Length;
        Console.WriteLine($"{id}: {all} pitched notes in {Count} songs");
        foreach (var role in notes.GroupBy(x => x.Role).OrderByDescending(x => x.Count()))
        {
            Console.WriteLine($"  {role.Key,-14} {role.Count() / (double)all,6:P0} of the notes");
            foreach (var program in role.GroupBy(x => x.Program).OrderByDescending(x => x.Count()))
                Console.WriteLine($"    {GeneralMidi.Programs[program.Key],-28} {program.Count() / (double)role.Count(),6:P0}");
        }

        void Shares(string what, (TrackRole Role, int Program, bool Muted)[] some)
        {
            Console.WriteLine($"  {what}, {some.Length / (double)all:P1} of the notes:");
            foreach (var program in some.GroupBy(x => x.Program).OrderByDescending(x => x.Count()))
                Console.WriteLine($"    {GeneralMidi.Programs[program.Key],-28} {program.Count() / (double)some.Length,6:P0}");
        }

        Shares("guitars' notes, by guitar", notes.Where(x => x.Program is >= 24 and <= 31).ToArray());
        Shares("palm-muted notes, by instrument", notes.Where(x => x.Muted).ToArray());
        await Task.CompletedTask;
    }
}
