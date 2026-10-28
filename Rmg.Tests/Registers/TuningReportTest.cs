using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Registers;

/// <summary>Where the metal family's guitars sit against their instruments' lowest note, a tuning's floor.</summary>
public sealed class TuningReportTest
{
    /// <summary>
    ///     Each metal preset's riff, rhythm part and bass: their notes' lowest pitch, the median and the lowest tenth, and how
    ///     many of their notes sit within a tone of their instrument's lowest note, as a band tuned down plays its open string,
    ///     and how many below it.
    /// </summary>
    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var genre in new[] { "metal", "thrash", "doom", "nuMetal", "djent", "blackMetal" })
        {
            var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre)))).ToArray();
            var line = new List<string>();
            foreach (var role in new[] { TrackRole.Riff, TrackRole.Rhythm, TrackRole.Bass })
            {
                var (lows, floor, below, all) = (new List<int>(), 0, 0, 0);
                foreach (var song in songs.Where(x => x.Song.TrackDefinitions.Values.Any(t => t.Role == role)))
                {
                    var part = (PitchInstrumentTrack)song.Song.TrackDefinitions[song.Track(role)];
                    foreach (var note in song.Song.Notes!.GetValueOrDefault(song.Track(role))?.ToArray() ?? [])
                    {
                        var low = note.Value.Pitches.Min();
                        lows.Add(low);
                        all++;
                        // on the instrument as the part tunes it, any of its own's family
                        var program = InstrumentsOverTime.ProgramOf(note.Value.State, part.InstrumentCode);
                        var bottom = InstrumentRanges.Of(program).Low + Rmg.Core.Composition.Registers.TuningOn(part, program);
                        floor += low - bottom <= 2 ? 1 : 0;
                        below += low < bottom ? 1 : 0;
                    }
                }
                lows.Sort();
                // the floors of the parts' ranges, the lowest note each may play
                var floors = songs.Where(x => x.Song.TrackDefinitions.Values.Any(t => t.Role == role))
                    .Select(x => Realizer.GetRange((PitchInstrumentTrack)x.Song.TrackDefinitions[x.Track(role)]).Low).CountBy(x => x).OrderBy(x => x.Key);
                line.Add(lows.Count == 0 ? $"{role} none" : $"{role} median {lows[lows.Count / 2]} lowest tenth {lows[lows.Count / 10]} at the floor {floor / (double)all:P0}, below it {below}, floors {string.Join(" ", floors.Select(x => $"{x.Key}:{x.Value}"))}");
            }
            Console.WriteLine($"{genre}: {string.Join("; ", line)}");
            // the rhythm part's shapes: single notes, power chords (a fifth, its octave or both over the root), and chords
            var shapes = songs.Where(x => x.Song.TrackDefinitions.Values.Any(t => t.Role == TrackRole.Rhythm))
                .SelectMany(x => (x.Song.Notes!.GetValueOrDefault(x.Track(TrackRole.Rhythm))?.ToArray() ?? []).Select(n => (Song: x, Note: n)))
                .Select(x => Shape(x.Song, x.Note)).CountBy(x => x).ToDictionary();
            var total = Math.Max(1, shapes.Values.Sum());
            Console.WriteLine($"  rhythm shapes: {string.Join(", ", shapes.OrderByDescending(x => x.Value).Take(10).Select(x => $"{x.Key} {x.Value / (double)total:P0}"))}");
        }

        await Task.CompletedTask;
    }

    /// <summary>A rhythm part's note's shape, on a driven guitar or a clean one, a single note doubling the riff's line or broken from a chord.</summary>
    private static string Shape(CorpusSong song, Rmg.Core.Events.TimelineItem<RealizedNote> note)
    {
        var part = (PitchInstrumentTrack)song.Song.TrackDefinitions[song.Track(TrackRole.Rhythm)];
        var driven = InstrumentsOverTime.ProgramOf(note.Value.State, part.InstrumentCode) is 29 or 30 ? "driven" : "clean";
        var pitches = note.Value.Pitches;
        var shape = pitches.Length > 1
            ? pitches.Select(x => x - pitches.Min()).All(x => x is 0 or 7 or 12 or 19 or 24) ? "power" : $"chord {string.Join(".", pitches.Order().Select(x => x - pitches.Min()))}"
            : note.Value.State.GetStateValue(CompositionStateKinds.Instrument).IsSet ? "unison"
            : note.Value.State.GetStateValue(CompositionStateKinds.Arpeggio) is { IsSet: true, Value: > 0 } ? "broken" : "single";
        return $"{driven} {shape}";
    }
}
