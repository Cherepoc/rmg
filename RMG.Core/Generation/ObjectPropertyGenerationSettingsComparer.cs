using System;
using System.Collections.Generic;
using System.Linq;
using RMG.Core.Music;

namespace RMG.Core.Generation
{
    internal sealed class ObjectPropertyGenerationSettingsComparer : IComparer<ObjectPropertyGenerationSettings>
    {
        private const string DurationPropertyName = nameof(IDuration.Duration);

        private static readonly IReadOnlyList<Type> DurationDependants = new[]
        {
            typeof(IEnumerable<ITimedEvent>),
            typeof(IDuration)
        };

        public static ObjectPropertyGenerationSettingsComparer Instance { get; } =
            new ObjectPropertyGenerationSettingsComparer();

        public int Compare(ObjectPropertyGenerationSettings x, ObjectPropertyGenerationSettings y)
        {
            var xDependsOnY = x.DependsOn.Contains(y.Property.Name);
            var yDependsOnX = y.DependsOn.Contains(x.Property.Name);
            if (xDependsOnY || yDependsOnX)
            {
                return Compare(xDependsOnY, yDependsOnX);
            }

            var declaringTypeHasDuration = typeof(IDuration).IsAssignableFrom(x.Property.DeclaringType);

            // can be timed events which should depend on IDuration.Duration
            if (declaringTypeHasDuration)
            {
                var xDependsOnYDuration = CheckDependsOnDuration(x, y);
                var yDependsOnXDuration = CheckDependsOnDuration(y, x);
                if (xDependsOnYDuration || yDependsOnXDuration)
                {
                    return Compare(xDependsOnYDuration, yDependsOnXDuration);
                }
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

        private static bool CheckDependsOnDuration(ObjectPropertyGenerationSettings x, ObjectPropertyGenerationSettings y)
        {
            return DurationDependants.Any(type => type.IsAssignableFrom(x.Property.PropertyType))
                   && y.Property.Name == DurationPropertyName;
        }
    }
}
