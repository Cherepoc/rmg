using System.Collections.Generic;

namespace RMG.Core.Music
{
    public sealed class Scale
    {
        public const int ScaleRankCount = 3;

        public IReadOnlyList<int> NoteOffsets { get; set; }

        public IReadOnlyList<IReadOnlyList<int>> RankedOffsetIndexes { get; set; }
    }
}
