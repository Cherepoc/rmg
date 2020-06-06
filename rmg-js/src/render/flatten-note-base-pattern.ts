import { Timeline } from '../core/timeline';
import { NoteBasePattern } from '../composition/note-base-pattern';
import { flattenPatternTimeline } from './flatten-pattern';
import { emptyNoteBaseTimelineMap, NoteBaseTimeline } from '../music/note-base';
import { noteBaseCombineFunctionMap } from './note-merge';
import { combineTimelineMaps, TimelineCombineFunction } from '../core/timeline-combine';
import { flattenPatternMapPattern, flattenPatternMapTimeline } from './flatten-pattern-map';

export function flattenNoteBasePattern<T> (pattern: NoteBasePattern<T>, duration: number, combine: TimelineCombineFunction<T>): NoteBaseTimeline<T> {
  const patternDuration = Math.min(pattern.duration, duration);
  let resultNoteBaseTimeline: NoteBaseTimeline<T> = {
    timeline: [],
    noteBaseTimeline: emptyNoteBaseTimelineMap()
  };

  if (pattern.patternTimeline) {
    const flattenedTimeline = flattenPatternTimeline(pattern.patternTimeline, patternDuration, combine);
    resultNoteBaseTimeline = {
      timeline: combine(resultNoteBaseTimeline.timeline, flattenedTimeline, 0, patternDuration),
      noteBaseTimeline: resultNoteBaseTimeline.noteBaseTimeline
    };
  }

  if (pattern.noteBasePatternTimeline) {
    const flattenedTimeline = flattenPatternMapTimeline(pattern.noteBasePatternTimeline, patternDuration, flattenPatternMapPattern, noteBaseCombineFunctionMap);
    resultNoteBaseTimeline = {
      timeline: resultNoteBaseTimeline.timeline,
      noteBaseTimeline: combineTimelineMaps(resultNoteBaseTimeline.noteBaseTimeline, flattenedTimeline, 0, patternDuration, noteBaseCombineFunctionMap)
    };
  }

  if (pattern.innerPatternTimeline) {
    const flattenedTimeline = flattenNoteBasePatternTimeline(pattern.innerPatternTimeline, patternDuration, combine);
    resultNoteBaseTimeline = combineNoteBaseTimelines(flattenedTimeline, resultNoteBaseTimeline, 0, patternDuration, combine);
  }

  return resultNoteBaseTimeline;
}

export function flattenNoteBasePatternTimeline<T> (timeline: Timeline<NoteBasePattern<T>>, duration: number, combine: TimelineCombineFunction<T>): NoteBaseTimeline<T> {
  let resultNoteBaseTimeline: NoteBaseTimeline<T> = {
    timeline: [],
    noteBaseTimeline: emptyNoteBaseTimelineMap()
  };

  for (let i = 0; i < timeline.length; i++) {
    const timelineItem = timeline[i];
    if (timelineItem.position >= duration) {
      break;
    }

    const nextTimelineItem = timeline[i + 1];
    const timelineItemDuration = Math.min(duration, nextTimelineItem?.position ?? duration) - timelineItem.position;
    const flattenedItem = flattenNoteBasePattern(timelineItem.value, timelineItemDuration, combine);
    resultNoteBaseTimeline = combineNoteBaseTimelines(flattenedItem, resultNoteBaseTimeline, timelineItem.position, timelineItemDuration, combine);
  }

  return resultNoteBaseTimeline;
}

export function combineNoteBaseTimelines<T> (source: NoteBaseTimeline<T>, target: NoteBaseTimeline<T>, position: number, duration: number, combine: TimelineCombineFunction<T>): NoteBaseTimeline<T> {
  return {
    timeline: combine(source.timeline, target.timeline, position, duration),
    noteBaseTimeline: combineTimelineMaps(
      source.noteBaseTimeline,
      target.noteBaseTimeline,
      position,
      duration,
      noteBaseCombineFunctionMap
    )
  };
}
