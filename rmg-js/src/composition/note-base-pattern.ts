import { Note } from '../music/note';
import { createTypeGuard, TypeGuard } from '../core/type-check';
import { AnyPattern, Pattern, Patternize } from './pattern';

export interface NoteBasePattern<T> extends Pattern<T> {
  noteBase: Patternize<Note>;
}

export type AnyNoteBasePattern<T> = Pattern<AnyNoteBasePattern<T>> | AnyPattern<T> | T;

export const isNoteBasePattern: TypeGuard<NoteBasePattern<any>>
  = createTypeGuard<NoteBasePattern<any>>('timeline', 'duration', 'noteBase');
