import * as equal from 'fast-deep-equal';

export interface TimelineItem<T> {
  position: number;
  value: T;
}

export interface Timeline<T> extends Array<TimelineItem<T>> {
}

export type Timelinize<T> = {
  [P in keyof T]: Timeline<T[P]>;
};

export function timelineItem<T>(position: number, value: T): TimelineItem<T> {
  return {
    position: position,
    value: value,
  };
}

export function shiftPosition<T>(timeline: Timeline<T>, shiftPosition: number): Timeline<T> {
  return timeline.map(x => ({
    position: x.position + shiftPosition,
    value: x.value,
  }));
}

export function mergeTimelines<T>(source: Timeline<T>, target: Timeline<T>, position: number, duration: number, merger: (t1: T, t2: T) => T): Timeline<T> {
  const shiftedSource = shiftPosition(source, position);
  const positionEnd = position + duration;
  const positions = new Set<number>([
    ...getPositions(shiftedSource, position, positionEnd),
    ...getPositions(target, position, positionEnd),
  ]);
  const sortedPositions = [...positions].sort();

  const timeline: Timeline<T> = [];
  let previousItem: T | undefined;
  let previousSourceItem: T | undefined;
  let previousTargetItem: T | undefined;
  for (let position of sortedPositions) {
    const sourceItem = shiftedSource.find(x => x.position === position)?.value;
    const targetItem = target.find(x => x.position === position)?.value;
    let newItem: T;
    if (sourceItem && targetItem) {
      newItem = merger(sourceItem, targetItem);
    } else if (sourceItem) {
      newItem = previousTargetItem ? merger(sourceItem, previousTargetItem) : sourceItem;
    } else if (targetItem) {
      newItem = previousSourceItem ? merger(previousSourceItem, targetItem) : targetItem;
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

  return timeline;
}

export function getPositions<T>(timeline: Timeline<T>, positionBegin: number, positionEnd: number): number[] {
  return timeline
    .filter(x => x.position >= positionBegin && x.position < positionEnd)
    .map(x => x.position);
}
