/**
 *     The song's harmony as its file marks it: at every change of chord a marker in its first track, "A Dorian: Dm7", its
 *     key and mode and its chord, which a DAW shows over the song and the page shows as it plays.
 */

const MARKER = 0x06;
const TEMPO = 0x51;

/**
 *     The markers of a MIDI file's first track, the tempos and the system messages beside them, each its time in seconds
 *     by the track's tempos and its key and chord; none where the file has none or is not one.
 */
export function readHarmony(buffer) {
    const bytes = new Uint8Array(buffer);
    const text = (at, length) => new TextDecoder().decode(bytes.subarray(at, at + length));
    if (bytes.length < 22 || text(0, 4) !== "MThd" || text(14, 4) !== "MTrk") return [];
    const ticksPerBeat = (bytes[12] << 8) | bytes[13];
    const end = 22 + ((bytes[18] << 24) | (bytes[19] << 16) | (bytes[20] << 8) | bytes[21]);

    let at = 22;
    const number = () => {
        let value = 0;
        let byte;
        do {
            byte = bytes[at++];
            value = (value << 7) | (byte & 0x7f);
        } while (byte & 0x80);
        return value;
    };

    // the seconds so far, and where and at what tempo they were last counted, a beat half a second until one is set
    let [seconds, counted, secondsPerTick] = [0, 0, 0.5 / ticksPerBeat];
    let ticks = 0;
    const harmony = [];
    while (at < Math.min(end, bytes.length)) {
        ticks += number();
        seconds += (ticks - counted) * secondsPerTick;
        counted = ticks;
        const status = bytes[at++];
        // a meta event, its type, its length and its bytes, or a system message, its length and its bytes; the first track
        // holds nothing else, and what does is not this file's
        if (status !== 0xff && status !== 0xf0 && status !== 0xf7) break;
        const type = status === 0xff ? bytes[at++] : -1;
        const length = number();
        if (type === TEMPO) secondsPerTick = ((bytes[at] << 16) | (bytes[at + 1] << 8) | bytes[at + 2]) / 1e6 / ticksPerBeat;
        if (type === MARKER) {
            const [key, chord] = text(at, length).split(": ");
            if (chord !== undefined) harmony.push({ seconds, key, chord });
        }
        at += length;
    }

    return harmony;
}

/** What plays at a time, its key and chord, the change before it held; none before the first. */
export function harmonyAt(harmony, seconds) {
    return harmony.findLast((x) => x.seconds <= seconds + 1e-3) ?? null;
}
