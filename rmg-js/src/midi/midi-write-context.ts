import { MidiWriteOptions } from './midi-write-options';

export interface MidiWriteContext {
  readonly stream: number[];
  readonly options: MidiWriteOptions;
}
