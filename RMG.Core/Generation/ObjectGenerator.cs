using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using RMG.Core.Utils;

namespace RMG.Core.Generation
{
    public class ObjectGenerator<T> : IGenerator
    {
        public IDictionary<string, IGenerator> PropertyGenerators { get; set; } = new Dictionary<string, IGenerator>();

        object IGenerator.Generate(GenerationContext context)
        {
            return Generate(context);
        }

        public ObjectGenerator<T> WithPropertyGenerator(
            Expression<Func<T, object>> propertyExpression,
            IGenerator generator
        )
        {
            var propertyName = propertyExpression.GetPropertyName();
            PropertyGenerators[propertyName] = generator;
            return this;
        }

        public T Generate(GenerationContext context)
        {
            var obj = Activator.CreateInstance<T>();
            var objectContext = new GenerationContext(context, obj);
            var type = typeof(T);
            foreach (var (propertyName, generator) in PropertyGenerators)
            {
                var property = type.GetProperty(propertyName);
                var propertyValue = generator.Generate(objectContext);
                property.SetValue(obj, propertyValue);
            }

            return obj;
        }
    }
}
