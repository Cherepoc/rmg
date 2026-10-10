using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Arrangements;

/// <summary>
///     A line played in octaves by its own part in a section now and then (<see cref="LineDoubling" />): the melody's, the
///     counter-melody's or the riff's, its note and the note an octave from it, where the part plays no other doubling,
///     the likelier the louder the section.
/// </summary>
public sealed class OctaveTest
{
    private static SongOverrides Sound(double sound) => new(Facets: ImmutableDictionary<Facet, double>.Empty.Add(Facet.Sound, sound));

    /// <summary>Every appearance of a section in which a part that may play octaves plays: whether it does, and its energy.</summary>
    private static IEnumerable<(TrackRole Role, bool InOctaves, double Energy)> Appearances(CorpusSong song) => song.Trace
        .Where(x => x.Point == TracePoints.Octave)
        .Select(x => ((int, TrackRole Role, double Energy, bool InOctaves))x.Value!)
        .Select(x => (x.Role, x.InOctaves, x.Energy));

    [Test]
    public async Task ANoteInOctaves_IsAPartsLine_TwoNotesAnOctaveApart()
    {
        var doubled = 0;
        foreach (var song in TestCorpus.Range(64))
        foreach (var (track, notes) in song.Song.Notes!)
        foreach (var note in notes.Where(x => x.Value.State.GetStateValue(CompositionStateKinds.Octave) > 0))
        {
            // a solo plays another part's line as it is
            if (note.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 0)
                await Assert.That(LineDoubling.OctaveRoles).Contains(song.Song.TrackDefinitions[track].Role);
            // a solo's stacked note plays its chord in place of the octave
            if (note.Value.Pitches.Length == 1 || note.Value.State.GetStateValue(CompositionStateKinds.LineVoices) > 1)
                continue;

            doubled++;
            await Assert.That(note.Value.Pitches.Length).IsEqualTo(2);
            await Assert.That(Math.Abs(note.Value.Pitches[1] - note.Value.Pitches[0])).IsEqualTo(12);
        }

        await Assert.That(doubled).IsGreaterThan(0);
    }

    /// <summary>A line in octaves bends and sings a vibrato about as often as the same parts' lines of one note, in the same songs.</summary>
    [Test]
    public async Task ALineInOctaves_BendsAndSingsAVibrato_AsAnyLine()
    {
        static bool Shaped(TimelineItem<RealizedNote> x) => x.Value.State.GetStateValue(CompositionStateKinds.Bend) != 0
                                                             || x.Value.State.GetStateValue(CompositionStateKinds.Vibrato) > 0 || x.Value.State.GetStateValue(CompositionStateKinds.WideVibrato) > 0;
        var notes = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, Sound(1)))
            .SelectMany(song => song.Song.Notes!.Where(x => LineDoubling.OctaveRoles.Contains(song.Song.TrackDefinitions[x.Key].Role)).SelectMany(n => n.Value))
            .ToArray();
        var (octaves, single) = (notes.Where(x => x.Value.Pitches.Length == 2 && x.Value.State.GetStateValue(CompositionStateKinds.Octave) > 0).ToArray(), notes.Where(x => x.Value.Pitches.Length == 1).ToArray());
        var (inOctaves, alone) = (octaves.Average(x => Shaped(x) ? 1.0 : 0), single.Average(x => Shaped(x) ? 1.0 : 0));
        Console.WriteLine($"bent or sung with a vibrato: {inOctaves:P1} of {octaves.Length} notes in octaves, {alone:P1} of {single.Length} alone");
        await Assert.That(octaves.Length).IsGreaterThan(100);
        // seen 35% in octaves against 30% alone
        await Assert.That(inOctaves).IsGreaterThan(alone / 2);
    }

    [Test]
    public async Task APartPlaysOneDoublingAtATime()
    {
        var checkedOctaves = 0;
        foreach (var song in TestCorpus.Range(128))
        {
            var octaves = song.Trace.Where(x => x.Point == TracePoints.Octave).Select(x => ((int Index, TrackRole Role, double, bool InOctaves))x.Value!).Where(x => x.InOctaves).ToArray();
            var pairs = song.Trace.Where(x => x.Point == TracePoints.LineDoubling).Select(x => ((int Index, TrackRole Source, TrackRole Target, int))x.Value!).ToArray();
            var hasTwin = song.Song.Notes!.GetValueOrDefault(song.Song.Track(TrackRole.RiffTwin))?.Count > 0;

            foreach (var octave in octaves)
            {
                checkedOctaves++;
                await Assert.That(pairs.Any(x => x.Index == octave.Index && (x.Source == octave.Role || x.Target == octave.Role))).IsFalse();
                await Assert.That(hasTwin && octave.Role == TrackRole.Riff).IsFalse();
            }
        }

        await Assert.That(checkedOctaves).IsGreaterThan(0);
    }

    [Test]
    public async Task ALoudSection_IsPlayedInOctaves_OftenerThanAQuietOne()
    {
        var appearances = TestCorpus.Measure(256, song => Appearances(song).ToArray()).SelectMany(x => x).OrderBy(x => x.Energy).ToArray();
        var third = appearances.Length / 3;
        double Share(IEnumerable<(TrackRole, bool InOctaves, double)> some) => some.Count(x => x.InOctaves) / (double)some.Count();

        await Assert.That(Share(appearances.TakeLast(third))).IsGreaterThan(Share(appearances.Take(third)) * 1.3);
    }

    /// <summary>
    ///     How often a part plays a section in octaves, of the appearances it plays: by part, by fifth of the song's sound
    ///     facet and at its ends given, and by third of the sections' energy.
    /// </summary>
    [Test]
    [Explicit]
    public async Task Report()
    {
        static string Shares(IEnumerable<(TrackRole Role, bool InOctaves, double)> some) =>
            string.Join(", ", some.GroupBy(x => x.Role).OrderBy(x => x.Key).Select(x => $"{x.Key} {x.Count(a => a.InOctaves) / (double)x.Count():P0} of {x.Count()}"));

        var drawn = TestCorpus.Measure(512, song => (Fifth: Math.Clamp((int)(song.Unconventionality[Facet.Sound] * 5), 0, 4), Appearances: Appearances(song).ToArray()));
        foreach (var band in drawn.GroupBy(x => x.Fifth).OrderBy(x => x.Key))
            Console.WriteLine($"  sound {band.Key}/5: {Shares(band.SelectMany(x => x.Appearances))}");
        foreach (var sound in new[] { 0.0, 1.0 })
            Console.WriteLine($"  sound {sound} given: {Shares(TestCorpus.InParallel(Enumerable.Range(0, 64), seed => Appearances(TestCorpus.Get(seed, Sound(sound))).ToArray()).SelectMany(x => x))}");

        var byEnergy = drawn.SelectMany(x => x.Appearances).OrderBy(x => x.Energy).ToArray();
        foreach (var (name, part) in new[] { ("quietest", 0), ("middle", 1), ("loudest", 2) })
        {
            var third = byEnergy.Skip(part * byEnergy.Length / 3).Take(byEnergy.Length / 3).ToArray();
            Console.WriteLine($"  {name} third of the sections, energy {third.First().Energy:F2} to {third.Last().Energy:F2}: {third.Count(x => x.InOctaves) / (double)third.Length:P0} in octaves");
        }

        await Task.CompletedTask;
    }
}
