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
