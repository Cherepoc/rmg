import { ScaleOffset } from '../music/simple-types';
import { sumArrays } from '../core/array-math';
import { Note } from '../music/note';
import { NoteBaseTimeline } from '../music/note-base';
import { mergeTimelines, TimelineMerger } from '../core/timeline-operations';
import { mapObject } from '../core/object-operations';

const additiveMerger: TimelineMerger<number> = {
  merge(t1: number, t2: number): number {
    return t1 + t2;
  },
  default(): number {
    return 0;
  },
};

const multiplicativeMerger: TimelineMerger<number> = {
  merge(t1: number, t2: number): number {
    return t1 * t2;
  },
  default(): number {
    return 1;
  },
};

const scaleOffsetMerger: TimelineMerger<ScaleOffset> = {
  merge(t1: ScaleOffset, t2: ScaleOffset): ScaleOffset {
    return sumArrays(t1, t2);
  },
  default(): ScaleOffset {
    return [];
  },
};

export const noteMergers: { readonly [P in keyof Note]: TimelineMerger<Note[P]> } = {
  key: additiveMerger,
  octave: additiveMerger,
  scaleOffset: scaleOffsetMerger,
  volume: multiplicativeMerger,
  duration: multiplicativeMerger,
};

const emptyNote = mapObject(noteMergers, (_, merger: TimelineMerger<any>) => merger.default());

export function mergeNoteBaseTimelines(source: NoteBaseTimeline, target: NoteBaseTimeline, position: number, duration: number): NoteBaseTimeline {
  return mapObject(noteMergers, (key, merger) => mergeTimelines<any>(source[key], target[key], position, duration, merger));
}

export function emptyNoteBase(): Note {
  return { ...emptyNote };
}
