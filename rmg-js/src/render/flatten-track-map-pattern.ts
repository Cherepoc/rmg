import {
  AnyMergeFunction,
  flattenNoteBasePattern,
  flattenNoteBasePatternInnerTimeline,
} from './flatten-note-base-pattern';
import { AnyTrackMapPattern, PatternizedTrackMap, TrackMap, TrackMapPattern } from '../composition/track-map-pattern';
import { createTypeGuard, TypeGuard } from '../core/type-check';
import { RecursiveNoteBasePattern } from '../composition/note-base-pattern';
import { Timeline } from '../core/timeline';
import { mergeNoteBaseTimelines } from './note-merge';
import { emptyNoteBaseTimeline } from '../music/note-base';
import { RecursivePattern } from '../composition/pattern';
import { mapObject } from '../core/object-operations';

export function flattenTrackMapPattern<T>(pattern: AnyTrackMapPattern<T>, duration: number, merger: AnyMergeFunction<T>): TrackMapPattern<T> {
  if (isMaybeNoteBasePattern(pattern)) {
    const patternDuration = Math.min(pattern.duration, duration);
    const flattenedTrackMap = flattenTrackMapPatternTimeline<T>(pattern.timeline, patternDuration, merger);
    let resultNoteBaseTimeline = flattenedTrackMap.noteBaseTimeline;
    if (isNoteBasePattern(pattern)) {
      const flattenedNoteBaseTimeline = flattenNoteBasePatternInnerTimeline(pattern.noteBaseTimeline, patternDuration);
      resultNoteBaseTimeline = mergeNoteBaseTimelines(resultNoteBaseTimeline, flattenedNoteBaseTimeline, 0, duration);
    }
    return {
      trackMap: flattenedTrackMap.trackMap,
      noteBaseTimeline: resultNoteBaseTimeline,
      duration: duration,
    };
  } else {
    const trackMap = flattenPatternizedTrackMap(pattern, duration, merger);
    return {
      trackMap: trackMap,
      noteBaseTimeline: emptyNoteBaseTimeline(),
      duration: duration,
    };
  }
}

function flattenTrackMapPatternTimeline<T>(timeline: Timeline<AnyTrackMapPattern<T>>, duration: number, merger: AnyMergeFunction<T>): TrackMapPattern<T> {
  let result: TrackMapPattern<T> = {
    trackMap: {},
    noteBaseTimeline: emptyNoteBaseTimeline(),
    duration: duration,
  };
  for (let i = 0; i < timeline.length; i++) {
    const timelineItem = timeline[i];
    if (timelineItem.position >= duration) {
      break;
    }

    const nextTimelineItem = timeline[i + 1];
    const timelineItemDuration = Math.min(duration, nextTimelineItem?.position ?? duration) - timelineItem.position;
    const flattenedTimelineItem = flattenTrackMapPattern<T>(timelineItem.value, timelineItemDuration, merger);
    result = {
      trackMap: mergeTrackMaps(flattenedTimelineItem.trackMap, result.trackMap, timelineItem.position, timelineItemDuration, merger),
      noteBaseTimeline: mergeNoteBaseTimelines(flattenedTimelineItem.noteBaseTimeline, result.noteBaseTimeline, timelineItem.position, timelineItemDuration),
      duration: result.duration,
    };
  }
  return result;
}

const isNoteBasePattern: TypeGuard<RecursiveNoteBasePattern<any>>
  = createTypeGuard<RecursiveNoteBasePattern<any>>('noteBaseTimeline');

const isMaybeNoteBasePattern: TypeGuard<RecursiveNoteBasePattern<any> | RecursivePattern<any>>
  = createTypeGuard<RecursiveNoteBasePattern<any> | RecursivePattern<any>>('timeline');

function flattenPatternizedTrackMap<T>(pattern: PatternizedTrackMap<T>, duration: number, merger: AnyMergeFunction<T>): TrackMap<T> {
  return mapObject(pattern, (_, value) => flattenNoteBasePattern<T>(value, duration, merger));
}

function mergeTrackMaps<T>(source: TrackMap<T>, target: TrackMap<T>, position: number, duration: number, merger: AnyMergeFunction<T>): TrackMap<T> {
  const trackNumbers = new Set<string>([
    ...Object.keys(source),
    ...Object.keys(target),
  ]);
  const result: TrackMap<T> = {};
  for (let trackNumberKey of trackNumbers) {
    const trackNumber = <number><unknown>trackNumberKey;
    const sourceTrack = source[trackNumber];
    const targetTrack = target[trackNumber];
    const trackDuration = Math.max(sourceTrack?.duration ?? 0, position + duration);
    result[trackNumber] = {
      timeline: merger(sourceTrack?.timeline ?? [], targetTrack?.timeline ?? [], position, duration),
      noteBaseTimeline: mergeNoteBaseTimelines(
        sourceTrack?.noteBaseTimeline ?? emptyNoteBaseTimeline(),
        targetTrack?.noteBaseTimeline ?? emptyNoteBaseTimeline(),
        position,
        duration
      ),
      duration: trackDuration,
    };
  }
  return result;
}
