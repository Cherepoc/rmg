const DATABASE = "rmg";
const STORE = "soundfonts";
const KEY = "current";
const SETTING = "rmg.keep-soundfont";
const AUTOPLAY = "rmg.autoplay";
const HISTORY = "rmg.songs";
const RATINGS = "rmg.song-ratings";
const SETTINGS = "rmg.settings";
const GENRES = "rmg.genres";
const GENRE = "rmg.genre";
const ANY_GENRE = "rmg.anyGenre";
const GENRE_COPIES = "rmg.genre-copies";
const GENRE_PRINCIPLES = "rmg.genre-principles";

/** How many ratings the browser keeps, the oldest let go of first. */
const RATINGS_KEPT = 500;

export const isKeeping = () => readFlag(SETTING);
export const setKeeping = (isOn) => writeFlag(SETTING, isOn);
export const isAutoplaying = () => readFlag(AUTOPLAY);
export const setAutoplaying = (isOn) => writeFlag(AUTOPLAY, isOn);
export const isShowingGenrePrinciples = () => readFlag(GENRE_PRINCIPLES);
export const setShowingGenrePrinciples = (isOn) => writeFlag(GENRE_PRINCIPLES, isOn);

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

/**
 *     A song of the list as the page keeps it: its seed and the settings it was asked for with, in letters and digits,
 *     and the key of the genre it was made in, where it was, "12Ab:6…:Xy3…", which is what asking for it again takes; whether its settings are of a format the page reads is the
 *     page's to say (settings.js), as the format changes.
 */
export const IS_SONG = /^[0-9A-Za-z_-]{1,11}:[0-9A-Za-z_-]+(:[0-9A-Za-z_-]{11})?$/;

/** The settings the next songs are asked for with (settings.js), as this browser last left them; null for none yet. */
export function recallSettings() {
    try {
        return localStorage.getItem(SETTINGS);
    } catch {
        return null;
    }
}

/** The genres made in this browser (genres.js), each a name and what it says of the choices; none for none. */
export function recallGenres() {
    try {
        const kept = JSON.parse(localStorage.getItem(GENRES) ?? "[]");
        return Array.isArray(kept) ? kept.filter((x) => typeof x?.name === "string" && typeof x?.choices === "object" && x.choices !== null) : [];
    } catch {
        return [];
    }
}

export function keepGenres(genres) {
    try {
        if (genres.length === 0) localStorage.removeItem(GENRES);
        else localStorage.setItem(GENRES, JSON.stringify(genres));
    } catch {
        // a browser that will not remember them keeps them for this visit only
    }
}

export function keepSettings(settings) {
    try {
        localStorage.setItem(SETTINGS, settings);
    } catch {
        // a browser that will not remember them starts every visit with every value drawn
    }
}

/**
 *     The genre the next songs are asked for in: a preset by its id ({ preset }), so that a preset tuned since is heard as
 *     it is now, or a name and what it says of the choices; null for none.
 */
export function recallGenre() {
    try {
        const kept = JSON.parse(localStorage.getItem(GENRE) ?? "null");
        if (typeof kept?.preset === "string") return { preset: kept.preset };
        return typeof kept?.name === "string" && typeof kept?.choices === "object" && kept.choices !== null ? kept : null;
    } catch {
        return null;
    }
}

/** The genres songs of the list were made in, by their keys (/api/genres/{key}), a copy kept here; none for none. */
export function recallGenreCopies() {
    try {
        const kept = JSON.parse(localStorage.getItem(GENRE_COPIES) ?? "{}");
        if (kept === null || typeof kept !== "object" || Array.isArray(kept)) return {};
        // every copy a genre, as the others are read, one of another shape left out
        return Object.fromEntries(Object.entries(kept).filter(([, x]) => typeof x?.name === "string" && typeof x?.choices === "object" && x.choices !== null));
    } catch {
        return {};
    }
}

/** Keeps the genres songs of the list were made in, by their keys, those of no song on it let go of. */
export function keepGenreCopies(copies) {
    try {
        if (Object.keys(copies).length === 0) localStorage.removeItem(GENRE_COPIES);
        else localStorage.setItem(GENRE_COPIES, JSON.stringify(copies));
    } catch {
        // a browser that will not remember them asks the server for a song's genre when the song is played again
    }
}

/** Whether every new song is asked for in a preset drawn for it, as the list's Any genre has them. */
export function recallAnyGenre() {
    try {
        return localStorage.getItem(ANY_GENRE) === "true";
    } catch {
        return false;
    }
}

/** Keeps whether every new song is asked for in a preset drawn for it. */
export function keepAnyGenre(any) {
    try {
        if (any) localStorage.setItem(ANY_GENRE, "true");
        else localStorage.removeItem(ANY_GENRE);
    } catch {
        // a browser that will not remember it asks the next visit's songs in the genre kept
    }
}

/** Keeps the genre the next songs are asked for in, a preset by its id ({ preset }), none for no genre. */
export function keepGenre(genre) {
    try {
        if (genre === null) localStorage.removeItem(GENRE);
        else localStorage.setItem(GENRE, JSON.stringify(genre));
    } catch {
        // a browser that will not remember it asks for the next visit's songs in no genre
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
 *     How this browser rated a song, "up", "down" or null, by the songs' version and the song's name, its seed and
 *     what of its settings names it too, since the same seed is another song in another version, and with other
 *     settings.
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
