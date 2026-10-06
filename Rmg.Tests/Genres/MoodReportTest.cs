using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Tests.Genres;

/// <summary>
///     How dark or bright a preset's songs sound, by what a listener hears as such: the melody on a minor key's dark notes,
///     its flat second and flat sixth, or on its major third; the time on major chords; the songs in a major mode; the
///     melody's register and its share on a bright, high sound, bells, a music box, a celesta, a vibraphone or a flute.
/// </summary>
public sealed class MoodReportTest
{
    private static readonly int[] Bright = [8, 9, 10, 11, 73];

    private sealed record Mood(bool MajorMode, double Melody, double Flat2, double Flat6, double Major3, int ChordBeats, int MajorChords, double Pitches, int Notes, bool BrightLead);

    private static Mood Measure(CorpusSong song)
    {
        var common = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap;
        var notes = song.Song.Notes!;
        var melody = notes.GetValueOrDefault(SongTracks.MelodyTrack)?.ToArray() ?? [];
        double Time(int degree) => melody
            .Where(x => (x.Value.Pitches[0] - common.GetEffectiveStateMapAt(x.Position).GetStateValue(StateKinds.KeyOffset)).Mod(12) == degree)
            .Sum(x => x.Value.Duration);

        // every beat a chord sounds on, its notes those of the chords, the pad and the rhythm part sounding there
        var (beats, major) = (0, 0);
        foreach (var span in song.Map.Sections)
            for (var t = span.Start; t < span.End - 1e-9; t += 1)
            {
                var classes = new[] { SongTracks.ChordsTrack, SongTracks.PadTrack, SongTracks.RhythmTrack }
                    .SelectMany(x => notes.GetValueOrDefault(x)?.Where(n => n.Position <= t + 1e-6 && n.Position + n.Value.Duration > t + 1e-6).SelectMany(n => n.Value.Pitches) ?? [])
                    .Select(x => x.Mod(12)).ToHashSet();
                if (classes.Count == 0)
                    continue;
                var root = Realizer.GetChord(common.GetEffectiveStateMapAt(t)).Chord.Root.Mod(12);
                beats++;
                major += classes.Contains((root + 4) % 12) && !classes.Contains((root + 3) % 12) ? 1 : 0;
            }

        var scale = song.Song.Draws!.Scale.Offsets;
        var lead = ((PitchInstrumentTrack)song.Song.TrackDefinitions[SongTracks.MelodyTrack]).InstrumentCode;
        return new Mood(scale.Contains(4) && !scale.Contains(3), melody.Sum(x => x.Value.Duration), Time(1), Time(8), Time(4), beats, major,
            melody.Sum(x => x.Value.Pitches[0]), melody.Length, melody.Length > 0 && Bright.Contains(lead));
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var genre in new[] { "", "trap", "hipHop", "techno", "dnb", "djent", "metal", "nuMetal", "trance", "pop" })
        {
            var moods = TestCorpus.InParallel(Enumerable.Range(0, 32), seed => Measure(genre == "" ? TestCorpus.Get(seed) : TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre)))));
            var melody = moods.Sum(x => x.Melody);
            Console.WriteLine($"{(genre == "" ? "drawn" : genre),-8} major mode {moods.Count(x => x.MajorMode) / (double)moods.Length:P0}; " +
                              $"melody on ♭2 {moods.Sum(x => x.Flat2) / melody:P0}, ♭6 {moods.Sum(x => x.Flat6) / melody:P0}, 3 {moods.Sum(x => x.Major3) / melody:P0}; " +
                              $"major chords {moods.Sum(x => x.MajorChords) / (double)moods.Sum(x => x.ChordBeats):P0}; " +
                              $"melody's pitch {moods.Sum(x => x.Pitches) / moods.Sum(x => x.Notes):F0}, on a bright sound {moods.Count(x => x.BrightLead) / (double)moods.Count(x => x.Notes > 0):P0}");
        }

        await Task.CompletedTask;
    }
}
