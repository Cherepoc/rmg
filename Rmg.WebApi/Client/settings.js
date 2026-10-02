/**
 *     What a song is heard with, written short enough for a link: its unconventionality and facets, its tempo, key and
 *     meter, its parts and their mix, its drum groups and drum pieces, and its volume, each value given, or as the song
 *     drew it. The server reads the same format (SongSettings), which a link and every event about a song carry: a
 *     format's character, then a character for every value of 64 steps at a fixed place, then every other value as its
 *     bits, one after another, six to a character. Seeds are written in the same 64 digits.
 */

const FORMAT_SIX = "6";

/**
 *     The format before, every value a character, a drum setup and every part's and drum group's being in the file among
 *     them, which it is read without.
 */
const FORMAT_FIVE = "5";

/** The format before that, which gave no drum pieces, read with the song's own. */
const FORMAT_FOUR = "4";

/** The format before, which gave a tempo by its place among those a song draws, read with its tempo as the song draws it. */
const FORMAT_THREE = "3";

/** The format before that, which named every part but the solo part, which it plays as the song draws it. */
const FORMAT_TWO = "2";
const PARTS_OF_FORMAT_TWO = 8;

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

// the drum setups a link of a format before may give, which the drum pieces make now
const DRUM_SETUPS = ["kit", "kitAndPercussion", "percussion"];
export const DRUM_GROUPS = ["kick", "snare", "timekeepers", "toms", "accents", "percussion", "calls"];

/** The drum pieces, by the names the server takes, in its order (DrumPieces.All), which the format keeps a switch for each of. */
export const DRUM_PIECES = [
    "kick", "acousticSnare", "electricSnare", "clap", "hiHat", "ride", "tambourine", "cabasa", "maracas", "tom", "cymbal", "vibraslap",
    "bongo", "conga", "timbale", "agogo", "cowbell", "claves", "woodBlock", "guiro", "triangle", "cuica", "whistle",
];

/** Whether a part plays: as the song draws it, given in, or given out. */
export const PLAYS = ["random", "on", "off"];

/** The values a song may be given that change its notes: the unconventionality, the facets, the tempo, the key and the meter. */
const GIVEN = ["unconventionality", ...FACETS, "tempo", "key", "meter"];

// the bits of a key, of twelve, of a meter, of up to sixteen options, of a part's plays, of three, and of a program, of 128
const KEY_BITS = 4;
const METER_BITS = 4;
const PLAYS_BITS = 2;
const PROGRAM_BITS = 7;

// the values of 64 steps, a character each: the unconventionality, the facets, the tempo, the volume, every part's volume
// and pan, and every drum group's volume; then the rest, bit by bit: whether each value is given, the key and the meter,
// every part's plays, whether its pan and its instrument are given and its program, and whether the drum pieces are given
// and every piece's being in
const CHARACTER_COUNT = 1 + FACETS.length + 1 + 1 + 2 * FORMAT_PARTS.length + DRUM_GROUPS.length;
const BIT_COUNT = GIVEN.length + KEY_BITS + METER_BITS + FORMAT_PARTS.length * (PLAYS_BITS + 2 + PROGRAM_BITS) + 1 + DRUM_PIECES.length;

// the length of a format before's settings
const lengthBefore = (parts, hasPieces) => GIVEN.length + 1 + 2 * parts + 1 + DRUM_GROUPS.length + 2 * parts
    + Math.ceil((GIVEN.length + 4 * parts + DRUM_GROUPS.length + (hasPieces ? 1 + DRUM_PIECES.length : 0)) / 6);
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
    const characters = [settings.unconventionality.value, ...FACETS.map((facet) => settings.facets[facet].value), settings.tempo.value, settings.volume];
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
    for (const name of FORMAT_PARTS) {
        const part = settings.parts[name];
        write(PLAYS.indexOf(part.plays), PLAYS_BITS);
        bits.push(part.pan.isGiven, part.instrument.isGiven);
        write(part.instrument.value, PROGRAM_BITS);
    }
    bits.push(settings.drumPieces.isGiven, ...DRUM_PIECES.map((piece) => settings.drumPieces.value.includes(piece)));
    return FORMAT_SIX + [...characters, ...packSwitches(bits)].map((x) => DIGITS[x]).join("");
}

