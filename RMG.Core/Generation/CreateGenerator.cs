using RMG.Core.Generation.ObjectGenerators;

namespace RMG.Core.Generation
{
    public static class CreateGenerator
    {
        public static ObjectGenerator<T> Object<T>()
        {
            return new ObjectGenerator<T>();
        }
    }
}
