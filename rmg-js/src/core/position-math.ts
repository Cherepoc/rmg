import { Timeline } from '../music/timed';

export function shiftPosition<T>(timeline: Timeline<T>, shiftPosition: number): Timeline<T> {
  return timeline.map(x => ({
    position: x.position + shiftPosition,
    value: x.value
  }))
}
