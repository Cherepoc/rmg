import { Note } from './note';
import { DurationEntity } from './duration-entity';
import { Timelinize } from '../core/timeline';

export interface NoteBaseEntity extends DurationEntity {
  noteBaseTimeline: NoteBaseTimeline
}

export type NoteBaseTimeline = Timelinize<Note>;

