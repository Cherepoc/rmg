import { Sequencer, WorkletSynthesizer } from "spessasynth_lib";

// how long a song is waited for, in milliseconds, before it is taken as loaded all the same
const SONG_WAIT = 5000;

// resolved through the import map, so the processor is always the one of the library the page loads
export const WORKLET_URL = new URL("spessasynth_processor.min.js", import.meta.resolve("spessasynth_lib")).href;
const SOUND_BANK_ID = "main";
const MAIN_VOLUME = 7;
const PAN = 10;

/** What a channel plays at when nothing says otherwise, which General MIDI puts at 100 of 127. */
const DEFAULT_CHANNEL_VOLUME = 100;

/**
 *     A limiter between the synthesizer and what it is heard through, so that the loudest moments are held under full
 *     scale rather than clipped, which is heard as distortion: a compressor at its hardest, a few decibels down, quick to
 *     catch a peak and quick to let go. The playback and the MP3 export are limited alike.
 */
export function createLimiter(context) {
    return new DynamicsCompressorNode(context, { threshold: -3, knee: 0, ratio: 20, attack: 0.002, release: 0.15 });
}

/** How many samples a level is measured over: about 6 ms. */
const LEVEL_SAMPLES = 256;

class Player {
    #context;
    #synth;
    #sequencer;
    #analysers;
    #samples = new Float32Array(LEVEL_SAMPLES);
    #hasSoundFont = false;
    #songWaiters = [];

    constructor(context, synth, sequencer, analysers) {
        this.#context = context;
        this.#synth = synth;
        this.#sequencer = sequencer;
        this.#analysers = analysers;
        this.#sequencer.eventHandler.addEvent("songChange", "rmg-song-loaded", () => {
            for (const resolve of this.#songWaiters.splice(0)) resolve();
        });
    }

    /** Resolves once the sequencer has taken the next song, when its length is known. */
    nextSong() {
        // a song the sequencer never reports, a file it could not read, given up on after a while, as it was then
        return Promise.race([new Promise((resolve) => this.#songWaiters.push(resolve)), new Promise((resolve) => setTimeout(resolve, SONG_WAIT))]);
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
            channel.lockController(PAN, false);
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

    /**
     *     Sets the instrument a channel plays. The lock is what makes a choice stick: the song sets its
     *     own instruments whenever it starts over, and a locked channel ignores that. Putting back what
     *     the song itself asked for wants no lock, so that the song may go on setting it.
     */
    setChannelInstrument(channel, instrument, isLocked = true) {
        const midiChannel = this.#synth.midiChannels[channel];
        if (!midiChannel) return;

        midiChannel.setSystemParameter("presetLock", false);
        this.#synth.programChange(channel, instrument);

        if (isLocked) midiChannel.setSystemParameter("presetLock", true);
    }

    /**
     *     Says whether a channel plays a kit rather than an instrument. Loading a sound bank puts every
     *     channel back to where it started, and a drum channel that has forgotten it is one plays the
     *     whole kit as a piano.
     */
    setChannelDrums(channel, isDrum) {
        const midiChannel = this.#synth.midiChannels[channel];
        if (!midiChannel) return;

        midiChannel.setSystemParameter("presetLock", false);
        midiChannel.setDrums(isDrum);
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

    /** Sets where a channel sits, from 0, left, through 64, the middle, to 127, right, locked as the volume is. */
    setChannelPan(channel, pan) {
        const midiChannel = this.#synth.midiChannels[channel];
        if (!midiChannel) return;

        midiChannel.lockController(PAN, false);
        this.#synth.controllerChange(channel, PAN, pan);
        midiChannel.lockController(PAN, true);
    }

    /** How loud a channel plays now, as the root mean square of its last samples, before reverb and chorus; 0 for silence. */
    setChannelMuted(channel, isMuted) {
        this.#synth.midiChannels[channel]?.setSystemParameter("isMuted", isMuted);
    }

    /** Silences one note of a channel, as a drum of the drums'. */
    muteNote(channel, note) {
        this.#synth.keyModifierManager.addModifier(channel, note, { gain: 0 });
    }

    /** Lets every note silenced sound again, on whichever channel it was. */
    unmuteNotes() {
        this.#synth.keyModifierManager.clearModifiers();
    }

    getLevel(channel) {
        const analyser = this.#analysers[channel];
        if (analyser === undefined) return 0;

        analyser.getFloatTimeDomainData(this.#samples);
        let sum = 0;
        for (const sample of this.#samples) sum += sample * sample;
        return Math.sqrt(sum / this.#samples.length);
    }

    onSongChange(callback) {
        this.#sequencer.eventHandler.addEvent("songChange", "rmg-song-change", () => callback());
    }

    onSongEnded(callback) {
        this.#sequencer.eventHandler.addEvent("songEnded", "rmg-song-ended", () => callback());
    }

    /** Every note as the synthesizer starts it: its channel, its MIDI note and its velocity, from 0 to 127. */
    onNoteOn(callback) {
        this.#synth.eventHandler.addEvent("noteOn", "rmg-note-on", (event) => callback(event.channel, event.midiNote, event.velocity));
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
    synth.connect(createLimiter(context)).connect(context.destination);
    // every channel's own output, which the synthesizer offers to be looked at, measured and heard no further
    const analysers = Array.from({ length: 16 }, (_, channel) => synth.connectChannel(new AnalyserNode(context, { fftSize: LEVEL_SAMPLES }), channel));
    await synth.isReady;

    return new Player(context, synth, new Sequencer(synth), analysers);
}
