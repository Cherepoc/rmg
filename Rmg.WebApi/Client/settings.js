/**
 *     What a song is heard with, written short enough for a link: its unconventionality and facets, its tempo, key, meter,
 *     scale, swing and length, its parts and their mix, its drum groups and drum pieces, and its volume, each value given,
 *     or as the song drew it; a genre is kept apart from them. The server reads the same format (SongSettings), which a
 *     link and every event about a song carry: a format's character, then a character for every value of 62 steps at a
 *     fixed place, then every other value as its bits, one after another, five to a character. Seeds are written in the
 *     same 62 digits, the letters and digits alone, which a chat's markdown leaves as they are.
 */

/** The format this writes, as its first character; one that is read differently takes the next. */
const FORMAT_CHARACTER = "B";

/** The 62 digits: the digits, the capitals and the small letters (the server's Base62). */
const DIGITS = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

/** The 64 digits seeds were written in before, which a link of an older format carries its seed in. */
const OLD_DIGITS = `${DIGITS}-_`;

/** How many bits a character of the settings carries, from 0 to V. */
const BITS_PER_CHARACTER = 5;

/** The last step of a value: the most experimental, the loudest, the right. */
export const LAST_STEP = 61;

/**
 *     The middle step, 31 below it and 30 above, as MIDI's pan has one more below, so that a value can be given at its
 *     middle: 50%, a pan in the middle, a tempo of 120 beats a minute (the server's StepScale).
 */
export const MIDDLE_STEP = 31;

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

/** The parts the songs play, every one, in the order the format keeps a place for each. */
export const PARTS = ["melody", "chords", "bass", "pad", "counterMelody", "drum", "riff", "rhythm", "solo"];

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
 *     meter, the scale, called its mode beside the facet of that name, the swing and the length.
 */
const GIVEN = ["unconventionality", ...FACETS, "tempo", "key", "meter", "mode", "swing", "length"];

// the bits of a key, of twelve, of a meter and of a scale, of up to sixteen options each, of a part's plays, of three,
// and of a program, of 128
const KEY_BITS = 4;
const METER_BITS = 4;
const MODE_BITS = 4;
const PLAYS_BITS = 2;
const PROGRAM_BITS = 7;

// the values of 62 steps, a character each: the unconventionality, the facets, the tempo, the swing, the length, the volume, every
// part's volume and pan, and every drum group's volume; then the rest, bit by bit: whether each value is given, the key,
// the meter and the scale, every part's plays, whether its pan and its instrument are given and its program, and whether
// the drum pieces are given and every piece's being in
const CHARACTER_COUNT = 1 + FACETS.length + 1 + 1 + 1 + 1 + 2 * PARTS.length + DRUM_GROUPS.length;
const BIT_COUNT = GIVEN.length + KEY_BITS + METER_BITS + MODE_BITS + PARTS.length * (PLAYS_BITS + 2 + PROGRAM_BITS) + 1 + DRUM_PIECES.length;

// how many characters the fixed places take, behind the format's
const FIXED_LENGTH = CHARACTER_COUNT + Math.ceil(BIT_COUNT / BITS_PER_CHARACTER);

// and the mood behind them: its darkness's and its intensity's values and whether each is given, a character's two bits,
// none in settings written before it, their mood drawn (the server's SongSettings.MoodLength)
const MOOD_LENGTH = 3;

const SEED_LENGTH = 11;

function encode(number, length) {
    const digits = [];
    for (let i = 0; i < length; i++) {
        digits.push(DIGITS[Number(number % 62n)]);
        number /= 62n;
    }
    return digits.reverse().join("");
}

function decode(text, digits) {
    let number = 0n;
    for (const digit of text) {
        const value = digits.indexOf(digit);
        if (value < 0) return null;
        number = number * BigInt(digits.length) + BigInt(value);
    }
    return number;
}

/**
 *     A seed as the page keeps it: its 62 digits, all 64 of its bits, with no zeros in front, which is how the server
 *     writes it; null for anything that is not a seed. A seed never becomes a JavaScript number, which holds 53 bits
 *     exactly and no more.
 */
export function readSeed(text) {
    return readSeedIn(text, DIGITS);
}

/**
 *     The seed of a link: in the 64 digits seeds were written in before, where the link's settings are of an older
 *     format, so that an old link plays its own song and never another; as readSeed otherwise.
 */
export function readLinkedSeed(text, settingsText) {
    return readSeedIn(text, settingsText !== null && settingsText[0] !== FORMAT_CHARACTER ? OLD_DIGITS : DIGITS);
}

function readSeedIn(text, digits) {
    if (typeof text !== "string" || text.length === 0 || text.length > SEED_LENGTH) return null;
    const number = decode(text, digits);
    return number !== null && number <= 0xFFFFFFFFFFFFFFFFn ? encode(number, SEED_LENGTH).replace(/^0+(?=.)/, "") : null;
}

function givenSetting(settings, name) {
    return FACETS.includes(name) ? settings.facets[name] : settings[name];
}

/** How many of the values the link keeps are given, the parts and the drum pieces aside. */
export function givenCount(settings) {
    return GIVEN.filter((name) => givenSetting(settings, name).isGiven).length;
}

/** Five switches a character, the first the lowest bit. */
function packSwitches(switches) {
    const values = [];
    for (let i = 0; i < switches.length; i += BITS_PER_CHARACTER)
        values.push(switches.slice(i, i + BITS_PER_CHARACTER).reduce((sum, isOn, bit) => sum + (isOn ? 1 << bit : 0), 0));
    return values;
}

/** A genre's name's longest, in bytes. */
export const MAX_GENRE_NAME_BYTES = 63;

