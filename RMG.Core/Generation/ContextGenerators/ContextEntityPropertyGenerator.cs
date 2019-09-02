using System;
using System.Linq.Expressions;
using RMG.Core.Utils;

namespace RMG.Core.Generation.ContextGenerators
{
    public sealed class ContextEntityPropertyGenerator<TEntity, TValue> : GeneratorBase<TValue>
    {
        public string PropertyName { get; set; }

        public override TValue Generate(GenerationContext context)
        {
            var propertyInfo = typeof(TEntity).GetProperty(PropertyName);
            var entity = context.FindParentValue<TEntity>();
            return (TValue) propertyInfo.GetValue(entity);
        }

        public ContextEntityPropertyGenerator<TEntity, TValue> FromProperty(Expression<Func<TEntity, TValue>> propertyExpression)
        {
            PropertyName = propertyExpression.GetPropertyName();
            return this;
        }
    }
}
