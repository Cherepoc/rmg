using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;

namespace Rmg.Tests.SongGenerators;

public sealed class SongGeneratorScaleTest
{
    private const int SongCount = 100;

    /// <summary>Every section's scale, by its id, as the trace recorded it.</summary>
    private static Dictionary<int, Scale> SectionScales(CorpusSong song) =>
        song.Trace.Where(x => x.Point == "Section scale").ToDictionary(x => x.Section, x => Core.Composition.Scales.All.Single(s => s.Name == x.Phrase));

    [Test]
    public async Task EveryPitchedNote_PlaysInOneScaleOfSevenNotes()
    {
        foreach (var song in TestCorpus.Range(40))
        foreach (var (track, notes) in song.Song.Notes!.Where(x => x.Key < DrumGroups.FirstTrackNumber))
        foreach (var note in notes)
            await Assert.That(note.Value.State.GetStateValue(StateKinds.ScaleOffsets).Length).IsEqualTo(7);
    }

    [Test]
    public async Task EveryChordNote_IsInItsSectionsScale()
    {
        foreach (var song in TestCorpus.Range(40))
        {
            var scales = SectionScales(song);
            foreach (var span in song.Map.Sections)
            foreach (var note in song.Song.Notes![SongTracks.ChordsTrack].Where(x => x.Position >= span.Start && x.Position < span.End))
            {
                var state = note.Value.State;
                var scale = Realizer.RaiseScaleSteps(scales[span.SectionId].Offsets, state.GetStateValue(StateKinds.RaisedScaleSteps));
                foreach (var pitch in note.Value.Pitches)
                    await Assert.That(scale.Contains((pitch - state.GetStateValue(StateKinds.KeyOffset)).Mod(12))).IsTrue();
            }
        }
    }

    [Test]
    public async Task ASectionNowAndThen_PlaysInAnotherScale_MostlyANeighbour_BrighterTheMoreEnergyItHas()
    {
        var changes = new List<int>();
        var brighter = new List<double>();
        var darker = new List<double>();
        var sections = 0;
        foreach (var song in TestCorpus.Range(SongCount))
        {
            var scales = SectionScales(song);
            var energies = song.Trace.Where(x => x.Point == "Section energy")
                .ToDictionary(x => x.Section, x => x.StateMap.GetStateValue(CompositionStateKinds.Energy));
            var first = song.Map.Sections[0].SectionId;
            foreach (var (id, scale) in scales.Where(x => x.Key != first))
            {
                sections++;
                if (scale == scales[first])
                    continue;
                changes.Add(scale.Distance(scales[first]));
                (scale.Brightness > scales[first].Brightness ? brighter : darker).Add(energies[id]);
            }
        }

        Console.WriteLine($"Energy of the sections that turn brighter {brighter.Average():F2} ({brighter.Count}), darker {darker.Average():F2} ({darker.Count})");
        await Assert.That(brighter.Average()).IsGreaterThan(darker.Average());

        Console.WriteLine($"{changes.Count} of {sections} sections change scale; by notes changed: " +
                          string.Join(", ", changes.GroupBy(x => x).OrderBy(x => x.Key).Select(x => $"{x.Key}: {x.Count()}")));
        await Assert.That(changes.Count / (double)sections).IsBetween(0.05, 0.3);
        await Assert.That(changes.Count(x => x == 1)).IsGreaterThan(changes.Count / 2);
    }
}
