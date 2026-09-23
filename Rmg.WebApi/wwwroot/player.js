import { Sequencer, WorkletSynthesizer } from "spessasynth_lib";

export const WORKLET_URL = "https://cdn.jsdelivr.net/npm/spessasynth_lib@4.3.14/dist/spessasynth_processor.min.js";
const SOUND_BANK_ID = "main";
const MAIN_VOLUME = 7;

/** What a channel plays at when nothing says otherwise, which General MIDI puts at 100 of 127. */
const DEFAULT_CHANNEL_VOLUME = 100;

class Player {
    #context;
    #synth;
    #sequencer;
    #hasSoundFont = false;

    constructor(context, synth, sequencer) {
        this.#context = context;
        this.#synth = synth;
        this.#sequencer = sequencer;
    }

    async loadSoundFont(buffer) {
        if (this.#hasSoundFont) {
            this.#hasSoundFont = false;
            await this.#synth.soundBankManager.deleteSoundBank(SOUND_BANK_ID);
        }

        await this.#synth.soundBankManager.addSoundBank(buffer, SOUND_BANK_ID);
        this.#hasSoundFont = true;
    }

    loadSong(buffer) {
        // a new song is played as it was written, so the locks the mixer left are let go of first
        for (const channel of this.#synth.midiChannels) {
            channel.setSystemParameter("presetLock", false);
            channel.lockController(MAIN_VOLUME, false);
        }

        this.#sequencer.loadNewSongList([{ binary: buffer }]);
    }

    async play() {
        await this.#context.resume();
        this.#sequencer.play();
    }

    pause() {
        this.#sequencer.pause();
    }

    stop() {
        this.#sequencer.pause();
        this.#sequencer.currentTime = 0;
    }

    get paused() {
        return this.#sequencer.paused;
    }

    get duration() {
        return this.#sequencer.duration;
    }

    get currentTime() {
        return this.#sequencer.currentTime;
    }

    set currentTime(seconds) {
        this.#sequencer.currentTime = seconds;
    }

    set masterGain(gain) {
        this.#synth.setSystemParameter("gain", gain);
    }

    /** The MIDI channels the loaded song actually uses, ascending. */
    get songChannels() {
        const channels = new Set();
        for (const track of this.#sequencer.midiData?.tracks ?? [])
            for (const channel of track.channels) channels.add(channel);

        return [...channels].sort((a, b) => a - b);
    }

    /**
     *     Sets the instrument a channel plays. The lock is what makes the choice stick: the song sets its
     *     own instruments whenever it starts over, and a locked channel ignores that.
     */
    setChannelInstrument(channel, instrument) {
        const midiChannel = this.#synth.midiChannels[channel];
        if (!midiChannel) return;

        midiChannel.setSystemParameter("presetLock", false);
        this.#synth.programChange(channel, instrument);
        midiChannel.setSystemParameter("presetLock", true);
    }

    /**
     *     Sets how loud a channel plays, as a part of the volume it plays at unasked, which is the same
     *     channel volume the written song carries. Locked for the same reason the instrument is: starting
     *     the song over resets every controller that is not.
     */
    setChannelVolume(channel, volume) {
        const midiChannel = this.#synth.midiChannels[channel];
        if (!midiChannel) return;

        midiChannel.lockController(MAIN_VOLUME, false);
        this.#synth.controllerChange(channel, MAIN_VOLUME, Math.min(127, Math.round(volume * DEFAULT_CHANNEL_VOLUME)));
        midiChannel.lockController(MAIN_VOLUME, true);
    }

    setChannelMuted(channel, isMuted) {
        this.#synth.midiChannels[channel]?.setSystemParameter("isMuted", isMuted);
    }

    getVoiceCount(channel) {
        return this.#synth.midiChannels[channel]?.voiceCount ?? 0;
    }

    onSongChange(callback) {
        this.#sequencer.eventHandler.addEvent("songChange", "rmg-song-change", () => callback());
    }

    onSongEnded(callback) {
        this.#sequencer.eventHandler.addEvent("songEnded", "rmg-song-ended", () => callback());
    }
}

let pending = null;

/** Creates the audio stack on first use; browsers only allow that from a user gesture. */
export function getPlayer() {
    pending ??= create().catch((error) => {
        pending = null;
        throw error;
    });

    return pending;
}

async function create() {
    const context = new AudioContext();
    await context.audioWorklet.addModule(WORKLET_URL);

    const synth = new WorkletSynthesizer(context);
    synth.connect(context.destination);
    await synth.isReady;

    return new Player(context, synth, new Sequencer(synth));
}
