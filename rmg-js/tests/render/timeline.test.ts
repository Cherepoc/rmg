import { mergeTimelines, TimelineMerger } from '../../src/core/timeline-merge';
import { Timeline, timelineItem } from '../../src/core/timeline';

describe('timeline merge', function () {
  const merger: TimelineMerger<number> = {
    merge (t1: number, t2: number): number {
      return t1 + t2
    },
    default (): number {
      return 0
    }
  }

  it('fills target', function () {
    const source: Timeline<number> = [
      timelineItem(0, 1),
      timelineItem(1, 2)
    ]
    const target: Timeline<number> = [
      timelineItem(0, 0),
      timelineItem(2, 0)
    ]
    const mergedTimeline = mergeTimelines(source, target, 1, 1, merger)
    expect(mergedTimeline).toStrictEqual([
      timelineItem(0, 0),
      timelineItem(1, 1),
      timelineItem(2, 0)
    ])
  });

  it('merges source and target', function () {
    const source: Timeline<number> = [
      timelineItem(0, 1)
    ]
    const target: Timeline<number> = [
      timelineItem(0, 2)
    ]
    const mergedTimeline = mergeTimelines(source, target, 1, 1, merger)
    expect(mergedTimeline).toStrictEqual([
      timelineItem(0, 2),
      timelineItem(1, 3),
      timelineItem(2, 2)
    ])
  });

  it('merges source and target no overlap', function () {
    const source: Timeline<number> = [
      timelineItem(0, 1)
    ]
    const target: Timeline<number> = [
      timelineItem(1, 2)
    ]
    const mergedTimeline = mergeTimelines(source, target, 0, 1, merger)
    expect(mergedTimeline).toStrictEqual([
      timelineItem(0, 1),
      timelineItem(1, 2)
    ])
  });

  it('merges source and empty target', function () {
    const source: Timeline<number> = [
      timelineItem(0, 1)
    ]
    const target: Timeline<number> = []
    const mergedTimeline = mergeTimelines(source, target, 1, 1, merger)
    expect(mergedTimeline).toStrictEqual([
      timelineItem(1, 1),
      timelineItem(2, 0)
    ])
  });
})
