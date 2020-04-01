import { Timeline, timelineItem } from '../core/timeline';
import { AnyPattern, RecursivePattern } from '../composition/pattern';
import { createTypeGuard, TypeGuard } from '../core/type-check';
import { sortTimeline } from '../core/timeline-operations';

export type TimelineMerger<T> = (source: Timeline<T>, target: Timeline<T>, position: number, duration: number) => Timeline<T>;

export function flattenPattern<T>(pattern: AnyPattern<T>, duration: number, merger: TimelineMerger<T>): Timeline<T> {
  if (isPattern(pattern)) {
    const patternDuration = Math.min(pattern.duration, duration);
    return flattenPatternTimeline<T>(pattern.timeline, patternDuration, merger);
  } else {
    const mergeResult = merger([timelineItem(0, pattern)], [], 0, duration);
    return mergeResult;
  }
}

function flattenPatternTimeline<T>(timeline: Timeline<AnyPattern<T>>, duration: number, merger: TimelineMerger<T>): Timeline<T> {
  let resultTimeline: Timeline<T> = [];
  for (let i = 0; i < timeline.length; i++) {
    const timelineItem = timeline[i];
    if (timelineItem.position >= duration) {
      break;
    }

    const nextTimelineItem = timeline[i + 1];
    const timelineItemDuration = Math.min(duration, nextTimelineItem?.position ?? duration) - timelineItem.position;
    const flattenedTimelineItem = flattenPattern<T>(timelineItem.value, timelineItemDuration, merger);
    resultTimeline = merger(flattenedTimelineItem, resultTimeline, timelineItem.position, timelineItemDuration);
  }
  return sortTimeline(resultTimeline);
}

const isPattern: TypeGuard<RecursivePattern<any>> = createTypeGuard<RecursivePattern<any>>('duration', 'timeline');
