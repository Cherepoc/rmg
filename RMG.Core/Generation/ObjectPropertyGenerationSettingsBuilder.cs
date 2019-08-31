using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using RMG.Core.Utils;

namespace RMG.Core.Generation
{
    public sealed class ObjectPropertyGenerationSettingsBuilder<TObject>
    {
        private readonly HashSet<string> _dependsOn = new HashSet<string>();
        private IGenerator _generator;

        internal ObjectPropertyGenerationSettingsBuilder()
        {
        }

        public ObjectPropertyGenerationSettingsBuilder<TObject> DependsOn(
            params Expression<Func<TObject, object>>[] dependsOn
        )
        {
            foreach (var dependsOnProperty in dependsOn)
            {
                _dependsOn.Add(dependsOnProperty.GetPropertyName());
            }

            return this;
        }

        public ObjectPropertyGenerationSettingsBuilder<TObject> WithValue<TArgument>(TArgument value)
        {
            _generator = new ConstantGenerator<TArgument>(value);

            return this;
        }

        public ObjectPropertyGenerationSettingsBuilder<TObject> WithGenerator(IGenerator generator)
        {
            _generator = generator;

            return this;
        }

        internal ObjectPropertyGenerationSettings Build(PropertyInfo propertyInfo)
        {
            return new ObjectPropertyGenerationSettings(propertyInfo, _generator, _dependsOn.ToArray());
        }
    }
}
