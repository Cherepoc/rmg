using Rmg.Core.Composition;

namespace Rmg.Tests.Generators;

/// <summary>The song's and a section's streams: each decision's its own, so that a change to one leaves the others as they were.</summary>
public sealed class StreamsTest
{
    [Test]
    public async Task EverySongStream_HasANumberOfItsOwn()
    {
        var shared = Enum.GetValues<SongStream>().GroupBy(x => (int)x).Where(x => x.Count() > 1).Select(x => string.Join(" and ", x)).ToArray();
        await Assert.That(shared).IsEmpty();
    }

    [Test]
    public async Task EverySectionStream_HasANumberOfItsOwn()
    {
        var shared = Enum.GetValues<SectionStream>().GroupBy(x => (int)x).Where(x => x.Count() > 1).Select(x => string.Join(" and ", x)).ToArray();
        await Assert.That(shared).IsEmpty();
    }
}
