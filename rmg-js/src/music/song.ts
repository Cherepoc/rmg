import { Scale } from './scale';
import { Note } from './note';
import { Track } from './track';
import { DurationEntity } from './duration-entity';
import { Timeline } from '../core/timeline';
import { NoteBaseTimelineMap } from './note-base';
import { TrackMap } from '../composition/track-note-base-pattern-map';

export interface Song extends DurationEntity {
  tracks: Track[]
  scale: Timeline<Scale>
  noteBase: NoteBaseTimelineMap
  tempo: Timeline<number>
  notes: TrackMap<Note>
}
