/**
 *     What a song was heard with, written short enough for a link: its unconventionality, drawn or given, and its mix.
 *     The server reads the same format (SongSettings), which a rating and a link carry. Every value is 7 bits, as
 *     MIDI's are, and every switch 1, in a number written as a format's character and 43 letters and digits.
 */

const FORMAT_ONE = "1";
const DIGITS = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
const LENGTH = 43;
export const CHANNEL_COUNT = 16;

/** The last step of a value: an unconventionality's, the wildest, and a volume's, the loudest. */
export const LAST_STEP = 127;

// the unconventionality and whether it was given, the song's volume, each channel's instrument, whether it is on and
// its volume, and a bit to spare
const BITS = 7 + 1 + 7 + CHANNEL_COUNT * (7 + 1 + 7) + 1;

/**
 *     The settings as text. <c>channels</c> is every channel's, from the first, as
 *     <c>{ instrument, isEnabled, volume }</c>.
 */
export function formatSettings({ unconventionality, isGiven, songVolume, channels }) {
    if (channels.length !== CHANNEL_COUNT) throw new RangeError(`Settings are of ${CHANNEL_COUNT} channels.`);

    let number = 0n;
    const put = (value, bits) => {
        if (!Number.isInteger(value) || value < 0 || value >= 2 ** bits) throw new RangeError(`${value} does not fit in ${bits} bits.`);
        number = (number << BigInt(bits)) | BigInt(value);
    };

    put(unconventionality, 7);
    put(isGiven ? 1 : 0, 1);
    put(songVolume, 7);
    for (const channel of channels) {
        put(channel.instrument, 7);
        put(channel.isEnabled ? 1 : 0, 1);
        put(channel.volume, 7);
    }
    put(0, 1);

    const digits = [];
    for (let i = 0; i < LENGTH; i++) {
        digits.push(DIGITS[Number(number % 62n)]);
        number /= 62n;
    }
    return FORMAT_ONE + digits.reverse().join("");
}

/** Reads settings, or null for anything that is not settings of a known format. */
export function parseSettings(text) {
    if (typeof text !== "string" || text.length !== 1 + LENGTH || text[0] !== FORMAT_ONE) return null;

    let number = 0n;
    for (const digit of text.slice(1)) {
        const value = DIGITS.indexOf(digit);
        if (value < 0) return null;
        number = number * 62n + BigInt(value);
    }
    if (number >> BigInt(BITS) !== 0n || (number & 1n) !== 0n) return null;

    let position = BITS;
    const take = (bits) => {
        position -= bits;
        return Number((number >> BigInt(position)) & ((1n << BigInt(bits)) - 1n));
    };

    const unconventionality = take(7);
    const isGiven = take(1) === 1;
    const songVolume = take(7);
    const channels = [];
    for (let i = 0; i < CHANNEL_COUNT; i++) channels.push({ instrument: take(7), isEnabled: take(1) === 1, volume: take(7) });

    return { unconventionality, isGiven, songVolume, channels };
}

/** A value from 0 to 1 as its step, which is how a volume is kept. */
export function toStep(value) {
    return Math.round(Math.min(Math.max(value, 0), 1) * LAST_STEP);
}
