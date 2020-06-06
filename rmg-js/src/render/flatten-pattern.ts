import { Timeline } from '../core/timeline';
import { sortTimeline } from '../core/timeline-operations';
import { Pattern } from '../composition/pattern';
import { TimelineCombineFunction } from '../core/timeline-combine';

export function flattenPattern<T> (pattern: Pattern<T>, duration: number, combine: TimelineCombineFunction<T>): Timeline<T> {
  let resultTimeline: Timeline<T> = [];
  const patternDuration = Math.min(pattern.duration, duration);

  if (pattern.timeline) {
    resultTimeline = combine(pattern.timeline, resultTimeline, 0, patternDuration);
  }

  if (pattern.innerPatternTimeline) {
    const flattenedTimeline = flattenPatternTimeline(pattern.innerPatternTimeline, patternDuration, combine);
    resultTimeline = combine(flattenedTimeline, resultTimeline, 0, patternDuration);
  }

  return sortTimeline(resultTimeline);
}

export function flattenPatternTimeline<T> (timeline: Timeline<Pattern<T>>, duration: number, combine: TimelineCombineFunction<T>): Timeline<T> {
  let resultTimeline: Timeline<T> = [];
  for (let i = 0; i < timeline.length; i++) {
    const timelineItem = timeline[i];
    if (timelineItem.position >= duration) {
      break;
    }

    const nextTimelineItem = timeline[i + 1];
    const timelineItemDuration = Math.min(duration, nextTimelineItem?.position ?? duration) - timelineItem.position;
    const flattenedTimelineItem = flattenPattern<T>(timelineItem.value, timelineItemDuration, combine);
    resultTimeline = combine(flattenedTimelineItem, resultTimeline, timelineItem.position, timelineItemDuration);
  }
  return sortTimeline(resultTimeline);
}
