const DATABASE = "rmg";
const STORE = "soundfonts";
const KEY = "current";
const SETTING = "rmg.keep-soundfont";
const AUTOPLAY = "rmg.autoplay";
const HISTORY = "rmg.history";

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
 *     The songs this browser has had, newest first. They go to localStorage rather than to the database
 *     next door, being a short list of numbers that the page wants before anything else it draws.
 */
export function recallHistory() {
    try {
        const kept = JSON.parse(localStorage.getItem(HISTORY) ?? "[]");

        // whatever else may be under that name is not this list, and none of it is asked for
        return Array.isArray(kept) ? kept.filter((seed) => typeof seed === "string" && IS_SEED.test(seed)) : [];
    } catch {
        return [];
    }
}

export function keepHistory(seeds) {
    try {
        if (seeds.length === 0) localStorage.removeItem(HISTORY);
        else localStorage.setItem(HISTORY, JSON.stringify(seeds));
    } catch {
        // a browser that will not remember the list simply shows what this visit has had
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
