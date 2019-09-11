using System.Collections.Generic;
using System.Linq;

namespace RMG.Core.Generation.MathGenerators
{
    public class IntMaxGenerator : ReduceOperatorGeneratorBase<int>
    {
        protected override int Reduce(IEnumerable<int> collection)
        {
            return collection.Max();
        }
    }
}
