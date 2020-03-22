import { flattenPattern } from '../src/render/flatten-pattern';
import { timelineItem } from '../src/core/timeline';
import { unionTimelines } from '../src/core/timeline-operations';

const merger = unionTimelines;

describe('flatten pattern', function() {
  it('unpacks flat value', function() {
    const timeline = flattenPattern(1, 1, merger);
    expect(timeline).toStrictEqual([timelineItem(0, 1)]);
  });

  it('filters out excess duration', function() {
    const timeline = flattenPattern(
      {
        duration: 2,
        timeline: [
          timelineItem(0, 1),
          timelineItem(1.5, 2),
        ],
      }, 1,
      merger,
    );
    expect(timeline).toStrictEqual([timelineItem(0, 1)]);
  });

  it('flattens recursive patterns and values', function() {
    const timeline = flattenPattern<number>(
      {
        duration: 3,
        timeline: [
          timelineItem(0, 1),
          timelineItem(0.5, {
            duration: 2,
            timeline: [
              timelineItem(0, 2),
              timelineItem(0.5, [
                timelineItem(0, 1),
                timelineItem(0.5, {
                  timeline: 1,
                  duration: 1,
                }),
              ]),
            ],
          }),
        ],
      },
      2,
      merger);
    expect(timeline).toStrictEqual([
      timelineItem(0, 1),
      timelineItem(0.5, 2),
      timelineItem(1, 1),
      timelineItem(1.5, 1),
    ]);
  });
});
