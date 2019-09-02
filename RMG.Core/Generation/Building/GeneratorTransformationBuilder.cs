using System;

namespace RMG.Core.Generation.Building
{
    public class GeneratorTransformationBuilder<T> : IGeneratorTransformationBuilder<T>
    {
        private readonly Func<IGenerator<T>> _generatorFactory;

        public GeneratorTransformationBuilder(Func<IGenerator<T>> generatorFactory)
        {
            _generatorFactory = generatorFactory;
        }

        public IGenerator<T> Build()
        {
            return _generatorFactory();
        }
    }
}
