import { unionTimelines } from '../src/core/timeline-operations';
import { timelineItem } from '../src/core/timeline';
import { emptyNoteBaseTimeline } from '../src/music/note-base';
import { flattenTrackMapPattern } from '../src/render/flatten-track-map-pattern';
import { AnyTrackMapPattern, TrackMapPattern } from '../src/composition/track-map-pattern';
import { emptyNoteBase } from '../src/render/note-merge';

type InputType = AnyTrackMapPattern<number>;
type OutputType = TrackMapPattern<number>;

const merger = unionTimelines;

describe('Flatten track map pattern', () => {
  it('unpacks flat value', function() {
    const timeline = flattenTrackMapPattern(<InputType>{ 1: 1 }, 1, merger);
    expect(timeline).toStrictEqual(<OutputType>{
      trackMap: {
        1: {
          timeline: [timelineItem(0, 1)],
          noteBaseTimeline: emptyNoteBaseTimeline(),
          duration: 1,
        },
      },
      noteBaseTimeline: emptyNoteBaseTimeline(),
      duration: 1,
    });
  });

  it('unpacks flat pattern', function() {
    const timeline = flattenTrackMapPattern(<InputType>{
      timeline: [
        timelineItem(0, {
          1: {
            timeline: [timelineItem(0, 1)],
            noteBaseTimeline: [timelineItem(0, {
              duration: 1,
              scaleOffset: [1],
              volume: 1,
              octave: 1,
              key: 1,
            })],
            duration: 1,
          },
          3: {
            timeline: [timelineItem(0, 2)],
            noteBaseTimeline: [timelineItem(0, {
              duration: 2,
              scaleOffset: [2],
              volume: 2,
              octave: 2,
              key: 2,
            })],
            duration: 1,
          },
        }),
      ],
      noteBaseTimeline: [timelineItem(0, {
        duration: 1,
        scaleOffset: [1],
        volume: 1,
        octave: 1,
        key: 1,
      })],
      duration: 1,
    }, 1, merger);
    expect(timeline).toStrictEqual(<OutputType>{
      trackMap: {
        1: {
          timeline: [timelineItem(0, 1)],
          noteBaseTimeline: {
            key: [
              timelineItem(0, 1),
              timelineItem(1, 0),
            ],
            octave: [
              timelineItem(0, 1),
              timelineItem(1, 0),
            ],
            volume: [
              timelineItem(0, 1),
              timelineItem(1, 1),
            ],
            scaleOffset: [
              timelineItem(0, [1]),
              timelineItem(1, []),
            ],
            duration: [
              timelineItem(0, 1),
              timelineItem(1, 1),
            ],
          },
          duration: 1,
        },
        3: {
          timeline: [timelineItem(0, 2)],
          noteBaseTimeline: {
            key: [
              timelineItem(0, 2),
              timelineItem(1, 0),
            ],
            octave: [
              timelineItem(0, 2),
              timelineItem(1, 0),
            ],
            volume: [
              timelineItem(0, 2),
              timelineItem(1, 1),
            ],
            scaleOffset: [
              timelineItem(0, [2]),
              timelineItem(1, []),
            ],
            duration: [
              timelineItem(0, 2),
              timelineItem(1, 1),
            ],
          },
          duration: 1,
        },
      },
      noteBaseTimeline: {
        key: [
          timelineItem(0, 1),
          timelineItem(1, 0),
        ],
        octave: [
          timelineItem(0, 1),
          timelineItem(1, 0),
        ],
        volume: [
          timelineItem(0, 1),
          timelineItem(1, 1),
        ],
        scaleOffset: [
          timelineItem(0, [1]),
          timelineItem(1, []),
        ],
        duration: [
          timelineItem(0, 1),
          timelineItem(1, 1),
        ],
      },
      duration: 1,
    });
  });

  it('combines track from different patterns', function() {
    const timeline = flattenTrackMapPattern(<InputType>{
      timeline: [
        timelineItem(0, {
          1: {
            timeline: [timelineItem(0, 1)],
            noteBaseTimeline: [timelineItem(0, {
              duration: 1,
              scaleOffset: [1],
              volume: 1,
              octave: 1,
              key: 1,
            })],
            duration: 1,
          }
        }),
        timelineItem(0.5, {
          3: {
            timeline: [timelineItem(0, 2)],
            noteBaseTimeline: [timelineItem(0, {
              timeline: [timelineItem(0, {
                duration: 2,
                scaleOffset: [2],
                volume: 2,
                octave: 2,
                key: 2,
              })],
              noteBaseTimeline: [timelineItem(0, emptyNoteBase())],
              duration: 1
            })],
            duration: 1,
          },
        }),
      ],
      noteBaseTimeline: [timelineItem(0, {
        duration: 1,
        scaleOffset: [1],
        volume: 1,
        octave: 1,
        key: 1,
      })],
      duration: 1,
    }, 1, merger);
    expect(timeline).toStrictEqual(<OutputType>{
      trackMap: {
        1: {
          timeline: [timelineItem(0, 1)],
          noteBaseTimeline: {
            key: [
              timelineItem(0, 1),
              timelineItem(0.5, 0),
            ],
            octave: [
              timelineItem(0, 1),
              timelineItem(0.5, 0),
            ],
            volume: [
              timelineItem(0, 1),
              timelineItem(0.5, 1),
            ],
            scaleOffset: [
              timelineItem(0, [1]),
              timelineItem(0.5, []),
            ],
            duration: [
              timelineItem(0, 1),
              timelineItem(0.5, 1),
            ],
          },
          duration: 1,
        },
        3: {
          timeline: [timelineItem(0.5, 2)],
          noteBaseTimeline: {
            key: [
              timelineItem(0.5, 2),
              timelineItem(1, 0),
            ],
            octave: [
              timelineItem(0.5, 2),
              timelineItem(1, 0),
            ],
            volume: [
              timelineItem(0.5, 2),
              timelineItem(1, 1),
            ],
            scaleOffset: [
              timelineItem(0.5, [2]),
              timelineItem(1, []),
            ],
            duration: [
              timelineItem(0.5, 2),
              timelineItem(1, 1),
            ],
          },
          duration: 1,
        },
      },
      noteBaseTimeline: {
        key: [
          timelineItem(0, 1),
          timelineItem(1, 0),
        ],
        octave: [
          timelineItem(0, 1),
          timelineItem(1, 0),
        ],
        volume: [
          timelineItem(0, 1),
          timelineItem(1, 1),
        ],
        scaleOffset: [
          timelineItem(0, [1]),
          timelineItem(1, []),
        ],
        duration: [
          timelineItem(0, 1),
          timelineItem(1, 1),
        ],
      },
      duration: 1,
    });
  });
});
