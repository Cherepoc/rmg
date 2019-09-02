namespace RMG.Core.Generation.ContextGenerators
{
    public sealed class ContextCacheGenerator<TEntity, TValue> : GeneratorBase<TValue>
    {
        public IGenerator<TValue> ValueGenerator { get; set; }

        public override TValue Generate(GenerationContext context)
        {
            var entityContext = context.FindParent(x => x.Value is TEntity);
            if (entityContext.Cache.TryGetValue(this, out var cachedValue))
            {
                return (TValue) cachedValue;
            }

            var value = ValueGenerator.Generate(context);
            entityContext.Cache.Add(this, value);
            return value;
        }
    }
}
