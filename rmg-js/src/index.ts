import * as fs from 'fs';
import {
  appendDelta,
  appendEndOfTrackEvent,
  appendHeader,
  appendNoteOffEvent,
  appendNoteOnEvent,
  appendProgramChangeEvent,
  appendTempoEvent,
  appendTimeSignatureEvent,
  appendTrack,
} from './midi/midi';

export async function generate(): Promise<void> {
  const stream: number[] = [];
  appendHeader(stream, 2);

  appendTrack(stream, trackStream => {
    appendDelta(trackStream, 0);
    appendTimeSignatureEvent(trackStream, 4, 4);
    appendDelta(trackStream, 0);
    appendTempoEvent(trackStream, 120);
    appendDelta(trackStream, 4);
    appendEndOfTrackEvent(trackStream);
  });

  appendTrack(stream, trackStream => {
    appendDelta(trackStream, 0);
    appendProgramChangeEvent(trackStream, 0, 0);
    appendDelta(trackStream, 0);
    appendNoteOnEvent(trackStream, 0, 0x40, 1);
    appendDelta(trackStream, 4);
    appendNoteOffEvent(trackStream, 0, 0x40);
    appendDelta(trackStream, 0);
    appendEndOfTrackEvent(trackStream);
  });

  const buffer = Buffer.from(stream);
  await fs.promises.writeFile('C:\\Projects\\RMG\\songs\\' + 'song.mid', buffer);

  console.log('aaaa');
}

(async () => {
  try {
    await generate();
  } catch (e) {
    console.error(e.toString());
  }
})();
