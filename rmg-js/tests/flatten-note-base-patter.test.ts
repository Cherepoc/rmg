import { timelineItem } from '../src/core/timeline';
import { AnyNoteBasePattern, NoteBasePattern } from '../src/composition/note-base-pattern';
import { emptyNoteBaseTimeline } from '../src/music/note-base';
import { Patternize } from '../src/composition/pattern';
import { Note } from '../src/music/note';
import { flattenNoteBasePattern } from '../src/render/flatten-note-base-pattern';
import { unionTimelines } from '../src/core/timeline-operations';

type InputType = AnyNoteBasePattern<number>;
type OutputType = NoteBasePattern<number>;

const merger = unionTimelines;

describe('Flatten note base pattern', () => {
  it('unpacks flat value', function() {
    const timeline = flattenNoteBasePattern(<InputType>1, 1, merger);
    expect(timeline).toStrictEqual(<OutputType>{
      timeline: [timelineItem(0, 1)],
      noteBaseTimeline: emptyNoteBaseTimeline(),
      duration: 1,
    });
  });

  it('unpacks flat pattern', function() {
    const timeline = flattenNoteBasePattern(<InputType>{
      timeline: [timelineItem(0, 1)],
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
    });
  });

  it('unpacks flat timeline pattern', function() {
    const timeline = flattenNoteBasePattern(<InputType>{
      timeline: [timelineItem(0, 1)],
      duration: 1,
    }, 1, merger);
    expect(timeline).toStrictEqual(<OutputType>{
      timeline: [timelineItem(0, 1)],
      noteBaseTimeline: emptyNoteBaseTimeline(),
      duration: 1,
    });
  });

  it('unpacks flat pattern with note base', function() {
    const timeline = flattenNoteBasePattern(<InputType>{
      timeline: [timelineItem(0, 1)],
      noteBaseTimeline: <Patternize<Note>>{
        duration: 1,
        scaleOffset: [1],
        volume: 1,
        octave: 1,
        key: 1,
      },
      duration: 1,
    }, 1, merger);
    expect(timeline).toStrictEqual(<OutputType>{
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
    });
  });

  it('unpacks recursive patterns with note bases', function() {
    const timeline = flattenNoteBasePattern({
      timeline: [
        timelineItem(0, 1),
        timelineItem(1, {
          timeline: {
            timeline: [
              timelineItem(0, 1),
              timelineItem(1, {
                timeline: 1,
                duration: 1,
              }),
            ],
            duration: 1,
          },
          noteBaseTimeline: {
            duration: 0.5,
            scaleOffset: [1],
            volume: 0.5,
            octave: 1,
            key: 1,
          },
          duration: 1,
        }),
      ],
      noteBaseTimeline: {
        duration: 0.5,
        scaleOffset: [1],
        volume: 0.5,
        octave: 1,
        key: 1,
      },
      duration: 2,
    }, 2, merger);
    expect(timeline).toStrictEqual(<OutputType>{
      timeline: [
        timelineItem(0, 1),
        timelineItem(1, 1),
      ],
      noteBaseTimeline: {
        key: [
          timelineItem(0, 1),
          timelineItem(1, 2),
          timelineItem(2, 0),
        ],
        octave: [
          timelineItem(0, 1),
          timelineItem(1, 2),
          timelineItem(2, 0),
        ],
        volume: [
          timelineItem(0, 0.5),
          timelineItem(1, 0.25),
          timelineItem(2, 1),
        ],
        scaleOffset: [
          timelineItem(0, [1]),
          timelineItem(1, [2]),
          timelineItem(2, []),
        ],
        duration: [
          timelineItem(0, 0.5),
          timelineItem(1, 0.25),
          timelineItem(2, 1),
        ],
      },
      duration: 2,
    });
  });
});
