using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Tests.SongGenerators;

public sealed class SongGeneratorScaleTest
{
    private const int SongCount = 128;

    /// <summary>Every section's scale, by its id, as the trace recorded it.</summary>
    private static Dictionary<int, Scale> SectionScales(CorpusSong song) =>
        song.Trace.Where(x => x.Point == TracePoints.SectionScale).ToDictionary(x => x.Section, x => (Scale)x.Value!);

    [Test]
    public async Task EveryPitchedNote_PlaysInOneScaleOfSevenNotes()
    {
        foreach (var song in TestCorpus.Range(32))
        foreach (var (track, notes) in song.Song.Notes!.Where(x => !song.IsDrum(x.Key)))
        foreach (var note in notes)
            await Assert.That(note.Value.State.GetStateValue(StateKinds.ScaleOffsets).Length).IsEqualTo(7);
    }

    [Test]
    public async Task EveryChordNote_IsInItsSectionsScale()
    {
        foreach (var song in TestCorpus.Range(32))
        {
            var scales = SectionScales(song);
            // but where the chords solo, their notes a solo's line in a line scale of its own (LineScales)
            foreach (var span in song.Map.Sections.Where(x => !(song.Solos.TryGetValue(song.Map.Sections.IndexOf(x), out var solo) && solo.Soloists.Contains(TrackRole.Chords))))
            foreach (var note in song.Song.Notes![song.Song.Track(TrackRole.Chords)].Where(x => x.Position >= span.Start && x.Position < span.End))
            {
                var state = note.Value.State;
                var scale = Realizer.AlterScaleSteps(scales[span.SectionId].Offsets, state.GetStateValue(StateKinds.ScaleAlterations) is { IsSet: true } altered ? altered.Value : StepAlterations.None);
                foreach (var pitch in note.Value.Pitches)
                    await Assert.That(scale.Contains((pitch - state.GetStateValue(StateKinds.KeyOffset) - state.GetStateValue(StateKinds.ChordShift)).Mod(12))).IsTrue();
            }
        }
    }

    [Test]
    public async Task ASectionNowAndThen_PlaysInAnotherScale_MostlyANeighbour_BrighterTheMoreEnergyItHas()
    {
        var changes = new List<int>();
        // the changes of sections of the scale facet's plain half, where the song's neighbours are allowed; the wild end
        // allows only the stranger scales, rarely a neighbour of the song's
        var plainChanges = new List<int>();
        var brighter = new List<double>();
        var darker = new List<double>();
        var sections = 0;
        foreach (var song in TestCorpus.Range(SongCount))
        {
            var scales = SectionScales(song);
            var energies = song.Trace.Where(x => x.Point == TracePoints.SectionEnergy)
                .ToDictionary(x => x.Section, x => x.StateMap.GetStateValue(CompositionStateKinds.Energy));
            var first = song.Map.Sections[0].SectionId;
            var facets = song.Trace.Where(x => x.Point == TracePoints.SectionUnconventionality).ToDictionary(x => x.Section, x => (Unconventionality)x.Value!);
            foreach (var (id, scale) in scales.Where(x => x.Key != first))
            {
                sections++;
                if (scale == scales[first])
                    continue;
                changes.Add(scale.Distance(scales[first]));
                if (facets[id][Facet.Scale] <= 0.5)
                    plainChanges.Add(scale.Distance(scales[first]));
                // a tie leaning neither way
                if (scale.Brightness != scales[first].Brightness)
                    (scale.Brightness > scales[first].Brightness ? brighter : darker).Add(energies[id]);
            }
        }

        Console.WriteLine($"Energy of the sections that turn brighter {brighter.Average():F2} ({brighter.Count}), darker {darker.Average():F2} ({darker.Count})");
        await Assert.That(brighter.Average()).IsGreaterThan(darker.Average());

        Console.WriteLine($"{changes.Count} of {sections} sections change scale; by notes changed: " +
                          string.Join(", ", changes.GroupBy(x => x).OrderBy(x => x.Key).Select(x => $"{x.Key}: {x.Count()}")));
        await Assert.That(changes.Count / (double)sections).IsBetween(0.05, 0.3);
        await Assert.That(plainChanges.Count(x => x == 1)).IsGreaterThan(plainChanges.Count / 2);
    }
}
