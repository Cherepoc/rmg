using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Tests.Genres;

/// <summary>What a genre moves against no genre over the corpus, and what it leaves: the measures every step of the genres is tuned by.</summary>
public sealed class GenreReportTest
{
    private const int Count = 64;

    /// <summary>The measures of a song, by name.</summary>
    internal static Dictionary<string, string> Measure(CorpusSong song)
    {
        var draws = song.Song.Draws!;
        var tempo = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetEffectiveStateMapAt(0).GetStateValue(StateKinds.Tempo);
        var pitched = song.Song.TrackDefinitions.Values.OfType<PitchInstrumentTrack>().Where(x => draws.Parts.Contains(x.Role)).ToArray();
        return new Dictionary<string, string>
        {
            ["meter"] = Meter.NameOf(song.Map.Meter),
            ["tempo"] = GenreChoices.TempoBandOf(tempo),
            ["swings"] = $"{draws.Swing.Delay > 0}",
            ["scale"] = draws.Scale.Name,
            ["drumSetup"] = $"{draws.DrumSetup}",
            ["bass"] = GeneralMidi.Programs[pitched.First(x => x.Role == TrackRole.Bass).InstrumentCode],
            ["melody"] = GeneralMidi.Programs[pitched.First(x => x.Role == TrackRole.Melody).InstrumentCode],
            ["lineUp"] = string.Join(",", pitched.OrderBy(x => x.Role).Select(x => x.InstrumentCode)),
            ["sections"] = $"{song.Map.Sections.Length}",
            ["key"] = $"{song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetEffectiveStateMapAt(0).GetStateValue(StateKinds.KeyOffset)}"
        };
    }

    /// <summary>Every measure's shares over the songs, the commonest first, and how many values it takes.</summary>
    internal static void Report(string name, IReadOnlyList<Dictionary<string, string>> songs)
    {
        Console.WriteLine($"== {name}");
        foreach (var measure in songs[0].Keys)
        {
            var shares = songs.GroupBy(x => x[measure]).OrderByDescending(x => x.Count()).ToArray();
            Console.WriteLine($"  {measure} ({shares.Length} values): " + string.Join(", ", shares.Take(6).Select(x => $"{x.Key} {100.0 * x.Count() / songs.Count:0}%")));
        }
    }

    [Test]
    [Explicit]
    public async Task AGenre_AgainstNone()
    {
        var genre = new Genre(new Dictionary<string, ImmutableDictionary<string, double>>
        {
            ["feelChances"] = ImmutableDictionary<string, double>.Empty.Add("swing", 0.9),
            ["scale"] = ImmutableDictionary<string, double>.Empty.Add("dorian", 1).Add("major", 0.6).Add("mixolydian", 0.4),
            ["instrument.bass"] = ImmutableDictionary<string, double>.Empty.Add("acousticBass", 1).Add("contrabass", 0.3),
            ["instrument.melody"] = ImmutableDictionary<string, double>.Empty.Add("tenorSax", 1).Add("altoSax", 1).Add("trumpet", 0.8).Add("mutedTrumpet", 0.5),
            ["tempo"] = ImmutableDictionary<string, double>.Empty.Add("moderate", 1).Add("brisk", 0.7).Add("slow", 0.3)
        }.ToImmutableDictionary());

        Report("none", TestCorpus.Measure(Count, Measure));
        Report("a genre", TestCorpus.InParallel(Enumerable.Range(0, Count), seed => Measure(TestCorpus.Get(seed, new SongOverrides(Genre: genre)))));
        await Task.CompletedTask;
    }
}
