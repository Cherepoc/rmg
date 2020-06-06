import { DurationEntity } from '../music/duration-entity';
import { Timeline } from '../core/timeline';
import { Pattern, PatternMap } from './pattern';
import { Note } from '../music/note';
import { NoteBasePattern } from './note-base-pattern';

export interface TrackNoteBasePatternMap<T> extends DurationEntity {
  trackPatternTimelineMap?: {
    [trackNumber: number]: Timeline<NoteBasePattern<T>>
  }
  noteBasePatternTimeline?: Timeline<Pattern<PatternMap<Note>>>
  innerPatternTimeline?: Timeline<TrackNoteBasePatternMap<T>>
}
