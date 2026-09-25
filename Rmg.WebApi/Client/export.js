import { BasicMIDI } from "spessasynth_core";
import { WorkletSynthesizer } from "spessasynth_lib";
import { WORKLET_URL } from "./player.js";

const SAMPLE_RATE = 44_100;

/** A second of room at the end, so the last notes fade rather than stop dead. */
const TAIL_SECONDS = 1;

/**
 *     Renders a song to audio with a synthesizer of its own, so the one the page plays through carries
 *     on untouched. The soundfont has to be read again for it, since rendering takes the buffer with it.
 */
export async function renderSong(song, soundFont, onProgress) {
    const midi = BasicMIDI.fromArrayBuffer(song);
    const context = new OfflineAudioContext({
        numberOfChannels: 2,
        sampleRate: SAMPLE_RATE,
        length: Math.ceil(SAMPLE_RATE * (midi.duration + TAIL_SECONDS)),
    });

    await context.audioWorklet.addModule(WORKLET_URL);

    // nothing may come between building the synthesizer and starting the render: Chromium drops
    // worklet messages sent to an offline context, so the render is what carries the song and the bank
    const synth = new WorkletSynthesizer(context, { eventsEnabled: false });
    synth.connect(context.destination);
    await synth.startOfflineRender({
        soundBankList: [{ bankOffset: 0, soundBankBuffer: soundFont }],
        midiSequence: midi,
        loopCount: 0,
    });

    // a compressed bank still has to be decoded before a note of it can be rendered
    await synth.isReady;

    const reporting = setInterval(() => onProgress(Math.min(1, synth.currentTime / midi.duration)), 200);
    try {
        return await context.startRendering();
    } finally {
        clearInterval(reporting);
        synth.destroy();
    }
}

/** Encodes rendered audio as an MP3, off the main thread, since the encoder is a long stretch of work. */
export function encodeMp3(audio, bitrate, onProgress) {
    return new Promise((resolve, reject) => {
        const worker = new Worker(new URL("mp3-worker.js", import.meta.url), { type: "module" });

        // copied out of the buffer, because the worker takes what it is given and this is still ours
        const channels = Array.from(
            { length: audio.numberOfChannels },
            (_, channel) => audio.getChannelData(channel).slice()
        );

        worker.addEventListener("message", ({ data }) => {
            if (data.mp3 === undefined) {
                onProgress(data.progress);
                return;
            }

            worker.terminate();
            resolve(data.mp3);
        });

        worker.addEventListener("error", (event) => {
            worker.terminate();
            reject(new Error(event.message || "the MP3 encoder did not start"));
        });

        worker.postMessage(
            { channels, sampleRate: audio.sampleRate, bitrate },
            channels.map((samples) => samples.buffer)
        );
    });
}
