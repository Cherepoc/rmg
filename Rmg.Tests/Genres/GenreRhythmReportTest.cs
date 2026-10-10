using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Genres;

/// <summary>Where a part's notes fall, with a genre's rhythm against none: the measures the rhythm's values are tuned by.</summary>
public sealed class GenreRhythmReportTest
{
    private const int Count = 64;

    /// <summary>Where a part's notes fall: its notes a beat, its shares on the beat, on an 8th's offbeat and on a 16th's, and of its bars playing the bar before's rhythm.</summary>
    internal sealed record Fall(double PerBeat, double OnBeat, double Offbeat, double Sixteenth, double Repeated);

    /// <summary>Every part's, the drums' by their song roles, over a song's notes as it decides them, before its swing.</summary>
    internal static Dictionary<RhythmPart, Fall> Falls(CorpusSong song)
    {
        // a part's onsets once, whichever of its tracks plays them
        var parts = new Dictionary<RhythmPart, HashSet<double>>();
        foreach (var (track, notes) in song.Song.Notes!)
        {
            RhythmPart? part = song.Song.TrackDefinitions[track] switch
            {
                PitchInstrumentTrack { Role: TrackRole.Melody } => RhythmPart.Melody,
                PitchInstrumentTrack { Role: TrackRole.Chords } => RhythmPart.Chords,
                PitchInstrumentTrack { Role: TrackRole.Bass } => RhythmPart.Bass,
                PitchInstrumentTrack { Role: TrackRole.Riff } => RhythmPart.Riff,
                PitchInstrumentTrack { Role: TrackRole.Rhythm } => RhythmPart.Rhythm,
                PercussionInstrumentTrack drum => DrumRoles.PartOf(DrumRoles.Of(drum.StateMap)),
                _ => null
            };
            if (part is not { } of || notes.Count == 0)
                continue;

            parts.TryAdd(of, []);
            parts[of].UnionWith(notes.Select(x => Math.Round(x.Position, 6)));
        }

        return parts.ToDictionary(x => x.Key, x =>
        {
            double Share(Func<double, bool> on) => x.Value.Count(on) / (double)x.Value.Count;
            // a position on a grid counted from its bar's start
            bool Near(double position, double grid, double at) => Math.Abs((song.Map.BeatInBar(position) % grid + grid) % grid - at) < 1e-6;
            // the part's bars, by the bars of each section, in its meter, from its start, the same as the bar before
            var byBar = x.Value.GroupBy(y => Math.Round(y - song.Map.BeatInBar(y), 6)).ToDictionary(y => y.Key, y => string.Join(",", y.Order().Select(z => Math.Round(song.Map.BeatInBar(z), 3))));
            var same = byBar.Count(y => byBar.TryGetValue(Math.Round(y.Key - song.Map.MeterAt(y.Key - 1e-6).BarDuration, 6), out var before) && before == y.Value);
            return new Fall(
                x.Value.Count / song.Map.Duration,
                Share(p => Near(p, 1, 0)),
                Share(p => Near(p, 1, 0.5)),
                Share(p => Near(p, 0.5, 0.25)),
                same / (double)Math.Max(1, byBar.Count)
            );
        });
    }

    internal static void Report(string name, IReadOnlyList<Dictionary<RhythmPart, Fall>> songs)
    {
        Console.WriteLine($"== {name}");
        foreach (var part in Enum.GetValues<RhythmPart>())
        {
            var falls = songs.Where(x => x.ContainsKey(part)).Select(x => x[part]).ToArray();
            if (falls.Length == 0)
                continue;
            Console.WriteLine($"  {part,-14} {falls.Length,3} songs: notes a beat {falls.Average(x => x.PerBeat):0.00}, on the beat {falls.Average(x => x.OnBeat):P0}, " +
                              $"8th offbeat {falls.Average(x => x.Offbeat):P0}, 16ths {falls.Average(x => x.Sixteenth):P0}, bars repeated {falls.Average(x => x.Repeated):P0}");
        }
    }

    private static Genre Of(params (RhythmPart Part, (string Setting, double Amount)[] Settings)[] parts) =>
        new(parts.ToImmutableDictionary(x => GenreChoices.Rhythms[x.Part].Name, x => x.Settings.ToImmutableDictionary(y => y.Setting, y => new Rmg.Core.Probabilities.ByConvention(y.Amount, y.Amount, y.Amount))));

    [Test]
    [Explicit]
    public async Task GenresRhythms_AgainstNone()
    {
        Report("none", TestCorpus.Measure(Count, Falls));
        foreach (var (name, genre) in new[]
                 {
                     ("chords skanking: a beat's cycle, half off it, one note", Of(
                         (RhythmPart.Chords, [("speed", 1), ("offbeat", 0.75), ("fineness", 0)]),
                         (RhythmPart.Rhythm, [("speed", 1), ("offbeat", 0.75), ("fineness", 0)]))),
                     ("four on the floor, the hi-hat on the offbeats", Of(
                         (RhythmPart.Ground, [("speed", 1), ("fineness", 0), ("fullness", 1), ("repetition", 1)]),
                         (RhythmPart.Time, [("speed", 1), ("offbeat", 0.75), ("fineness", 0), ("fullness", 1), ("repetition", 1)]))),
                     ("a walking bass, every beat", Of(
                         (RhythmPart.Bass, [("speed", 1), ("fineness", 0), ("fullness", 1)]))),
                     ("busy 16ths: time finer, the bass syncopated and fine", Of(
                         (RhythmPart.Time, [("fineness", 1), ("fullness", 1)]),
                         (RhythmPart.Bass, [("syncopation", 1), ("fineness", 1), ("fullness", 0.75)])))
                 })
            Report(name, TestCorpus.InParallel(Enumerable.Range(0, Count), seed => Falls(TestCorpus.Get(seed, new SongOverrides(Genre: genre)))));
        await Task.CompletedTask;
    }
}
