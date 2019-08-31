using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using RMG.Core.Music;
using RMG.Core.Utils;

namespace RMG.Core.Generation
{
    public class ObjectGenerator<T> : IGenerator
    {
        private readonly IDictionary<string, ObjectPropertyGenerationSettings> _propertyGenerators =
            new Dictionary<string, ObjectPropertyGenerationSettings>();

        object IGenerator.Generate(GenerationContext context)
        {
            return Generate(context);
        }

        public ObjectGenerator<T> WithProperty(
            Expression<Func<T, object>> propertyExpression,
            Action<ObjectPropertyGenerationSettingsBuilder<T>> propertySettingsSetupFunc
        )
        {
            var propertyName = propertyExpression.GetPropertyName();
            var property = typeof(T).GetProperty(propertyName);

            var propertySettingsBuilder = new ObjectPropertyGenerationSettingsBuilder<T>();
            propertySettingsSetupFunc(propertySettingsBuilder);

            _propertyGenerators[propertyName] = propertySettingsBuilder.Build(property);

            return this;
        }

        public T Generate(GenerationContext context)
        {
            var obj = Activator.CreateInstance<T>();

            if (obj is IDuration durationObj && context.Value is IDuration parentDuration)
            {
                durationObj.Duration = parentDuration.Duration;
            }

            var objectContext = new GenerationContext(context, obj);
            var propertyGenerators = ObjectPropertyGenerationSettingsSorter.Sort(_propertyGenerators.Values);
            foreach (var propertyGenerator in propertyGenerators)
            {
                var propertyValue = propertyGenerator.Generator.RunGeneration(
                    objectContext,
                    propertyGenerator.Property.PropertyType);
                propertyGenerator.Property.SetValue(obj, propertyValue);
            }

            return obj;
        }
    }
}
