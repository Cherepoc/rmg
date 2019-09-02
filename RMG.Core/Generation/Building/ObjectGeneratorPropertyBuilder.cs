using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using RMG.Core.Generation.ObjectGenerators;
using RMG.Core.Utils;

namespace RMG.Core.Generation.Building
{
    public sealed class ObjectGeneratorPropertyBuilder<TEntity, TProperty> : IObjectGeneratorPropertyBuilder
    {
        private readonly HashSet<PropertyInfo> _dependsOn = new HashSet<PropertyInfo>();

        private IGeneratorTransformationBuilder<TProperty> _generatorBuilder;

        public ObjectGeneratorPropertyBuilder(Expression<Func<TEntity, TProperty>> propertyExpression)
        {
            var propertyName = propertyExpression.GetPropertyName();
            var property = typeof(TEntity).GetProperty(propertyName);
            Property = property;
        }

        public PropertyInfo Property { get; }

        public ObjectPropertyGenerationSettings Build()
        {
            var generator = _generatorBuilder.Build();
            return new ObjectPropertyGenerationSettings(Property, generator, _dependsOn.ToList());
        }

        public ObjectGeneratorPropertyBuilder<TEntity, TProperty> DependsOn(
            Expression<Func<TEntity, object>> propertyExpression
        )
        {
            var propertyName = propertyExpression.GetPropertyName();
            var property = typeof(TEntity).GetProperty(propertyName);
            _dependsOn.Add(property);

            return this;
        }

        public ObjectGeneratorPropertyBuilder<TEntity, TProperty> From(
            Func<GeneratorBuilder<TProperty>, IGeneratorTransformationBuilder<TProperty>> setup
        )
        {
            var propertyValueBuilder = new GeneratorBuilder<TProperty>();
            _generatorBuilder = setup(propertyValueBuilder);

            return this;
        }
    }
}
