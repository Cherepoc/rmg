using System;
using System.Collections.Generic;
using System.Linq;
using RMG.Core.Music;

namespace RMG.Core.Render
{
    internal static class TimelineFlattener
    {
        public static FlattenedSongTimeline FlattenSongTimeline(Song song)
        {
            var songContext = new FlattenedSongContext(song);

            RenderDurationItem(
                songContext,
                song.Parts,
                song.NoteBasePattern,
                0,
                song.Duration,
                FlattenPartTimeline);

            return new FlattenedSongTimeline(
                song,
                songContext.TrackContexts.Values
                    .Select(
                        trackContext => new FlattenedTrackTimeline(
                            trackContext.Track,
                            trackContext.NoteTimeline,
                            trackContext.NoteBasePattern))
                    .ToList());
        }

        private static void FlattenPartTimeline(
            FlattenedSongContext songContext,
            Part part,
            NoteBasePattern noteBasePattern,
            double position,
            double duration
        )
        {
            var partNoteBasePattern = NoteBaseTimelineOperations.Merge(
                noteBasePattern,
                part.NoteBasePattern,
                position,
                duration);
            
            // parts can have other parts, but if there are track patterns - render them instead
            if (part.TrackPatterns != null && part.TrackPatterns.Count > 0)
            {
                foreach (var (track, patternTimeline) in part.TrackPatterns)
                {
                    var trackContext = songContext.GetTrackContext(track);

                    RenderDurationItem(
                        trackContext,
                        patternTimeline,
                        partNoteBasePattern,
                        position,
                        duration,
                        FlattenPatternTimeline);
                }
            }
            else if (part.Parts != null && part.Parts.Count > 0)
            {
                RenderDurationItem(
                    songContext,
                    part.Parts,
                    partNoteBasePattern,
                    position,
                    duration,
                    FlattenPartTimeline);
            }
        }

        private static void RenderDurationItem<TEvent, TContext>(
            TContext context,
            IList<TimedEvent<TEvent>> timeline,
            NoteBasePattern noteBasePattern,
            double position,
            double duration,
            Action<TContext, TEvent, NoteBasePattern, double, double> renderFunc
        )
            where TEvent : IDuration
        {
            var itemTimeline = timeline
                .OrderBy(x => x.Position)
                .ToArray();
            for (var itemIndex = 0; itemIndex < itemTimeline.Length; itemIndex++)
            {
                var timedItem = itemTimeline[itemIndex];
                if (timedItem.Position >= duration)
                {
                    return;
                }

                var nextItemPosition = itemIndex < itemTimeline.Length - 1
                    ? itemTimeline[itemIndex + 1].Position
                    : double.MaxValue;

                var maxDuration = position - timedItem.Position + Math.Min(duration, nextItemPosition);
                var itemDuration = Math.Min(maxDuration, timedItem.Event.Duration);
                if (itemDuration <= 0)
                {
                    continue;
                }

                renderFunc(context, timedItem.Event, noteBasePattern, position + timedItem.Position, itemDuration);
            }
        }

        private static void FlattenPatternTimeline(
            FlattenedTrackContext trackContext,
            Pattern pattern,
            NoteBasePattern noteBasePattern,
            double position,
            double duration
        )
        {
            var patternNoteBasePattern = NoteBaseTimelineOperations.Merge(
                noteBasePattern,
                pattern.NoteBasePattern,
                position,
                duration);
            
            trackContext.NoteBasePattern = NoteBaseTimelineOperations.Merge(
                trackContext.NoteBasePattern,
                NoteBaseTimelineOperations.SubTimeline(patternNoteBasePattern, position, duration),
                position,
                duration);
            
            foreach (var timedEvent in pattern.Notes.OrderBy(x => x.Position))
            {
                if (timedEvent.Position >= duration)
                {
                    return;
                }

                var notePosition = position + timedEvent.Position;

                trackContext.NoteTimeline.Add(
                    new TimedEvent<Note>
                    {
                        Position = notePosition,
                        Event = timedEvent.Event
                    });
            }
        }

        private sealed class FlattenedSongContext
        {
            public FlattenedSongContext(Song song)
            {
                Song = song;
            }

            public Song Song { get; }

            public Dictionary<Track, FlattenedTrackContext> TrackContexts { get; } =
                new Dictionary<Track, FlattenedTrackContext>();

            public NoteBasePattern NoteBasePattern { get; } = new NoteBasePattern();

            public FlattenedTrackContext GetTrackContext(Track track)
            {
                if (!TrackContexts.TryGetValue(track, out var trackContext))
                {
                    trackContext = new FlattenedTrackContext(this, track);
                    TrackContexts.Add(track, trackContext);
                }

                return trackContext;
            }
        }

        private sealed class FlattenedTrackContext
        {
            public FlattenedTrackContext(FlattenedSongContext songContext, Track track)
            {
                Track = track;
                NoteBasePattern.Duration = songContext.Song.Duration;
            }

            public Track Track { get; }

            public List<TimedEvent<Note>> NoteTimeline { get; } =
                new List<TimedEvent<Note>>();

            public NoteBasePattern NoteBasePattern { get; set; } = new NoteBasePattern();
        }
    }
}
