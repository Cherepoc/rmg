import { flattenSongPatternMap } from '../../src/render/flatten-song-pattern-map';
import { timelineItem } from '../../src/core/timeline';

describe("Flatten patternized song", () => {
  it("works", () => {
    const song = flattenSongPatternMap({
      duration: 1,
      tracks: [],
      tempo: {
        duration: 1,
        timeline: [
          timelineItem(0, 1)
        ],
        innerPatternTimeline: [
          timelineItem(0.5, {
            duration: 1,
            timeline: [
              timelineItem(0, 2)
            ]
          })
        ]
      },
      scale: {
        duration: 1,
        timeline: [
          timelineItem(0, {
            noteOffsets: [1],
            rankedOffsetIndexes: [[1]]
          })
        ],
        innerPatternTimeline: [
          timelineItem(0.5, {
            duration: 1,
            timeline: [
              timelineItem(0, {
                noteOffsets: [1, 2],
                rankedOffsetIndexes: [[1], [2]]
              })
            ]
          })
        ]
      },
      noteBase: {
        duration: 1,
        timeline: [
          timelineItem(0, {
            duration: {
              duration: 1,
              timeline: [timelineItem(0, 1)],
            },
            volume: {
              duration: 1,
              timeline: [timelineItem(0, 1)],
            },
            scaleOffset: {
              duration: 1,
              timeline: [timelineItem(0, [1])],
            },
            octave: {
              duration: 1,
              timeline: [timelineItem(0, 1)],
            },
            key: {
              duration: 1,
              timeline: [timelineItem(0, 1)],
            },
          })
        ]
      },
      notes: {
        duration: 1,
        trackPatternTimelineMap: {
          [1]: [
            timelineItem(0, {
              duration: 1,
              patternTimeline: [
                timelineItem(0, {
                  duration: 1,
                  timeline: [
                    timelineItem(0, {
                      duration: 1,
                      volume: 1,
                      scaleOffset: [1],
                      octave: 1,
                      key: 1
                    })
                  ]
                })
              ]
            })
          ]
        }
      }
    });
    expect(song).toStrictEqual({
      duration: 1,
      tracks: [],
      tempo: [
        timelineItem(0, 1),
        timelineItem(0.5, 2),
        timelineItem(1, 1),
      ],
      scale: [
        timelineItem(0, {
          noteOffsets: [1],
          rankedOffsetIndexes: [[1]]
        }),
        timelineItem(0.5, {
          noteOffsets: [1, 2],
          rankedOffsetIndexes: [[1], [2]]
        }),
      ],
      noteBase: {
        key: [
          timelineItem(0, 1),
          timelineItem(1, 0)
        ],
        octave: [
          timelineItem(0, 1),
          timelineItem(1, 0)
        ],
        scaleOffset: [
          timelineItem(0, [1]),
          timelineItem(1, [])
        ],
        duration: [
          timelineItem(0, 1),
          timelineItem(1, 1)
        ],
        volume: [
          timelineItem(0, 1),
          timelineItem(1, 1)
        ]
      },
      notes: {
        [1]: {
          timeline: [
            timelineItem(0, {
              duration: 1,
              volume: 1,
              scaleOffset: [1],
              octave: 1,
              key: 1
            })
          ],
          noteBaseTimeline: {
            duration: [],
            volume: [],
            scaleOffset: [],
            octave: [],
            key: []
          }
        }
      }
    });
  });
});
