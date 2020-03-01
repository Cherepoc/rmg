import { flattenPattern } from '../src/render/flatten-pattern';
import { timelineItem } from '../src/core/timeline';

describe('flatten pattern', function() {
  it('unpacks flat value', function() {
    const timeline = flattenPattern(1, 1);
    expect(timeline).toStrictEqual([timelineItem(0, 1)]);
  });

  it('filters out excess duration', function() {
    const timeline = flattenPattern({
      duration: 2,
      timeline: [
        timelineItem(0, 1),
        timelineItem(1.5, 2),
      ],
    }, 1);
    expect(timeline).toStrictEqual([timelineItem(0, 1)]);
  });

  it('flattens recursive patterns and values', function() {
    const timeline = flattenPattern<number>({
      duration: 2,
      timeline: [
        timelineItem(0, 1),
        timelineItem(0.5, {
          duration: 1,
          timeline: [
            timelineItem(0, 2),
            timelineItem(1, 3),
          ],
        }),
      ],
    }, 1);
    expect(timeline).toStrictEqual([
      timelineItem(0, 1),
      timelineItem(0.5, 2),
    ]);
  });
});
