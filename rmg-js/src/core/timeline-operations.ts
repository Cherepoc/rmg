import * as equal from 'fast-deep-equal';
import { Timeline, timelineItem } from './timeline';

export interface TimelineMerger<T> {
  merge(t1: T, t2: T): T;
  default(): T;
}

export function sortTimeline<T>(timeline: Timeline<T>): Timeline<T> {
  return [...timeline].sort((a, b) => a.position - b.position);
}

export function shiftPosition<T>(timeline: Timeline<T>, position: number, duration: number): Timeline<T> {
  return timeline
    .filter(x => x.position < duration)
    .map(x => timelineItem(x.position + position, x.value));
}

export function unionTimelines<T>(source: Timeline<T>, target: Timeline<T>, position: number, duration: number): Timeline<T> {
  return [
    ...shiftPosition(source, position, duration),
    ...target,
  ].sort(x => x.position);
}

export function mergeTimelines<T>(source: Timeline<T>, target: Timeline<T>, position: number, duration: number, merger: TimelineMerger<T>): Timeline<T> {
  const shiftedSource = shiftPosition(source, position, duration);
  const sortedTarget = [...target].sort(x => x.position);
  const positionEnd = position + duration;
  const positions = new Set<number>([
    ...getPositions(shiftedSource, position, positionEnd),
    ...getPositions(sortedTarget, position, positionEnd),
  ]);
  const sortedPositions = [...positions].sort();

  const timeline: Timeline<T> = sortedTarget.filter(x => x.position < position);
  let previousItem: T | undefined;
  let previousSourceItem: T | undefined;
  let previousTargetItem: T | undefined = timeline[timeline.length-1]?.value;
  for (let position of sortedPositions) {
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

export function getPositions<T>(timeline: Timeline<T>, positionBegin: number, positionEnd: number): number[] {
  return timeline
    .filter(x => x.position >= positionBegin && x.position < positionEnd)
    .map(x => x.position);
}
