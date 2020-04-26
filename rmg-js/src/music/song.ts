import { Scale } from './scale';
import { Note } from './note';
import { Track } from './track';
import { DurationEntity } from './duration-entity';
import { Timeline } from '../core/timeline';
import { NoteBaseTimeline } from './note-base';
import { TrackMap } from '../composition/track-map-pattern';

export interface Song extends DurationEntity {
  tracks: Track[];
  scale: Timeline<Scale>;
  noteBase: NoteBaseTimeline;
  tempo: Timeline<number>;
  notes: TrackMap<Note>;
}
