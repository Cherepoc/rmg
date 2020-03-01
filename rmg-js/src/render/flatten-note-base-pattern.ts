import { Note } from '../music/note';
import { shiftPosition, Timeline } from '../core/timeline';
import { AnyNoteBasePattern } from '../composition/note-base-pattern';
import { AnyPattern, isPattern, Pattern } from '../composition/pattern';

export interface FlatNoteBasePattern<T> {
  timeline: Timeline<T>;
  noteBaseTimeline: Timeline<Note>;
}

export function flattenNoteBasePattern<T>(patternValue: AnyNoteBasePattern<T>, duration: number): FlatNoteBasePattern<T> {
  if (isPattern(patternValue)) {
    const flatResult: FlatNoteBasePattern<T> = {
      timeline: [],
      noteBaseTimeline: []
    };
    flattenPatternRecursive<T>(flatResult, patternValue, 0, duration);
    return flatResult;
  } else {
    return {
      timeline: [timed(0, patternValue)],
      noteBaseTimeline: [timed(0, createDefaultNoteBase())]
    };
  }
}

export function mergeNoteBases(source: Timeline<Note>, target: Timeline<Note>, position: number, duration: number) {
  const shiftedSource = shiftPosition(source, position);
  const positionEnd = position + duration;
  const positions = new Set<number>([
    ...getPositions(shiftedSource, position, positionEnd),
    ...getPositions(target, position, positionEnd),
  ]);
  const sortedPositions = [...positions].sort();

  let lastNote
  for(let position of sortedPositions) {

  }
}

function getPositions(timeline: Timeline<Note>, positionBegin: number, positionEnd: number): number[] {
  return timeline
    .filter(x => x.position >= positionBegin && x.position < positionEnd)
    .map(x => x.position);
}

function flattenPatternRecursive<T>(
  flatResult: FlatNoteBasePattern<T>,
  pattern: Pattern<AnyNoteBasePattern<T>> | Pattern<AnyPattern<T>>,
  position: number,
  duration: number,
): void {
  for (let patternItem of pattern.timeline.filter(x => x.position < duration)) {
    const patternValue = patternItem.value;
    const patternPosition = patternItem.position + position;
    if (isPattern(patternValue)) {
      const patternDuration = Math.min(patternValue.duration, duration - patternItem.position);
      flattenPatternRecursive<T>(timeline, patternValue, patternPosition, patternDuration);
    } else {
      timeline.push(timed<T>(patternPosition, patternValue));
    }
  }
}

function createDefaultNoteBase(): Note {
  return {
    key: 0,
    octave: 0,
    scaleOffset: [],
    volume: 1
  };
}
