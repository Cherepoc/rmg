import { unionTimelines } from '../../src/core/timeline-operations';
import { flattenNoteBasePattern } from '../../src/render/flatten-note-base-pattern';
import { timelineItem } from '../../src/core/timeline';
import { NoteBaseTimeline } from '../../src/music/note-base';

const combine = unionTimelines;

describe('Flatten note base pattern', () => {
  it('works', function () {
    const timeline = flattenNoteBasePattern<number>(
      {
        duration: 1,
        patternTimeline: [
          timelineItem(0, {
            duration: 1,
            timeline: [timelineItem(0, 1)],
            innerPatternTimeline: [
              timelineItem(0, {
                duration: 1,
                timeline: [timelineItem(0.5, 1)]
              })
            ]
          })
        ],
        noteBasePatternTimeline: [
          timelineItem(0, {
            duration: 1,
            timeline: [
              timelineItem(0, {
                duration: {
                  duration: 1,
                  timeline: [timelineItem(0, 1)]
                },
                volume: {
                  duration: 1,
                  timeline: [timelineItem(0, 1)]
                },
                scaleOffset: {
                  duration: 1,
                  timeline: [timelineItem(0, [1])]
                },
                octave: {
                  duration: 1,
                  timeline: [timelineItem(0, 1)]
                },
                key: {
                  duration: 1,
                  timeline: [timelineItem(0, 1)]
                }
              })
            ]
          })
        ],
        innerPatternTimeline: [
          timelineItem(0, {
            duration: 1,
            patternTimeline: [
              timelineItem(0.25, {
                duration: 1,
                timeline: [timelineItem(0, 1)],
                innerPatternTimeline: [
                  timelineItem(0, {
                    duration: 1,
                    timeline: [timelineItem(0.5, 1)]
                  })
                ]
              })
            ],
            noteBasePatternTimeline: [
              timelineItem(0, {
                duration: 1,
                timeline: [timelineItem(0, {
                  duration: {
                    duration: 1,
                    timeline: [timelineItem(0, 2)]
                  },
                  volume: {
                    duration: 1,
                    timeline: [timelineItem(0, 2)]
                  },
                  scaleOffset: {
                    duration: 1,
                    timeline: [timelineItem(0, [2])]
                  },
                  octave: {
                    duration: 1,
                    timeline: [timelineItem(0, 2)]
                  },
                  key: {
                    duration: 1,
                    timeline: [timelineItem(0, 2)]
                  }
                })
                ]
              })
            ]
          })
        ]
      },
      1,
      combine
    )
    expect(timeline).toStrictEqual(<NoteBaseTimeline<Number>>{
      timeline: [
        timelineItem(0, 1),
        timelineItem(0.25, 1),
        timelineItem(0.5, 1),
        timelineItem(0.75, 1)
      ],
      noteBaseTimeline: {
        key: [
          timelineItem(0, 3),
          timelineItem(1, 0)
        ],
        octave: [
          timelineItem(0, 3),
          timelineItem(1, 0)
        ],
        scaleOffset: [
          timelineItem(0, [3]),
          timelineItem(1, [])
        ],
        duration: [
          timelineItem(0, 2),
          timelineItem(1, 1)
        ],
        volume: [
          timelineItem(0, 2),
          timelineItem(1, 1)
        ]
      }
    })
  });
})
