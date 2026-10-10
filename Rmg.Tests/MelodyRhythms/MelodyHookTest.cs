using System.Collections.Immutable;
using Rmg.Core.Composition;

namespace Rmg.Tests.MelodyRhythms;

/// <summary>A section's melody on a hook (<see cref="MelodyLayers.HookChanceOf" />): a chorus's convention, every time in the plainest songs and never in the wildest, another section's never but by a genre's.</summary>
public sealed class MelodyHookTest
{
    private static SongOverrides Melody(double melody) => new(Facets: ImmutableDictionary<Facet, double>.Empty.Add(Facet.Melody, melody));

    private static (bool IsChorus, bool Hooks)[] Hooks(CorpusSong song)
    {
        var structure = (SongStructure)song.Trace.Single(x => x.Point == TracePoints.SongForm).Value!;
        return [..song.Trace.Where(x => x.Point == TracePoints.MelodyHook).Select(x => (structure.Roles[x.Section] == SectionRole.Chorus, (bool)x.Value!))];
    }

    [Test]
    public async Task AChorus_IsOnItsHook_InThePlainestSongs_AndNeverInTheWildest_AndNoOtherSection()
    {
        var plain = TestCorpus.InParallel(Enumerable.Range(0, 32), seed => Hooks(TestCorpus.Get(seed, Melody(0)))).SelectMany(x => x).ToArray();
        var wild = TestCorpus.InParallel(Enumerable.Range(0, 32), seed => Hooks(TestCorpus.Get(seed, Melody(1)))).SelectMany(x => x).ToArray();

        await Assert.That(plain.Count(x => x.IsChorus)).IsGreaterThan(10);
        await Assert.That(plain.Where(x => x.IsChorus).All(x => x.Hooks)).IsTrue();
        await Assert.That(wild.Any(x => x.Hooks)).IsFalse();
        await Assert.That(plain.Where(x => !x.IsChorus).Any(x => x.Hooks)).IsFalse();
    }
}
