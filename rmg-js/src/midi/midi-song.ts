import { MidiTrack } from './midi-track';
import { intToBytes } from './utils';
import { MidiWriteOptions } from './midi-write-options';
import { MidiWriteContext } from './midi-write-context';

export class MidiSong {
  tracks: MidiTrack[] = [];

  write(options?: MidiWriteOptions): Uint8Array {
    const context: MidiWriteContext = {
      stream: [],
      options: {
        ticksPerQuarterNote: options?.ticksPerQuarterNote ?? 96,
      },
    };

    // header
    context.stream.push(0x4D, 0x54, 0x68, 0x64, 0x00, 0x00, 0x00, 0x06, 0x00, 0x01);
    context.stream.push(...intToBytes(this.tracks?.length ?? 0, 2));
    context.stream.push(...intToBytes(context.options.ticksPerQuarterNote, 2));

    if (this.tracks?.length) {
      this.tracks.forEach(x => x.writeData(context));
    }

    return new Uint8Array(context.stream);
  }
}
