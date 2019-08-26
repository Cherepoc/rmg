using System;
using System.Linq.Expressions;
using System.Reflection;
using RMG.Core.Utils;

namespace RMG.Core.Generation
{
    public sealed class ValueProviderBuilder<TProperty>
    {
        private PropertyInfo _property;
        private Type _parentType;
        
        public ValueProviderBuilder<TProperty> FromParentProperty<TEntity>(
            Expression<Func<TEntity, TProperty>> propertyExpression
        )
        {
            var propertyName = propertyExpression.GetPropertyName();
            _property = typeof(TEntity).GetProperty(propertyName);
            _parentType = typeof(TEntity);

            return this;
        }

        public ValueProvider<TProperty> Build()
        {
            return new ValueProvider<TProperty>(_property, _parentType);
        }
    }
}
