import { AnyPattern, EntityPattern, Pattern } from './pattern';
import { Note } from './note';
import { createTypeGuard, TypeGuard } from '../core/type-check';
import { DurationEntity } from './duration-entity';
import { EntityTimeline } from './timed';

export interface NoteBaseEntity extends DurationEntity {
  noteBase: EntityTimeline<Note>
}

export interface NoteBasePattern<T> extends Pattern<T> {
  noteBase: EntityPattern<Note>;
}

export type AnyNoteBasePattern<T> = Pattern<AnyNoteBasePattern<T>> | AnyPattern<T> | T;

export const isNoteBasePattern: TypeGuard<NoteBasePattern<any>>
  = createTypeGuard<NoteBasePattern<any>>('timeline', 'duration', 'noteBase');
