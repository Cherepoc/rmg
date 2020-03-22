import { Timeline, TimelineItem, timelineItem } from '../core/timeline';
import { AnyPattern, RecursivePattern } from '../composition/pattern';
import { createTypeGuard, TypeGuard } from '../core/type-check';
import { sortTimeline } from '../core/timeline-operations';

export type TimelineMerger<T> = (source: Timeline<T>, target: Timeline<T>, position: number, duration: number) => Timeline<T>;

export function flattenPattern<T>(pattern: AnyPattern<T>, duration: number, merger: TimelineMerger<T>): Timeline<T> {
  if (isPattern(pattern)) {
    const patternDuration = Math.min(pattern.duration, duration);
    return flattenPattern<T>(pattern.timeline, patternDuration, merger);
  } else if (isTimeline(pattern)) {
    let resultTimeline: Timeline<T> = [];
    for (let i = 0; i < pattern.length; i++) {
      const timelineItem = pattern[i];
      if (timelineItem.position >= duration) {
        break;
      }

      const nextTimelineItem = pattern[i + 1];
      const timelineItemDuration = Math.min(duration, nextTimelineItem?.position ?? duration) - timelineItem.position;
      const flattenedTimelineItem = flattenPattern<T>(timelineItem.value, timelineItemDuration, merger);
      resultTimeline = merger(flattenedTimelineItem, resultTimeline, timelineItem.position, timelineItemDuration);
    }
    return sortTimeline(resultTimeline);
  } else {
    return merger([timelineItem(0, pattern)], [], 0, duration);
  }
}

const isPattern: TypeGuard<RecursivePattern<any>> = createTypeGuard<RecursivePattern<any>>('duration', 'timeline');

const isTimeline: TypeGuard<Timeline<any>> = function(obj: any): obj is Timeline<any> {
  return Array.isArray(obj) && obj.every(x => isTimelineItem(x));
}

const isTimelineItem: TypeGuard<TimelineItem<any>> = createTypeGuard<TimelineItem<any>>('position', 'value');
