using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using RMG.Core.Generation.ObjectGenerators;

namespace RMG.Core.Generation.Building
{
    public sealed class ObjectGeneratorBuilder<TEntity> : IGeneratorTransformationBuilder<TEntity>
    {
        private readonly Dictionary<PropertyInfo, IObjectGeneratorPropertyBuilder> _propertyBuilders =
            new Dictionary<PropertyInfo, IObjectGeneratorPropertyBuilder>();

        public IGenerator<TEntity> Build()
        {
            return new ObjectGenerator<TEntity>
            {
                PropertyGenerators = _propertyBuilders.Values
                    .Select(x => x.Build())
                    .ToArray()
            };
        }

        public ObjectGeneratorBuilder<TEntity> ForProperty<TProperty>(
            Expression<Func<TEntity, TProperty>> propertyExpression,
            Func<ObjectGeneratorPropertyBuilder<TEntity, TProperty>, IGeneratorTransformationBuilder<TProperty>> setup
        )
        {
            var propertyBuilder = new ObjectGeneratorPropertyBuilder<TEntity, TProperty>(propertyExpression);
            _propertyBuilders[propertyBuilder.Property] = propertyBuilder;
            setup(propertyBuilder);

            return this;
        }
    }
}
