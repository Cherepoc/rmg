using System.Collections.Generic;
using System.Reflection;

namespace RMG.Core.Generation.ObjectGenerators
{
    public sealed class ObjectPropertyGenerationSettings
    {
        public ObjectPropertyGenerationSettings(
            PropertyInfo property,
            IGenerator generator,
            IReadOnlyList<PropertyInfo> dependsOn
        )
        {
            Property = property;
            Generator = generator;
            DependsOn = dependsOn;
        }

        public PropertyInfo Property { get; }

        public IGenerator Generator { get; }

        public IReadOnlyList<PropertyInfo> DependsOn { get; }
    }
}
