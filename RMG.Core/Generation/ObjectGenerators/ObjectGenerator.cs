using System;
using System.Collections.Generic;
using System.Linq;
using RMG.Core.Music;

namespace RMG.Core.Generation.ObjectGenerators
{
    public class ObjectGenerator<T> : GeneratorBase<T>
    {
        public IList<ObjectPropertyGenerationSettings> PropertyGenerators { get; set; }

        public override T Generate(GenerationContext context)
        {
            // check if property generators are unique
            var distinctPropertiesCount = PropertyGenerators.Select(x => x.Property).Distinct().Count();
            if (PropertyGenerators.Count != distinctPropertiesCount)
            {
                throw new ApplicationException("Multiple generators for same properties found");
            }

            var obj = Activator.CreateInstance<T>();

            if (obj is IDuration durationObj && context.Value is IDuration parentDuration)
            {
                durationObj.Duration = parentDuration.Duration;
            }

            var objectContext = new GenerationContext(context, obj);
            var propertyGenerators = ObjectPropertyGenerationSettingsSorter.Sort(PropertyGenerators);
            foreach (var propertyGenerator in propertyGenerators)
            {
                var propertyValue = propertyGenerator.Generator.Generate(objectContext);
                propertyGenerator.Property.SetValue(obj, propertyValue);
            }

            return obj;
        }
    }
}
