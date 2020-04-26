import { Song } from '../music/song';
import { PatternizedSong } from '../composition/patternized-song';
import { flattenNoteBasePatternInner } from './flatten-note-base-pattern';
import { flattenTrackMapPattern } from './flatten-track-map-pattern';
import { mergeTimelines, unionTimelines } from '../core/timeline-operations';
import { flattenPattern, TimelineMerger } from './flatten-pattern';
import { multiplicativeMerger } from './timeline-merger';
import { mergeNoteBaseTimelines } from './note-merge';

export function flattenPatternizedSong(patternizedSong: PatternizedSong): Song {
  const duration = patternizedSong.duration;
  const trackMapPattern = flattenTrackMapPattern(patternizedSong.notes, duration, unionTimelines);
  const noteBase = flattenNoteBasePatternInner(patternizedSong.noteBase, duration);
  return {
    duration: duration,
    tracks: patternizedSong.tracks,
    noteBase: mergeNoteBaseTimelines(noteBase, trackMapPattern.noteBaseTimeline, 0, duration),
    notes: trackMapPattern.trackMap,
    scale: flattenPattern(patternizedSong.scale, duration, unionTimelines),
    tempo: flattenPattern(patternizedSong.tempo, duration, multiplicativeTimelineMerger)
  }
}

const multiplicativeTimelineMerger: TimelineMerger<number> = (source, target, position, duration) =>
  mergeTimelines(source, target, position, duration, multiplicativeMerger);
