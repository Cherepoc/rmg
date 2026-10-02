/**
 *     What a song is heard with, written short enough for a link: its unconventionality and facets, its tempo, key, meter,
 *     scale and swing, its parts and their mix, its drum groups and drum pieces, and its volume, each value given, or as the song
 *     drew it. The server reads the same format (SongSettings), which a link and every event about a song carry: a
 *     format's character, then a character for every value of 64 steps at a fixed place, then every other value as its
 *     bits, one after another, six to a character. Seeds are written in the same 64 digits.
 */

/** The format this writes, as its first character; one that is read differently takes the next. */
const FORMAT_CHARACTER = "7";

/** The 64 digits: the digits, the letters, "-" and "_", in base 62's order and on from it. */
const DIGITS = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz-_";

/** The last step of a value: the most experimental, the loudest, the right. */
export const LAST_STEP = 63;

/**
 *     The middle step, 32 below it and 31 above, as MIDI's pan has, so that a value can be given at its middle: 50%, a pan
 *     in the middle, a tempo of 120 beats a minute (the server's StepScale).
 */
export const MIDDLE_STEP = 32;

/** A step's amount, from 0 at the first through 0.5 at the middle to 1 at the last. */
export function fromStep(step) {
    return step <= MIDDLE_STEP ? step / (2 * MIDDLE_STEP) : 0.5 + (step - MIDDLE_STEP) / (2 * (LAST_STEP - MIDDLE_STEP));
}

/** An amount from 0 to 1 as its nearest step. */
export function toStep(amount) {
    return Math.round(amount <= 0.5 ? amount * 2 * MIDDLE_STEP : MIDDLE_STEP + (amount - 0.5) * 2 * (LAST_STEP - MIDDLE_STEP));
}

/** The facets, the parts, the drum setups and the drum groups, by the names the server takes, in the format's order. */
export const FACETS = ["feel", "groove", "fills", "form", "chords", "progression", "scale", "melody", "sound"];

/** The parts the format keeps a place for, every one the songs play. */
export const FORMAT_PARTS = ["melody", "chords", "bass", "pad", "counterMelody", "drum", "riff", "rhythm", "solo"];

/** The parts the songs play. */
export const PARTS = FORMAT_PARTS;

export const DRUM_GROUPS = ["kick", "snare", "timekeepers", "toms", "accents", "percussion", "calls"];

/** The drum pieces, by the names the server takes, in its order (DrumPieces.All), which the format keeps a switch for each of. */
export const DRUM_PIECES = [
    "kick", "acousticSnare", "electricSnare", "clap", "hiHat", "ride", "tambourine", "cabasa", "maracas", "tom", "cymbal", "vibraslap",
    "bongo", "conga", "timbale", "agogo", "cowbell", "claves", "woodBlock", "guiro", "triangle", "cuica", "whistle",
];

/** Whether a part plays: as the song draws it, given in, or given out. */
export const PLAYS = ["random", "on", "off"];

/**
 *     The values a song may be given that change its notes: the unconventionality, the facets, the tempo, the key, the
 *     meter, the scale, called its mode beside the facet of that name, and the swing.
 */
const GIVEN = ["unconventionality", ...FACETS, "tempo", "key", "meter", "mode", "swing"];

// the bits of a key, of twelve, of a meter and of a scale, of up to sixteen options each, of a part's plays, of three,
// and of a program, of 128
const KEY_BITS = 4;
const METER_BITS = 4;
const MODE_BITS = 4;
const PLAYS_BITS = 2;
const PROGRAM_BITS = 7;

// the values of 64 steps, a character each: the unconventionality, the facets, the tempo, the swing, the volume, every
// part's volume and pan, and every drum group's volume; then the rest, bit by bit: whether each value is given, the key,
// the meter and the scale, every part's plays, whether its pan and its instrument are given and its program, and whether
// the drum pieces are given and every piece's being in
const CHARACTER_COUNT = 1 + FACETS.length + 1 + 1 + 1 + 2 * FORMAT_PARTS.length + DRUM_GROUPS.length;
const BIT_COUNT = GIVEN.length + KEY_BITS + METER_BITS + MODE_BITS + FORMAT_PARTS.length * (PLAYS_BITS + 2 + PROGRAM_BITS) + 1 + DRUM_PIECES.length;

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

