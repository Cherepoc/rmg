import { TrackNoteBasePatternMap } from '../composition/track-note-base-pattern-map';
import { TimelineCombineFunction } from '../core/timeline-combine';
import { TrackNoteBaseTimelineMap } from '../music/track-note-base-timeline-map';
import { mapObject } from '../core/object-operations';
import { Timeline } from '../core/timeline';
import { NoteBasePattern } from '../composition/note-base-pattern';
import { combineNoteBaseTimelines, flattenNoteBasePatternTimeline } from './flatten-note-base-pattern';
import { flattenPatternMapPattern, flattenPatternMapTimeline } from './flatten-pattern-map';
import { noteBaseCombineFunctionMap } from './note-merge';
import { emptyNoteBaseTimeline, NoteBaseTimeline } from '../music/note-base';

export function flattenTrackNoteBasePatternMap<T> (pattern: TrackNoteBasePatternMap<T>, duration: number, combine: TimelineCombineFunction<T>): TrackNoteBaseTimelineMap<T> {
  const patternDuration = Math.min(pattern.duration, duration);
  let resultTrackNoteBasePatternMap: TrackNoteBaseTimelineMap<T> = {};

  if (pattern.trackPatternTimelineMap) {
    const flattenedTrackMap = mapObject(
      pattern.trackPatternTimelineMap,
      (_, trackNoteBasePatternTimeline: Timeline<NoteBasePattern<T>>) =>
        flattenNoteBasePatternTimeline(trackNoteBasePatternTimeline, patternDuration, combine)
    );
    resultTrackNoteBasePatternMap = combineTrackNoteBaseTimelineMaps(flattenedTrackMap, resultTrackNoteBasePatternMap, 0, patternDuration, combine);
  }

  if (pattern.innerPatternTimeline) {
    const flattenedInnerTimeline = flattenTrackNoteBasePatternMapTimeline(pattern.innerPatternTimeline, patternDuration, combine);
    resultTrackNoteBasePatternMap = combineTrackNoteBaseTimelineMaps(flattenedInnerTimeline, resultTrackNoteBasePatternMap, 0, patternDuration, combine);
  }

  if (pattern.noteBasePatternTimeline) {
    const flattenedNoteBaseTimeline = flattenPatternMapTimeline(pattern.noteBasePatternTimeline, patternDuration, flattenPatternMapPattern, noteBaseCombineFunctionMap);
    resultTrackNoteBasePatternMap = mapObject(
      resultTrackNoteBasePatternMap,
      (_, noteBasePattern: NoteBaseTimeline<T>) => combineNoteBaseTimelines(
        {
          timeline: [],
          noteBaseTimeline: flattenedNoteBaseTimeline
        },
        noteBasePattern,
        0,
        patternDuration,
        combine
      )
    );
  }

  return resultTrackNoteBasePatternMap;
}

export function flattenTrackNoteBasePatternMapTimeline<T> (timeline: Timeline<TrackNoteBasePatternMap<T>>, duration: number, combine: TimelineCombineFunction<T>): TrackNoteBaseTimelineMap<T> {
  let resultTrackNoteBasePatternMap: TrackNoteBaseTimelineMap<T> = {};

  for (let i = 0; i < timeline.length; i++) {
    const timelineItem = timeline[i];
    if (timelineItem.position >= duration) {
      break;
    }

    const nextTimelineItem = timeline[i + 1];
    const timelineItemDuration = Math.min(duration, nextTimelineItem?.position ?? duration) - timelineItem.position;
    const flattenedItem = flattenTrackNoteBasePatternMap(timelineItem.value, timelineItemDuration, combine);
    resultTrackNoteBasePatternMap = combineTrackNoteBaseTimelineMaps(flattenedItem, resultTrackNoteBasePatternMap, timelineItem.position, timelineItemDuration, combine);
  }

  return resultTrackNoteBasePatternMap;
}

export function combineTrackNoteBaseTimelineMaps<T> (source: TrackNoteBaseTimelineMap<T>, target: TrackNoteBaseTimelineMap<T>, position: number, duration: number, combine: TimelineCombineFunction<T>): TrackNoteBaseTimelineMap<T> {
  const trackNumbers = new Set<any>([
    ...Object.keys(source),
    ...Object.keys(target)
  ]);
  const result: TrackNoteBaseTimelineMap<T> = {};
  for (const trackNumberKey of trackNumbers) {
    const trackNumber = <number>trackNumberKey;
    const sourceTrack = source[trackNumber];
    const targetTrack = target[trackNumber];
    result[trackNumber] = combineNoteBaseTimelines(sourceTrack ?? emptyNoteBaseTimeline(), targetTrack ?? emptyNoteBaseTimeline(), position, duration, combine);
  }
  return result;
}
