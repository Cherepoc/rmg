using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Rendering;

/// <summary>
///     How loud the tracks play, as MIDI velocities from 0 to 127: every role's level, its accent (a note on the bar's
///     downbeat over one an 8th off the beat), how much a note at the same place varies from bar to bar within a
///     section, and how loud a chord of several notes sounds against a single note; and by genre, how hard every part and
///     every piece of the drums plays, and how many notes reach the loudest a player strikes again and again, 111, or
///     above it, as an accent does.
/// </summary>
public sealed class VelocityReportTest
{
    private const int SongCount = 128;

    internal sealed record RoleMeasures(double Level, double Accent, double Spread, double Loudness);

    /// <summary>Every role's measures, the drums as one.</summary>
    internal static Dictionary<string, RoleMeasures> Measure(IEnumerable<CorpusSong> songs)
    {
        var values = new Dictionary<string, (List<double> Level, List<double> Down, List<double> Off, List<double> Spread, List<double> Loud)>();
        foreach (var song in songs)
        foreach (var (track, notes) in song.Song.Notes!)
        {
            var role = song.Song.TrackDefinitions[track].Role.ToString();
            if (!values.TryGetValue(role, out var v))
                values[role] = v = ([], [], [], [], []);
            var rendered = Rendered(song, track);
            foreach (var (position, velocity, count) in rendered)
            {
                v.Level.Add(velocity);
                // a chord of n notes sounds about 10·log10(n) dB louder than one of them, on a velocity curve of 40·log10
                v.Loud.Add(40 * Math.Log10(Math.Max(1, velocity) / 127) + 10 * Math.Log10(count));
                var inBar = song.Map.BeatInBar(position);
                if (Math.Abs(inBar) < 1e-6)
                    v.Down.Add(velocity);
                else if (Math.Abs(inBar % 1 - 0.5) < 1e-6)
                    v.Off.Add(velocity);
            }

            // the same place in the bar, from bar to bar, within a section as it plays
            foreach (var span in song.Map.Sections)
            foreach (var place in rendered.Where(x => x.Position >= span.Start && x.Position < span.End)
                         .GroupBy(x => Math.Round(song.Map.BeatInBar(x.Position), 6)))
            {
                var at = place.Select(x => x.Velocity).ToArray();
                if (at.Length < 2)
                    continue;
                var mean = at.Average();
                v.Spread.Add(Math.Sqrt(at.Average(x => (x - mean) * (x - mean))));
            }
        }

        // a role with no notes off the beat, as a pad, has no accent to measure
        return values.ToDictionary(
            x => x.Key,
            x => new RoleMeasures(
                x.Value.Level.Average(),
                x.Value.Down.Count > 0 && x.Value.Off.Count > 0 ? x.Value.Down.Average() - x.Value.Off.Average() : double.NaN,
                x.Value.Spread.Count > 0 ? x.Value.Spread.Average() : double.NaN,
                x.Value.Loud.Average()
            )
        );
    }

    /// <summary>
    ///     A track's notes as rendered, each once, with its velocity and how many pitches it plays together; but a note
    ///     the rendering moves or spreads out of its place, an ornament's or an up-strummed chord's, which is not measured
    ///     rather than measured as silent.
    /// </summary>
    private static (double Position, double Velocity, int Count)[] Rendered(CorpusSong song, int track)
    {
        var notes = song.Song.Notes![track];
        // a note plays where the song's swing moves it
        var swing = Rmg.Core.Rendering.Render.GetSwing(song.Song);
        var rendered = song.Rendered.Tracks
            .Where(x => song.Song.TrackDefinitions[track] is PitchInstrumentTrack pitched
                ? !x.IsPercussionInstrument && x.PitchInstrumentCode == pitched.InstrumentCode
                : x.IsPercussionInstrument)
            .SelectMany(x => x.NoteTimeline)
            .GroupBy(x => (Math.Round(x.Position, 6), x.Value.Offset))
            .ToDictionary(x => x.Key, x => x.First().Value.Velocity * 127);
        return
        [
            ..notes.Select(n => (n.Position, Key: (Math.Round(swing.Apply(n.Position), 6), n.Value.Pitches[0]), n.Value.Pitches.Length))
                .Where(n => rendered.ContainsKey(n.Key))
                .Select(n => (n.Position, rendered[n.Key], n.Length))
        ];
    }

    [Test]
    public async Task TheBassAndTheChordsPlayEvenly_AChordAsLoudAsANote_AndTheMelodyAndTheBassOnTop()
    {
        var m = Measure(TestCorpus.Range(128));
        var (bass, chords, melody, drums) = (m["Bass"], m["Chords"], m["Melody"], m["Drum"]);

        // a bass and chords accent their beats less than a melody or the drums
        await Assert.That(bass.Accent).IsLessThan(melody.Accent * 0.75);
        await Assert.That(chords.Accent).IsLessThan(melody.Accent * 0.75);
        // the melody and the bass lead the mix, about as loud, the bass a little quieter for its even beats, and the
        // chords, a chord of several notes about as loud as a single note, and the drums under them
        // within a few decibels against the mechanism breaking, the target of one in the Report
        Console.WriteLine($"melody {melody.Loudness:F1} dB, bass {bass.Loudness:F1}, chords {chords.Loudness:F1}, drums {drums.Loudness:F1}");
        await Assert.That(Math.Abs(melody.Loudness - bass.Loudness)).IsLessThan(4);
        await Assert.That(Math.Min(melody.Loudness, bass.Loudness)).IsGreaterThan(Math.Max(chords.Loudness, drums.Loudness));
    }

