using System;
using System.Linq.Expressions;
using RMG.Core.Generation.ContextGenerators;
using RMG.Core.Generation.ObjectGenerators;

namespace RMG.Core.Generation
{
    public static class GeneratorExtensions
    {
        public static IGenerator<TGenerator> Cache<TGenerator, TEntity>(this IGenerator<TGenerator> generator)
        {
            return new ContextCacheGenerator<TGenerator, TEntity>
            {
                ValueGenerator = generator
            };
        }

        public static IGenerator<TProperty> Property<TEntity, TProperty>(
            this IGenerator<TEntity> generator,
            Expression<Func<TEntity, TProperty>> propertyExpression
        )
        {
            return new EntityPropertyGenerator<TEntity, TProperty>
            {
                EntityGenerator = generator
            }.FromProperty(propertyExpression);
        }
    }
}
