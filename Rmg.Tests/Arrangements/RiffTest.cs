using Rmg.Core;
using Rmg.Core.Events;
using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Arrangements;

public sealed class RiffTest
{
    private static (int Index, TrackRole Source, TrackRole Target, int Step)[] Doublings(CorpusSong song) =>
        [..song.Trace.Where(x => x.Point == TracePoints.LineDoubling).Select(x => ((int, TrackRole, TrackRole, int))x.Value!)];

    [Test]
    public async Task HalfTheSongs_HaveARiff()
    {
        var absent = TestCorpus.Measure(256, x => x.Absent);

        // bounded widely, about half
        var share = absent.Count(x => !x.Contains(TrackRole.Riff)) / 256.0;
        Console.WriteLine($"a riff in {share:P0} of the songs");
        await Assert.That(share).IsBetween(0.3, 0.7);
    }

    [Test]
    public async Task ARiff_StacksANote_OnlyWhereASectionStarts_OnAnInstrumentThatPlaysChords()
    {
        var (starts, stacked) = (0, 0);
        foreach (var song in TestCorpus.Range(256).Where(x => !x.Absent.Contains(TrackRole.Riff)))
        {
            var riff = (PitchInstrumentTrack)song.Song.TrackDefinitions[song.Song.Track(TrackRole.Riff)];
            var sectionStarts = song.PlayedSpans.Select(x => Math.Round(x.Start, 6)).ToHashSet();
            foreach (var note in song.Song.Notes!.GetValueOrDefault(song.Song.Track(TrackRole.Riff), Rmg.Core.Events.EventTimeline.Create<RealizedNote>(0))
                         .Where(x => x.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 0))
            {
                var isStart = sectionStarts.Contains(Math.Round(note.Position, 6));
                var isStacked = note.Value.Pitches.Length > 1 && note.Value.State.GetStateValue(CompositionStateKinds.LineVoices) > 1;
                if (isStacked)
                {
                    await Assert.That(isStart).IsTrue().Because($"seed {song.Seed} at {note.Position}");
                    await Assert.That(Rmg.Core.Composition.InstrumentRoles.PlaysChords(riff.InstrumentCode)).IsTrue();
                    // the riff's own note on top
                    await Assert.That(note.Value.Pitches.Skip(1).All(x => x < note.Value.Pitches[0])).IsTrue();
                    stacked++;
                }

                if (isStart && Rmg.Core.Composition.InstrumentRoles.PlaysChords(riff.InstrumentCode))
                    starts++;
            }
        }

        await Assert.That(stacked).IsGreaterThan(10);
        // by its chance, where its note is the chord's, as a section's first note mostly is: about 0.45, bounded widely
        Console.WriteLine($"stacked on {stacked} of {starts} section starts");
        await Assert.That(stacked / (double)starts).IsBetween(0.2, 0.7);
    }

    [Test]
    public async Task AMelodyOnAChordInstrument_LeansOnDoubleStops_OnItsBarsDownbeats_InAQuarterOfItsSections()
    {
        var (sections, stacking) = (0, 0);
        foreach (var song in TestCorpus.Range(256))
        {
            var melody = (PitchInstrumentTrack)song.Song.TrackDefinitions[song.Song.Track(TrackRole.Melody)];
            if (!Rmg.Core.Composition.InstrumentRoles.PlaysChords(melody.InstrumentCode))
                continue;

            var notes = song.Song.Notes![song.Song.Track(TrackRole.Melody)].Where(x => x.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 0).ToArray();
            foreach (var span in song.Map.Sections)
            {
                var stacked = notes.Where(x => x.Position >= span.Start - 1e-9 && x.Position < span.End - 1e-9 && x.Value.Pitches.Length == 2 && x.Value.State.GetStateValue(CompositionStateKinds.LineVoices) == 2).ToArray();
                // on its bars' downbeats alone
                await Assert.That(stacked.All(x => Math.Abs((x.Position - span.Start) / span.Meter.BarDuration - Math.Round((x.Position - span.Start) / span.Meter.BarDuration)) < 1e-6)).IsTrue();
                sections++;
                if (stacked.Length > 0)
                    stacking++;
            }
        }

        await Assert.That(sections).IsGreaterThan(50);
        // about 0.2, bounded widely
        Console.WriteLine($"stacking in {stacking} of {sections} sections");
        await Assert.That(stacking / (double)sections).IsBetween(0.05, 0.4);
    }

    [Test]
    public async Task ACounterMelodyHarmonisingTheMelody_PlaysInItsSound_HalfTheTime()
    {
        var (doubled, inTimbre) = (0, 0);
        foreach (var song in TestCorpus.Range(256))
        {
            var program = (int track, TimelineItem<RealizedNote> note) => InstrumentsOverTime.ProgramOf(note.Value.State, ((PitchInstrumentTrack)song.Song.TrackDefinitions[track]).InstrumentCode);
            var melody = song.Song.Notes!.GetValueOrDefault(song.Song.Track(TrackRole.Melody), Rmg.Core.Events.EventTimeline.Create<RealizedNote>(0));
            var counter = song.Song.Notes!.GetValueOrDefault(song.Song.Track(TrackRole.CounterMelody), Rmg.Core.Events.EventTimeline.Create<RealizedNote>(0));
            foreach (var (index, source, _, _) in Doublings(song).Where(x => x.Source == TrackRole.Melody && x.Target == TrackRole.CounterMelody))
            {
                var span = song.Map.Sections[index];
                var notes = counter.Where(x => x.Position >= span.Start - 1e-9 && x.Position < span.End - 1e-9).ToArray();
                if (notes.Length == 0)
                    continue;

                doubled++;
                var twinned = notes.Where(x => x.Value.State.GetStateValue(CompositionStateKinds.SoundsAs) > 0).ToArray();
                // a melody to sound as
                if (twinned.Length == 0 || melody.Count == 0)
                    continue;

                inTimbre++;
                // in the program the melody plays there
                foreach (var note in twinned)
                {
                    // the melody's note sounding there, or the nearest, but a pinch's, which sounds its harmonic on its own program
                    var near = melody.Where(x => x.Value.State.GetStateValue(CompositionStateKinds.Pinch) == 0)
                        .MinBy(x => x.Position <= note.Position + 1e-9 && x.Position + x.Value.Duration > note.Position ? -1 : Math.Abs(x.Position - note.Position));
                    await Assert.That(program(song.Track(TrackRole.CounterMelody), note)).IsEqualTo(program(song.Track(TrackRole.Melody), near)).Because($"seed {song.Seed} at {note.Position}");
                }
            }
        }

        await Assert.That(doubled).IsGreaterThan(20);
        Console.WriteLine($"in its timbre {inTimbre} of {doubled} doubled");
        await Assert.That(inTimbre / (double)doubled).IsBetween(0.2, 0.8);
    }

