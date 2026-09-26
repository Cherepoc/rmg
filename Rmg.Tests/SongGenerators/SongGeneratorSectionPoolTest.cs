using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;

namespace Rmg.Tests.SongGenerators;

public sealed class SongGeneratorSectionPoolTest
{
    private static readonly StateKind<ImmutableArray<Chord>> Pool = CompositionStateKinds.ChordPool.Collection;

    private static readonly StateKind<int> Index = CompositionStateKinds.ChordPool.Index;

    private static StateMap PoolOf(params double[] firstHeights) =>
        StateMap.FromStates([Pool.CreateState([..firstHeights.Select(x => new Chord([0, x], false))])]);

    [Test]
    public async Task SectionPool_ListsTheSongsEntriesFirst()
    {
        var song = PoolOf(0.1, 0.2).MergeWith(StateMap.FromStates([Index.CreateState(1)]));
        var section = PoolOf(0.3, 0.4);

        var result = SongGenerator.CreateSectionStateMap(song, StateMap.FromStates([Index.CreateState(-1)]), section);

        await Assert.That(result.GetStateValue(Pool).Select(x => x.Heights[1])).IsEquivalentTo([0.1, 0.2, 0.3, 0.4]);
        await Assert.That(result.GetStateValue(Pool)[0].Heights[1]).IsEqualTo(0.1);
        await Assert.That(result.GetStateValue(Pool)[3].Heights[1]).IsEqualTo(0.4);
        // the section's draws still add to the song's
        await Assert.That(result.GetStateValue(Index)).IsEqualTo(0);
    }
}
