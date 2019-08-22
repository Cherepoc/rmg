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

            if (obj is IDuration durationObj && context.Value is IDuration parentDuration)
            {
                durationObj.Duration = parentDuration.Duration;
            }
            
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

            private static readonly IReadOnlyList<Type> DurationDependants = new[]
            {
                typeof(IEnumerable<ITimedEvent>),
                typeof(IDuration)
            };

            public int Compare(PropertyGenerator x, PropertyGenerator y)
            {
                var xDependsOnY = x.DependsOn.Contains(y.Property);
                var yDependsOnX = y.DependsOn.Contains(x.Property);
                if (xDependsOnY || yDependsOnX)
                    return Compare(xDependsOnY, yDependsOnX);
                
                var declaringTypeHasDuration = typeof(IDuration).IsAssignableFrom(x.Property.DeclaringType);
                
                // can be timed events which should depend on IDuration.Duration
                if (declaringTypeHasDuration)
                {
                    var xDependsOnYDuration = CheckDependsOnDuration(x, y);
                    var yDependsOnXDuration = CheckDependsOnDuration(y, x);
                    if (xDependsOnYDuration || yDependsOnXDuration)
                        return Compare(xDependsOnYDuration, yDependsOnXDuration);
                }
                
                return 0;
            }

            private static int Compare(bool firstDepends, bool secondDepends)
            {
                return firstDepends == secondDepends
                    ? 0
                    : firstDepends
                        ? 1
                        : -1;
            }

            private static bool CheckDependsOnDuration(PropertyGenerator x, PropertyGenerator y)
            {
                return DurationDependants.Any(type => type.IsAssignableFrom(x.Property.PropertyType))
                       && y.Property.Name == DurationPropertyName;
            }
        }
    }
}
