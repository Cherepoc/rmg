import { createTypeGuard, TypeGuard } from '../core/type-check';
import { Timeline } from '../core/timeline';
import { DurationEntity } from '../music/duration-entity';


export interface Pattern<T> extends DurationEntity {
  timeline: Timeline<T>;
}

export type AnyPattern<T> = Pattern<AnyPattern<T>> | T;

export type Patternize<T> = {
  [P in keyof T]: AnyPattern<T[P]>;
}

export type PatternizeTimeline<T> = {[K in keyof T]: T[K] extends Timeline<infer R> ? AnyPattern<R> : T[K]};

export const isPattern: TypeGuard<Pattern<any>> = createTypeGuard<Pattern<any>>('timeline', 'duration');
