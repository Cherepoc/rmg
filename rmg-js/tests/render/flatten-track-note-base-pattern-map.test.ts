import { unionTimelines } from '../../src/core/timeline-operations';
import { flattenTrackNoteBasePatternMap } from '../../src/render/flatten-track-note-base-pattern-map';
import { timelineItem } from '../../src/core/timeline';
import { TrackNoteBaseTimelineMap } from '../../src/music/track-note-base-timeline-map';

const combine = unionTimelines;

describe('Flatten track note base pattern map', () => {
  it('works', () => {
    const trackNoteBaseTimeline = flattenTrackNoteBasePatternMap<number>(
      {
        trackPatternTimelineMap: {
          [1]: [
            timelineItem(0, {
              duration: 1,
              patternTimeline: [
                timelineItem(0, {
                  duration: 1,
                  timeline: [timelineItem(0, 1)],
                  innerPatternTimeline: [
                    timelineItem(0.25, {
                      duration: 1,
                      timeline: [timelineItem(0, 1)],
                    }),
                  ],
                }),
              ],
              noteBasePatternTimeline: [
                timelineItem(0, {
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
                    }),
                  ],
                }),
              ],
            }),
          ],
        },
        innerPatternTimeline: [
          timelineItem(0, {
            duration: 1,
            trackPatternTimelineMap: {
              [2]: [
                timelineItem(0, {
                  duration: 1,
                  patternTimeline: [
                    timelineItem(0, {
                      duration: 1,
                      timeline: [timelineItem(0, 1)],
                    }),
                  ],
                }),
              ],
            },
          }),
          timelineItem(0.5, {
            duration: 1,
            trackPatternTimelineMap: {
              [1]: [
                timelineItem(0, {
                  duration: 1,
                  patternTimeline: [
                    timelineItem(0, {
                      duration: 1,
                      timeline: [timelineItem(0, 2)]
                    }),
                  ],
                  noteBasePatternTimeline: [
                    timelineItem(0, {
                      duration: 1,
                      timeline: [
                        timelineItem(0, {
                          duration: {
                            duration: 1,
                            timeline: [timelineItem(0, 2)],
                          },
                          volume: {
                            duration: 1,
                            timeline: [timelineItem(0, 2)],
                          },
                          scaleOffset: {
                            duration: 1,
                            timeline: [timelineItem(0, [2])],
                          },
                          octave: {
                            duration: 1,
                            timeline: [timelineItem(0, 2)],
                          },
                          key: {
                            duration: 1,
                            timeline: [timelineItem(0, 2)],
                          },
                        }),
                      ],
                    }),
                  ],
                }),
              ],
            },
          }),
        ],
        noteBasePatternTimeline: [
          timelineItem(0, {
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
              }),
            ],
          }),
        ],
        duration: 1,
      },
      1,
      combine,
    );
    expect(trackNoteBaseTimeline).toStrictEqual(<TrackNoteBaseTimelineMap<number>>{
      [1]: {
        timeline: [
          timelineItem(0, 1),
          timelineItem(0.25, 1),
          timelineItem(0.5, 2),
        ],
        noteBaseTimeline: {
          key: [
            timelineItem(0, 2),
            timelineItem(0.5, 4),
            timelineItem(1, 0)
          ],
          octave: [
            timelineItem(0, 2),
            timelineItem(0.5, 4),
            timelineItem(1, 0)
          ],
          scaleOffset: [
            timelineItem(0, [2]),
            timelineItem(0.5, [4]),
            timelineItem(1, [])
          ],
          duration: [
            timelineItem(0, 1),
            timelineItem(0.5, 2),
            timelineItem(1, 1)
          ],
          volume: [
            timelineItem(0, 1),
            timelineItem(0.5, 2),
            timelineItem(1, 1)
          ]
        }
      },
      [2]: {
        timeline: [
          timelineItem(0, 1),
        ],
        noteBaseTimeline: {
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
        }
      }
    })
  });
});
