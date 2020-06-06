import { Track } from '../music/track';
import { Scale } from '../music/scale';
import { Note } from '../music/note';
import { DurationEntity } from '../music/duration-entity';
import { Pattern, PatternMap } from './pattern';
import { TrackNoteBasePatternMap } from './track-note-base-pattern-map';

export interface SongPatternMap extends DurationEntity {
  tracks: Track[];
  scale: Pattern<Scale>;
  noteBase: Pattern<PatternMap<Note>>;
  tempo: Pattern<number>;
  notes: TrackNoteBasePatternMap<Note>
}
