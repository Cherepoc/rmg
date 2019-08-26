using System;
using System.Reflection;

namespace RMG.Core.Generation
{
    public sealed class ValueProvider<T>
    {
        private readonly Type _parentType;
        private readonly PropertyInfo _property;

        public ValueProvider(PropertyInfo property, Type parentType)
        {
            _property = property;
            _parentType = parentType;
        }

        public T GetValue(GenerationContext context)
        {
            var parentContext = context
                .FindParent(x => x.Value != null && x.Value.GetType().IsAssignableFrom(_parentType));
            return (T) _property.GetValue(parentContext.Value);
        }
    }
}
