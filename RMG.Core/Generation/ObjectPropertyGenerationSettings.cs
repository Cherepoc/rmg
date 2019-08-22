using System.Collections.Generic;

namespace RMG.Core.Generation
{
    public sealed class ObjectPropertyGenerationSettings
    {
        public string PropertyName { get; set; }
        
        public IGenerator Generator { get; set; }
        
        public IList<string> DependsOn { get; set; }
    }
}
