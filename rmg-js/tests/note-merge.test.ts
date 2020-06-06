import { emptyNoteBaseTimelineMap, NoteBaseTimelineMap } from '../src/music/note-base'
import { timelineItem } from '../src/core/timeline'
import { mergeNoteBaseTimelines } from '../src/render/note-merge'

describe('Note timeline merge', () => {
  it('correctly merges', () => {
    const source: NoteBaseTimelineMap = {
      key: [timelineItem(0, 1)],
      octave: [timelineItem(0, 1)],
      volume: [timelineItem(0, 0.5)],
      duration: [timelineItem(0, 0.5)],
      scaleOffset: [timelineItem(0, [1])]
    }
    const target: NoteBaseTimelineMap = {
      key: [timelineItem(0, 1)],
      octave: [timelineItem(0, 1)],
      volume: [timelineItem(0, 0.5)],
      duration: [timelineItem(0, 0.5)],
      scaleOffset: [timelineItem(0, [1])]
    }
    const mergeResult = mergeNoteBaseTimelines(source, target, 1, 1)
    expect(mergeResult).toStrictEqual(<NoteBaseTimelineMap>{
      key: [
        timelineItem(0, 1),
        timelineItem(1, 2),
        timelineItem(2, 1)
      ],
      octave: [
        timelineItem(0, 1),
        timelineItem(1, 2),
        timelineItem(2, 1)
      ],
      volume: [
        timelineItem(0, 0.5),
        timelineItem(1, 0.25),
        timelineItem(2, 0.5)
      ],
      duration: [
        timelineItem(0, 0.5),
        timelineItem(1, 0.25),
        timelineItem(2, 0.5)
      ],
      scaleOffset: [
        timelineItem(0, [1]),
        timelineItem(1, [2]),
        timelineItem(2, [1])
      ]
    })
  });

  it('leaves no duplicates', () => {
    const source = emptyNoteBaseTimelineMap()
    const target: NoteBaseTimelineMap = {
      key: [timelineItem(0, 1)],
      octave: [timelineItem(0, 1)],
      volume: [timelineItem(0, 0.5)],
      duration: [timelineItem(0, 0.5)],
      scaleOffset: [timelineItem(0, [1])]
    }
    const mergeResult = mergeNoteBaseTimelines(source, target, 0, 1)
    expect(mergeResult).toStrictEqual(<NoteBaseTimelineMap>{
      key: [timelineItem(0, 1)],
      octave: [timelineItem(0, 1)],
      volume: [timelineItem(0, 0.5)],
      duration: [timelineItem(0, 0.5)],
      scaleOffset: [timelineItem(0, [1])]
    })
  });

  it('produces empty result if inputs are empty', () => {
    const source = emptyNoteBaseTimelineMap()
    const target: NoteBaseTimelineMap = emptyNoteBaseTimelineMap()
    const mergeResult = mergeNoteBaseTimelines(source, target, 0, 1)
    expect(mergeResult).toStrictEqual(emptyNoteBaseTimelineMap())
  });
})
