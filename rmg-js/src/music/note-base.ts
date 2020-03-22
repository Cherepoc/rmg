import { Note } from './note';
import { DurationEntity } from './duration-entity';
import { Timelinize } from '../core/timeline';
import { createTypeGuard, TypeGuard } from '../core/type-check';

export interface NoteBaseEntity extends DurationEntity {
  noteBaseTimeline: NoteBaseTimeline
}

export type NoteBaseTimeline = Timelinize<Note>;

export function emptyNoteBaseTimeline(): NoteBaseTimeline {
  return {
    key: [],
    octave: [],
    volume: [],
    duration: [],
    scaleOffset: []
  }
}

export const isNote: TypeGuard<Note | NoteBaseTimeline>
  = createTypeGuard<Note | NoteBaseTimeline>('duration', 'key', 'octave', 'scaleOffset', 'volume');
