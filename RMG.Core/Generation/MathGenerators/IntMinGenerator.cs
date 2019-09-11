using System.Collections.Generic;
using System.Linq;

namespace RMG.Core.Generation.MathGenerators
{
    public sealed class IntMinGenerator : ReduceOperatorGeneratorBase<int>
    {
        protected override int Reduce(IEnumerable<int> collection)
        {
            return collection.Min();
        }
    }
}
