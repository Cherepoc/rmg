const DATABASE = "rmg";
const STORE = "soundfonts";
const KEY = "current";
const SETTING = "rmg.keep-soundfont";
const AUTOPLAY = "rmg.autoplay";
const HISTORY = "rmg.history";

/**
 *     Kept as the absence of an opt-out, so the default stays on for a first visit and for a
 *     browser that refuses local storage altogether.
 */
export function isKeeping() {
    try {
        return localStorage.getItem(SETTING) !== "off";
    } catch {
        return true;
    }
}

export function setKeeping(isOn) {
    try {
        if (isOn) localStorage.removeItem(SETTING);
        else localStorage.setItem(SETTING, "off");
    } catch {
        // a browser that will not remember the setting will not remember the soundfont either
    }
}

/**
 *     Kept the same way as the setting above, as the absence of an opt-out, so a song that runs out
 *     leads to another until somebody says otherwise.
 */
export function isAutoplaying() {
    try {
        return localStorage.getItem(AUTOPLAY) !== "off";
    } catch {
        return true;
    }
}

export function setAutoplaying(isOn) {
    try {
        if (isOn) localStorage.removeItem(AUTOPLAY);
        else localStorage.setItem(AUTOPLAY, "off");
    } catch {
        // a browser that will not remember the setting simply starts each visit with it on
    }
}

/** A seed as the page keeps it: the digits the server reported, and nothing that is not one. */
const IS_SEED = /^-?\d{1,10}$/;

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
        request.onblocked = () => reject(new Error("another tab is holding the database open"));
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
            transaction.onabort = () => reject(transaction.error ?? new Error("the write was rolled back"));
        });
    } finally {
        database.close();
    }
}

export async function keep(name, soundFont) {
    // asking is enough: a browser that says no still stores, it just may evict under pressure
    try {
        await navigator.storage?.persist?.();
    } catch {
        // not worth reporting, the soundfont is stored either way
    }

    await withStore("readwrite", (store) => store.put({ name, soundFont }, KEY));
}

export async function recall() {
    return await withStore("readonly", (store) => store.get(KEY));
}

export async function forget() {
    await withStore("readwrite", (store) => store.delete(KEY));
}
