import { flattenPattern } from '../src/render/flatten-pattern';
import { timed } from '../src/music/timed';

describe('flatten pattern', function() {
  it('unpacks flat value', function() {
    const timeline = flattenPattern(1, 1);
    expect(timeline).toStrictEqual([timed(0, 1)]);
  });

  it('filters out excess duration', function() {
    const timeline = flattenPattern({
      duration: 2,
      timeline: [
        timed(0, 1),
        timed(1.5, 2),
      ],
    }, 1);
    expect(timeline).toStrictEqual([timed(0, 1)]);
  });

  it('flattens recursive patterns and values', function() {
    const timeline = flattenPattern<number>({
      duration: 2,
      timeline: [
        timed(0, 1),
        timed(0.5, {
          duration: 1,
          timeline: [
            timed(0, 2),
            timed(1, 3),
          ],
        }),
      ],
    }, 1);
    expect(timeline).toStrictEqual([
      timed(0, 1),
      timed(0.5, 2),
    ]);
  });
});
