const DATABASE = "rmg";
const STORE = "soundfonts";
const KEY = "current";
const SETTING = "rmg.keep-soundfont";

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
