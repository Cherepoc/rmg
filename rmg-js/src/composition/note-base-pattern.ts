import { Note } from '../music/note';
import { AnyPattern, Pattern, Patternize, RecursivePattern } from './pattern';
import { NoteBaseTimeline } from '../music/note-base';
import { DurationEntity } from '../music/duration-entity';
import { Timeline } from '../core/timeline';

export interface NoteBasePattern<T> extends Pattern<T> {
  noteBaseTimeline: NoteBaseTimeline;
}

export interface RecursiveNoteBasePattern<T> extends DurationEntity {
  timeline: Timeline<AnyNoteBasePattern<T>>;
  noteBaseTimeline: Timeline<AnyNoteBasePattern<Patternize<Note>>>;
}

export type AnyNoteBasePattern<T> =
  RecursiveNoteBasePattern<AnyNoteBasePattern<T>>
  | RecursivePattern<AnyNoteBasePattern<T>>
  | AnyPattern<T>;
