export interface Timed<T> {
  position: number;
  value: T;
}

export interface Timeline<T> extends Array<Timed<T>> {
}

export type EntityTimeline<T> = {
  [P in keyof T]: Timeline<T[P]>;
};

export function timed<T>(position: number, value: T): Timed<T> {
  return {
    position: position,
    value: value
  }
}
