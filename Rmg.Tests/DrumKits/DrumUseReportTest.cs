using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

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

        // by section: which groups groove there, and how many drums
        bool Grooves(CorpusSong song, SectionSpan span, PercussionInstrumentDefinition drum) =>
            song.Song.Notes!.TryGetValue(DrumGroups.GetTrackNumber(drum), out var n) && n.Count(x => x.Position >= span.Start && x.Position < span.End) > 8;
        var all = songs.SelectMany(s => s.Map.Sections.Select(span => (s, span))).ToArray();
        foreach (var group in DrumGroups.All)
            Console.WriteLine($"{group.Name,-12} grooves in {all.Count(x => group.Drums.Any(d => Grooves(x.s, x.span, d))) / (double)all.Length:P0} of the sections");
        Console.WriteLine($"Drums grooving in a section: {all.Average(x => DrumGroups.AllDrums.Count(d => Grooves(x.s, x.span, d))):F2}");

        // by bar: how many drums play in it, and how often that changes from the bar before within a section
        var perBar = all.Select(x => Enumerable.Range(0, (int)(x.span.Duration / Meter.BarDuration)).Select(bar =>
                DrumGroups.AllDrums.Select(DrumGroups.GetTrackNumber).Where(t => x.s.Song.Notes!.TryGetValue(t, out var n) &&
                    n.Any(note => note.Position >= x.span.Start + bar * Meter.BarDuration && note.Position < x.span.Start + (bar + 1) * Meter.BarDuration)).ToHashSet())
            .ToArray()).ToArray();
        var bars = perBar.SelectMany(x => x).ToArray();
        var changes = perBar.Sum(x => x.Zip(x.Skip(1)).Count(p => !p.First.SetEquals(p.Second)));
        Console.WriteLine($"Drums playing in a bar: {bars.Average(x => x.Count):F2}; the drums change from the bar before in {changes / (double)perBar.Sum(x => x.Length - 1):P0} of the bars");

        // the sounds the drums land on, by drum, and the cymbal's by sound
        var landings = songs.SelectMany(x => x.Trace.Where(e => e.Point == TracePoints.FillDecision).SelectMany(e => ((FillDecision)e.Value!).Landing)).ToArray();
        Console.WriteLine("Landings: " + string.Join(", ", landings.GroupBy(x => x.Drum).OrderByDescending(x => x.Count()).Select(x => $"{x.Key} {x.Count() / (double)landings.Length:P1}")));
        Console.WriteLine("Cymbal landings: " + string.Join(", ", landings.Where(x => x.Drum == DrumDefinitions.Cymbal.Name).GroupBy(x => x.Code).OrderBy(x => x.Key).Select(x => $"{x.Key} {x.Count()}")));

        // every drum of more than one sound: how its notes share them, in the groove and in the fills, which name theirs
        foreach (var drum in DrumGroups.AllDrums.Where(x => x.Sounds.Length > 1))
        {
            var notes = songs.SelectMany(x => x.Song.Notes!.TryGetValue(DrumGroups.GetTrackNumber(drum), out var n) ? n.ToArray() : []).ToArray();
            string Shares(IEnumerable<Rmg.Core.Events.TimelineItem<RealizedNote>> of)
            {
                var all = of.ToArray();
                return string.Join(" ", drum.ArticulationCodes.Select(code => $"{code} {all.Count(x => x.Value.Pitches[0] == code) / (double)Math.Max(1, all.Length):P0}"));
            }
            bool IsFill(Rmg.Core.Events.TimelineItem<RealizedNote> note) => note.Value.State.GetStateValue(Rmg.Core.Events.StateKinds.ArticulationIndex) > 0;
            Console.WriteLine($"Sounds of {drum.Name,-16} groove: {Shares(notes.Where(x => !IsFill(x)))}; fills: {Shares(notes.Where(IsFill))}");
        }

        // sections of percussion only: how many, in how many songs, and how loud they are meant to be
        var percussionOnly = songs.SelectMany(song =>
        {
            var energies = song.Trace.Where(x => x.Point == TracePoints.SectionEnergy).ToDictionary(x => x.Section, x => ((SectionEnergyTrace)x.Value!).Energy);
            var only = song.Trace.Where(x => x.Point == TracePoints.PercussionOnly).ToDictionary(x => x.Section, x => (bool)x.Value!);
            return song.Map.Sections.Select(span => (song.Seed, Only: only[span.SectionId], Energy: energies[span.SectionId]));
        }).ToArray();
        var bySong = percussionOnly.GroupBy(x => x.Seed).Select(x => x.Count(y => y.Only) / (double)x.Count()).ToArray();
        Console.WriteLine($"Percussion only: {percussionOnly.Count(x => x.Only) / (double)percussionOnly.Length:P1} of the sections, in {bySong.Count(x => x > 0)} songs, " +
                          $"{bySong.Count(x => x >= 0.5)} of them half or more; energy {percussionOnly.Where(x => x.Only).DefaultIfEmpty().Average(x => x.Energy):F2} " +
                          $"against {percussionOnly.Where(x => !x.Only).Average(x => x.Energy):F2}");

        // where a drum plays within its bars, where it grooves, and how often a bar repeats the one before
        Console.WriteLine("Where a drum grooves: notes a bar; on 1 and 3, on 2 and 4, on the 8ths between, finer; bars as the one before");
        foreach (var drum in DrumGroups.AllDrums)
        {
            var track = DrumGroups.GetTrackNumber(drum);
            var grooveBars = all.Where(x => Grooves(x.s, x.span, drum))
                .SelectMany(x => Enumerable.Range(0, (int)(x.span.Duration / Meter.BarDuration)).Select(bar =>
                    x.s.Song.Notes![track].Where(n => n.Position >= x.span.Start + bar * Meter.BarDuration && n.Position < x.span.Start + (bar + 1) * Meter.BarDuration)
                        .Select(n => Math.Round(n.Position - x.span.Start - bar * Meter.BarDuration, 3)).ToArray()).ToArray())
                .ToArray();
            var places = grooveBars.SelectMany(x => x).ToArray();
            if (places.Length == 0)
                continue;
            double Share(Func<double, bool> at) => places.Count(at) / (double)places.Length;
            var repeats = grooveBars.Zip(grooveBars.Skip(1)).Count(x => x.First.Length > 0 && x.First.SequenceEqual(x.Second)) / (double)Math.Max(1, grooveBars.Length - 1);
            Console.WriteLine($"{drum.Name,-18} {places.Length / (double)grooveBars.Length,5:F1} a bar; {Share(x => x is 0 or 2),4:P0} {Share(x => x is 1 or 3),4:P0} " +
                              $"{Share(x => x is 0.5 or 1.5 or 2.5 or 3.5),4:P0} {Share(x => x % 0.5 != 0),4:P0}; repeats {repeats:P0}");
        }

        await Task.CompletedTask;
    }
}
