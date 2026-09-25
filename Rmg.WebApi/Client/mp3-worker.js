// the import map of the page does not reach a worker, so the encoder is named in full here
import { Mp3Encoder } from "https://cdn.jsdelivr.net/npm/@breezystack/lamejs@1.2.7/dist/lamejs.js";

/** One MP3 frame, which is what the encoder takes at a time. */
const FRAME = 1152;

/** How often to say how far along it is: often enough to move, rarely enough to stay out of the way. */
const FRAMES_PER_REPORT = 100;

self.addEventListener("message", ({ data }) => {
    const { channels, sampleRate, bitrate } = data;
    const encoder = new Mp3Encoder(channels.length, sampleRate, bitrate);
    const frames = channels.map(() => new Int16Array(FRAME));
    const length = channels[0].length;
    const parts = [];

    for (let start = 0, frame = 0; start < length; start += FRAME, frame++) {
        const count = Math.min(FRAME, length - start);
        for (let channel = 0; channel < channels.length; channel++)
            for (let index = 0; index < count; index++)
                frames[channel][index] = toPcm(channels[channel][start + index]);

        const encoded = channels.length === 1
            ? encoder.encodeBuffer(frames[0].subarray(0, count))
            : encoder.encodeBuffer(frames[0].subarray(0, count), frames[1].subarray(0, count));
        if (encoded.length > 0) parts.push(encoded);

        if (frame % FRAMES_PER_REPORT === 0) self.postMessage({ progress: start / length });
    }

    const last = encoder.flush();
    if (last.length > 0) parts.push(last);

    self.postMessage({ mp3: new Blob(parts, { type: "audio/mpeg" }) });
});

/** A render can overshoot what a sample is allowed to be, so it is held inside what 16 bits can say. */
function toPcm(sample) {
    return Math.max(-32_768, Math.min(32_767, Math.round(sample * 32_767)));
}
