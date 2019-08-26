using System.Collections.Generic;
using System.Reflection;

namespace RMG.Core.Generation
{
    public sealed class ObjectPropertyGenerationSettings
    {
        public ObjectPropertyGenerationSettings(
            PropertyInfo property,
            IGenerator generator,
            IReadOnlyList<string> dependsOn
        )
        {
            Property = property;
            Generator = generator;
            DependsOn = dependsOn;
        }

        public PropertyInfo Property { get; }

        public IGenerator Generator { get; }

        public IReadOnlyList<string> DependsOn { get; }
    }
}
