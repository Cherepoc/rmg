import PRESET_FILE from "./genres.json";

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
        const [a, b] = [first[choice] ?? {}, second[choice] ?? {}];
        const options = new Set([...Object.keys(a), ...Object.keys(b)]);
        for (const option of options) if (JSON.stringify(a[option]) !== JSON.stringify(b[option])) return false;
    }
    return true;
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
