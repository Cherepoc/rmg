using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using RMG.Core.Music;
using RMG.Core.Utils;

namespace RMG.Core.Generation
{
    public class ObjectGenerator<T> : IGenerator
    {
        public IDictionary<string, ObjectPropertyGenerationSettings> PropertyGenerators { get; set; } =
            new Dictionary<string, ObjectPropertyGenerationSettings>();

        object IGenerator.Generate(GenerationContext context)
        {
            return Generate(context);
        }

        public ObjectGenerator<T> WithPropertyGenerator(
            Expression<Func<T, object>> propertyExpression,
            IGenerator generator,
            params Expression<Func<T, object>>[] dependsOn
        )
        {
            var propertyName = propertyExpression.GetPropertyName();
            PropertyGenerators[propertyName] = new ObjectPropertyGenerationSettings
            {
                PropertyName = propertyName,
                Generator = generator,
                DependsOn = dependsOn.Select(x => x.GetPropertyName()).ToList()
            };
            return this;
        }

        public T Generate(GenerationContext context)
        {
            var obj = Activator.CreateInstance<T>();
            var objectContext = new GenerationContext(context, obj);
            foreach (var propertyGenerator in GetPropertyGenerators())
            {
                var propertyValue = propertyGenerator.Generator.Generate(objectContext);
                propertyGenerator.Property.SetValue(obj, propertyValue);
            }

            return obj;
        }

        private IReadOnlyList<PropertyGenerator> GetPropertyGenerators()
        {
            var type = typeof(T);
            var propertyGenerators = PropertyGenerators.Values
                .Select(
                    x => new PropertyGenerator(
                        type.GetProperty(x.PropertyName),
                        x.Generator,
                        x.DependsOn.Select(d => type.GetProperty(d)).ToList())
                )
                .OrderBy(x => x, PropertyGeneratorComparer.Instance)
                .ToList();
            return propertyGenerators;
        }

        private sealed class PropertyGenerator
        {
            public PropertyGenerator(PropertyInfo property, IGenerator generator, IReadOnlyList<PropertyInfo> dependsOn)
            {
                Property = property;
                Generator = generator;
                DependsOn = dependsOn;
            }

            public PropertyInfo Property { get; }

            public IGenerator Generator { get; }

            public IReadOnlyList<PropertyInfo> DependsOn { get; }
        }

        private sealed class PropertyGeneratorComparer : IComparer<PropertyGenerator>
        {
            public static PropertyGeneratorComparer Instance { get; } =
                new PropertyGeneratorComparer();

            private const string DurationPropertyName = nameof(IDuration.Duration);

            public int Compare(PropertyGenerator x, PropertyGenerator y)
            {
                if (x.DependsOn.Contains(y.Property))
                    return 1;
                if (y.DependsOn.Contains(x.Property))
                    return -1;
                
                // can be timed events which should depend on IDuration.Duration
                if (CheckTimelineDependsOnDuration(x, y))
                    return 1;
                if (CheckTimelineDependsOnDuration(y, x))
                    return -1;
                
                return 0;
            }

            private static bool CheckTimelineDependsOnDuration(PropertyGenerator x, PropertyGenerator y)
            {
                var declaringType = x.Property.DeclaringType;
                return typeof(IEnumerable<ITimedEvent>).IsAssignableFrom(x.Property.PropertyType)
                       && typeof(IDuration).IsAssignableFrom(declaringType)
                       && y.Property.Name == DurationPropertyName;
            }
        }
    }
}
