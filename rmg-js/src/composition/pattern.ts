import { Timeline } from '../core/timeline';
import { DurationEntity } from '../music/duration-entity';

export interface Pattern<T> extends DurationEntity {
  timeline?: Timeline<T>
  innerPatternTimeline?: Timeline<Pattern<T>>
}

export type PatternMap<T> = {
  [P in keyof T]: Pattern<T[P]>;
};
