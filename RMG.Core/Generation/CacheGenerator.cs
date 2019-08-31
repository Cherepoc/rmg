namespace RMG.Core.Generation
{
    public sealed class CacheGenerator<TEntity, TValue> : IGenerator
    {
        public IGenerator ValueGenerator { get; set; }

        object IGenerator.Generate(GenerationContext context)
        {
            return Generate(context);
        }

        public TValue Generate(GenerationContext context)
        {
            var entityContext = context.FindParent(x => x.Value is TEntity);
            if (entityContext.Cache.TryGetValue(this, out var cachedValue))
            {
                return (TValue) cachedValue;
            }

            var value = ValueGenerator.RunGeneration<TValue>(context);
            entityContext.Cache.Add(this, value);
            return value;
        }
    }
}
