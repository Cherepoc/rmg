import { SongPatternMap } from '../composition/song-pattern-map';
import { Song } from '../music/song';
import { flattenPattern } from './flatten-pattern';
import { unionTimelines } from '../core/timeline-operations';
import { multiplicativeMerger } from './timeline-merger';
import { flattenTrackNoteBasePatternMap } from './flatten-track-note-base-pattern-map';
import { flattenPatternMapPattern } from './flatten-pattern-map';
import { noteBaseCombineFunctionMap } from './note-merge';
import { createCombineFromMerger } from '../core/timeline-merge';

const mergeTimelineMultiplicative = createCombineFromMerger(multiplicativeMerger);

export function flattenSongPatternMap(patternMap: SongPatternMap): Song {
  return {
    duration: patternMap.duration,
    tracks: patternMap.tracks,
    scale: flattenPattern(patternMap.scale, patternMap.duration, unionTimelines),
    tempo: flattenPattern(patternMap.tempo, patternMap.duration, mergeTimelineMultiplicative),
    notes: flattenTrackNoteBasePatternMap(patternMap.notes, patternMap.duration, unionTimelines),
    noteBase: flattenPatternMapPattern(patternMap.noteBase, patternMap.duration, noteBaseCombineFunctionMap)
  }
}
