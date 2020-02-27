import { intToBytes } from './utils';

const ticksPerQuarterNote = 96;

export function appendHeader(stream: number[], numberOfTracks: number): void {
  stream.push(0x4D, 0x54, 0x68, 0x64, 0x00, 0x00, 0x00, 0x06, 0x00, 0x01);
  stream.push(...intToBytes(numberOfTracks, 2));
  stream.push(...intToBytes(ticksPerQuarterNote, 2));
}

export function appendTrack(stream: number[], trackFunction: (eventStream: number[]) => void): void {
  stream.push(0x4D, 0x54, 0x72, 0x6B);
  const lengthBefore = stream.length;
  trackFunction(stream);
  const trackLength = stream.length - lengthBefore;
  stream.splice(lengthBefore, 0, ...intToBytes(trackLength, 4));
}

export function appendDelta(stream: number[], delta: number): void {
  const deltaData = intToBytes(Math.round(delta * ticksPerQuarterNote), 0, 7);
  for (let i = 0; i < deltaData.length - 1; i++) {
    deltaData[i] |= 0x80;
  }
  stream.push(...deltaData);
}

export function appendTimeSignatureEvent(stream: number[], numerator: number, denominator: number): void {
  const fixedNumerator = Math.floor(numerator);
  const fixedDenominator = Math.floor(denominator);
  const denominatorPower = Math.log2(fixedDenominator);
  stream.push(0xff, 0x58, 0x04, fixedNumerator, denominatorPower, 0x18, 0x08);
}

export function appendTempoEvent(stream: number[], tempo: number): void {
  stream.push(0xff, 0x51, 0x03, ...intToBytes(60 * 1_000_000 / tempo, 3));
}

export function appendEndOfTrackEvent(stream: number[]): void {
  stream.push(0xff, 0x2f, 0x00);
}

export function appendChannelEvent(stream: number[], type: number, channel: number, ...args: number[]): void {

  stream.push((type << 4) | channel, ...args);
}

export function appendProgramChangeEvent(stream: number[], channel: number, program: number): void {
  appendChannelEvent(stream, 0x0C, channel, program);
}

export function appendNoteOnEvent(stream: number[], channel: number, note: number, volume: number): void {
  appendChannelEvent(stream, 0x09, channel, note, Math.round(volume * 0x7f));
}

export function appendNoteOffEvent(stream: number[], channel: number, note: number): void {
  appendChannelEvent(stream, 0x08, channel, note, 0x40);
}
