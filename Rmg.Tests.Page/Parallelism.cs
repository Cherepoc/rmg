using TUnit.Core.Interfaces;

[assembly: ParallelLimiter<Rmg.Tests.Page.Parallelism>]

namespace Rmg.Tests.Page;

/// <summary>
///     How many pages are driven at once: a few, as every test's songs go to the one server from the one address, which
///     keeps only 4 of an address's songs waiting, and a test's timing is the point of it.
/// </summary>
public sealed class Parallelism : IParallelLimit
{
    public int Limit => 4;
}
