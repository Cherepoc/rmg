using System.Collections.Generic;

namespace RMG.Core.Music
{
    public sealed class Scale
    {
        public const int ScaleRankCount = 3;
        
        public IList<int> NoteOffsets { get; set; }
        
        public IList<IList<int>> RankedOffsetIndexes { get; set; }
    }
}
