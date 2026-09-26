using Rmg.Core.Composition;
using Rmg.Core.Events;

namespace Rmg.Tests.SongGenerators;

/// <summary>The song keeps only the state Render reads; the generation's own state stays in the generation.</summary>
public sealed class SongGeneratorStateScopeTest
{
    [Test]
    [Arguments(1)]
    [Arguments(2)]
    public async Task SongState_IsRenderStateOnly(int seed)
    {
        var map = SongGenerator.GenerateSong(seed).TrackEventStateTimelineMap;

        var kinds = map.CommonStateTimelineMap.StateTimelines
            .Concat(map.TrackTimelineMap.Values.SelectMany(x => x.StateTimelineMap.StateTimelines))
            .Select(x => x.StateKind)
            .Concat(map.TrackTimelineMap.Values.SelectMany(x => x.EventTimeline).SelectMany(x => x.Value.Kinds))
            .Distinct()
            .ToArray();

        await Assert.That(kinds.Length).IsGreaterThan(0);
        await Assert.That(kinds.Where(x => x.Scope != StateScope.Render).Select(x => x.Name)).IsEquivalentTo(Array.Empty<string>());
    }
}
