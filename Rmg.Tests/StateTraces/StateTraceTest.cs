using Rmg.Core.Composition;
using Rmg.Core.Events;

namespace Rmg.Tests.StateTraces;

public sealed class StateTraceTest
{
    private static readonly HashSet<int> SnareTracks =
    [
        ..new[] { DrumDefinitions.AcousticSnare, DrumDefinitions.ElectricSnare, DrumDefinitions.Clap, DrumDefinitions.CrossStick }
            .Select(DrumGroups.GetTrackNumber)
    ];

    private static readonly string[] Layers =
    [
        "Song", "Section", "Track", "Track role", "Section track", "Drum", "Drum group", "Section drum group",
        "Bar pattern", "Beat", "Note", "Bar", StateContribution.UnlabeledLayer
    ];

    [Test]
    public async Task Trace_RecordsEveryTracksBarPatterns()
    {
        using var trace = StateTrace.Start();
        var song = SongGenerator.GenerateSong(1);

        var tracks = trace.Entries.Select(x => x.Track).ToHashSet();

        await Assert.That(trace.Entries.Count).IsGreaterThan(0);
        await Assert.That(tracks.IsSupersetOf(song.TrackDefinitions.Keys.Where(x => x < DrumGroups.FirstTrackNumber))).IsTrue();
        await Assert.That(trace.Entries.All(x => x.Bar is >= 0 and < 4)).IsTrue();
        await Assert.That(trace.Entries.All(x => x.Position is >= 0 and < 4)).IsTrue();
    }

    [Test]
    public async Task ChordPick_IsExplainedBySongSectionAndBar()
    {
        using var trace = StateTrace.Start();
        SongGenerator.GenerateSong(1);

        var index = CompositionStateKinds.ChordPool.Index;
        var chordEntries = trace.Entries.Where(x => x.Point == "Chord").ToArray();
        var layers = new HashSet<string>();

        await Assert.That(chordEntries.Length).IsGreaterThan(0);
        foreach (var entry in chordEntries)
        {
            var contributions = entry.StateMap.Explain(index);
            layers.UnionWith(contributions.Select(x => x.Layer));

            // the pick adds up from the shared layers alone, and the pool it picks from is there
            await Assert.That(contributions.Sum(x => (int)x.Value)).IsEqualTo(entry.StateMap.GetStateValue(index));
            await Assert.That(entry.StateMap.GetStateValue(CompositionStateKinds.ChordPool.Collection).Length).IsGreaterThan(0);
        }

        // the bar's part is named now, and no track's layer takes part
        await Assert.That(layers.IsSubsetOf(["Song", "Section", "Bar"])).IsTrue();
        await Assert.That(layers).Contains("Bar");
    }

    [Test]
    public async Task SnareBackbeat_IsExplainedByItsLayers()
    {
        using var trace = StateTrace.Start();
        SongGenerator.GenerateSong(1);

        var kind = CompositionStateKinds.Rhythm.Phase.Rank;
        var snareEntries = trace.Entries.Where(x => x.Point == "Bar pattern" && SnareTracks.Contains(x.Track)).ToArray();

        await Assert.That(snareEntries.Length).IsGreaterThan(0);
        // a value the layers add up to 0 is the default, which a map does not keep, nor its parts
        var explained = snareEntries.Where(x => x.StateMap.GetStateValue(kind) != 0).ToArray();
        await Assert.That(explained.Length).IsGreaterThan(0);
        foreach (var entry in explained)
        {
            var contributions = entry.StateMap.Explain(kind);

            // the snare's own +1 is there, every layer is a known one, and the parts add up to the value
            await Assert.That(contributions).Contains(new StateContribution("Drum", 1));
            await Assert.That(contributions.All(x => Layers.Contains(x.Layer))).IsTrue();
            await Assert.That(contributions.Sum(x => (int)x.Value)).IsEqualTo(entry.StateMap.GetStateValue(kind));
        }
    }

    [Test]
    public async Task EveryRhythmLayer_ContributesToSomeBarPattern()
    {
        using var trace = StateTrace.Start();
        SongGenerator.GenerateSong(1);

        var layers = trace.Entries
            .Where(x => x.Point == "Bar pattern" && SnareTracks.Contains(x.Track))
            .SelectMany(x => x.StateMap.Explain(CompositionStateKinds.Rhythm.Phase.RankedOffset))
            .Select(x => x.Layer)
            .ToHashSet();

        // the phase's offset is drawn in every rhythm layer, so each shows up
        await Assert.That(layers.IsSupersetOf(["Song", "Section", "Track", "Section track", "Drum group", "Section drum group", "Bar pattern"])).IsTrue();
    }

    [Test]
    public async Task WithoutTrace_StatesKeepNoContributions()
    {
        var map = new StateMapBuilder("Song")
            .Add(StateKinds.KeyOffset, 2)
            .ToStateMap(new Rmg.Core.Probabilities.GenerationContext(0))
            .MergeWith(StateMap.FromStates([StateKinds.KeyOffset.CreateState(3)]));

        await Assert.That(map.Explain(StateKinds.KeyOffset).IsEmpty).IsTrue();
    }

    [Test]
    public async Task Trace_AlreadyRunning_ResultsIn_InvalidOperation()
    {
        using var trace = StateTrace.Start();

        await Assert.That(() => StateTrace.Start()).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task DisposedTrace_StopsRecording()
    {
        var trace = StateTrace.Start();
        trace.Dispose();
        SongGenerator.GenerateSong(1);

        await Assert.That(trace.Entries.Count).IsEqualTo(0);

        // and another can start
        using var next = StateTrace.Start();
        await Assert.That(next.Entries.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Trace_DoesNotChangeTheSong()
    {
        var plain = Rmg.Core.Rendering.Render.RenderSong(SongGenerator.GenerateSong(3));
        Rmg.Core.Rendering.RenderedSong traced;
        using (StateTrace.Start())
            traced = Rmg.Core.Rendering.Render.RenderSong(SongGenerator.GenerateSong(3));

        using var plainStream = new MemoryStream();
        using var tracedStream = new MemoryStream();
        Rmg.Core.Midi.Write(plain, plainStream);
        Rmg.Core.Midi.Write(traced, tracedStream);

        await Assert.That(tracedStream.ToArray().SequenceEqual(plainStream.ToArray())).IsTrue();
    }
}
