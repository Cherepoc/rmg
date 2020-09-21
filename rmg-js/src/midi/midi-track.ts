import { Timeline } from '../core/timeline';
import { MidiEvent } from './events';
import { intToBytes } from './utils';
import { MidiWriteContext } from './midi-write-context';

export class MidiTrack {
  events: Timeline<MidiEvent> = [];

  writeData(context: MidiWriteContext): void {
    context.stream.push(0x4D, 0x54, 0x72, 0x6B);

    const lengthBefore = context.stream.length;

    if (this.events?.length) {
      const midiTimeEvents = processEventDelta(this.events, context.options.ticksPerQuarterNote);

      let previousPosition = 0;
      for(let i = 0; i < midiTimeEvents.length; i++) {
        const timedEvent = midiTimeEvents[i];
        writeDelta(context, timedEvent.position - previousPosition);
        timedEvent.value.writeData(context);
        previousPosition = timedEvent.position;
      }
    }

    const trackLength = context.stream.length - lengthBefore;
    context.stream.splice(lengthBefore, 0, ...intToBytes(trackLength, 4));
  }
}

function getMidiTime(value: number, ticksPerQuarterNote: number){
  return Math.round(value * ticksPerQuarterNote);
}

function processEventDelta(events: Timeline<MidiEvent>, ticksPerQuarterNote: number): Timeline<MidiEvent> {
  const midiTimeEvents = events.map((x, index) => ({
    position: getMidiTime(x.position, ticksPerQuarterNote),
    value: x.value,
    index: index,
  }));

  midiTimeEvents.sort((a, b) => {
    const positionDelta = a.position - b.position;
    if (positionDelta !== 0)
      return positionDelta;
    return a.index - b.index;
  });

  return midiTimeEvents;
}

function writeDelta (context: MidiWriteContext, delta: number): void {
  const deltaData = intToBytes(delta, 0, 7);
  for (let i = 0; i < deltaData.length - 1; i++) {
    deltaData[i] |= 0x80;
  }
  context.stream.push(...deltaData);
}
