using System.Collections.Generic;

namespace RMG.Core.Music
{
    public sealed class Song
    {
        public Scale Scale { get; set; }

        public IReadOnlyList<Track> Tracks { get; set; }

        public Part Part { get; set; }

        public double Tempo { get; set; }
    }
}
