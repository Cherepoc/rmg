using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using RMG.Core.Utils;

namespace RMG.Core.Generation.ObjectGenerators
{
    public sealed class ObjectPropertyGenerationSettingsBuilder<TObject, TProperty>
    {
        private readonly HashSet<PropertyInfo> _dependsOn = new HashSet<PropertyInfo>();
        private IGenerator<TProperty> _generator;

        internal ObjectPropertyGenerationSettingsBuilder()
        {
        }

        public ObjectPropertyGenerationSettingsBuilder<TObject, TProperty> DependsOn(
            params Expression<Func<TObject, object>>[] dependsOn
        )
        {
            foreach (var dependsOnProperty in dependsOn)
            {
                _dependsOn.Add(dependsOnProperty.GetPropertyInfo());
            }

            return this;
        }

        public ObjectPropertyGenerationSettingsBuilder<TObject, TProperty> WithValue(TProperty value)
        {
            _generator = new ConstantGenerator<TProperty>(value);

            return this;
        }

        public ObjectPropertyGenerationSettingsBuilder<TObject, TProperty> WithGenerator(IGenerator<TProperty> generator)
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
