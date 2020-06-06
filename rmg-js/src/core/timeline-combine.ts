import { Timeline, TimelineMap } from './timeline';
import { mapObject } from './object-operations';

export type TimelineCombineFunction<T> = (source: Timeline<T>, target: Timeline<T>, position: number, duration: number) => Timeline<T>;

export type TimelineCombineFunctionMap<T> = {
  [P in keyof T]: TimelineCombineFunction<T[P]>;
};

export function combineTimelineMaps<T> (source: TimelineMap<T>, target: TimelineMap<T>, position: number, duration: number, combineMap: TimelineCombineFunctionMap<T>): TimelineMap<T> {
  return mapObject(
    combineMap,
    (key, combine: TimelineCombineFunction<any>) => combine(source[key], target[key], position, duration)
  );
}