/** The settings as text, every facet, part, drum group and drum piece named, as parseSettings reads them. */
export function formatSettings(settings) {
    const characters = [settings.unconventionality.value, ...FACETS.map((facet) => settings.facets[facet].value), settings.tempo.value, settings.swing.value, settings.length.value, settings.volume];
    for (const name of PARTS) characters.push(settings.parts[name].volume, settings.parts[name].pan.value);
    for (const group of DRUM_GROUPS) characters.push(settings.drumGroups[group].volume);
    if (characters.some((x) => !Number.isInteger(x) || x < 0 || x > LAST_STEP)) throw new RangeError("A value does not fit in a character.");

    const bits = GIVEN.map((name) => givenSetting(settings, name).isGiven);
    const write = (value, count) => {
        if (!Number.isInteger(value) || value < 0 || value >= 1 << count) throw new RangeError(`${value} does not fit in ${count} bits.`);
        for (let bit = 0; bit < count; bit++) bits.push(((value >> bit) & 1) === 1);
    };
    write(settings.key.value, KEY_BITS);
    write(settings.meter.value, METER_BITS);
    write(settings.mode.value, MODE_BITS);
    for (const name of PARTS) {
        const part = settings.parts[name];
        write(PLAYS.indexOf(part.plays), PLAYS_BITS);
        bits.push(part.pan.isGiven, part.instrument.isGiven);
        write(part.instrument.value, PROGRAM_BITS);
    }
    bits.push(settings.drumPieces.isGiven, ...DRUM_PIECES.map((piece) => settings.drumPieces.value.includes(piece)));
    const mood = [settings.darkness.value, settings.intensity.value, (settings.darkness.isGiven ? 1 : 0) + (settings.intensity.isGiven ? 2 : 0)];
    if (mood.some((x) => !Number.isInteger(x) || x < 0 || x > LAST_STEP)) throw new RangeError("A mood does not fit in a character.");
    return FORMAT_CHARACTER + [...characters, ...packSwitches(bits), ...mood].map((x) => DIGITS[x]).join("");
}

/** Reads settings, with no genre, or null for anything that is not settings of this format. */
export function parseSettings(text) {
    if (typeof text !== "string" || text[0] !== FORMAT_CHARACTER || (text.length !== 1 + FIXED_LENGTH && text.length !== 1 + FIXED_LENGTH + MOOD_LENGTH)) return null;
    // its mood where it has one, as written, and drawn where it has none
    const mood = [...text.slice(1 + FIXED_LENGTH)].map((x) => DIGITS.indexOf(x));
    if (mood.length > 0 && (mood.slice(0, 2).some((x) => x < 0 || x > LAST_STEP) || mood[2] < 0 || mood[2] > 3)) return null;
    const [darkness, intensity] = mood.length > 0
        ? [{ isGiven: (mood[2] & 1) === 1, value: mood[0] }, { isGiven: (mood[2] & 2) === 2, value: mood[1] }]
        : [{ isGiven: false, value: MIDDLE_STEP }, { isGiven: false, value: MIDDLE_STEP }];
    const values = [...text.slice(1, 1 + FIXED_LENGTH)].map((x) => DIGITS.indexOf(x));
    // a value no more than the last step, and a character of bits no more than five of them, its padding zeros, so that
    // settings are written one way only, as the server reads them
    const used = BIT_COUNT - (FIXED_LENGTH - CHARACTER_COUNT - 1) * BITS_PER_CHARACTER;
    if (values.some((x, i) => x < 0 || x > (i < CHARACTER_COUNT ? LAST_STEP : (1 << BITS_PER_CHARACTER) - 1)) || values[values.length - 1] >> used !== 0)
        return null;

    let at = 0;
    const next = () => values[at++];
    const unconventionality = next();
    const facets = FACETS.map(() => next());
    const tempo = next();
    const swing = next();
    const length = next();
    const volume = next();
    const mixes = PARTS.map(() => ({ volume: next(), pan: next() }));
    const groupVolumes = DRUM_GROUPS.map(() => next());
    const bits = values.slice(at).flatMap((x) => [0, 1, 2, 3, 4].map((bit) => ((x >> bit) & 1) === 1));

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
        length: { isGiven: isGiven[GIVEN.indexOf("length")], value: length },
        darkness,
        intensity,
        parts: {},
        drumGroups: Object.fromEntries(DRUM_GROUPS.map((group, i) => [group, { volume: groupVolumes[i] }])),
        volume,
        genre: { name: "", choices: {} },
    };
    for (const [i, name] of PARTS.entries()) {
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
 *     are given, five to a character, the values of those given alone, every part's plays, the drum pieces given, five
 *     to a character behind a 1, with the zeros at its end left off; "" where nothing is given.
 *     The server works it out the same way (SongSettings.Identity).
 */
export function identityOf(settings) {
    const given = GIVEN.map((name) => givenSetting(settings, name));
    const values = packSwitches(given.map((x) => x.isGiven));
    values.push(...given.filter((x) => x.isGiven).map((x) => x.value));
    values.push(...PARTS.map((name) => PLAYS.indexOf(settings.parts[name].plays)));
    if (settings.drumPieces.isGiven) values.push(1, ...packSwitches(DRUM_PIECES.map((piece) => settings.drumPieces.value.includes(piece))));
    // and the mood where it is given, behind a 2, so that a song given none keeps the name it had before
    const mood = [settings.darkness, settings.intensity];
    if (mood.some((x) => x.isGiven)) values.push(2, ...packSwitches(mood.map((x) => x.isGiven)), ...mood.filter((x) => x.isGiven).map((x) => x.value));
    return values.map((x) => DIGITS[x]).join("").replace(/0+$/, "");
}