/** The settings as text, every facet, part, drum group and drum piece named, as parseSettings reads them. */
export function formatSettings(settings) {
    const characters = [settings.unconventionality.value, ...FACETS.map((facet) => settings.facets[facet].value), settings.tempo.value, settings.swing.value, settings.volume];
    for (const name of FORMAT_PARTS) characters.push(settings.parts[name].volume, settings.parts[name].pan.value);
    for (const group of DRUM_GROUPS) characters.push(settings.drumGroups[group].volume);
    if (characters.some((x) => !Number.isInteger(x) || x < 0 || x > 63)) throw new RangeError("A value does not fit in a character.");

    const bits = GIVEN.map((name) => givenSetting(settings, name).isGiven);
    const write = (value, count) => {
        if (!Number.isInteger(value) || value < 0 || value >= 1 << count) throw new RangeError(`${value} does not fit in ${count} bits.`);
        for (let bit = 0; bit < count; bit++) bits.push(((value >> bit) & 1) === 1);
    };
    write(settings.key.value, KEY_BITS);
    write(settings.meter.value, METER_BITS);
    write(settings.mode.value, MODE_BITS);
    for (const name of FORMAT_PARTS) {
        const part = settings.parts[name];
        write(PLAYS.indexOf(part.plays), PLAYS_BITS);
        bits.push(part.pan.isGiven, part.instrument.isGiven);
        write(part.instrument.value, PROGRAM_BITS);
    }
    bits.push(settings.drumPieces.isGiven, ...DRUM_PIECES.map((piece) => settings.drumPieces.value.includes(piece)));
    return FORMAT_CHARACTER + [...characters, ...packSwitches(bits)].map((x) => DIGITS[x]).join("");
}

/** Reads settings, or null for anything that is not settings of this format. */
export function parseSettings(text) {
    if (typeof text !== "string" || text[0] !== FORMAT_CHARACTER || text.length !== 1 + CHARACTER_COUNT + Math.ceil(BIT_COUNT / 6)) return null;
    const values = [...text.slice(1)].map((x) => DIGITS.indexOf(x));
    if (values.some((x) => x < 0)) return null;

    let at = 0;
    const next = () => values[at++];
    const unconventionality = next();
    const facets = FACETS.map(() => next());
    const tempo = next();
    const swing = next();
    const volume = next();
    const mixes = FORMAT_PARTS.map(() => ({ volume: next(), pan: next() }));
    const groupVolumes = DRUM_GROUPS.map(() => next());
    const bits = values.slice(at).flatMap((x) => [0, 1, 2, 3, 4, 5].map((bit) => ((x >> bit) & 1) === 1));

    let b = 0;
    const bit = () => bits[b++];
    const read = (count) => Array.from({ length: count }, (_, i) => (bit() ? 1 << i : 0)).reduce((sum, x) => sum + x, 0);
    const isGiven = GIVEN.map(() => bit());
    const key = read(KEY_BITS);
    const meter = read(METER_BITS);
    const mode = read(MODE_BITS);
    const settings = {
        unconventionality: { isGiven: isGiven[0], value: unconventionality },
        facets: Object.fromEntries(FACETS.map((facet, i) => [facet, { isGiven: isGiven[1 + i], value: facets[i] }])),
        tempo: { isGiven: isGiven[GIVEN.indexOf("tempo")], value: tempo },
        key: { isGiven: isGiven[GIVEN.indexOf("key")], value: key },
        meter: { isGiven: isGiven[GIVEN.indexOf("meter")], value: meter },
        mode: { isGiven: isGiven[GIVEN.indexOf("mode")], value: mode },
        swing: { isGiven: isGiven[GIVEN.indexOf("swing")], value: swing },
        parts: {},
        drumGroups: Object.fromEntries(DRUM_GROUPS.map((group, i) => [group, { volume: groupVolumes[i] }])),
        volume,
    };
    for (const [i, name] of FORMAT_PARTS.entries()) {
        const plays = PLAYS[read(PLAYS_BITS)];
        const pan = { isGiven: bit(), value: mixes[i].pan };
        const instrumentGiven = bit();
        if (plays === undefined) return null;
        settings.parts[name] = { plays, instrument: { isGiven: instrumentGiven, value: read(PROGRAM_BITS) }, volume: mixes[i].volume, pan };
    }
    const piecesGiven = bit();
    const pieces = DRUM_PIECES.filter(() => bit());
    settings.drumPieces = { isGiven: piecesGiven, value: piecesGiven ? pieces : [] };
    // a key is one of twelve, the meters' and the scales' options the server's, and pieces given at least one
    if (key > 11 || (piecesGiven && pieces.length === 0)) return null;
    return settings;
}

/**
 *     What of the settings names the song, as its seed does, whatever changes its notes: the switches of which values
 *     are given, six to a character, the values of those given alone, every part's plays, and the drum pieces given, six
 *     to a character behind a 1, with the zeros at its end left off; "" where nothing is given.
 *     The server works it out the same way (SongSettings.Identity).
 */
export function identityOf(settings) {
    const given = GIVEN.map((name) => givenSetting(settings, name));
    const values = packSwitches(given.map((x) => x.isGiven));
    values.push(...given.filter((x) => x.isGiven).map((x) => x.value));
    values.push(...FORMAT_PARTS.map((name) => PLAYS.indexOf(settings.parts[name].plays)));
    if (settings.drumPieces.isGiven) values.push(1, ...packSwitches(DRUM_PIECES.map((piece) => settings.drumPieces.value.includes(piece))));
    return values.map((x) => DIGITS[x]).join("").replace(/0+$/, "");
}
