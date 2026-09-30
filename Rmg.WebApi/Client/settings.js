/**
 *     What a song is heard with, written short enough for a link: its unconventionality and facets, its tempo, key and
 *     meter, its parts and their mix, its drum setup and drum groups, and its volume, each value given, or as the song
 *     drew it. The server reads the same format (SongSettings), which a link and every event about a song carry: a
 *     format's character, then a character for each value from 0 to 63 at a fixed place, then the instruments' two
 *     characters each, then the switches, six to a character. Seeds are written in the same 64 digits.
 */

const FORMAT_TWO = "2";

/** The 64 digits: the digits, the letters, "-" and "_", in base 62's order and on from it. */
const DIGITS = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz-_";

/** The last step of a value: the most experimental, the loudest, the right. */
export const LAST_STEP = 63;

/** The middle of a pan's steps: 32 to its left and 31 to its right, as MIDI's 64 is. */
export const MIDDLE_PAN = 32;

/** The facets, the parts, the drum setups and the drum groups, by the names the server takes, in the format's order. */
export const FACETS = ["feel", "groove", "fills", "form", "chords", "progression", "scale", "melody", "sound"];

/**
 *     The parts the format keeps a place for: the six the songs play, and the riff and the rhythm part a later version
 *     adds, which the page neither shows nor asks for yet.
 */
export const FORMAT_PARTS = ["melody", "chords", "bass", "pad", "counterMelody", "drum", "riff", "rhythm"];

/** The parts the songs play. */
export const PARTS = FORMAT_PARTS.slice(0, 6);

export const DRUM_SETUPS = ["kit", "kitAndPercussion", "percussion"];
export const DRUM_GROUPS = ["kick", "snare", "timekeepers", "toms", "accents", "percussion", "calls"];

/** Whether a part plays: as the song draws it, given in, or given out. */
export const PLAYS = ["random", "on", "off"];

/** The values a song may be given that change its notes: the unconventionality, the facets, the tempo, the key and the meter. */
const GIVEN = ["unconventionality", ...FACETS, "tempo", "key", "meter"];

const LENGTH = GIVEN.length + 1 + 2 * FORMAT_PARTS.length + 1 + DRUM_GROUPS.length + 2 * FORMAT_PARTS.length
    + Math.ceil((GIVEN.length + 4 * FORMAT_PARTS.length + DRUM_GROUPS.length) / 6);
const SEED_LENGTH = 11;

function encode(number, length) {
    const digits = [];
    for (let i = 0; i < length; i++) {
        digits.push(DIGITS[Number(number % 64n)]);
        number /= 64n;
    }
    return digits.reverse().join("");
}

function decode(text) {
    let number = 0n;
    for (const digit of text) {
        const value = DIGITS.indexOf(digit);
        if (value < 0) return null;
        number = number * 64n + BigInt(value);
    }
    return number;
}

/**
 *     A seed as the page keeps it: its 64 digits, all 64 of its bits, with no zeros in front, which is how the server
 *     writes it; null for anything that is not a seed. A seed never becomes a JavaScript number, which holds 53 bits
 *     exactly and no more.
 */
export function readSeed(text) {
    if (typeof text !== "string" || text.length === 0 || text.length > SEED_LENGTH) return null;
    const number = decode(text);
    return number !== null && number <= 0xFFFFFFFFFFFFFFFFn ? encode(number, SEED_LENGTH).replace(/^0+(?=.)/, "") : null;
}

function givenSetting(settings, name) {
    return FACETS.includes(name) ? settings.facets[name] : settings[name];
}

/** Six switches a character, the first the lowest bit. */
function packSwitches(switches) {
    const values = [];
    for (let i = 0; i < switches.length; i += 6)
        values.push(switches.slice(i, i + 6).reduce((sum, isOn, bit) => sum + (isOn ? 1 << bit : 0), 0));
    return values;
}

