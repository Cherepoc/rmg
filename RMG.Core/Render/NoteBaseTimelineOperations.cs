using System;
using RMG.Core.Music;

namespace RMG.Core.Render
{
    public static class NoteBaseTimelineOperations
    {
        public static NoteBasePattern Merge(
            NoteBasePattern source,
            NoteBasePattern target,
            double position,
            double duration
        )
        {
            var endPosition = position + duration;
            return new NoteBasePattern
            {
                Duration = Math.Max(source.Duration, endPosition),
                KeyTimeline = EventTimelineOperations.Merge(
                    source.KeyTimeline,
                    target.KeyTimeline,
                    position,
                    duration,
                    MergeAdditive,
                    0),
                OctaveTimeline = EventTimelineOperations.Merge(
                    source.OctaveTimeline,
                    target.OctaveTimeline,
                    position,
                    duration,
                    MergeAdditive,
                    0),
                VolumeTimeline = EventTimelineOperations.Merge(
                    source.VolumeTimeline,
                    target.VolumeTimeline,
                    position,
                    duration,
                    MergeMultiplicative,
                    1),
                ScaleOffsetTimeline = EventTimelineOperations.Merge(
                    source.ScaleOffsetTimeline,
                    target.ScaleOffsetTimeline,
                    position,
                    duration,
                    MergeScaleNoteOffset,
                    new int[Scale.ScaleRankCount])
            };
        }
        
        public static NoteBasePattern Merge(
            NoteBasePattern source,
            NoteBasePattern target
        )
        {
            return Merge(source, target, 0, target.Duration);
        }

        public static NoteBasePattern SubTimeline(
            NoteBasePattern timeline,
            double position,
            double duration
        )
        {
            var startPosition = Math.Max(0, position);
            var endPosition = Math.Min(timeline.Duration, position + duration);
            return new NoteBasePattern
            {
                Duration = endPosition - startPosition,
                KeyTimeline = EventTimelineOperations.SubTimeline(timeline.KeyTimeline, position, duration),
                OctaveTimeline = EventTimelineOperations.SubTimeline(timeline.OctaveTimeline, position, duration),
                VolumeTimeline = EventTimelineOperations.SubTimeline(timeline.VolumeTimeline, position, duration),
                ScaleOffsetTimeline = EventTimelineOperations.SubTimeline(timeline.ScaleOffsetTimeline, position, duration)
            };
        }

        private static int MergeAdditive(int a, int b)
        {
            return a + b;
        }

        private static double MergeMultiplicative(double a, double b)
        {
            return a * b;
        }

        private static int[] MergeScaleNoteOffset(int[] a, int[] b)
        {
            var result = new int[Math.Max(a.Length, b.Length)];
            for (int i = 0; i < result.Length; i++)
            {
                var aValue = i < a.Length ? a[i] : 0;
                var bValue = i < b.Length ? b[i] : 0;
                result[i] = aValue + bValue;
            }

            return result;
        }
    }
}
