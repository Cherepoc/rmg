import { Track } from '../music/track';
import { Scale } from '../music/scale';
import { Note } from '../music/note';
import { AnyPattern, PatternMap } from './pattern';
import { AnyNoteBasePattern } from './note-base-pattern';
import { AnyTrackMapPattern } from './track-note-base-pattern-map';
import { DurationEntity } from '../music/duration-entity';

export interface PatternizedSong extends DurationEntity {
  tracks: Track[]
  scale: AnyPattern<Scale>
  noteBase: AnyNoteBasePattern<PatternMap<Note>>
  tempo: AnyPattern<number>
  notes: AnyTrackMapPattern<Note>
}
