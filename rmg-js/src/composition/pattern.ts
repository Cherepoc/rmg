import { Timeline } from '../core/timeline';
import { DurationEntity } from '../music/duration-entity';


export interface Pattern<T> extends DurationEntity {
  timeline: Timeline<T>;
}

export interface RecursivePattern<T> extends DurationEntity {
  timeline: Timeline<AnyPattern<T>>
}

export type AnyPattern<T> = RecursivePattern<AnyPattern<T>> | T;

export type Patternize<T> = {
  [P in keyof T]: AnyPattern<T[P]>;
}
