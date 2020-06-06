import { timelineItem } from '../src/core/timeline'
import { PatternizedSong } from '../src/composition/patternized-song'
import { emptyNoteBaseTimelineMap } from '../src/music/note-base'
import { Note } from '../src/music/note'
import { flattenPatternizedSong } from '../src/render/flatten-patternized-song'
import { Song } from '../src/music/song'

describe('flatten patternized song', () => {
  it('unpacks flat song', function () {
    const input: PatternizedSong = {
      duration: 1,
      tracks: [],
      tempo: 120,
      scale: {
        noteOffsets: [1],
        rankedOffsetIndexes: [[1]]
      },
      noteBase: {
        key: 1,
        octave: 1,
        volume: 1,
        scaleOffset: [1],
        duration: 1
      },
      notes: {
        1: note(1)
      }
    }
    const song = flattenPatternizedSong(input)
    const expected: Song = {
      duration: 1,
      tracks: [],
      tempo: [
        timelineItem(0, 120),
        timelineItem(1, 1)
      ],
      scale: [timelineItem(0, {
        noteOffsets: [1],
        rankedOffsetIndexes: [[1]]
      })],
      noteBase: {
        key: [
          timelineItem(0, 1),
          timelineItem(1, 0)
        ],
        octave: [
          timelineItem(0, 1),
          timelineItem(1, 0)
        ],
        volume: [
          timelineItem(0, 1), timelineItem(1, 1)
        ],
        scaleOffset: [
          timelineItem(0, [1]), timelineItem(1, [])
        ],
        duration: [
          timelineItem(0, 1), timelineItem(1, 1)
        ]
      },
      notes: {
        1: {
          noteBaseTimeline: emptyNoteBaseTimelineMap(),
          timeline: [timelineItem(0, note(1))],
          duration: 1
        }
      }
    }
    expect(song).toStrictEqual(expected)
  });
})

function note (value: number): Note {
  return {
    duration: value,
    scaleOffset: [value],
    volume: value,
    octave: value,
    key: value
  }
}
