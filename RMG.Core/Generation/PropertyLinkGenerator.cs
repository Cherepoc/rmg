using System;
using System.Linq.Expressions;
using RMG.Core.Utils;

namespace RMG.Core.Generation
{
    public sealed class PropertyLinkGenerator<TEntity, TValue> : IGenerator
    {
        public string PropertyName { get; set; }

        object IGenerator.Generate(GenerationContext context)
        {
            return Generate(context);
        }

        public TValue Generate(GenerationContext context)
        {
            var propertyInfo = typeof(TEntity).GetProperty(PropertyName);
            var entity = context.FindParentValue<TEntity>();
            return (TValue) propertyInfo.GetValue(entity);
        }

        public PropertyLinkGenerator<TEntity, TValue> FromProperty(Expression<Func<TEntity, TValue>> propertyExpression)
        {
            PropertyName = propertyExpression.GetPropertyName();
            return this;
        }
    }
}
