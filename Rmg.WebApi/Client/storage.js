const DATABASE = "rmg";
const STORE = "soundfonts";
const KEY = "current";
const SETTING = "rmg.keep-soundfont";
const AUTOPLAY = "rmg.autoplay";
const HISTORY = "rmg.history";
const RATINGS = "rmg.ratings";
const UNCONVENTIONALITY = "rmg.unconventionality";

/** How many ratings the browser keeps, the oldest let go of first. */
const RATINGS_KEPT = 500;

export const isKeeping = () => readFlag(SETTING);
export const setKeeping = (isOn) => writeFlag(SETTING, isOn);
export const isAutoplaying = () => readFlag(AUTOPLAY);
export const setAutoplaying = (isOn) => writeFlag(AUTOPLAY, isOn);

/**
 *     A setting kept as the absence of an opt-out, so it stays on for a first visit and for a browser
 *     that refuses local storage altogether.
 */
function readFlag(key) {
    try {
        return localStorage.getItem(key) !== "off";
    } catch {
        return true;
    }
}

function writeFlag(key, isOn) {
    try {
        if (isOn) localStorage.removeItem(key);
        else localStorage.setItem(key, "off");
    } catch {
        // a browser that will not remember the setting simply starts each visit with it on
    }
}

/** A seed as the page keeps it: the digits the server reported, and nothing that is not one. */
export const IS_SEED = /^-?\d{1,10}$/;

/**
 *     A song as the page keeps it: its seed, and, where it was asked for rather than drawn, its unconventionality's
 *     step after a "u", which names the song as much as the seed does: "12345" or "12345u64".
 */
export const IS_SONG = /^-?\d{1,10}(u\d{1,3})?$/;

/**
 *     The unconventionality the next songs are asked for with, as a step from 0 to 127, or null for each to draw its
 *     own, which is how a first visit starts.
 */
export function recallUnconventionality() {
    try {
        const step = Number(localStorage.getItem(UNCONVENTIONALITY));
        return localStorage.getItem(UNCONVENTIONALITY) !== null && Number.isInteger(step) && step >= 0 && step <= 127 ? step : null;
    } catch {
        return null;
    }
}

export function keepUnconventionality(step) {
    try {
        if (step === null) localStorage.removeItem(UNCONVENTIONALITY);
        else localStorage.setItem(UNCONVENTIONALITY, String(step));
    } catch {
        // a browser that will not remember it lets every visit start with songs drawing their own
    }
}

/**
 *     The songs this browser has had, newest first. They go to localStorage rather than to the database
 *     next door, being a short list of numbers that the page wants before anything else it draws.
 */
export function recallHistory() {
    try {
        const kept = JSON.parse(localStorage.getItem(HISTORY) ?? "[]");

        // whatever else may be under that name is not this list, and none of it is asked for
        return Array.isArray(kept) ? kept.filter((song) => typeof song === "string" && IS_SONG.test(song)) : [];
    } catch {
        return [];
    }
}

export function keepHistory(songs) {
    try {
        if (songs.length === 0) localStorage.removeItem(HISTORY);
        else localStorage.setItem(HISTORY, JSON.stringify(songs));
    } catch {
        // a browser that will not remember the list simply shows what this visit has had
    }
}

/**
 *     How this browser rated a song, "up", "down" or null, by the songs' version and the song (IS_SONG), since the
 *     same seed is another song in another version, and at another given unconventionality.
 */
export function recallRating(version, song) {
    const rating = readRatings()[`${version}:${song}`];
    return rating === "up" || rating === "down" ? rating : null;
}

export function keepRating(version, song, rating) {
    try {
        const ratings = readRatings();
        const key = `${version}:${song}`;
        delete ratings[key];
        if (rating !== null) ratings[key] = rating;

        // insertion order is age order, so the oldest go first
        const kept = Object.entries(ratings).slice(-RATINGS_KEPT);
        localStorage.setItem(RATINGS, JSON.stringify(Object.fromEntries(kept)));
    } catch {
        // a browser that will not remember a rating still sends it
    }
}

function readRatings() {
    try {
        const kept = JSON.parse(localStorage.getItem(RATINGS) ?? "{}");
        return kept !== null && typeof kept === "object" && !Array.isArray(kept) ? kept : {};
    } catch {
        return {};
    }
}

/**
 *     Soundfonts run to hundreds of megabytes, which is far past what localStorage takes, so the
 *     bytes go to IndexedDB and only the opt-out lives in localStorage.
 */
function open() {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(DATABASE, 1);
        request.onupgradeneeded = () => request.result.createObjectStore(STORE);
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
        request.onblocked = () => reject(new Error("close other RMG tabs and try again"));
    });
}

async function withStore(mode, action) {
    const database = await open();
    try {
        return await new Promise((resolve, reject) => {
            const transaction = database.transaction(STORE, mode);
            const request = action(transaction.objectStore(STORE));
            transaction.oncomplete = () => resolve(request?.result ?? null);
            transaction.onerror = () => reject(transaction.error);
            transaction.onabort = () => reject(transaction.error ?? new Error("saving was cancelled"));
        });
    } finally {
        database.close();
    }
}

/**
 *     Asks for storage that is not cleared under pressure. Only ever when somebody has just ticked the
 *     box: Firefox answers with a permission prompt, which a save nobody asked for has no business
 *     raising. Asking is enough: a browser that says no still stores, it just may evict under pressure.
 */
export async function askToPersist() {
    try {
        await navigator.storage?.persist?.();
    } catch {
        // not worth reporting, the soundfont is stored either way
    }
}

export async function keep(name, soundFont) {
    await withStore("readwrite", (store) => store.put({ name, soundFont }, KEY));
}

export async function recall() {
    return await withStore("readonly", (store) => store.get(KEY));
}

export async function forget() {
    await withStore("readwrite", (store) => store.delete(KEY));
}
