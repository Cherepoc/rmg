import { AnyPattern, isPattern, Pattern } from '../music/pattern';
import { timed, Timeline } from '../music/timed';

export function flattenPattern<T>(patternValue: AnyPattern<T>, duration: number): Timeline<T> {
  if (isPattern(patternValue)) {
    const timeline: Timeline<T> = [];
    flattenPatternRecursive<T>(timeline, patternValue, 0, duration);
    return timeline;
  } else {
    return [timed(0, patternValue)];
  }
}

function flattenPatternRecursive<T>(
  timeline: Timeline<T>,
  pattern: Pattern<AnyPattern<T>>,
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
