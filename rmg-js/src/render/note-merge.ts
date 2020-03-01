import { ScaleOffset } from '../music/simple-types';
import { sumArrays } from '../core/array-math';
import { Note } from '../music/note';
import { NoteBaseTimeline } from '../music/note-base';
import { mergeTimelines } from '../core/timeline';

type Merger<T> = (t1: T, t2: T) => T;

const additiveMerger: Merger<number> = (t1: number, t2: number) => t1 + t2;

const multiplicativeMerger: Merger<number> = (t1: number, t2: number) => t1 * t2;

const scaleOffsetMerger: Merger<ScaleOffset> = (t1: ScaleOffset, t2: ScaleOffset) => sumArrays(t1, t2);

export const noteMergers: { readonly [P in keyof Note]: Merger<Note[P]> } = {
  key: additiveMerger,
  octave: additiveMerger,
  scaleOffset: scaleOffsetMerger,
  volume: multiplicativeMerger,
  duration: multiplicativeMerger,
};

export function mergeNoteBaseTimelines(source: NoteBaseTimeline, target: NoteBaseTimeline, position: number, duration: number): NoteBaseTimeline {
  const result: any = {};
  for (let noteBaseKey in noteMergers) {
    const key = <keyof Note>noteBaseKey;
    result[key] = mergeTimelines<any>(source[key], target[key], position, duration, noteMergers[key]);
  }

  return result;
}
