import { TimelineCombineFunctionMap } from '../../src/core/timeline-combine';
import { mergeTimelines } from '../../src/core/timeline-merge';
import { additiveMerger, multiplicativeMerger } from '../../src/render/timeline-merger';
import { flattenPatternMapPattern } from '../../src/render/flatten-pattern-map';
import { timelineItem } from '../../src/core/timeline';

interface TestMap {
  fieldAdd: number
  fieldMul: number
}

const combineMap: TimelineCombineFunctionMap<TestMap> = {
  fieldAdd: (source, target, position, duration) =>
    mergeTimelines(source, target, position, duration, additiveMerger),
  fieldMul: (source, target, position, duration) =>
    mergeTimelines(source, target, position, duration, multiplicativeMerger)
}

describe('flatten pattern map', function () {
  it('works recursively for different fields', function () {
    const timeline = flattenPatternMapPattern<TestMap>(
      {
        duration: 1,
        timeline: [
          timelineItem(0, {
            fieldAdd: {
              duration: 1,
              timeline: [
                timelineItem(0, 1)
              ],
              innerPatternTimeline: [
                timelineItem(0, {
                  duration: 1,
                  timeline: [timelineItem(0, 2)]
                })
              ]
            },
            fieldMul: {
              duration: 1,
              timeline: [
                timelineItem(0, 1)
              ],
              innerPatternTimeline: [
                timelineItem(0, {
                  duration: 1,
                  timeline: [timelineItem(0, 2)]
                })
              ]
            }
          })
        ],
        innerPatternTimeline: [timelineItem(0, {
          duration: 1,
          timeline: [timelineItem(0, {
            fieldAdd: {
              duration: 1,
              timeline: [timelineItem(0, 2)]
            },
            fieldMul: {
              duration: 1,
              timeline: [timelineItem(0, 2)]
            }
          })]
        })]
      },
      1,
      combineMap
    )
    expect(timeline).toStrictEqual({
      fieldAdd: [
        timelineItem(0, 5),
        timelineItem(1, 0)
      ],
      fieldMul: [
        timelineItem(0, 4),
        timelineItem(1, 1)
      ]
    })
  });
})
