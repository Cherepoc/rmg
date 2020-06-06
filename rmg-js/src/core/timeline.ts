export interface TimelineItem<T> {
  position: number
  value: T
}

export type Timeline<T> = Array<TimelineItem<T>>;

export type TimelineMap<T> = {
  [P in keyof T]: Timeline<T[P]>;
};

export function timelineItem<T> (position: number, value: T): TimelineItem<T> {
  return {
    position: position,
    value: value
  };
}
