import { Timeline, timelineItem } from './timeline';

export function sortTimeline<T> (timeline: Timeline<T>): Timeline<T> {
  return [...timeline].sort((a, b) => a.position - b.position);
}

export function shiftPosition<T> (timeline: Timeline<T>, position: number, duration: number): Timeline<T> {
  return timeline
    .filter(x => x.position < duration)
    .map(x => timelineItem(x.position + position, x.value));
}

export function unionTimelines<T> (source: Timeline<T>, target: Timeline<T>, position: number, duration: number): Timeline<T> {
  const shiftedPositions = shiftPosition(source, position, duration);
  return sortTimeline([
    ...shiftedPositions,
    ...target
  ]);
}

export function getPositions<T> (timeline: Timeline<T>, positionBegin: number, positionEnd: number): number[] {
  return timeline
    .filter(x => x.position >= positionBegin && x.position < positionEnd)
    .map(x => x.position);
}
