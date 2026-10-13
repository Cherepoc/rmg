/**
 *     The song's harmony as its file marks it: at every change of chord a marker in its first track, "A Dorian: Dm7", its
 *     key and mode and its chord, which a DAW shows over the song and the page shows as it plays, with the tempo and the
 *     meter the same track sets.
 */

const MARKER = 0x06;
const TEMPO = 0x51;
const METER = 0x58;

/**
 *     The markers of a MIDI file's first track, its tempos and meters, and the system messages beside them: every change of
 *     one, from the first chord on, its time in seconds by the track's tempos, its key and chord, its tempo in beats a
 *     minute and its meter; none where the file has none or is not one.
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
    // what plays as of now, the key and chord from the first marker on, the tempo in beats a minute and the meter
    let playing = { key: null, chord: null, bpm: 120, meter: "4/4" };
    const changed = (change) => {
        playing = { ...playing, ...change };
        if (playing.chord === null) return;
        // a change at the same time as the one before replaces it, so that a place shows all of what starts there
        if (harmony.length > 0 && Math.abs(harmony[harmony.length - 1].seconds - seconds) < 1e-6) harmony.pop();
        harmony.push({ seconds, ...playing });
    };
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
        if (type === TEMPO) {
            const microseconds = (bytes[at] << 16) | (bytes[at + 1] << 8) | bytes[at + 2];
            secondsPerTick = microseconds / 1e6 / ticksPerBeat;
            changed({ bpm: Math.round(60e6 / microseconds) });
        }
        if (type === METER) changed({ meter: `${bytes[at]}/${2 ** bytes[at + 1]}` });
        if (type === MARKER) {
            const [key, chord] = text(at, length).split(": ");
            if (chord !== undefined) changed({ key, chord });
        }
        at += length;
    }

    return harmony;
}

/** What plays at a time, its key and chord, its tempo and its meter, the change before it held; none before the first chord. */
export function harmonyAt(harmony, seconds) {
    return harmony.findLast((x) => x.seconds <= seconds + 1e-3) ?? null;
}
