import { Note } from './note';
import { AnyPattern } from './pattern';
import { Track } from './track';
import { DurationEntity } from './duration-entity';
import { AnyNoteBasePattern } from './note-base-pattern';

export interface Section extends DurationEntity {
  noteBaseTimeline: AnyPattern<Note>;
  trackTimeline: TrackNoteTimeline[];
}

export interface TrackNoteTimeline {
  track: Track;
  timeline: AnyNoteBasePattern<Note>
}
