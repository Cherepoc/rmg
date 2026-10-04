import PRESETS from "./genres.json";

/**
 *     Genres as the page has them: the presets it ships (genres.json), and those made here, which this browser keeps. A
 *     genre is a name and what it says of the choices the songs draw (/api/genres/choices), every option's amounts whole
 *     percents, one alike at every end or three, at the plain end, in the middle and at the wild; the server knows the
 *     choices, never a genre.
 */
export { PRESETS };

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
