import { AnyNoteBasePattern, NoteBasePattern } from './note-base-pattern';
import { NoteBaseEntity } from '../music/note-base';

export interface TrackMap<T> {
  [trackNumber: number]: NoteBasePattern<T>;
}

export interface TrackMapPattern<T> extends NoteBaseEntity {
  trackMap: TrackMap<T>;
}

export interface PatternizedTrackMap<T> {
  [trackNumber: number]: AnyNoteBasePattern<T>;
}

export type AnyTrackMapPattern<T> = AnyNoteBasePattern<PatternizedTrackMap<T>>;
