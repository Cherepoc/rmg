import { Song } from '../music/song';
import { MidiWriteOptions } from './midi-write-options';
import { MidiSong } from './midi-song';
import { MidiTrack } from './midi-track';
import { Timeline, TimelineItem, timelineItem } from '../core/timeline';
import {
  MidiEvent,
  NoteOffMidiEvent,
  NoteOnMidiEvent,
  ProgramChangeMidiEvent,
  TempoMidiEvent,
  TimeSignatureMidiEvent,
} from './events';
import { TimeSignature } from '../music/time-signature';
import { Note } from '../music/note';
import { NoteBaseTimeline, NoteBaseTimelineMap } from '../music/note-base';
import { Scale } from '../music/scale';
import { getEffectiveTimelineItem, getEffectiveTimelineItemMap } from '../core/timeline-operations';
import { emptyNoteBase, mergeNoteBaseTimelines } from '../render/note-merge';
import { mod } from '../core/math-utils';
import { Track } from '../music/track';
import { combineNoteBaseTimelines } from '../render/flatten-note-base-pattern';
import { mapObject } from '../core/object-operations';

export function writeSong(song: Song, options?: SongWriteOptions): Uint8Array {
  const timeSignature = options?.timeSignature ?? {
    numerator: 4,
    denominator: 4
  };
  const midiSong = new MidiSong();

  const systemTrack = new MidiTrack();
  midiSong.tracks.push(systemTrack);

  systemTrack.events.push(timelineItem(0, new TimeSignatureMidiEvent(timeSignature.numerator, timeSignature.denominator)));
  systemTrack.events.push(...song.tempo.map(x => timelineItem(x.position, new TempoMidiEvent(x.value))));

  return midiSong.write(options?.midiWriteOptions);
}

function convertTrack(song: Song, trackIndex: number): MidiTrack {
  const songTrack = song.tracks[trackIndex];
  const songTrackNotes = song.notes[trackIndex];

  const midiTrack = new MidiTrack();
  midiTrack.events.push(timelineItem(0, new ProgramChangeMidiEvent(trackIndex, songTrack.instrument.code)));

  let trackNoteBaseTimeline: NoteBaseTimelineMap = mapObject(songTrack.noteBase, (_, value) => timelineItem(0, value));
  trackNoteBaseTimeline = mergeNoteBaseTimelines(trackNoteBaseTimeline, song.noteBase, 0, song.duration);
  trackNoteBaseTimeline = mergeNoteBaseTimelines(trackNoteBaseTimeline, songTrackNotes.noteBaseTimeline, 0, song.duration);

  for (let note of songTrackNotes.timeline) {
    writeNote(note, midiTrack.events, trackIndex, trackNoteBaseTimeline, song.scale, songTrack.minOctave, songTrack.maxOctave);
  }

  return midiTrack;
}

function writeNote(
  note: TimelineItem<Note>,
  midiEvents: Timeline<MidiEvent>,
  channel: number,
  noteBaseTimeline: NoteBaseTimelineMap,
  scaleTimeline: Timeline<Scale>,
  minOctave: number,
  maxOctave: number
): void {
  const effectiveScale = getEffectiveTimelineItem(scaleTimeline, note.position);
  if (!effectiveScale)
    throw new Error('No scale in the timeline');
  const effectiveNoteBase = getEffectiveTimelineItemMap(noteBaseTimeline, note.position) ?? emptyNoteBase();

  let noteOffset = getNoteOffset(note.value, effectiveNoteBase, effectiveScale);
  noteOffset = fixOctaveNoteOffset(noteOffset, minOctave, maxOctave);
  noteOffset = FixMidiNoteOffset(noteOffset);

  const volume = note.value.volume * effectiveNoteBase.volume;
  const duration = note.value.duration * effectiveNoteBase.duration;

  midiEvents.push(timelineItem(note.position, new NoteOnMidiEvent(channel, noteOffset, volume)));
  midiEvents.push(timelineItem(note.position + duration, new NoteOffMidiEvent(channel, noteOffset)));
}

export function FixMidiNoteOffset(noteOffset: number): number {
  let offset = 5 * 12 + Math.round(noteOffset);
  while (offset < 0 || offset > 127)
  {
    if (offset < 0)
      offset += 12 * (-offset / 12 + 1);
    if (offset > 127)
      offset -= 12 * ((offset - 127) / 12 + 1);
  }
  return offset;
}

export function getNoteOffset(note: Note, noteBase: Note, scale: Scale): number {
  let scaleOffsetIndex = 0;
  for (let i = 0; i < scale.rankedOffsetIndexes.length; i++) {
    const rankedScaleIndexes = scale.rankedOffsetIndexes[i];
    const offset = (note.scaleOffset[i] ?? 0)
      + (noteBase.scaleOffset[i] ?? 0);
    const rankScaleOffsetIndex = Math.floor(mod(offset, rankedScaleIndexes.length));
    scaleOffsetIndex += rankedScaleIndexes[rankScaleOffsetIndex];
  }

  const scaleOffset = scale.noteOffsets[scaleOffsetIndex % scale.noteOffsets.length];
  const octaveOffset = (note.octave + (noteBase.octave ?? 0)) * 12;
  const keyOffset = note.key + (noteBase.key ?? 0);
  return keyOffset + octaveOffset + scaleOffset;
}

export function fixOctaveNoteOffset(noteOffset: number, minOctave: number, maxOctave: number): number {
  const minNoteOffset = minOctave * 12;
  const maxNoteOffset = (maxOctave + 1) * 12;
  const noteLength = maxNoteOffset - minNoteOffset;
  let fixedNoteOffset = noteOffset;
  while (true)
  {
    if (fixedNoteOffset < minNoteOffset)
      fixedNoteOffset += noteLength;
    else if (fixedNoteOffset >= maxNoteOffset)
      fixedNoteOffset -= noteLength;
    else
      break;
  }

  return fixedNoteOffset;
}

export interface SongWriteOptions {
  timeSignature?: TimeSignature;
  midiWriteOptions?: MidiWriteOptions;
}
