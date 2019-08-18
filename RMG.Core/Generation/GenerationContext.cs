using System;

namespace RMG.Core.Generation
{
    public class GenerationContext
    {
        public GenerationContext(Random random)
        {
            Random = random;
        }

        public GenerationContext(GenerationContext parentContext, object value)
        {
            ParentContext = parentContext;
            Random = parentContext.Random;
            Value = value;
        }

        public GenerationContext ParentContext { get; }

        public object Value { get; }

        public Random Random { get; }

        public GenerationContext FindParent(Func<GenerationContext, bool> predicate)
        {
            var context = this;
            while (context != null)
            {
                if (predicate(context))
                {
                    break;
                }

                context = context.ParentContext;
            }

            return context;
        }
    }
}
