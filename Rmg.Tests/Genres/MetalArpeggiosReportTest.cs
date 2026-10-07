using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Genres;

/// <summary>Metal's arpeggios: the parts breaking their chords, clean or driven, at what pace, in what shapes and colours.</summary>
public sealed class MetalArpeggiosReportTest
{
    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var name in new[] { "metal", "nuMetal", "djent" })
        {
            var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(name))));
            // every chord part's section breaking its chord: its notes a beat, whether clean, its pattern and its colour
            var broken = songs.SelectMany(song => song.Song.Notes!
                    .Where(x => song.Song.TrackDefinitions[x.Key] is PitchInstrumentTrack { Role: TrackRole.Chords or TrackRole.Rhythm })
                    .SelectMany(x => song.Map.Sections.Select(span => (Part: (PitchInstrumentTrack)song.Song.TrackDefinitions[x.Key],
                        Notes: x.Value.Where(n => n.Position >= span.Start - 1e-9 && n.Position < span.End - 1e-9).ToArray(), span.Duration))))
                .Where(x => x.Notes.Length > 0)
                .ToArray();
            var arpeggios = broken.Where(x => x.Notes.Any(n => n.Value.State.GetStateValue(CompositionStateKinds.Arpeggio) != 0)).ToArray();
            int Program(PitchInstrumentTrack part, RealizedNote note) => note.State.GetStateValue(CompositionStateKinds.Program) is var p and > 0 ? p - 1 : part.InstrumentCode;
            var clean = arpeggios.Count(x => x.Notes.Count(n => Program(x.Part, n.Value) is >= 24 and <= 28) > x.Notes.Length / 2);
            var colours = arpeggios.Select(x => (ChordColour)x.Notes[0].Value.State.GetStateValue(CompositionStateKinds.Colour)).ToArray();
            var patterns = arpeggios.Select(x => (ArpeggioPattern)x.Notes[0].Value.State.GetStateValue(CompositionStateKinds.Arpeggio)).ToArray();
            Console.WriteLine($"{name}: of the chord parts' sections {arpeggios.Length / (double)broken.Length:P0} broken, {clean / (double)Math.Max(1, arpeggios.Length):P0} of them clean, " +
                              $"{arpeggios.Average(x => x.Notes.Length / x.Duration):F1} notes a beat, the rhythm part's {arpeggios.Where(x => x.Part.Role == TrackRole.Rhythm).DefaultIfEmpty().Average(x => x.Notes is null ? 0 : x.Notes.Length / x.Duration):F1}; " +
                              $"patterns {string.Join(", ", patterns.GroupBy(x => x).OrderByDescending(x => x.Count()).Select(x => $"{x.Key} {x.Count() / (double)patterns.Length:P0}"))}; " +
                              $"colours {string.Join(", ", colours.GroupBy(x => x).OrderByDescending(x => x.Count()).Select(x => $"{x.Key} {x.Count() / (double)colours.Length:P0}"))}");
        }

        await Task.CompletedTask;
    }
}