/** Reads settings, or null for anything that is not settings of a format; the formats before too (parseBefore). */
export function parseSettings(text) {
    if (typeof text !== "string") return null;
    if (text[0] !== FORMAT_SIX) return parseBefore(text);
    if (text.length !== 1 + CHARACTER_COUNT + Math.ceil(BIT_COUNT / 6)) return null;
    const values = [...text.slice(1)].map((x) => DIGITS.indexOf(x));
    if (values.some((x) => x < 0)) return null;

    let at = 0;
    const next = () => values[at++];
    const unconventionality = next();
    const facets = FACETS.map(() => next());
    const tempo = next();
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
    const settings = {
        unconventionality: { isGiven: isGiven[0], value: unconventionality },
        facets: Object.fromEntries(FACETS.map((facet, i) => [facet, { isGiven: isGiven[1 + i], value: facets[i] }])),
        tempo: { isGiven: isGiven[GIVEN.length - 3], value: tempo },
        key: { isGiven: isGiven[GIVEN.length - 2], value: key },
        meter: { isGiven: isGiven[GIVEN.length - 1], value: meter },
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
    // a key is one of twelve, the meter's options the server's, and pieces given at least one
    if (key > 11 || (piecesGiven && pieces.length === 0)) return null;
    return settings;
}

/**
 *     Reads settings of a format before: a character for every value, the instruments' two each, and the switches after
 *     them, six to a character; their drum pieces, their tempo and the solo part the oldest does not name as the song draws
 *     them, and their drum setup and their being in the file let go of, as the drum pieces make the setup now.
 */
function parseBefore(text) {
    const count = { [FORMAT_FIVE]: FORMAT_PARTS.length, [FORMAT_FOUR]: FORMAT_PARTS.length, [FORMAT_THREE]: FORMAT_PARTS.length, [FORMAT_TWO]: PARTS_OF_FORMAT_TWO }[text[0]];
    const hasPieces = text[0] === FORMAT_FIVE;
    if (count === undefined || text.length !== 1 + lengthBefore(count, hasPieces)) return null;
    const named = FORMAT_PARTS.slice(0, count);
    const values = [...text.slice(1)].map((x) => DIGITS.indexOf(x));
    if (values.some((x) => x < 0)) return null;

    let at = 0;
    const next = () => values[at++];
    const given = GIVEN.map(() => next());
    const volume = next();
    const mixes = named.map(() => ({ volume: next(), pan: next() }));
    const setup = next();
    const groupVolumes = DRUM_GROUPS.map(() => next());
    const instruments = named.map(() => next() * 64 + next());
    const switches = values.slice(at).flatMap((x) => [0, 1, 2, 3, 4, 5].map((bit) => ((x >> bit) & 1) === 1));

    let s = 0;
    const settings = { facets: {}, parts: {}, drumGroups: {} };
    GIVEN.forEach((name, i) => {
        const setting = { isGiven: switches[s++], value: given[i] };
        if (FACETS.includes(name)) settings.facets[name] = setting;
        else settings[name] = setting;
    });
    for (const [i, name] of named.entries()) {
        const plays = PLAYS[(switches[s++] ? 2 : 0) + (switches[s++] ? 1 : 0)];
        // whether it was in the file, which every part now is
        s++;
        const pan = { isGiven: switches[s++], value: mixes[i].pan };
        if (plays === undefined || instruments[i] >= 256) return null;
        settings.parts[name] = { plays, instrument: { isGiven: instruments[i] >= 128, value: instruments[i] & 127 }, volume: mixes[i].volume, pan };
    }
    // a part the format did not name as the song draws it, at its fullest, in the file
    for (const name of FORMAT_PARTS.slice(count))
        settings.parts[name] = { plays: "random", instrument: { isGiven: false, value: 0 }, volume: LAST_STEP, pan: { isGiven: false, value: MIDDLE_STEP } };
    for (const [i, group] of DRUM_GROUPS.entries()) {
        // whether its drums were in the file, which every group's now are
        s++;
        settings.drumGroups[group] = { volume: groupVolumes[i] };
    }
    const piecesGiven = hasPieces && switches[s++];
    const pieces = DRUM_PIECES.filter(() => hasPieces && switches[s++]);
    settings.drumPieces = { isGiven: piecesGiven, value: piecesGiven ? pieces : [] };
    if (setup > DRUM_SETUPS.length || settings.key.value > 11 || (piecesGiven && pieces.length === 0)) return null;
    if (text[0] === FORMAT_THREE || text[0] === FORMAT_TWO) settings.tempo = { isGiven: false, value: MIDDLE_STEP };
    settings.volume = volume;
    return settings;
}

/**
 *     What of the settings names the song, as its seed does, whatever changes its notes: the switches of which values
 *     are given, six to a character, the values of those given alone, every part's plays, the drum setup, none now, and
 *     the drum pieces given, six to a character behind a 1, with the zeros at its end left off; "" where nothing is given.
 *     The server works it out the same way (SongSettings.Identity).
 */
export function identityOf(settings) {
    const given = GIVEN.map((name) => givenSetting(settings, name));
    const values = packSwitches(given.map((x) => x.isGiven));
    values.push(...given.filter((x) => x.isGiven).map((x) => x.value));
    values.push(...FORMAT_PARTS.slice(0, PARTS_OF_FORMAT_TWO).map((name) => PLAYS.indexOf(settings.parts[name].plays)));
    values.push(0);
    // a part the format before did not name after the drum setup, so that a song naming it as the song draws it is named as it was
    values.push(...FORMAT_PARTS.slice(PARTS_OF_FORMAT_TWO).map((name) => PLAYS.indexOf(settings.parts[name].plays)));
    if (settings.drumPieces.isGiven) values.push(1, ...packSwitches(DRUM_PIECES.map((piece) => settings.drumPieces.value.includes(piece))));
    return values.map((x) => DIGITS[x]).join("").replace(/0+$/, "");
}
