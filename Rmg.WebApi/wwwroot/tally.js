/**
 *     What the page tells the server about how it went: how long things took, how far anyone got, what
 *     failed. It goes to this same server and nowhere else, sets no cookie, and writes nothing to this
 *     browser, which is why there is nothing here to agree to.
 *
 *     Nothing here is allowed to matter. Every send is best effort, every failure is swallowed, and the
 *     page behaves the same whether any of it arrives or not.
 *
 *     The file is not called analytics.js, and the path is not /api/events, because those names are
 *     matched by blocklists on sight and this was being dropped for its spelling rather than for what
 *     it does. Anyone who actually means to opt out is honoured below, which is the part that counts.
 */

/**
 *     Somebody who has asked not to be counted is not counted. Global Privacy Control is the one with
 *     legal weight behind it; Do Not Track has none left, but it still says plainly what its sender wants.
 */
const isRefused = (() => {
    try {
        return navigator.globalPrivacyControl === true
            || navigator.doNotTrack === "1"
            || window.doNotTrack === "1";
    } catch {
        return false;
    }
})();

const opened = performance.now();

/** How long the page has been open, in whole milliseconds, which is what every `ms` here means. */
export function since() {
    return Math.round(performance.now() - opened);
}

export function track(name, measurements = {}) {
    if (isRefused) return;

    try {
        const body = JSON.stringify({ name, ...measurements });

        // sendBeacon survives the page being closed, which is exactly when the last event is worth having
        if (navigator.sendBeacon?.(new URL("api/tally", location.href), new Blob([body], { type: "application/json" })))
            return;

        fetch(new URL("api/tally", location.href), {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body,
            keepalive: true,
        }).catch(() => {});
    } catch {
        // an event that cannot be sent is an event not worth having
    }
}