    /// <summary>
    ///     A calibration: a genre played as evenly as can be plays a part's notes at the same place from bar to bar closer
    ///     together than the songs do, the same songs measured with it and without it; measured 0.49 of the drums' spread and
    ///     0.52 of the bass's (October 2026).
    /// </summary>
    [Test]
    public async Task AnEvenGenre_PlaysItsNotesCloserTogether()
    {
        var even = new Genre(ImmutableDictionary<string, ImmutableDictionary<string, ByConvention>>.Empty
            .Add(GenreChoices.Dynamics.Name, ImmutableDictionary<string, ByConvention>.Empty.Add("evenness", new(1, 1, 1))));
        var songs = Measure(TestCorpus.Range(64));
        var evenSongs = Measure(TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: even))));
        foreach (var role in new[] { "Drum", "Bass" })
        {
            Console.WriteLine($"{role}: spread at the same place {songs[role].Spread:F2} to {evenSongs[role].Spread:F2}, {evenSongs[role].Spread / songs[role].Spread:F2} of it");
            await Assert.That(evenSongs[role].Spread).IsLessThan(songs[role].Spread * 0.8);
        }
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var songs = TestCorpus.Range(SongCount).ToArray();
        foreach (var (role, m) in Measure(songs).OrderBy(x => x.Key))
            Console.WriteLine($"{role}: level {m.Level:F0}, accent {m.Accent:F1}, spread at the same place {m.Spread:F1}, loudness {m.Loudness:F1} dB");

        var sums = songs.SelectMany(x => x.Song.Notes!.Values.SelectMany(n => n.Select(v => v.Value.Velocity))).Order().ToArray();
        Console.WriteLine($"Velocity sums: 5% {sums[sums.Length / 20]:F2}, median {sums[sums.Length / 2]:F2}, 95% {sums[sums.Length * 19 / 20]:F2}");
        await Task.CompletedTask;
    }

    /// <summary>The loudest velocity a player strikes again and again; above it, an accent.</summary>
    private const int Repeated = 111;

    private static readonly (string Name, int[] Notes)[] Pieces =
        [("kick", [35, 36]), ("snare", [38, 40]), ("hi-hat", [42, 44, 46]), ("ride", [51, 53, 59]), ("crash", [49, 52, 55, 57])];

    [Test]
    [Explicit]
    public async Task GenreReport()
    {
        foreach (var genre in new string?[] { null, "metal", "blackMetal", "djent", "pop", "techno", "trance", "rock", "jazz", "folk", "orchestral" })
        {
            var songs = TestCorpus.InParallel(Enumerable.Range(0, 32), seed => genre is null ? TestCorpus.Get(seed) : TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre))));
            Console.WriteLine($"{genre ?? "the corpus"}:");
            var drums = songs.SelectMany(s => s.Rendered.Tracks.Where(t => t.IsPercussionInstrument).SelectMany(t => t.NoteTimeline.Select(n => n.Value))).ToArray();
            Print("drums", [..drums.Select(x => Midi(x.Velocity))]);
            foreach (var (name, notes) in Pieces)
                Print(name, [..drums.Where(x => notes.Contains(x.Offset)).Select(x => Midi(x.Velocity))]);
            foreach (var role in new[] { TrackRole.Riff, TrackRole.Chords, TrackRole.Bass, TrackRole.Melody })
                Print(role.ToString().ToLowerInvariant(), [..songs.SelectMany(s => s.Rendered.Tracks.Where(t => !t.IsPercussionInstrument && t.Role == role).SelectMany(t => t.NoteTimeline.Select(n => Midi(n.Value.Velocity))))]);
        }

        await Task.CompletedTask;
    }

    /// <summary>A velocity as MIDI writes it (<see cref="Rmg.Core.Midi" />).</summary>
    private static int Midi(double velocity) => (int)Math.Min(127, Math.Floor(velocity * 127) + 1);

    private static void Print(string name, int[] velocities)
    {
        if (velocities.Length == 0)
            return;
        var sorted = velocities.Order().ToArray();
        int At(double share) => sorted[Math.Min(sorted.Length - 1, (int)(share * sorted.Length))];
        Console.WriteLine($"  {name,-7} {sorted.Length,6} notes: p10 {At(0.1),3} p50 {At(0.5),3} p90 {At(0.9),3} p99 {At(0.99),3} max {sorted[^1],3}, IQR {At(0.75) - At(0.25),2}; " +
                          $"over {Repeated} {sorted.Count(x => x > Repeated) / (double)sorted.Length,6:P1}, 127 {sorted.Count(x => x == 127) / (double)sorted.Length:P2}");
    }
}
