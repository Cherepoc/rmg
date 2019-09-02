using System;
using System.Linq.Expressions;
using RMG.Core.Utils;

namespace RMG.Core.Generation.ObjectGenerators
{
    public sealed class EntityPropertyValueGenerator<TEntity, TValue> : GeneratorBase<TValue>
    {
        public IGenerator<TEntity> EntityGenerator { get; set; }
        
        public string PropertyName { get; set; }

        public override TValue Generate(GenerationContext context)
        {
            var entity = EntityGenerator.Generate(context);
            var propertyInfo = typeof(TEntity).GetProperty(PropertyName);
            return (TValue) propertyInfo.GetValue(entity);
        }

        public EntityPropertyValueGenerator<TEntity, TValue> FromProperty(
            Expression<Func<TEntity, TValue>> propertyExpression
        )
        {
            PropertyName = propertyExpression.GetPropertyName();

            return this;
        }
    }
}
