using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;

namespace Rmg.Tests.Chords;

public sealed class KeyPreparationTest
{
    private static ImmutableArray<(double Change, ImmutableArray<double> Chords, KeyChange.Device Device)> Preparations(CorpusSong song) =>
        (ImmutableArray<(double, ImmutableArray<double>, KeyChange.Device)>)song.Trace.Single(x => x.Point == TracePoints.KeyPreparation).Value!;

    private static (double Change, ImmutableArray<double> Chords, KeyChange.Device Device)[] Chains(CorpusSong song) =>
        [..Preparations(song).Where(x => x.Device == KeyChange.Device.Chain)];

    /// <summary>The state at a place as every part reads it: the common state's, its chord's root over the section's home, which the parts' own state holds.</summary>
    private static StateMap State(CorpusSong song, double position)
    {
        var map = song.Song.TrackEventStateTimelineMap;
        var common = map.CommonStateTimelineMap.GetEffectiveStateMapAt(position);
        var home = map.TrackTimelineMap.First(x => song.Song.TrackDefinitions[x.Key] is PitchInstrumentTrack).Value.StateTimelineMap.GetEffectiveStateMapAt(position).GetStateValue(StateKinds.ChordRoot);
        return common.With(StateKinds.ChordRoot, common.GetStateValue(StateKinds.ChordRoot) + home);
    }

    /// <summary>The root's pitch class of the chord sounding at a place, as every part reads it.</summary>
    private static int Root(CorpusSong song, double position) => Realizer.GetChord(State(song, position)).Chord.GetPitch(0).Mod(12);

    /// <summary>Every chain's links, the semitones from each chord's root down to the next's: 7 a dominant's, 1 a substitute's, 11 a leading tone's, 4 a mediant's, in a major scale.</summary>
    private static int[][] Links(CorpusSong song) =>
    [
        ..Chains(song).Select(chain => chain.Chords.Append(chain.Change).ToArray())
            .Select(places => places.Zip(places.Skip(1), (chord, next) => (Root(song, chord) - Root(song, next)).Mod(12)).ToArray())
    ];

    private static SongOverrides Scale(double scale) => new(Facets: TestCorpus.FacetsOf((Facet.Scale, scale), (Facet.Form, 0.5)));

    [Test]
    [Arguments(1)]
    [Arguments(3)]
    public async Task EveryChordOfAChain_LeadsToTheNext_ByItsLink_OneLinkAChain(int phrases)
    {
        var overrides = phrases == 1 ? Scale(1) : Scale(1) with { Genre = TestCorpus.PhrasesOnly(phrases) };
        var chains = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => Links(TestCorpus.Get(seed, overrides))).SelectMany(x => x).ToArray();

