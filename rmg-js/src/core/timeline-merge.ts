import { Timeline, timelineItem, TimelineMap } from './timeline';
import * as equal from 'fast-deep-equal';
import { getPositions, shiftPosition, sortTimeline } from './timeline-operations';
import { mapObject } from './object-operations';
import { TimelineCombineFunction } from './timeline-combine';

export interface TimelineMerger<T> {
  merge: (t1: T, t2: T) => T
  default: () => T
}

export type TimelineMergerMap<T> = {
  [P in keyof T]: TimelineMerger<T[P]>;
};

export function mergeTimelines<T> (source: Timeline<T>, target: Timeline<T>, position: number, duration: number, merger: TimelineMerger<T>): Timeline<T> {
  const shiftedSource = shiftPosition(source, position, duration);
  const sortedTarget = sortTimeline([...target]);
  const positionEnd = position + duration;
  const positions = new Set<number>([
    ...getPositions(shiftedSource, position, positionEnd),
    ...getPositions(sortedTarget, position, positionEnd)
  ]);
  const sortedPositions = [...positions].sort();

  const timeline: Timeline<T> = sortedTarget.filter(x => x.position < position);
  let previousItem: T | undefined;
  let previousSourceItem: T | undefined;
  let previousTargetItem: T | undefined = timeline[timeline.length - 1]?.value;
  for (const position of sortedPositions) {
    const sourceItem = shiftedSource.find(x => x.position === position)?.value;
    const targetItem = sortedTarget.find(x => x.position === position)?.value;
    let newItem: T;
    if (sourceItem != null && targetItem != null) {
      newItem = merger.merge(sourceItem, targetItem);
    } else if (sourceItem != null) {
      newItem = previousTargetItem ? merger.merge(sourceItem, previousTargetItem) : sourceItem;
    } else if (targetItem != null) {
      newItem = previousSourceItem ? merger.merge(previousSourceItem, targetItem) : targetItem;
    } else {
      throw Error('This should be unreachable');
    }

    if (!previousItem || !equal(previousItem, newItem)) {
      timeline.push(timelineItem(position, newItem));
    }

    previousItem = newItem;
    previousSourceItem = sourceItem;
    previousTargetItem = targetItem;
  }

  // add closing element from target after source is placed if it does not exist
  const targetTimelineBeforeEnd = sortedTarget.filter(x => x.position <= positionEnd);
  const lastEffectiveTargetTimelineItem = targetTimelineBeforeEnd[targetTimelineBeforeEnd.length - 1];
  const lastMergedItemValue = timeline[timeline.length - 1]?.value;
  if (lastEffectiveTargetTimelineItem && lastEffectiveTargetTimelineItem.position !== positionEnd && lastEffectiveTargetTimelineItem.value !== lastMergedItemValue) {
    timeline.push(timelineItem(positionEnd, lastEffectiveTargetTimelineItem.value));
  } else if (!lastEffectiveTargetTimelineItem && timeline.length !== 0) {
    timeline.push(timelineItem(positionEnd, merger.default()));
  }

  timeline.push(...sortedTarget.filter(x => x.position >= positionEnd));

  return timeline;
}

export function createCombineFromMerger<T>(merger: TimelineMerger<T>): TimelineCombineFunction<T> {
  return (source: Timeline<T>, target: Timeline<T>, position: number, duration: number) => mergeTimelines(source, target, position, duration, merger);
}

export function mergeTimelineMaps<T> (source: TimelineMap<T>, target: TimelineMap<T>, position: number, duration: number, mergerMap: TimelineMergerMap<T>): TimelineMap<T> {
  return mapObject(mergerMap, (key, merger: TimelineMerger<any>) => mergeTimelines(source[key], target[key], position, duration, merger));
}
