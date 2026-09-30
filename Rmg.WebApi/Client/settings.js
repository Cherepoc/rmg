/**
 *     What a song is heard with, written short enough for a link: its unconventionality and facets, its parts and their
 *     mix, its drum setup and drum groups, and its volume, each value given, or as the song drew it. The server reads the
 *     same format (SongSettings), which a link and every event about a song carry. Every value is 7 bits, as MIDI's are,
 *     a switch 1, whether a part plays 2 and the drum setup 2, in a number written as a format's character and letters
 *     and digits. Seeds are written in the same letters and digits.
 */

const FORMAT_ONE = "1";
const DIGITS = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

/** The last step of a value: the most experimental, the loudest, the right. */
export const LAST_STEP = 127;

/** The middle of a pan's steps. */
export const MIDDLE_PAN = 64;

/** The facets, the parts, the drum setups and the drum groups, by the names the server takes, in the format's order. */
export const FACETS = ["feel", "groove", "fills", "form", "chords", "progression", "scale", "melody"];
export const PARTS = ["melody", "chords", "bass", "pad", "counterMelody", "drum"];
export const DRUM_SETUPS = ["kit", "kitAndPercussion", "percussion"];
export const DRUM_GROUPS = ["kick", "snare", "timekeepers", "toms", "accents", "percussion", "calls"];

/** Whether a part plays: as the song draws it, given in, or given out. */
export const PLAYS = ["random", "on", "off"];

const BITS = 8 + FACETS.length * 8 + PARTS.length * (2 + 8 + 7 + 8 + 1) + 2 + DRUM_GROUPS.length * 8 + 7;
const LENGTH = Math.ceil(BITS / Math.log2(62));
const SEED_LENGTH = 11;

function encode(number, length) {
    const digits = [];
    for (let i = 0; i < length; i++) {
        digits.push(DIGITS[Number(number % 62n)]);
        number /= 62n;
    }
    return digits.reverse().join("");
}

function decode(text) {
    let number = 0n;
    for (const digit of text) {
        const value = DIGITS.indexOf(digit);
        if (value < 0) return null;
        number = number * 62n + BigInt(value);
    }
    return number;
}

/**
 *     A seed as the page keeps it: its letters and digits, all 64 of its bits, with no zeros in front, which is how the
 *     server writes it; null for anything that is not a seed. A seed never becomes a JavaScript number, which holds 53
 *     bits exactly and no more.
 */
export function readSeed(text) {
    if (typeof text !== "string" || text.length === 0 || text.length > SEED_LENGTH) return null;
    const number = decode(text);
    return number !== null && number <= 0xFFFFFFFFFFFFFFFFn ? encode(number, SEED_LENGTH).replace(/^0+(?=.)/, "") : null;
}

function writer() {
    let number = 0n;
    const put = (value, bits) => {
        if (!Number.isInteger(value) || value < 0 || value >= 2 ** bits) throw new RangeError(`${value} does not fit in ${bits} bits.`);
        number = (number << BigInt(bits)) | BigInt(value);
    };
    const setting = ({ isGiven, value }) => {
        put(isGiven ? 1 : 0, 1);
        put(value, 7);
    };
    return { put, setting, number: () => number };
}

/** The settings as text, every facet, part and drum group named, as parseSettings reads them. */
export function formatSettings(settings) {
    const w = writer();
    w.setting(settings.unconventionality);
    for (const facet of FACETS) w.setting(settings.facets[facet]);
    for (const name of PARTS) {
        const part = settings.parts[name];
        w.put(PLAYS.indexOf(part.plays), 2);
        w.setting(part.instrument);
        w.put(part.volume, 7);
        w.setting(part.pan);
        w.put(part.isOn ? 1 : 0, 1);
    }
    w.put(settings.drumSetup === null ? 0 : DRUM_SETUPS.indexOf(settings.drumSetup) + 1, 2);
    for (const group of DRUM_GROUPS) {
        w.put(settings.drumGroups[group].isOn ? 1 : 0, 1);
        w.put(settings.drumGroups[group].volume, 7);
    }
    w.put(settings.volume, 7);
    return FORMAT_ONE + encode(w.number(), LENGTH);
}

/** Reads settings, or null for anything that is not settings of the format. */
export function parseSettings(text) {
    if (typeof text !== "string" || text.length !== 1 + LENGTH || text[0] !== FORMAT_ONE) return null;
    const number = decode(text.slice(1));
    if (number === null || number >> BigInt(BITS) !== 0n) return null;

    let position = BITS;
    const take = (bits) => {
        position -= bits;
        return Number((number >> BigInt(position)) & ((1n << BigInt(bits)) - 1n));
    };
    const setting = () => ({ isGiven: take(1) === 1, value: take(7) });

    const unconventionality = setting();
    const facets = Object.fromEntries(FACETS.map((facet) => [facet, setting()]));
    const parts = {};
    for (const name of PARTS) {
        const plays = PLAYS[take(2)];
        if (plays === undefined) return null;
        parts[name] = { plays, instrument: setting(), volume: take(7), pan: setting(), isOn: take(1) === 1 };
    }
    const setup = take(2);
    const drumGroups = Object.fromEntries(DRUM_GROUPS.map((group) => [group, { isOn: take(1) === 1, volume: take(7) }]));
    const volume = take(7);

    return { unconventionality, facets, parts, drumSetup: setup === 0 ? null : DRUM_SETUPS[setup - 1], drumGroups, volume };
}

/**
 *     What of the settings names the song, as its seed does: every value given that changes its notes, the
 *     unconventionality, a facet, a part in or out and the drum setup, as letters and digits; "" where none is. The
 *     server works it out the same way (SongSettings.Identity).
 */
export function identityOf(settings) {
    const w = writer();
    for (const setting of [settings.unconventionality, ...FACETS.map((facet) => settings.facets[facet])])
        w.setting(setting.isGiven ? setting : { isGiven: false, value: 0 });
    for (const name of PARTS) w.put(PLAYS.indexOf(settings.parts[name].plays), 2);
    w.put(settings.drumSetup === null ? 0 : DRUM_SETUPS.indexOf(settings.drumSetup) + 1, 2);
    return w.number() === 0n ? "" : encode(w.number(), 15).replace(/^0+/, "");
}

/** A value from 0 to 1 as its step. */
export function toStep(value) {
    return Math.round(Math.min(Math.max(value, 0), 1) * LAST_STEP);
}
