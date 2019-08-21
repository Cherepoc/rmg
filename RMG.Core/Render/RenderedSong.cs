using System.Collections.Generic;
using RMG.Core.Music;

namespace RMG.Core.Render
{
    public sealed class RenderedSong
    {
        public RenderedSong(double tempo, double duration, IReadOnlyList<RenderedTrack> tracks)
        {
            Tempo = tempo;
            Duration = duration;
            Tracks = tracks;
        }

        public double Tempo { get; }

        public double Duration { get; }

        public IReadOnlyList<RenderedTrack> Tracks { get; }
    }
}