    [Test]
    public async Task ADoubledLine_PlaysTheOthersNotes_AtTheirPlaces()
    {
        var (doubled, unison, unisonSame) = (0, 0, 0);
        foreach (var song in TestCorpus.Range(64))
        foreach (var (index, source, target, step) in Doublings(song))
        {
            var span = song.Map.Sections[index];
            // a solo plays over a doubling, and stop time's hits the band strikes together, the bass walking out of them
            if (song.IsSolo(span) || song.Trace.Any(x => x.Point == TracePoints.StopTime && x.Section == span.SectionId && x.Value is true))
                continue;
            // but a part's walk into the next section, which it plays as its own
            double[] At(TrackRole role) => [..song.Song.Notes!.Single(x => song.Song.TrackDefinitions[x.Key].Role == role).Value
                .Where(x => x.Position >= span.Start && x.Position < span.End && x.Value.State.GetStateValue(CompositionStateKinds.Walk) == 0).Select(x => x.Position)];
            var (sourceAt, targetAt) = (At(source), At(target));
            if (targetAt.Length == 0)
                continue;

            // the doubling part's notes are the doubled part's in every pattern that part plays, and a held note struck again
            // where a chord changes, an eighth early where the section pushes its changes (Pushes); its own where a texture
            // rests the doubled part
            var pushes = song.Trace.Any(x => x.Point == TracePoints.Push && x.Section == span.SectionId && x.Value is true);
            var changes = song.ChordChanges.Concat(pushes ? song.ChordChanges.Select(x => x - Pushes.Early) : []).ToArray();
            bool InPlayedPattern(double position)
            {
                var start = span.Start + Math.Floor((position - span.Start) / span.Meter.PatternDuration + 1e-9) * span.Meter.PatternDuration;
                return sourceAt.Any(y => y >= start - 1e-9 && y < start + span.Meter.PatternDuration - 1e-9);
            }

            await Assert.That(targetAt.Where(InPlayedPattern).All(x => sourceAt.Concat(changes).Any(y => Math.Abs(x - y) < 1e-6))).IsTrue().Because($"seed {song.Seed}, section {index}");
            // and in unison every note it doubles the doubled part's pitch class, a chromatic passing note's too
            if (step == 0)
            {
                Dictionary<double, int> Classes(TrackRole role) => song.Song.Notes!.Single(x => song.Song.TrackDefinitions[x.Key].Role == role).Value
                    .Where(x => x.Position >= span.Start && x.Position < span.End && (role == source || x.Value.State.GetStateValue(CompositionStateKinds.Doubled) > 0)).GroupBy(x => Math.Round(x.Position, 6)).ToDictionary(x => x.Key, x => x.First().Value.Pitches[0].Mod(12));
                var (from, to) = (Classes(source), Classes(target));
                foreach (var (at, pitch) in to.Where(x => from.ContainsKey(x.Key)))
                    (unison, unisonSame) = (unison + 1, unisonSame + (pitch == from[at] ? 1 : 0));
            }
            doubled++;
        }

        await Assert.That(doubled).IsGreaterThan(10);

        // a calibration: a later stage may move a note the doubled part plays, a riff's landing say, which the doubling
        // played as it was; a unison's chromatic notes doubled a semitone apart would fall far below it (all but one in 2,710 seen)
        Console.WriteLine($"of {unison} notes doubled in unison, {unisonSame / (double)Math.Max(1, unison):P1} in the doubled part's pitch class");
        await Assert.That(unison).IsGreaterThan(50);
        await Assert.That(unisonSame / (double)unison).IsGreaterThan(0.9);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var songs = TestCorpus.Range(256);
        var withRiff = songs.Where(x => !x.Absent.Contains(TrackRole.Riff)).ToArray();
        var riffSections = withRiff.Sum(x => x.Map.Sections.Count(span => !x.Resting(span).Contains(TrackRole.Riff)));
        var sections = withRiff.Sum(x => x.Map.Sections.Length);
        var doublings = songs.SelectMany(Doublings).ToArray();
        Console.WriteLine($"a riff in {withRiff.Length} of {songs.Length} songs, playing in {riffSections / (double)sections:P0} of their sections; " +
                          $"doublings: {string.Join(", ", doublings.GroupBy(x => (x.Source, x.Target)).Select(x => $"{x.Key.Target} with {x.Key.Source} {x.Count()} ({string.Join("/", x.GroupBy(y => y.Step).OrderBy(y => y.Key).Select(y => $"{y.Key}:{y.Count()}"))})"))}");
        await Task.CompletedTask;
    }
}
