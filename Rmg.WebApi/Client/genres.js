import PRESET_FILE from "./genres.json";
import { MAX_GENRE_NAME_BYTES } from "./settings.js";

/**
 *     Genres as the page has them: the presets it ships (genres.json), and those made here, which this browser keeps. A
 *     genre is a name and what it says of the choices the songs draw (/api/genres/choices), every option's amounts whole
 *     percents, one alike at every end or three, at the plain end, in the middle and at the wild; the server knows the
 *     choices, never a genre.
 */
const isFamily = (x) => typeof x === "object" && x !== null && !Array.isArray(x);

export const PRESETS = PRESET_FILE.map((preset) => ({ ...preset, choices: flatChoices(preset.choices) }));

/**
 *     A genre's choices as the page and the server name them, from those a file writes in families: an entry's member
 *     that is itself options is a family's choice, "rhythm": { "ground": { ... } } the choice "rhythm.ground", and any
 *     other member an option of the entry's own choice, so that "feel" holds the song's feel and the parts' feels both;
 *     a file's choices already flat, as an older export writes them, as they are.
 */
export function flatChoices(choices) {
    const flat = {};
    for (const [name, members] of Object.entries(choices)) {
        // a choice keeping none of its options, which is no genre's, kept as it is to be told so
        if (Object.keys(members).length === 0) flat[name] = {};
        for (const [member, value] of Object.entries(members)) {
            if (isFamily(value)) flat[`${name}.${member}`] = value;
            else (flat[name] ??= {})[member] = value;
        }
    }
    return flat;
}

/** A genre's choices as a file writes them, in families (flatChoices), in the order given, the editor's. */
export function nestedChoices(choices, order) {
    const nested = {};
    for (const name of [...order.filter((x) => x in choices), ...Object.keys(choices).filter((x) => !order.includes(x))]) {
        const [family, member] = name.split(".");
        const entry = (nested[family] ??= {});
        if (member === undefined) Object.assign(entry, choices[name]);
        else entry[member] = choices[name];
    }
    return nested;
}

/**
 *     The preset a genre is, as a link names it by its id, such as "rock", in place of the server's key; none for a genre
 *     that is no preset, or one changed from it. An id is letters alone and shorter than a key, so that it is never one.
 */
export function presetOf(genre) {
    return PRESETS.find((preset) => isSameGenre(asGenre(preset), genre)) ?? null;
}

/** The preset a link names by its id, none for anything else. */
export function presetById(id) {
    return PRESETS.find((preset) => preset.id === id) ?? null;
}

/** Whether a name is a preset's, whatever its case and spacing, which no genre of one's own is saved under. */
export function isPresetName(name) {
    return PRESETS.some((preset) => sameName(preset.name, name));
}

/** Genres in the order they are listed: by name, whatever its case, a number in it as a number, "Rock 2" before "Rock 10". */
export const byName = (first, second) => first.name.localeCompare(second.name, undefined, { sensitivity: "base", numeric: true });

/** Whether two genres' names are one, whatever their case and spacing, as no two genres are named. */
export function sameName(first, second) {
    const plain = (x) => x.trim().replace(/\s+/g, " ").toLowerCase();
    return plain(first) === plain(second);
}

/** A genre as the settings carry it, a copy of the one given. */
export function asGenre(genre) {
    return { name: genre.name, choices: structuredClone(genre.choices) };
}

/** Whether two genres say the same of every choice, whatever the order they name them in. */
export function isSameGenre(first, second) {
    return first.name === second.name && sameChoices(first.choices, second.choices);
}

function sameChoices(first, second) {
    const choices = new Set([...Object.keys(first), ...Object.keys(second)]);
    for (const choice of choices) {
        // a choice named, even as {} (every option never), is another genre than one leaving it to the songs
        if ((choice in first) !== (choice in second)) return false;
        const [a, b] = [first[choice], second[choice]];
        const options = new Set([...Object.keys(a), ...Object.keys(b)]);
        for (const option of options) if (!sameAmount(a[option], b[option])) return false;
    }
    return true;
}

/** Whether two amounts are alike: by their three ends, one written alike at every end as three, anything else as written. */
function sameAmount(first, second) {
    const amount = (x) => typeof x === "number" || (Array.isArray(x) && x.length === 3 && x.every((y) => typeof y === "number"));
    return amount(first) && amount(second)
        ? endsOf(first).every((x, i) => x === endsOf(second)[i])
        : JSON.stringify(first) === JSON.stringify(second);
}

