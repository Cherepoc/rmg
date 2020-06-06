import { Note } from './note';
import { Timeline, TimelineMap } from '../core/timeline';

export interface NoteBaseTimeline<T> {
  timeline: Timeline<T>
  noteBaseTimeline: NoteBaseTimelineMap
}

export type NoteBaseTimelineMap = TimelineMap<Note>;

export function emptyNoteBaseTimeline<T>(): NoteBaseTimeline<T> {
  return {
    timeline: [],
    noteBaseTimeline: emptyNoteBaseTimelineMap(),
  };
}

export function emptyNoteBaseTimelineMap(): NoteBaseTimelineMap {
  return {
    key: [],
    octave: [],
    volume: [],
    duration: [],
    scaleOffset: [],
  };
}
