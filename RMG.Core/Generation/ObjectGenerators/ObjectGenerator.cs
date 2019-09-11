using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using RMG.Core.Music;
using RMG.Core.Utils;

namespace RMG.Core.Generation.ObjectGenerators
{
    public class ObjectGenerator<T> : GeneratorBase<T>
    {
        private readonly IDictionary<PropertyInfo, ObjectPropertyGenerationSettings> _propertyGenerators =
            new Dictionary<PropertyInfo, ObjectPropertyGenerationSettings>();

        public ObjectGenerator<T> WithProperty<TProperty>(
            Expression<Func<T, TProperty>> propertyExpression,
            Action<ObjectPropertyGenerationSettingsBuilder<T, TProperty>> propertySettingsSetupFunc
        )
        {
            var property = propertyExpression.GetPropertyInfo();

            var propertySettingsBuilder = new ObjectPropertyGenerationSettingsBuilder<T, TProperty>();
            propertySettingsSetupFunc(propertySettingsBuilder);

            _propertyGenerators[property] = propertySettingsBuilder.Build(property);

            return this;
        }

        public override T Generate(GenerationContext context)
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
                var propertyValue = propertyGenerator.Generator.Generate(objectContext);
                propertyGenerator.Property.SetValue(obj, propertyValue);
            }

            return obj;
        }
    }
}