        await Assert.That(chains.Length).IsGreaterThan(20);
        // one link a chain, its intervals as its section's scale has them: the leading tone's a whole step in a minor
        // scale, the subtonic; the dominant's a fifth in every mode, Locrian's raised
        foreach (var links in chains)
            await Assert.That(links.Distinct().Count()).IsEqualTo(1).Because(string.Join(" ", links));
        await Assert.That(chains.Count(x => x[0] is 7 or 1 or 11 or 4) / (double)chains.Length).IsGreaterThan(0.8);
    }

    [Test]
    public async Task APivotChord_IsOneTheOldKeyHasToo()
    {
        var pivots = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 128), seed => TestCorpus.Get(seed, Scale(0.4))))
        {
            var common = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap;
            foreach (var (_, chords, _) in Preparations(song).Where(x => x.Device == KeyChange.Device.Pivot))
            {
                // its triad's notes, as every part reads them, all of the scale the moment before it plays
                var state = State(song, chords[0]);
                var triad = new[] { 0, 2, 4 }.Select(x => Realizer.GetChord(state).Chord.GetPitch(x).Mod(12));
                // the scale there in the key before
                var key = common.GetEffectiveStateMapAt(chords[0] - 1e-6).GetStateValue(StateKinds.KeyOffset);
                var scale = Enumerable.Range(0, 7).Select(x => Realizer.GetChord(state.With(StateKinds.KeyOffset, key).With(StateKinds.ChordRoot, x)).Chord.GetPitch(0).Mod(12)).ToHashSet();
                await Assert.That(triad.All(scale.Contains)).IsTrue().Because($"seed {song.Seed} at {chords[0]}");
                pivots++;
            }
        }

        await Assert.That(pivots).IsGreaterThan(5);
    }

    [Test]
    public async Task APedal_HoldsTheNewKeysDominant_InTheBassUnderTheBarBefore()
    {
        var (notes, held) = (0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 128), seed => TestCorpus.Get(seed, Scale(1))))
        {
            var common = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap;
            foreach (var (change, from, _) in Preparations(song).Where(x => x.Device == KeyChange.Device.Pedal).Select(x => (x.Change, x.Chords[0], x.Device)))
            {
                var dominant = Realizer.GetChord(common.GetEffectiveStateMapAt(change).With(StateKinds.ChordRoot, 4)).Chord.GetPitch(0).Mod(12);
                foreach (var note in song.Song.Notes!.GetValueOrDefault(song.Song.Track(TrackRole.Bass), EventTimeline.Create<Rmg.Core.Songs.RealizedNote>(0)).Where(x => x.Position >= from - 1e-9 && x.Position < change - 1e-9))
                {
                    notes++;
                    if (note.Value.Pitches[0].Mod(12) == dominant)
                        held++;
                }
            }
        }

        await Assert.That(notes).IsGreaterThan(20);
        await Assert.That(held / (double)notes).IsGreaterThan(0.9);
    }

    [Test]
    public async Task ASequence_PlaysTheSectionsLastPhrase_HalfwayToTheNewKey()
    {
        var sequences = 0;
        // between the middle and the wild end, where a change leaves its section's last phrase to climb in
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 256), seed => TestCorpus.Get(seed, Scale(0.8))))
        {
            var keys = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetStateTimeline(StateKinds.KeyOffset);
            foreach (var (change, chords, _) in Preparations(song).Where(x => x.Device == KeyChange.Device.Sequence))
            {
                // the way round the octave from the key before to the new one, and the phrase's key on it, strictly between
                var (before, halfway, after) = (keys.GetEffectiveValueAt(chords[0] - 1e-6), keys.GetEffectiveValueAt(chords[0]), keys.GetEffectiveValueAt(change));
                var way = (after - before + 6).Mod(12) - 6;
                var first = (halfway - before + 6).Mod(12) - 6;
                await Assert.That(Math.Sign(first) == Math.Sign(way) && Math.Abs(first) < Math.Abs(way)).IsTrue().Because($"seed {song.Seed} at {change}: {before} {halfway} {after}");
                sequences++;
            }
        }

        await Assert.That(sequences).IsGreaterThan(5);
    }

    [Test]
    public async Task AMiddlingSongsChain_IsTheDominant_MostOften()
    {
        var links = TestCorpus.InParallel(Enumerable.Range(0, 128), seed => Links(TestCorpus.Get(seed, Scale(0.5)))).SelectMany(x => x).Select(x => x[0]).ToArray();

        Console.WriteLine($"{links.Count(x => x == 7)} of {links.Length} chains by the dominant");
        // measured 61% to 72% of 36 to 50 chains, at offsets 0 and a million
        await Assert.That(links.Length).IsGreaterThan(10);
        await Assert.That(links.Count(x => x == 7) / (double)links.Length).IsGreaterThan(0.5);
    }

    /// <summary>How many key changes are prepared, by fifth of the scale facet, with how long chains of which links.</summary>
    [Test]
    [Explicit]
    public async Task Report()
    {
        var songs = TestCorpus.Measure(1024, song => (Scale: song.Unconventionality[Facet.Scale],
            Changes: ((ImmutableArray<KeyChange>)song.Trace.Single(t => t.Point == TracePoints.KeyChange).Value!).Length, Links: Links(song),
            Pivots: Preparations(song).Count(x => x.Device == KeyChange.Device.Pivot), Pedals: Preparations(song).Count(x => x.Device == KeyChange.Device.Pedal), Sequences: Preparations(song).Count(x => x.Device == KeyChange.Device.Sequence)));
        foreach (var band in songs.GroupBy(x => Math.Clamp((int)(x.Scale * 5), 0, 4)).OrderBy(x => x.Key))
        {
            var chains = band.SelectMany(x => x.Links).ToArray();
            Console.WriteLine($"  scale {band.Key}/5: {band.Sum(x => x.Changes)} key changes, {chains.Length} chains, {band.Sum(x => x.Pivots)} pivots, {band.Sum(x => x.Pedals)} pedals, {band.Sum(x => x.Sequences)} sequences; chains of {string.Join(" ", chains.GroupBy(x => x.Length).OrderBy(x => x.Key).Select(x => $"{x.Key}:{x.Count()}"))}, " +
                              $"links {string.Join(" ", chains.GroupBy(x => x[0]).OrderBy(x => x.Key).Select(x => $"{x.Key}:{x.Count()}"))}");
        }

        await Task.CompletedTask;
    }
}
