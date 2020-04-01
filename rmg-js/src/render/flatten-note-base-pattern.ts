import { Note } from '../music/note';
import { Timeline, timelineItem } from '../core/timeline';
import { AnyNoteBasePattern, NoteBasePattern, RecursiveNoteBasePattern } from '../composition/note-base-pattern';
import { Patternize, RecursivePattern } from '../composition/pattern';
import { flattenPattern } from './flatten-pattern';
import { emptyNoteBaseTimeline, NoteBaseTimeline } from '../music/note-base';
import { mergeNoteBaseTimelines } from './note-merge';
import { mapObject } from '../core/object-operations';
import { sortTimeline, unionTimelines } from '../core/timeline-operations';
import { createTypeGuard, TypeGuard } from '../core/type-check';

export type AnyMergeFunction<T> = (source: Timeline<T>, target: Timeline<T>, position: number, duration: number) => Timeline<T>;

export function flattenNoteBasePattern<T>(pattern: AnyNoteBasePattern<T>, duration: number, merger: AnyMergeFunction<T>): NoteBasePattern<T> {
  if (isMaybeNoteBasePattern(pattern)) {
    const patternDuration = Math.min(pattern.duration, duration);
    const flattenedTimeline = flattenNoteBasePatternTimeline<T>(pattern.timeline, patternDuration, merger);
    let resultNoteBaseTimeline = flattenedTimeline.noteBaseTimeline;
    if (isNoteBasePattern(pattern)) {
      const flattenedNoteBaseTimeline = flattenNoteBasePatternInnerTimeline(pattern.noteBaseTimeline, patternDuration);
      resultNoteBaseTimeline = mergeNoteBaseTimelines(resultNoteBaseTimeline, flattenedNoteBaseTimeline, 0, duration);
    }
    return {
      timeline: flattenedTimeline.timeline,
      noteBaseTimeline: resultNoteBaseTimeline,
      duration: duration,
    };
  } else {
    return {
      timeline: merger([timelineItem(0, pattern)], [], 0, duration),
      noteBaseTimeline: emptyNoteBaseTimeline(),
      duration: duration,
    };
  }
}

export function flattenNoteBasePatternTimeline<T>(timeline: Timeline<AnyNoteBasePattern<T>>, duration: number, merger: AnyMergeFunction<T>): NoteBasePattern<T> {
  let result: NoteBasePattern<T> = {
    timeline: [],
    noteBaseTimeline: emptyNoteBaseTimeline(),
    duration: duration,
  }
  for (let i = 0; i < timeline.length; i++) {
    const timelineItem = timeline[i];
    if (timelineItem.position >= duration) {
      break;
    }

    const nextTimelineItem = timeline[i + 1];
    const timelineItemDuration = Math.min(duration, nextTimelineItem?.position ?? duration) - timelineItem.position;
    const flattenedTimelineItem = flattenNoteBasePattern<T>(timelineItem.value, timelineItemDuration, merger);
    result = {
      timeline: merger(flattenedTimelineItem.timeline, result.timeline, timelineItem.position, timelineItemDuration),
      noteBaseTimeline: mergeNoteBaseTimelines(flattenedTimelineItem.noteBaseTimeline, result.noteBaseTimeline, timelineItem.position, timelineItemDuration),
      duration: result.duration
    }
  }
  return {
    timeline: sortTimeline(result.timeline),
    noteBaseTimeline: result.noteBaseTimeline,
    duration: duration,
  };
}

export function flattenNoteBasePatternInner(pattern: AnyNoteBasePattern<Patternize<Note>>, duration: number): NoteBaseTimeline {
  if (isMaybeNoteBasePattern(pattern)) {
    const patternDuration = Math.min(pattern.duration, duration);
    let resultTimeline = flattenNoteBasePatternInnerTimeline(pattern.timeline, patternDuration);
    if (isNoteBasePattern(pattern)) {
      const flattenedNoteBaseTimeline = flattenNoteBasePatternInnerTimeline(pattern.noteBaseTimeline, patternDuration);
      resultTimeline = mergeNoteBaseTimelines(resultTimeline, flattenedNoteBaseTimeline, 0, duration);
    }
    return resultTimeline;
  } else {
    return mergeNoteBaseTimelines(flattenPatternizedNoteBase(pattern, duration), emptyNoteBaseTimeline(), 0, duration);
  }
}

export function flattenNoteBasePatternInnerTimeline(timeline: Timeline<AnyNoteBasePattern<Patternize<Note>>>, duration: number): NoteBaseTimeline {
  let resultTimeline = emptyNoteBaseTimeline();
  for (let i = 0; i < timeline.length; i++) {
    const timelineItem = timeline[i];
    if (timelineItem.position >= duration) {
      break;
    }

    const nextTimelineItem = timeline[i + 1];
    const timelineItemDuration = Math.min(duration, nextTimelineItem?.position ?? duration) - timelineItem.position;
    const flattenedTimelineItem = flattenNoteBasePatternInner(timelineItem.value, timelineItemDuration);
    resultTimeline = mergeNoteBaseTimelines(flattenedTimelineItem, resultTimeline, timelineItem.position, timelineItemDuration);
  }
  return resultTimeline;
}

const isNoteBasePattern: TypeGuard<RecursiveNoteBasePattern<any>>
  = createTypeGuard<RecursiveNoteBasePattern<any>>('noteBaseTimeline');

const isMaybeNoteBasePattern: TypeGuard<RecursiveNoteBasePattern<any> | RecursivePattern<any>>
  = createTypeGuard<RecursiveNoteBasePattern<any> | RecursivePattern<any>>('duration', 'timeline');

function flattenPatternizedNoteBase(pattern: Patternize<Note>, duration: number): NoteBaseTimeline {
  return mapObject(pattern, (_, value) => flattenPattern(value, duration, unionTimelines));
}