/** Whether settings carry a genre: a name or a choice it names. */
export function hasAGenre(genre) {
    return genre.name.length > 0 || Object.keys(genre.choices).length > 0;
}

/** An option's amounts at its three ends, plainest, middling and wildest, from a genre's one alike at every end or three. */
export const endsOf = (amount) => (Array.isArray(amount) ? amount : [amount, amount, amount]);

/** Amounts as a genre writes them: one where they are alike, three otherwise. */
export const written = ([plain, middle, wild]) => (plain === middle && middle === wild ? middle : [plain, middle, wild]);

/** An option's amounts as the songs have them (/api/genres/choices), in the whole numbers a genre gives. */
export const ownOf = (option) => written(option.own.map((x) => Math.round(x)));

/** What a file that holds no genre is told. */
export const NOT_A_GENRE = "That file is not a genre: a name and the choices the editor knows, or a list of them.";

/**
 *     The genres a JSON file holds, a list of them or one, every one a name, choices the server knows (by `knows`, a
 *     choice's name and an option's of it) and a description if any, every option an amount or three, whole and not
 *     below 0, its choices in families or flat as an older export writes them; null for a file that is no genre nor a
 *     list of them.
 */
export function readGenreFile(text, knows) {
    let read;
    try {
        read = JSON.parse(text);
    } catch {
        return null;
    }
    // a choice that is no family of options, such as null or a number, no genre's
    const fromFile = (x) => (typeof x?.choices === "object" && x.choices !== null
        ? Object.values(x.choices).every(isFamily) ? { ...x, choices: flatChoices(x.choices) } : null
        : x);
    // an amount a whole number from 0, a range's lean from -100 to 100 and its ends any whole number, a mood's below 0
    // too (GenreAmounts.RangeOf), the server checking the rest
    const isAmount = (option) => (x) => Number.isInteger(x) && (option === "lean" ? x >= -100 && x <= 100 : option === "from" || option === "to" || x >= 0);
    const isOptions = (choice, options) => typeof options === "object" && options !== null && !Array.isArray(options)
        && Object.entries(options).every(([option, amount]) => knows(choice, option)
            && (isAmount(option)(amount) || Array.isArray(amount) && amount.length === 3 && amount.every(isAmount(option))));
    const isGenre = (x) => typeof x?.name === "string" && typeof x?.choices === "object" && x.choices !== null
        && Object.entries(x.choices).every(([choice, options]) => knows(choice) && isOptions(choice, options)) && (x.description === undefined || typeof x.description === "string");
    const parsed = Array.isArray(read) ? read.map(fromFile) : fromFile(read);
    if (Array.isArray(parsed)) return parsed.length > 0 && parsed.every(isGenre) ? { genres: parsed } : null;
    return isGenre(parsed) ? { genre: parsed } : null;
}

/**
 *     A JSON file imported, as both the editor and the manager read it: its genres (readGenreFile), and the first of them
 *     the server would refuse, said by its name and its problem, or null; null for a file that is no genre.
 */
export function readImport(text, knows, choiceOf) {
    const read = readGenreFile(text, knows);
    if (read === null) return null;
    const refused = (read.genres ?? [read.genre]).find((x) => genreProblem(x, choiceOf) !== null);
    return { ...read, refusal: refused === undefined ? null : `${refused.name}: ${genreProblem(refused, choiceOf)}` };
}

/**
 *     What the server would refuse a genre for, said as the page says it, or null: a name too long, or a pool keeping no
 *     option, or none that weighs anything at any end; by the server's choices, each its label and kind, by its name.
 */
export function genreProblem(genre, choiceOf) {
    if (new TextEncoder().encode(genre.name).length > MAX_GENRE_NAME_BYTES) return "The name is too long.";
    const weighs = (amount) => endsOf(amount).some((x) => typeof x === "number" && x > 0);
    const empty = Object.entries(genre.choices).find(([name, options]) => choiceOf(name)?.kind === "pool" && !Object.values(options).some(weighs));
    return empty ? `${poolName(empty[0], choiceOf)} keeps none of its options.` : null;
}

/** A pool as a problem names it: a part's by its part and what it chooses, such as "Melody's feel". */
function poolName(choiceName, choiceOf) {
    const label = choiceOf(choiceName).label;
    const of = { instrument: "instruments", feel: "feel", drumRole: "drums", figure: "figures" }[choiceName.split(".")[0]];
    return choiceName.includes(".") ? `${label}'s ${of}` : label;
}
