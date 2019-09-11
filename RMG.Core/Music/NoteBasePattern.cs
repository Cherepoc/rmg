using System.Collections.Generic;

namespace RMG.Core.Music
{
    public sealed class NoteBasePattern : IDuration
    {
        public IReadOnlyList<TimedEvent<int>> KeyTimeline { get; set; } = new List<TimedEvent<int>>();

        public IReadOnlyList<TimedEvent<int[]>> ScaleOffsetTimeline { get; set; } = new List<TimedEvent<int[]>>();

        public IReadOnlyList<TimedEvent<int>> OctaveTimeline { get; set; } = new List<TimedEvent<int>>();

        public IReadOnlyList<TimedEvent<double>> VolumeTimeline { get; set; } = new List<TimedEvent<double>>();

        public double Duration { get; set; }
    }
}
