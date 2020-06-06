import { NoteBaseTimeline } from './note-base';

export interface TrackNoteBaseTimelineMap<T> {
  [trackNumber: number]: NoteBaseTimeline<T>
}
