import { Note } from '../music/note';
import { Timeline, TimelineItem, timelineItem } from '../core/timeline';
import { AnyNoteBasePattern, NoteBasePattern, RecursiveNoteBasePattern } from '../composition/note-base-pattern';
import { Patternize, RecursivePattern } from '../composition/pattern';
import { flattenPattern } from './flatten-pattern';
import { emptyNoteBaseTimeline, NoteBaseTimeline } from '../music/note-base';
import { mergeNoteBaseTimelines } from './note-merge';
import { mapObject } from '../core/object-operations';
import { sortTimeline, unionTimelines } from '../core/timeline-operations';
import { createTypeGuard, TypeGuard } from '../core/type-check';

export type TimelineMerger<T> = (source: Timeline<T>, target: Timeline<T>, position: number, duration: number) => Timeline<T>;

export function flattenNoteBasePattern<T>(pattern: AnyNoteBasePattern<T>, duration: number, merger: TimelineMerger<T>): NoteBasePattern<T> {
  if (isMaybeNoteBasePattern(pattern)) {
    const patternDuration = Math.min(pattern.duration, duration);
    const flattenedTimeline = flattenNoteBasePattern<T>(pattern.timeline, patternDuration, merger);
    let resultNoteBaseTimeline = flattenedTimeline.noteBaseTimeline;
    if (isNoteBasePattern(pattern)) {
      const flattenedNoteBaseTimeline = flattenNoteBasePatternInner(pattern.noteBaseTimeline, patternDuration);
      resultNoteBaseTimeline = mergeNoteBaseTimelines(resultNoteBaseTimeline, flattenedNoteBaseTimeline, 0, duration);
    }
    return {
      timeline: flattenedTimeline.timeline,
      noteBaseTimeline: resultNoteBaseTimeline,
      duration: duration,
    };
  } else if (isTimeline(pattern)) {
    let resultTimeline: Timeline<T> = [];
    let resultNoteBaseTimeline = emptyNoteBaseTimeline();
    for (let i = 0; i < pattern.length; i++) {
      const timelineItem = pattern[i];
      if (timelineItem.position >= duration) {
        break;
      }

      const nextTimelineItem = pattern[i + 1];
      const timelineItemDuration = Math.min(duration, nextTimelineItem?.position ?? duration) - timelineItem.position;
      const flattenedTimelineItem = flattenNoteBasePattern<T>(timelineItem.value, timelineItemDuration, merger);
      resultTimeline = merger(flattenedTimelineItem.timeline, resultTimeline, timelineItem.position, timelineItemDuration);
      resultNoteBaseTimeline = mergeNoteBaseTimelines(flattenedTimelineItem.noteBaseTimeline, resultNoteBaseTimeline, timelineItem.position, timelineItemDuration);
    }
    return {
      timeline: sortTimeline(resultTimeline),
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

function flattenNoteBasePatternInner(pattern: AnyNoteBasePattern<Patternize<Note>>, duration: number): NoteBaseTimeline {
  if (isMaybeNoteBasePattern(pattern)) {
    const patternDuration = Math.min(pattern.duration, duration);
    let resultTimeline = flattenNoteBasePatternInner(pattern.timeline, patternDuration);
    if (isNoteBasePattern(pattern)) {
      const flattenedNoteBaseTimeline = flattenNoteBasePatternInner(pattern.noteBaseTimeline, patternDuration);
      resultTimeline = mergeNoteBaseTimelines(resultTimeline, flattenedNoteBaseTimeline, 0, duration);
    }
    return resultTimeline;
  } else if (isTimeline(pattern)) {
    let resultTimeline = emptyNoteBaseTimeline();
    for (let i = 0; i < pattern.length; i++) {
      const timelineItem = pattern[i];
      if (timelineItem.position >= duration) {
        break;
      }

      const nextTimelineItem = pattern[i + 1];
      const timelineItemDuration = Math.min(duration, nextTimelineItem?.position ?? duration) - timelineItem.position;
      const flattenedTimelineItem = flattenNoteBasePatternInner(timelineItem.value, timelineItemDuration);
      resultTimeline = mergeNoteBaseTimelines(flattenedTimelineItem, resultTimeline, timelineItem.position, timelineItemDuration);
    }
    return resultTimeline;
  } else {
    return mergeNoteBaseTimelines(flattenPatternizedNoteBase(pattern, duration), emptyNoteBaseTimeline(), 0, duration);
  }
}

const isNoteBasePattern: TypeGuard<RecursiveNoteBasePattern<any>>
  = createTypeGuard<RecursiveNoteBasePattern<any>>('noteBaseTimeline');

const isMaybeNoteBasePattern: TypeGuard<RecursiveNoteBasePattern<any> | RecursivePattern<any>>
  = createTypeGuard<RecursiveNoteBasePattern<any> | RecursivePattern<any>>('duration', 'timeline');

function flattenPatternizedNoteBase(pattern: Patternize<Note>, duration: number): NoteBaseTimeline {
  return mapObject(pattern, (_, value) => flattenPattern(value, duration, unionTimelines));
}

const isTimeline: TypeGuard<Timeline<any>> = function(obj: any): obj is Timeline<any> {
  return Array.isArray(obj) && obj.every(x => isTimelineItem(x));
}

const isTimelineItem: TypeGuard<TimelineItem<any>> = createTypeGuard<TimelineItem<any>>('position', 'value');
