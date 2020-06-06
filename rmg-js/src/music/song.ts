import { Scale } from './scale';
import { Note } from './note';
import { Track } from './track';
import { DurationEntity } from './duration-entity';
import { Timeline } from '../core/timeline';
import { NoteBaseTimelineMap } from './note-base';
import { TrackNoteBaseTimelineMap } from './track-note-base-timeline-map';

export interface Song extends DurationEntity {
  tracks: Track[]
  scale: Timeline<Scale>
  noteBase: NoteBaseTimelineMap
  tempo: Timeline<number>
  notes: TrackNoteBaseTimelineMap<Note>
}
