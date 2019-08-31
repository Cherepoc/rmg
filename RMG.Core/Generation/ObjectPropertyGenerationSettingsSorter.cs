using System;
using System.Collections.Generic;
using System.Linq;
using RMG.Core.Music;

namespace RMG.Core.Generation
{
    public static class ObjectPropertyGenerationSettingsSorter
    {
        private const string DurationPropertyName = nameof(IDuration.Duration);

        private static readonly IReadOnlyList<Type> DurationDependants = new[]
        {
            typeof(IEnumerable<ITimedEvent>),
            typeof(IDuration)
        };

        public static IReadOnlyList<ObjectPropertyGenerationSettings> Sort(
            IEnumerable<ObjectPropertyGenerationSettings> collection
        )
        {
            var propertyGenerators = collection.ToList();
            for (var i = 0; i < propertyGenerators.Count; i++)
            {
                var propertyGenerator = propertyGenerators[i];
                for (var j = i + 1; j < propertyGenerators.Count; j++)
                {
                    var otherPropertyGenerator = propertyGenerators[j];
                    var depends = CheckDepends(propertyGenerator, otherPropertyGenerator);
                    if (depends)
                    {
                        propertyGenerators.RemoveAt(i);
                        propertyGenerators.Insert(j, propertyGenerator);
                        i--;
                        break;
                    }
                }
            }

            return propertyGenerators;
        }

        private static bool CheckDepends(
            ObjectPropertyGenerationSettings dependant,
            ObjectPropertyGenerationSettings dependee
        )
        {
            var dependsOn = dependant.DependsOn.Contains(dependee.Property.Name);
            if (dependsOn)
            {
                return true;
            }

            var declaringTypeHasDuration = typeof(IDuration).IsAssignableFrom(dependant.Property.DeclaringType);

            // can be timed events which should depend on IDuration.Duration
            if (declaringTypeHasDuration)
            {
                var dependsOnDuration =
                    DurationDependants.Any(type => type.IsAssignableFrom(dependant.Property.PropertyType))
                    && dependee.Property.Name == DurationPropertyName;
                if (dependsOnDuration)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
