using System;
using System.Linq.Expressions;
using RMG.Core.Utils;

namespace RMG.Core.Generation.ObjectGenerators
{
    public sealed class EntityPropertyGenerator<TEntity, TValue> : GeneratorBase<TValue>
    {
        public string PropertyName { get; set; }

        public IGenerator<TEntity> EntityGenerator { get; set; }

        public override TValue Generate(GenerationContext context)
        {
            var entity = EntityGenerator.Generate(context);
            var propertyInfo = typeof(TEntity).GetProperty(PropertyName);
            return (TValue) propertyInfo.GetValue(entity);
        }

        public EntityPropertyGenerator<TEntity, TValue> FromProperty(
            Expression<Func<TEntity, TValue>> propertyExpression
        )
        {
            PropertyName = propertyExpression.GetPropertyName();
            return this;
        }
    }
}
