import { Note } from './note';
import { Track } from './track';
import { NoteBaseEntity, NoteBaseTimeline } from './note-base';
import { Timeline } from '../core/timeline';

export interface Section extends NoteBaseEntity {
  trackTimeline: TrackNoteTimeline[];
}

export interface TrackNoteTimeline {
  track: Track;
  timeline: Timeline<Note>;
  noteBaseTimeline: NoteBaseTimeline;
}
