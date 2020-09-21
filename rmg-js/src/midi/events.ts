import { intToBytes } from './utils';
import { MidiWriteContext } from './midi-write-context';

export abstract class MidiEvent {
  abstract writeData(context: MidiWriteContext): void;
}

export abstract class ChannelMidiEvent extends MidiEvent {
  abstract readonly type: number;

  protected constructor(readonly channel: number) {
    super();
  }

  writeData(context: MidiWriteContext): void {
    context.stream.push((this.type << 4) | this.channel);
  }
}

export class TimeSignatureMidiEvent extends MidiEvent {
  constructor(readonly numerator: number, readonly denominator: number) {
    super();
  }

  writeData(context: MidiWriteContext): void {
    const fixedNumerator = Math.floor(this.numerator);
    const fixedDenominator = Math.floor(this.denominator);
    const denominatorPower = Math.log2(fixedDenominator);
    context.stream.push(0xff, 0x58, 0x04, fixedNumerator, denominatorPower, 0x18, 0x08);
  }
}

export class TempoMidiEvent extends MidiEvent {
  constructor(readonly tempo: number) {
    super();
  }

  writeData(context: MidiWriteContext): void {
    context.stream.push(0xff, 0x51, 0x03, ...intToBytes(60 * 1_000_000 / this.tempo, 3));
  }
}

export class EndOfTrackMidiEvent extends MidiEvent {
  constructor() {
    super();
  }

  writeData(context: MidiWriteContext): void {
    context.stream.push(0xff, 0x2f, 0x00);
  }
}

export class ProgramChangeMidiEvent extends ChannelMidiEvent {
  readonly type: number = 0x0C;

  constructor(readonly channel: number, readonly program: number) {
    super(channel);
  }

  writeData(context: MidiWriteContext): void {
    context.stream.push(this.program);
  }
}

export class NoteOnMidiEvent extends ChannelMidiEvent {
  readonly type: number = 0x09;

  constructor(readonly channel: number, readonly note: number, readonly volume: number) {
    super(channel);
  }

  writeData(context: MidiWriteContext): void {
    context.stream.push(this.note, Math.round(this.volume * 0x7f));
  }
}

export class NoteOffMidiEvent extends ChannelMidiEvent {
  readonly type: number = 0x08;

  constructor(readonly channel: number, readonly note: number) {
    super(channel);
  }

  writeData(context: MidiWriteContext): void {
    context.stream.push(this.note, 0x40);
  }
}