/** The settings as text, every facet, part and drum group named, as parseSettings reads them. */
export function formatSettings(settings) {
    const values = [];
    for (const name of GIVEN) values.push(givenSetting(settings, name).value);
    values.push(settings.volume);
    for (const name of FORMAT_PARTS) values.push(settings.parts[name].volume, settings.parts[name].pan.value);
    values.push(settings.drumSetup === null ? 0 : DRUM_SETUPS.indexOf(settings.drumSetup) + 1);
    for (const group of DRUM_GROUPS) values.push(settings.drumGroups[group].volume);
    for (const name of FORMAT_PARTS) {
        const instrument = settings.parts[name].instrument;
        const value = (instrument.isGiven ? 128 : 0) + instrument.value;
        values.push(value >> 6, value & 63);
    }

    const switches = GIVEN.map((name) => givenSetting(settings, name).isGiven);
    for (const name of FORMAT_PARTS) {
        const part = settings.parts[name];
        const plays = PLAYS.indexOf(part.plays);
        switches.push((plays & 2) !== 0, (plays & 1) !== 0, part.isOn, part.pan.isGiven);
    }
    for (const group of DRUM_GROUPS) switches.push(settings.drumGroups[group].isOn);
    values.push(...packSwitches(switches));

    if (values.some((x) => !Number.isInteger(x) || x < 0 || x > 63)) throw new RangeError("A value does not fit in a character.");
    return FORMAT_TWO + values.map((x) => DIGITS[x]).join("");
}

/** Reads settings, or null for anything that is not settings of the format. */
export function parseSettings(text) {
    if (typeof text !== "string" || text.length !== 1 + LENGTH || text[0] !== FORMAT_TWO) return null;
    const values = [...text.slice(1)].map((x) => DIGITS.indexOf(x));
    if (values.some((x) => x < 0)) return null;

    let at = 0;
    const next = () => values[at++];
    const given = GIVEN.map(() => next());
    const volume = next();
    const mixes = FORMAT_PARTS.map(() => ({ volume: next(), pan: next() }));
    const setup = next();
    const groupVolumes = DRUM_GROUPS.map(() => next());
    const instruments = FORMAT_PARTS.map(() => next() * 64 + next());
    const switches = values.slice(at).flatMap((x) => [0, 1, 2, 3, 4, 5].map((bit) => ((x >> bit) & 1) === 1));

    let s = 0;
    const settings = { facets: {}, parts: {}, drumGroups: {} };
    GIVEN.forEach((name, i) => {
        const setting = { isGiven: switches[s++], value: given[i] };
        if (FACETS.includes(name)) settings.facets[name] = setting;
        else settings[name] = setting;
    });
    for (const [i, name] of FORMAT_PARTS.entries()) {
        const plays = PLAYS[(switches[s++] ? 2 : 0) + (switches[s++] ? 1 : 0)];
        const isOn = switches[s++];
        const pan = { isGiven: switches[s++], value: mixes[i].pan };
        if (plays === undefined || instruments[i] >= 256) return null;
        settings.parts[name] = { plays, instrument: { isGiven: instruments[i] >= 128, value: instruments[i] & 127 }, volume: mixes[i].volume, pan, isOn };
    }
    for (const [i, group] of DRUM_GROUPS.entries()) settings.drumGroups[group] = { isOn: switches[s++], volume: groupVolumes[i] };
    if (setup > DRUM_SETUPS.length || settings.key.value > 11) return null;
    settings.drumSetup = setup === 0 ? null : DRUM_SETUPS[setup - 1];
    settings.volume = volume;
    return settings;
}

/**
 *     What of the settings names the song, as its seed does, whatever changes its notes: the switches of which values
 *     are given, six to a character, the values of those given alone, every part's plays, and the drum setup, with the
 *     zeros at its end left off; "" where nothing is given. The server works it out the same way (SongSettings.Identity).
 */
export function identityOf(settings) {
    const given = GIVEN.map((name) => givenSetting(settings, name));
    const values = packSwitches(given.map((x) => x.isGiven));
    values.push(...given.filter((x) => x.isGiven).map((x) => x.value));
    values.push(...FORMAT_PARTS.map((name) => PLAYS.indexOf(settings.parts[name].plays)));
    values.push(settings.drumSetup === null ? 0 : DRUM_SETUPS.indexOf(settings.drumSetup) + 1);
    return values.map((x) => DIGITS[x]).join("").replace(/0+$/, "");
}
