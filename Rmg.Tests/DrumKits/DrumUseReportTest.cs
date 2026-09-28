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

        // the roles the drums play where they groove: the section's, or the song's where it keeps it
        Console.WriteLine("Roles where a drum grooves: ground, backbeat, time, colour");
        foreach (var drum in DrumGroups.AllDrums)
        {
            var track = DrumGroups.GetTrackNumber(drum);
            var roles = all.Where(x => Grooves(x.s, x.span, drum)).Select(x =>
            {
                var section = x.s.Trace.First(e => e.Point == TracePoints.DrumRoles && e.Section == x.span.SectionId);
                var changed = (System.Collections.Immutable.ImmutableDictionary<int, DrumRole>)section.Value!;
                return changed.TryGetValue(track, out var role)
                    ? role
                    : (DrumRole)x.s.Song.TrackDefinitions[track].StateMap.GetStateValue(CompositionStateKinds.DrumRole).Value;
            }).ToArray();
            if (roles.Length == 0)
                continue;
            Console.WriteLine($"{drum.Name,-18} {string.Join(" ", Enum.GetValues<DrumRole>().Select(r => $"{roles.Count(x => x == r) / (double)roles.Length,4:P0}"))} of {roles.Length}");
        }

        // where the accents fall: the share of the open hi-hat's and the ride bell's groove notes on the beats
        foreach (var (drum, code) in new[] { (DrumDefinitions.HiHat, 46), (DrumDefinitions.Ride, 53) })
        {
            var accents = songs.SelectMany(song => song.Song.Notes!.TryGetValue(DrumGroups.GetTrackNumber(drum), out var n)
                    ? n.Where(x => x.Value.Pitches[0] == code && x.Value.State.GetStateValue(Rmg.Core.Events.StateKinds.ArticulationIndex) == 0)
                        .Select(x => song.Map.BeatInBar(x.Position)).ToArray()
                    : [])
                .ToArray();
            Console.WriteLine($"{drum.Name} {code} in the groove: {accents.Length} notes, {accents.Count(x => Math.Abs(x - Math.Round(x)) < 1e-6) / (double)Math.Max(1, accents.Length):P0} on the beats");
        }

        // the snare's stroke by section: on its cross-stick mostly, by the section's energy, and how many songs switch
        var snareSections = songs.SelectMany(song =>
        {
            var energies = song.Trace.Where(x => x.Point == TracePoints.SectionEnergy).ToDictionary(x => x.Section, x => ((SectionEnergyTrace)x.Value!).Energy);
            var snares = new[] { DrumDefinitions.AcousticSnare, DrumDefinitions.ElectricSnare }.Select(DrumGroups.GetTrackNumber).Where(song.Song.Notes!.ContainsKey).ToArray();
            return song.Map.Sections.Select(span =>
            {
                var notes = snares.SelectMany(t => song.Song.Notes![t]).Where(x => x.Position >= span.Start && x.Position < span.End &&
                    x.Value.State.GetStateValue(Rmg.Core.Events.StateKinds.ArticulationIndex) == 0).ToArray();
                return (song.Seed, Energy: energies[span.SectionId], Notes: notes.Length, CrossStick: notes.Count(x => x.Value.Pitches[0] == 37) > notes.Length / 2);
            }).Where(x => x.Notes > 8);
        }).ToArray();
        var median = snareSections.Select(x => x.Energy).Order().ElementAt(snareSections.Length / 2);
        Console.WriteLine($"Snare sections on the cross-stick: {snareSections.Count(x => x.CrossStick) / (double)snareSections.Length:P0}, " +
                          $"the quieter half {snareSections.Where(x => x.Energy < median).Count(x => x.CrossStick) / (double)snareSections.Count(x => x.Energy < median):P0}, " +
                          $"the louder {snareSections.Where(x => x.Energy >= median).Count(x => x.CrossStick) / (double)snareSections.Count(x => x.Energy >= median):P0}; " +
                          $"songs that switch {snareSections.GroupBy(x => x.Seed).Count(x => x.Any(y => y.CrossStick) && x.Any(y => !y.CrossStick))} of {snareSections.Select(x => x.Seed).Distinct().Count()}");

        // the drums that double a lead: which on which, in the quieter and the louder half of the sections
        var doubled = songs.SelectMany(song =>
        {
            var energies = song.Trace.Where(x => x.Point == TracePoints.SectionEnergy).ToDictionary(x => x.Section, x => ((SectionEnergyTrace)x.Value!).Energy);
            return song.Trace.Where(x => x.Point == TracePoints.Doubles)
                .Select(x => (Energy: energies[x.Section], Doubles: (System.Collections.Immutable.ImmutableDictionary<int, Doubling>)x.Value!));
        }).ToArray();
        var middle = doubled.Select(x => x.Energy).Order().ElementAt(doubled.Length / 2);
        Console.WriteLine($"Doubling in {doubled.Count(x => !x.Doubles.IsEmpty) / (double)doubled.Length:P0} of the sections, " +
                          $"the quieter half {doubled.Where(x => x.Energy < middle).Count(x => !x.Doubles.IsEmpty) / (double)doubled.Count(x => x.Energy < middle):P0}, " +
                          $"the louder {doubled.Where(x => x.Energy >= middle).Count(x => !x.Doubles.IsEmpty) / (double)doubled.Count(x => x.Energy >= middle):P0}: " +
                          string.Join(", ", doubled.SelectMany(x => x.Doubles).GroupBy(x => $"{DrumGroups.GetDrum(x.Key).Name} on {DrumGroups.GetDrum(x.Value.Lead).Name}")
                              .OrderByDescending(x => x.Count()).Select(x => $"{x.Key} {x.Count()}")));
        var clapLeads = all.Count(x => Grooves(x.s, x.span, DrumDefinitions.Clap) &&
                                       !x.s.Trace.Any(e => e.Point == TracePoints.Doubles && e.Section == x.span.SectionId &&
                                                           ((System.Collections.Immutable.ImmutableDictionary<int, Doubling>)e.Value!).ContainsKey(DrumGroups.GetTrackNumber(DrumDefinitions.Clap))));
        Console.WriteLine($"The clap leads the backbeat in {clapLeads} sections");

        // the songs by their drum setup, and the drum parts of the percussion songs against those of the kit songs:
        // notes a bar, bars that repeat the one before, and bars with a note on the downbeat and on both 2 and 4
        var setups = songs.ToDictionary(x => x.Seed, x => (DrumSetup)x.Trace.First(e => e.Point == TracePoints.DrumSetup).Value!);
        Console.WriteLine("Setups: " + string.Join(", ", Enum.GetValues<DrumSetup>().Select(x => $"{x} {setups.Values.Count(s => s == x)}")));
        foreach (var setup in new[] { DrumSetup.Kit, DrumSetup.Percussion })
        {
            var setupBars = songs.Where(x => setups[x.Seed] == setup).SelectMany(song =>
            {
                var drums = song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key].Role == TrackRole.Drum)
                    .SelectMany(x => x.Value.Where(n => n.Value.State.GetStateValue(Rmg.Core.Events.StateKinds.ArticulationIndex) == 0)
                        .Select(n => (Track: x.Key, n.Position))).ToArray();
                return song.Map.Sections.SelectMany(span => Enumerable.Range(0, (int)(span.Duration / Meter.BarDuration)).Select(bar =>
                    drums.Where(n => n.Position >= span.Start + bar * Meter.BarDuration && n.Position < span.Start + (bar + 1) * Meter.BarDuration)
                        .Select(n => (n.Track, Place: Math.Round(n.Position - span.Start - bar * Meter.BarDuration, 3))).OrderBy(n => n.Track).ThenBy(n => n.Place).ToArray()));
            }).ToArray();
            if (setupBars.Length == 0)
                continue;
            var repeats = setupBars.Zip(setupBars.Skip(1)).Count(x => x.First.Length > 0 && x.First.SequenceEqual(x.Second)) / (double)(setupBars.Length - 1);
            Console.WriteLine($"{setup} songs' drums: {setupBars.Average(x => x.Length):F1} notes a bar, {repeats:P0} of the bars as the one before, " +
                              $"the downbeat in {setupBars.Count(x => x.Any(n => n.Place == 0)) / (double)setupBars.Length:P0}, " +
                              $"2 and 4 in {setupBars.Count(x => x.Any(n => n.Place == 1) && x.Any(n => n.Place == 3)) / (double)setupBars.Length:P0}");
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
