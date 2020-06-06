import { DurationEntity } from '../music/duration-entity';
import { Timeline } from '../core/timeline';
import { Pattern, PatternMap } from './pattern';
import { Note } from '../music/note';

export interface NoteBasePattern<T> extends DurationEntity {
  patternTimeline?: Timeline<Pattern<T>>
  noteBasePatternTimeline?: Timeline<Pattern<PatternMap<Note>>>
  innerPatternTimeline?: Timeline<NoteBasePattern<T>>
}
