using System.Collections.Generic;
using RMG.Core.Music;

namespace RMG.Core.Render
{
    internal sealed class FlattenedSongTimeline
    {
        public FlattenedSongTimeline(
            Song song,
            IReadOnlyList<FlattenedTrackTimeline> trackTimelines
        )
        {
            Song = song;
            TrackTimelines = trackTimelines;
        }

        public Song Song { get; }

        public IReadOnlyList<FlattenedTrackTimeline> TrackTimelines { get; }
    }
}
