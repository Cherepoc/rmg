import { Timeline } from './timed';
import { DurationEntity } from './duration-entity';
import { createTypeGuard, TypeGuard } from '../core/type-check';


export interface Pattern<T> extends DurationEntity {
  timeline: Timeline<T>;
}

export type AnyPattern<T> = Pattern<AnyPattern<T>> | T;

export type EntityPattern<T> = {
  [P in keyof T]: AnyPattern<T[P]>;
}

export const isPattern: TypeGuard<Pattern<any>> = createTypeGuard<Pattern<any>>('timeline', 'duration');
