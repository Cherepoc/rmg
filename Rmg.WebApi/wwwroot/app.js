import {
    DRUM_CHANNEL,
    DRUM_KITS,
    INSTRUMENT_FAMILIES,
    LAST_INSTRUMENT_BEFORE_EFFECTS,
} from "./instruments.js";
import { encodeMp3, renderSong } from "./export.js";
import { getPlayer } from "./player.js";
import {
    forget,
    isAutoplaying,
    isKeeping,
    keep,
    keepHistory,
    recall,
    recallHistory,
    setAutoplaying,
    setKeeping,
} from "./storage.js";
import { since, track } from "./tally.js";

const PLAY_ICON = "M8 5v14l11-7z";
const PAUSE_ICON = "M7 5h3.5v14H7zM13.5 5H17v14h-3.5z";

const elements = {
    soundFont: document.getElementById("soundfont"),
    soundFontName: document.getElementById("soundfont-name"),
    libraryRow: document.getElementById("library-row"),
    library: document.getElementById("library"),
    libraryLicense: document.getElementById("library-license"),
    keep: document.getElementById("keep"),
    seed: document.getElementById("seed"),
    generate: document.getElementById("generate"),
    showSeeded: document.getElementById("show-seeded"),
    seeded: document.getElementById("seeded"),
    seedInput: document.getElementById("seed-input"),
    generateSeeded: document.getElementById("generate-seeded"),
    historyRow: document.getElementById("history-row"),
    history: document.getElementById("history"),
    clearHistory: document.getElementById("clear-history"),
    download: document.getElementById("download"),
    exportMp3: document.getElementById("export-mp3"),
    share: document.getElementById("share"),
    bitrate: document.getElementById("bitrate"),
    play: document.getElementById("play"),
    stop: document.getElementById("stop"),
    seek: document.getElementById("seek"),
    elapsed: document.getElementById("elapsed"),
    total: document.getElementById("total"),
    volume: document.getElementById("volume"),
    autoplay: document.getElementById("autoplay"),
    mixerPanel: document.getElementById("mixer-panel"),
    songVolume: document.getElementById("song-volume"),
    songVolumeValue: document.getElementById("song-volume-value"),
    rollInstruments: document.getElementById("roll-instruments"),
    rollFirst: document.getElementById("roll-first"),
    rollLast: document.getElementById("roll-last"),
    mixer: document.getElementById("mixer"),
    status: document.getElementById("status"),
};

const muted = new Set();
const soloed = new Set();
const rows = new Map();

// the mix: what every channel plays, how loud, and whether it is in the song at all
const instruments = new Map();
const volumes = new Map();
const switchedOff = new Set();

// the channels whose instrument was picked here, rather than the one the song was generated with
const chosen = new Set();
let songVolume = 1;
let songChannels = [];

// hasSoundFont and hasSong say that the bytes are here, not that the player has them: the page fetches
// both the moment it opens, and the audio stack cannot exist until the page has been interacted with
let hasSoundFont = false;
let source = null;
let loading = Promise.resolve();
let hasSong = false;
let pendingSoundFont = null;
let pendingSong = null;
let delivery = Promise.resolve();
let startedListening = null;
let isSeeking = false;

// whether the song on the page was rolled here rather than asked for by seed, which is the only kind
// that leads to another when it ends, and whether one is on its way of its own accord
let isSongRandom = false;
let isAdvancing = false;
let isListening = false;
let downloadUrl = null;
let exportUrl = null;
let frame = null;
let downloadTimer = null;
let downloadRequest = 0;

function setStatus(message, isError = false) {
    elements.status.textContent = message;
    elements.status.classList.toggle("error", isError);
}

/** What the soundfont panel says while it is shut, which is all most visitors will ever see of it. */
function setSoundFontState(message, isError = false) {
    elements.soundFontName.textContent = message;
    elements.soundFontName.classList.toggle("error", isError);
}

function formatTime(seconds) {
    const whole = Number.isFinite(seconds) && seconds > 0 ? Math.floor(seconds) : 0;
    return `${Math.floor(whole / 60)}:${String(whole % 60).padStart(2, "0")}`;
}

function updateTransport() {
    const isReady = hasSoundFont && hasSong;
    elements.play.disabled = !isReady;
    elements.stop.disabled = !isReady;
    elements.seek.disabled = !isReady;

    // an export is the song rendered through the soundfont, so it wants both of them as much as playing does
    elements.exportMp3.disabled = !isReady;

    // sharing is only a seed, which the server has already answered with: no sound is needed to pass it on
    elements.share.disabled = !hasSong;
}

function setPlayIcon(isPlaying) {
    elements.play.querySelector("path").setAttribute("d", isPlaying ? PAUSE_ICON : PLAY_ICON);
    elements.play.setAttribute("aria-label", isPlaying ? "Pause" : "Play");
}

/**
 *     Runs <paramref name="action" /> against the audio stack, which is only built once the user
 *     has asked for something that needs it, because browsers block audio before a gesture.
 */
async function withPlayer(action, failureMessage) {
    try {
        const player = await getPlayer();
        if (!isListening) {
            isListening = true;
            listen(player);
        }

        return await action(player);
    } catch (error) {
        setStatus(`${failureMessage}: ${error.message}`, true);
        return null;
    }
}

function listen(player) {
    player.onSongEnded(() => {
        setPlayIcon(false);
        reportListening();
        render(player);
        advance();
    });
    player.masterGain = Number(elements.volume.value);
}

/**
 *     What a song running out leads to: another one, rolled and played where the last left off, the way
 *     a video site goes on to the next. Only a song rolled here leads anywhere. One asked for by seed, or
 *     arrived at by a link, is the song that was asked for, and it ends where it ends.
 */
async function advance() {
    if (!elements.autoplay.checked || !isSongRandom) return;

    isAdvancing = true;
    try {
        const generated = await generate(null);

        // the player has to have been handed the song before it can be started on it
        await delivery;
        if (!generated || !isAdvancing) return;

        await withPlayer((player) => startPlaying(player, "auto"), "Playback failed");
    } finally {
        isAdvancing = false;
    }
}

/**
 *     Lets go of a song on its way of its own accord. Whatever was asked for by hand since is what should
 *     be playing, rather than the one the song before it asked for.
 */
function stopAdvancing() {
    isAdvancing = false;
}

// --- soundfont -------------------------------------------------------------

elements.keep.checked = isKeeping();

elements.soundFont.addEventListener("change", async () => {
    const file = elements.soundFont.files?.[0];
    if (!file) return;

    elements.library.value = "";
    showLicense(null);
    await useSoundFont(file.name, () => file.arrayBuffer(), "file");
});

elements.library.addEventListener("change", async () => {
    const option = elements.library.selectedOptions[0];
    showLicense(option);
    if (!option?.value) return;

    elements.soundFont.value = "";
    await useSoundFont(option.dataset.name, () => download(option.value, option.dataset.name), "server");
});

elements.keep.addEventListener("change", async () => {
    setKeeping(elements.keep.checked);

    if (!elements.keep.checked) {
        await forget().catch(() => {});
        setStatus("Soundfonts are no longer kept in this browser.");
        return;
    }

    if (source === null) {
        setStatus("The next soundfont you load will be kept in this browser.");
        return;
    }

    setStatus(`Keeping ${source.name}…`);
    try {
        const problem = await store(source.name, await source.read());
        setStatus(problem === null
            ? `${source.name} is kept in this browser.`
            : `${source.name} could not be kept: ${problem}.`);
    } catch (error) {
        setStatus(`${source.name} could not be kept: ${error.message}.`);
    }
});

/** Serialised, because two loads at once would race each other inside the sound bank manager. */
function useSoundFont(name, read, origin, isStored = false) {
    loading = loading.then(() => loadSoundFont(name, read, origin, isStored));
    return loading;
}

/**
 *     Takes the soundfont <paramref name="read" /> returns, rather than its bytes, because the
 *     worklet may take the buffer with it: holding a second copy of a few hundred megabytes just
 *     to serve a later opt-in is not worth it, so a re-read is asked for instead.
 */
async function loadSoundFont(name, read, origin, isStored = false) {
    setSoundFontState(`Loading ${name}…`);

    let soundFont;
    try {
        soundFont = await read();
    } catch (error) {
        clearSoundFont();
        setSoundFontState(`${name} could not be read`, true);
        setStatus(`Could not read ${name}: ${explain(error)}`, true);

        // the byte count tells the two failures apart: nothing arrived, or the connection gave out partway
        track("soundfont_failed", { detail: error.name || "unreadable", bytes: error.got ?? null });
        return;
    }

    // stored before the player is handed it, since loading detaches the buffer
    const problem = isKeeping() && !isStored ? await store(name, soundFont) : null;

    source = { name, read };
    pendingSoundFont = soundFont;
    hasSoundFont = true;
    updateTransport();

    setSoundFontState(problem === null ? name : `${name}, not kept: ${problem}`);

    // the size and where it came from, never its name: a file of your own is yours, and its name can say
    // more about you than this has any business knowing
    track("soundfont_ready", { ms: since(), bytes: soundFont.byteLength, detail: origin });

    deliver();
}

/**
 *     Hands the player whatever has been fetched. A browser allows no audio before the page has been
 *     interacted with, and a synthesizer built any earlier never reports itself ready, so the song and
 *     the soundfont are fetched the moment the page opens and wait here for the first gesture.
 */
function deliver() {
    delivery = delivery.then(deliverPending);
    return delivery;
}

async function deliverPending() {
    if (pendingSoundFont === null && pendingSong === null) return;

    await firstGesture();

    // taken before the await, since loading detaches the buffers and a second delivery must not resend them
    const soundFont = pendingSoundFont;
    const song = pendingSong;
    pendingSoundFont = null;
    pendingSong = null;

    const delivered = await withPlayer(async (player) => {
        if (soundFont !== null) await player.loadSoundFont(soundFont);

        if (song !== null) {
            player.loadSong(song);
            applyMix(player);
        } else if (soundFont !== null) {
            // a soundfont swapped under a song already playing: the bank took every channel back to
            // where it started, and the song will not say what it plays again until it comes round
            restoreMix(player);
        }

        return true;
    }, "The player could not start");

    if (delivered === true) {
        track("audio_ready", { ms: since() });
        return;
    }

    if (soundFont !== null) {
        // whatever would not load is not worth keeping, and would only fail again on the next visit
        await forget().catch(() => {});
        clearSoundFont();
        hasSoundFont = false;
    }

    if (song !== null) hasSong = false;
    updateTransport();
}

/** How many times a soundfont is asked for before giving up on it. */
const DOWNLOAD_ATTEMPTS = 4;

/**
 *     Fetches a soundfont a piece at a time, keeping what has arrived when the connection gives out and
 *     asking only for the rest. Tens of megabytes takes long enough on a phone to be interrupted by a
 *     lock screen, a lift, or changing masts, and one whole failed attempt is a poor answer to that.
 */
async function download(file, name) {
    const address = new URL(`soundfonts/${encodeURIComponent(file)}`, location.href);
    const pieces = [];

    let got = 0;
    let total = 0;
    let failure = null;

    for (let attempt = 1; attempt <= DOWNLOAD_ATTEMPTS; attempt++) {
        try {
            const response = await fetch(address, got > 0 ? { headers: { Range: `bytes=${got}-` } } : {});
            if (!response.ok) throw new Error(await describeFailure(response));

            // a server that took no notice of the range is starting over, so what was kept is not the start
            if (got > 0 && response.status !== 206) {
                pieces.length = 0;
                got = 0;
            }

            const remaining = Number(response.headers.get("Content-Length"));
            if (total === 0 && Number.isFinite(remaining)) total = got + remaining;

            // read as it arrives, so an interruption keeps what came before it
            if (response.body === null || response.body === undefined) {
                pieces.push(new Uint8Array(await response.arrayBuffer()));
            } else {
                const reader = response.body.getReader();

                for (;;) {
                    const { done, value } = await reader.read();
                    if (done) break;

                    pieces.push(value);
                    got += value.length;
                    setSoundFontState(`Downloading ${name}… ${sofar(got, total)}`);
                }
            }

            return join(pieces);
        } catch (error) {
            failure = error;
            if (attempt === DOWNLOAD_ATTEMPTS) break;

            setSoundFontState(`Downloading ${name}… ${sofar(got, total)}, trying again`);
            await pause(400 * attempt);
        }
    }

    // how far it got says which kind of failure this was: nothing at all, or a connection that gave out
    failure.got = got;
    throw failure;
}

function sofar(got, total) {
    return total > 0 ? formatPercent(got / total) : formatSize(got);
}

function join(pieces) {
    const all = new Uint8Array(pieces.reduce((length, piece) => length + piece.length, 0));

    let at = 0;
    for (const piece of pieces) {
        all.set(piece, at);
        at += piece.length;
    }

    return all.buffer;
}

function pause(ms) {
    return new Promise((resolve) => setTimeout(resolve, ms));
}

async function store(name, soundFont) {
    try {
        await keep(name, soundFont);
        return null;
    } catch (error) {
        return error.name === "QuotaExceededError" ? "this browser has no room for it" : error.message;
    }
}

function clearSoundFont() {
    setSoundFontState("None loaded");
    elements.soundFont.value = "";
    elements.library.value = "";
    showLicense(null);
    source = null;
    pendingSoundFont = null;
}

/**
 *     Serving a soundfont is distributing it, and the licences they carry ask to travel with the file,
 *     so whatever notice the server holds next to the chosen one is linked rather than left in the
 *     directory. A soundfont with nothing beside it simply has no link.
 */
function showLicense(option) {
    const file = option?.dataset.license;
    elements.libraryLicense.hidden = !file;
    if (!file) return;

    elements.libraryLicense.href = new URL(`soundfonts/${encodeURIComponent(file)}`, location.href);
    elements.libraryLicense.textContent = `Licence for ${option.dataset.name}`;
}

/** Fills the dropdown, and answers with the soundfont to load unasked, which the server names. */
async function listLibrary() {
    let soundFonts;
    try {
        const response = await fetch(new URL("api/soundfonts", location.href));
        if (!response.ok) return null;

        soundFonts = await response.json();
    } catch {
        return null; // a server with nothing to offer just leaves the page to a file of your own
    }

    if (soundFonts.length === 0) return null;

    const choose = document.createElement("option");
    choose.value = "";
    choose.textContent = "Choose…";

    const options = soundFonts.map(describeSoundFont);
    elements.library.replaceChildren(choose, ...options);
    elements.libraryRow.hidden = false;

    return options.find((option) => option.dataset.isDefault === "true") ?? options[0];
}

function describeSoundFont(soundFont) {
    const option = document.createElement("option");
    option.value = soundFont.file;
    option.dataset.name = soundFont.name;
    if (soundFont.isDefault) option.dataset.isDefault = "true";
    if (soundFont.license) option.dataset.license = soundFont.license;
    option.textContent = `${soundFont.name} (${formatSize(soundFont.size)})`;
    return option;
}

function formatSize(bytes) {
    const megabytes = bytes / 1024 ** 2;
    return megabytes >= 1024 ? `${(megabytes / 1024).toFixed(1)} GB` : `${Math.max(1, Math.round(megabytes))} MB`;
}

/** What this browser kept from a previous visit, if it kept anything and will still say so. */
async function recallKept() {
    if (!isKeeping()) return null;

    try {
        return await recall();
    } catch {
        return null; // a browser that will not open the database simply has nothing kept
    }
}

function firstGesture() {
    return new Promise((resolve) => {
        if (navigator.userActivation?.hasBeenActive) {
            resolve();
            return;
        }

        const options = { once: true, capture: true };
        const done = () => {
            document.removeEventListener("pointerdown", done, options);
            document.removeEventListener("keydown", done, options);
            resolve();
        };

        document.addEventListener("pointerdown", done, options);
        document.addEventListener("keydown", done, options);
    });
}

async function reread() {
    const saved = await recall();
    if (!saved) throw new Error("it is no longer kept in this browser");

    return saved.soundFont;
}

// --- generating ------------------------------------------------------------

elements.showSeeded.addEventListener("change", () => {
    elements.seeded.hidden = !elements.showSeeded.checked;
    if (elements.showSeeded.checked) elements.seedInput.focus();
});

// no seed at all, so the server rolls one and reports it back in X-Song-Seed
elements.generate.addEventListener("click", () => {
    stopAdvancing();
    generate(null);
});

elements.generateSeeded.addEventListener("click", () => {
    const seed = readSeed(elements.seedInput.value.trim());
    if (seed === null) {
        setStatus(`'${elements.seedInput.value.trim()}' is not a valid seed. A seed is a 32-bit integer.`, true);
        return;
    }

    stopAdvancing();
    generate(seed);
});

elements.seedInput.addEventListener("keydown", (event) => {
    if (event.key === "Enter") elements.generateSeeded.click();
});

/** A seed is a 32-bit integer, and anything else is worth saying so before it is sent. */
function readSeed(text) {
    const seed = Number(text);
    const isSeed = text !== "" && Number.isInteger(seed) && seed >= -(2 ** 31) && seed <= 2 ** 31 - 1;
    return isSeed ? seed : null;
}

/**
 *     The seed asked for in the address, as <c>?seed=12345</c>: the seed itself, and whether one was
 *     asked for at all. A link carrying something that is not a seed is worth saying so about, rather
 *     than quietly playing a different song and letting whoever sent it wonder.
 */
function linkedSeed() {
    const asked = new URL(location.href).searchParams.get("seed");
    if (asked === null) return { seed: null, wasAsked: false };

    return { seed: readSeed(asked.trim()), wasAsked: true };
}

/**
 *     Keeps the address on the song being heard, so a refresh gives the same one back and the address
 *     bar is itself a shareable link. Replaced rather than pushed: every roll of the dice is not a place
 *     to go back to.
 */
function rememberSeed(songSeed) {
    try {
        const address = new URL(location.href);
        address.searchParams.set("seed", songSeed);
        history.replaceState(null, "", address);
    } catch {
        // an address that cannot be rewritten costs nothing here; the share button reads the seed itself
    }
}

/** The link to the song on the page, which is this address with the seed it is playing. */
function songLink() {
    const address = new URL(location.href);
    address.searchParams.set("seed", elements.seed.value);

    return address.toString();
}

function requestSong(song) {
    return fetch(new URL("api/songs/generate", location.href), {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(song),
    });
}

/** The song as the mixer has it: every channel, whatever was done to it. */
function describeMix() {
    return {
        volume: songVolume,
        tracks: [...instruments].map(([channel, instrument]) => ({
            channel: channel + 1,
            instrument,
            volume: volumes.get(channel) ?? 1,
            isEnabled: !switchedOff.has(channel),
        })),
    };
}

/** Answers whether a song arrived, which is what tells a roll of the next one that it has something to play. */
async function generate(seed) {
    setGenerating(true);
    cancelDownloadRefresh();
    setStatus("Generating…");

    try {
        const response = await requestSong({ seed });
        if (!response.ok) {
            setStatus(`Could not generate a song: ${await describeFailure(response)}`, true);
            track("song_failed", { detail: `http ${response.status}` });
            return false;
        }

        const songSeed = response.headers.get("X-Song-Seed") ?? String(seed);
        readSong(response.headers.get("X-Song-Instruments"));

        // built from what the server reported rather than from the loaded song, so the mix is on the
        // page as soon as there is a song at all, without waiting for the audio stack a click builds
        buildMixer([...instruments.keys()].sort((first, second) => first - second));

        const song = await response.arrayBuffer();

        elements.seed.value = songSeed;
        rememberSeed(songSeed);
        addToHistory(songSeed);
        // offered for download first, and as a copy, so it stays usable whatever the audio stack does
        offerDownload(song, songSeed);
        setStatus(`Generated song ${songSeed}.`);
        track("song_generated", { ms: since(), seed: Number(songSeed) });

        pendingSong = song;
        hasSong = true;
        isSongRandom = seed === null;
        updateTransport();
        deliver();

        return true;
    } catch (error) {
        setStatus(`Could not generate a song: ${error.message}`, true);
        track("song_failed", { detail: error.name || "failed" });
        return false;
    } finally {
        setGenerating(false);
    }
}

/** Nothing is asked for while a song is on its way, the list of songs so far included. */
function setGenerating(isGenerating) {
    elements.generate.disabled = isGenerating;
    elements.generateSeeded.disabled = isGenerating;

    // a button disabled under the keyboard drops it, and the list is where the keyboard just was
    const held = isGenerating && elements.history.contains(document.activeElement) ? document.activeElement : null;

    for (const item of elements.history.children) item.disabled = isGenerating;

    if (isGenerating) {
        heldByKeyboard = held;
        return;
    }

    // put the keyboard back only where it was taken from: anything pressed since is the last word
    if (heldByKeyboard?.isConnected && document.activeElement === document.body) heldByKeyboard.focus();
    heldByKeyboard = null;
}

/**
 *     Safari says "Load failed" and Chrome "Failed to fetch"; neither says what to do next. A soundfont
 *     is tens of megabytes, so the answer is usually to try it again or to take a smaller one.
 */
function explain(error) {
    if (error.name !== "TypeError") return error.message;

    return error.got > 0
        ? "the download stopped partway. Open the soundfont section and choose it again, or take a smaller one."
        : "the download would not start. Open the soundfont section and try again.";
}

async function describeFailure(response) {
    try {
        const body = await response.json();
        return body.error ?? body.detail ?? body.title ?? response.statusText;
    } catch {
        return response.statusText || `HTTP ${response.status}`;
    }
}

function offerDownload(song, songSeed) {
    if (downloadUrl) URL.revokeObjectURL(downloadUrl);

    downloadUrl = URL.createObjectURL(new Blob([song], { type: "audio/midi" }));
    elements.download.href = downloadUrl;
    elements.download.download = `song-${songSeed}.mid`;
    elements.download.hidden = false;
}

// the download is a plain anchor, so the click is the only place it can be noticed
elements.download.addEventListener("click", () => {
    track("download_mid", { seed: Number(elements.seed.value) || null });
});

// --- the songs so far ------------------------------------------------------

/** How many seeds the page keeps before the oldest is let go of. */
const SONGS_KEPT = 50;

const heardSeeds = [];

/** The button the keyboard was on when the list was disabled, so it can be given back. */
let heldByKeyboard = null;

restoreHistory();

/**
 *     Puts back the list this browser kept, before a song is asked for, so the one this visit opens
 *     with goes on top of what came before rather than starting the list over.
 */
function restoreHistory() {
    for (const songSeed of recallHistory().slice(0, SONGS_KEPT)) {
        heardSeeds.push(songSeed);
        elements.history.append(createHistoryItem(songSeed));
    }

    elements.historyRow.hidden = heardSeeds.length === 0;
}

/** The list is this browser's to be rid of, being the one thing here that says what anybody listened to. */
elements.clearHistory.addEventListener("click", () => {
    heardSeeds.length = 0;
    elements.history.replaceChildren();
    elements.historyRow.hidden = true;
    keepHistory(heardSeeds);
});

/**
 *     Puts a seed down as one the page has had, however it came about: rolled, typed, followed from a
 *     link, or rolled by the song before it running out. A seed already down is not put down twice, so
 *     coming back to a song through the list leaves the list as it was.
 *
 *     The buttons are left where they are rather than written out again, so the one just pressed is
 *     still the one under the finger, and still the one the keyboard is on.
 */
function addToHistory(songSeed) {
    if (!heardSeeds.includes(songSeed)) {
        heardSeeds.unshift(songSeed);
        elements.history.prepend(createHistoryItem(songSeed));

        while (heardSeeds.length > SONGS_KEPT) {
            heardSeeds.pop();
            elements.history.lastElementChild?.remove();
        }

        keepHistory(heardSeeds);
    }

    markCurrentSong(songSeed);
    elements.historyRow.hidden = heardSeeds.length === 0;
}

/** Marks the song on the page, which is where the list is being read from. */
function markCurrentSong(songSeed) {
    for (const item of elements.history.children) {
        if (item.textContent === songSeed) item.setAttribute("aria-current", "true");
        else item.removeAttribute("aria-current");
    }
}

/**
 *     One song to come back to. It is asked for by its seed like any other, so what comes back is the
 *     song as it was generated rather than the mixer as it was left, and it is the song that was asked
 *     for: it leads to no other when it ends.
 */
function createHistoryItem(songSeed) {
    const button = document.createElement("button");
    button.type = "button";
    button.textContent = songSeed;
    button.setAttribute("aria-label", `Song ${songSeed}`);

    // one arriving while a song is on its way is as unaskable as the buttons above it
    button.disabled = elements.generate.disabled;

    button.addEventListener("click", () => {
        stopAdvancing();
        generate(Number(songSeed));
    });

    return button;
}

// --- sharing ---------------------------------------------------------------

elements.share.addEventListener("click", share);

/**
 *     Hands over a link to the song on the page. The share sheet where there is one, which is phones and
 *     little else, and the clipboard everywhere else. The link is only the seed, so what arrives is the
 *     song as it was generated rather than the mixer as it was left.
 */
async function share() {
    const link = songLink();
    const seed = elements.seed.value;

    if (navigator.share !== undefined) {
        try {
            await navigator.share({ title: `RMG song ${seed}`, url: link });
            track("shared", { seed: Number(seed) || null, detail: "sheet" });
            return;
        } catch (error) {
            // thinking better of it halfway through a share sheet is not a failure worth reporting
            if (error.name === "AbortError") return;
        }
    }

    if (await copy(link)) {
        say("Copied");
        setStatus(`Link to song ${seed} copied.`);
        track("shared", { seed: Number(seed) || null, detail: "clipboard" });
        return;
    }

    // nothing worked, so the link goes where it can at least be read and copied by hand
    setStatus(link);
    say("Above");
}

async function copy(text) {
    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch {
        // the clipboard is refused outside a secure context, which is any plain http address but localhost
    }

    return copyTheOldWay(text);
}

/** The way it was done before there was a clipboard to ask, which still works where asking is refused. */
function copyTheOldWay(text) {
    const field = document.createElement("textarea");
    field.value = text;
    field.setAttribute("readonly", "");
    field.style.position = "fixed";
    field.style.opacity = "0";
    document.body.append(field);

    try {
        field.select();
        return document.execCommand("copy");
    } catch {
        return false;
    } finally {
        field.remove();
    }
}

/** Says so on the button itself, which is where the eye already is, and puts it back afterwards. */
function say(word) {
    elements.share.textContent = word;
    setTimeout(() => (elements.share.textContent = "Share"), 1600);
}

// --- exporting -------------------------------------------------------------

elements.exportMp3.addEventListener("click", exportMp3);

/**
 *     Renders the song as the mixer has it and encodes it as an MP3. Both are long stretches of work, so
 *     they say how far along they are, and the page stays its own the whole time.
 */
async function exportMp3() {
    const songSeed = elements.seed.value;
    const name = `song-${songSeed}.mp3`;

    elements.exportMp3.disabled = true;
    setStatus("Fetching the song…");

    try {
        const response = await requestSong({ seed: Number(songSeed), ...describeMix() });
        if (!response.ok) throw new Error(await describeFailure(response));

        const song = await response.arrayBuffer();
        const soundFont = await source.read();

        const audio = await renderSong(song, soundFont, (progress) =>
            setStatus(`Rendering ${formatPercent(progress)}…`));

        const mp3 = await encodeMp3(audio, Number(elements.bitrate.value), (progress) =>
            setStatus(`Encoding ${formatPercent(progress)}…`));

        offerExport(mp3, name);
        setStatus(`Exported ${name}, ${formatSize(mp3.size)}.`);
        track("export_mp3", { ms: since(), seed: Number(songSeed) || null, detail: `${elements.bitrate.value} kbps` });
    } catch (error) {
        setStatus(`Could not export ${name}: ${error.message}`, true);
    } finally {
        updateTransport();
    }
}

/** Hands the file over at once: it was asked for outright, rather than offered like the song is. */
function offerExport(mp3, name) {
    if (exportUrl) URL.revokeObjectURL(exportUrl);

    exportUrl = URL.createObjectURL(mp3);

    const link = document.createElement("a");
    link.href = exportUrl;
    link.download = name;
    link.click();
}

// --- transport -------------------------------------------------------------

elements.autoplay.checked = isAutoplaying();

elements.autoplay.addEventListener("change", () => setAutoplaying(elements.autoplay.checked));

elements.play.addEventListener("click", async () => {
    stopAdvancing();

    // this click is very likely the gesture the fetched song and soundfont have been waiting for
    await deliver();

    await withPlayer(async (player) => {
        if (player.paused) {
            await startPlaying(player);
            return;
        }

        stopTicking();
        player.pause();
        reportListening();
        setPlayIcon(false);
    }, "Playback failed");
});

/** Starts the song, and everything that follows it while it plays. */
async function startPlaying(player, origin = null) {
    await player.play();
    startTicking(player);
    setPlayIcon(true);
    startedListening = performance.now();
    track("play", { seed: Number(elements.seed.value) || null, detail: origin });
}

/**
 *     How long the last unbroken stretch of playing lasted. Reported when it stops rather than while it
 *     runs, so listening costs one event however long it goes on for.
 */
function reportListening() {
    if (startedListening === null) return;

    const seconds = (performance.now() - startedListening) / 1000;
    startedListening = null;

    if (seconds >= 1) track("listened", { seconds, seed: Number(elements.seed.value) || null });
}

elements.stop.addEventListener("click", async () => {
    stopAdvancing();
    reportListening();

    await withPlayer((player) => {
        stopTicking();
        player.stop();
        setPlayIcon(false);
        render(player, 0);
    }, "Playback failed");
});

elements.seek.addEventListener("pointerdown", () => (isSeeking = true));
elements.seek.addEventListener("keydown", () => (isSeeking = true));
elements.seek.addEventListener("keyup", () => (isSeeking = false));
window.addEventListener("pointerup", () => (isSeeking = false));

elements.seek.addEventListener("input", async () => {
    await withPlayer((player) => {
        const time = Number(elements.seek.value) * player.duration;
        player.currentTime = time;
        render(player, time);
    }, "Seeking failed");
});

elements.volume.addEventListener("input", async () => {
    await withPlayer((player) => (player.masterGain = Number(elements.volume.value)), "Could not set the volume");
});

// --- mixer -----------------------------------------------------------------

function buildMixer(channels) {
    rows.clear();
    muted.clear();
    soloed.clear();
    elements.mixer.replaceChildren();

    songChannels = channels;

    for (const channel of channels) {
        const number = document.createElement("td");
        number.textContent = String(channel + 1);

        const onCell = document.createElement("td");
        onCell.append(createSwitch(channel));

        const instrumentCell = document.createElement("td");
        const instrument = createInstrumentPicker(channel);
        const roll = createRoll(channel);
        const instrumentField = document.createElement("div");
        instrumentField.className = "instrument-field";
        instrumentField.append(instrument, roll);
        instrumentCell.append(instrumentField);

        const volumeCell = document.createElement("td");
        volumeCell.append(createVolume(channel));

        const muteCell = document.createElement("td");
        const mute = createToggle("Mute", () => {
            toggle(muted, channel, mute);
            applyMuting();
            track("mix_changed", { detail: "mute" });
        });
        muteCell.append(mute);

        const soloCell = document.createElement("td");
        const solo = createToggle("Solo", () => {
            toggle(soloed, channel, solo);
            applyMuting();
            track("mix_changed", { detail: "solo" });
        });
        soloCell.append(solo);

        const meterCell = document.createElement("td");
        const meter = document.createElement("div");
        meter.className = "meter";
        const level = document.createElement("span");
        meter.append(level);
        meterCell.append(meter);

        const row = document.createElement("tr");
        row.append(number, onCell, instrumentCell, volumeCell, muteCell, soloCell, meterCell);
        elements.mixer.append(row);
        rows.set(channel, { instrument, level });
    }

    elements.mixerPanel.hidden = channels.length === 0;
}

/**
 *     The instruments of a channel: the General MIDI ones, or the drum kits, which is what an instrument
 *     means on the percussion channel.
 */
function createInstrumentPicker(channel) {
    const select = document.createElement("select");
    select.setAttribute("aria-label", `Instrument of channel ${channel + 1}`);

    if (channel === DRUM_CHANNEL)
        select.append(...DRUM_KITS.map((kit) => createInstrumentOption(kit.instrument, kit.name)));
    else
        fillInstruments(select);

    select.value = String(instruments.get(channel) ?? 0);
    select.addEventListener("change", () => setInstrument(channel, Number(select.value)));

    return select;
}

/** Takes one instrument at random, which is worth nothing if it is the one already playing. */
function createRoll(channel) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "roll";
    button.textContent = "Roll";
    button.setAttribute("aria-label", `Roll the instrument of channel ${channel + 1}`);
    button.addEventListener("click", () => rollInstrument(channel));
    return button;
}

function setInstrument(channel, instrument) {
    track("mix_changed", { detail: "instrument" });
    instruments.set(channel, instrument);
    chosen.add(channel);
    rows.get(channel).instrument.value = String(instrument);
    scheduleDownloadRefresh();

    // the page and the download are right whatever the audio does, so the player is told separately
    withPlayer((player) => player.setChannelInstrument(channel, instrument), "Could not set the instrument");
}

/**
 *     What a roll of this channel may land on: the drum kits on the percussion channel, and the range the
 *     settings hold everywhere else, whichever way round it was set.
 */
function rollable(channel) {
    if (channel === DRUM_CHANNEL) return DRUM_KITS.map((kit) => kit.instrument);

    const bounds = [Number(elements.rollFirst.value), Number(elements.rollLast.value)];
    const first = Math.min(...bounds);
    return Array.from({ length: Math.max(...bounds) - first + 1 }, (_, index) => first + index);
}

/** Rolls one instrument, never landing on the one already playing while there is anything else to land on. */
function rollInstrument(channel) {
    const choices = rollable(channel).filter((instrument) => instrument !== instruments.get(channel));
    if (choices.length === 0) return;

    setInstrument(channel, choices[Math.floor(Math.random() * choices.length)]);
}

elements.rollInstruments.addEventListener("click", () => {
    for (const channel of songChannels) rollInstrument(channel);
});

/**
 *     Switches a track off, which leaves it out of the file and silences it here, so what is heard is what
 *     would be downloaded.
 */
function createSwitch(channel) {
    const toggle = document.createElement("input");
    toggle.type = "checkbox";
    toggle.checked = !switchedOff.has(channel);
    toggle.setAttribute("aria-label", `Channel ${channel + 1} on`);

    toggle.addEventListener("change", () => {
        if (toggle.checked) switchedOff.delete(channel);
        else switchedOff.add(channel);

        applyMuting();
        scheduleDownloadRefresh();
        track("mix_changed", { detail: "channel" });
    });

    return toggle;
}

/** The volume of a track, which the song carries, rather than the volume this page is played at. */
function createVolume(channel) {
    const fader = document.createElement("input");
    fader.type = "range";
    fader.min = "0";
    fader.max = "1";
    fader.step = "0.01";
    fader.value = String(volumes.get(channel) ?? 1);
    fader.className = "fader";
    fader.setAttribute("aria-label", `Volume of channel ${channel + 1}`);

    const shown = document.createElement("output");
    shown.className = "percent";
    shown.textContent = formatPercent(Number(fader.value));

    fader.addEventListener("input", () => {
        const volume = Number(fader.value);
        volumes.set(channel, volume);
        shown.textContent = formatPercent(volume);
        scheduleDownloadRefresh();
        track("mix_changed", { detail: "volume" });

        withPlayer((player) => player.setChannelVolume(channel, songVolume * volume), "Could not set the volume");
    });

    const field = document.createElement("div");
    field.className = "fader-field";
    field.append(fader, shown);
    return field;
}

function formatPercent(volume) {
    return `${Math.round(volume * 100)}%`;
}

function fillInstruments(select) {
    select.append(...INSTRUMENT_FAMILIES.map(createFamily));
}

function createFamily(family, familyIndex) {
    const group = document.createElement("optgroup");
    group.label = family.name;
    group.append(...family.instruments.map((name, index) =>
        createInstrumentOption(familyIndex * family.instruments.length + index, name)));
    return group;
}

function createInstrumentOption(instrument, name) {
    const option = document.createElement("option");
    option.value = String(instrument);
    option.textContent = name;
    return option;
}

/**
 *     "1:40,10:0": what every channel of a song plays, with the channels counted from 1 as the page shows
 *     them. A new song arrives with a clean mix, so the rest of it is reset to what the song itself says.
 */
function readSong(header) {
    instruments.clear();
    volumes.clear();
    switchedOff.clear();
    chosen.clear();

    songVolume = 1;
    elements.songVolume.value = "1";
    showSongVolume();

    for (const pair of (header ?? "").split(",").filter((pair) => pair !== "")) {
        const [channel, instrument] = pair.split(":").map(Number);
        instruments.set(channel - 1, instrument);
    }
}

function createToggle(label, onToggle) {
    const button = document.createElement("button");
    button.type = "button";
    button.textContent = label;
    button.setAttribute("aria-pressed", "false");
    button.addEventListener("click", onToggle);
    return button;
}

function toggle(set, channel, button) {
    const isOn = !set.has(channel);
    if (isOn) set.add(channel);
    else set.delete(channel);
    button.setAttribute("aria-pressed", String(isOn));
}

/** An explicit mute still wins over a solo, so soloing never un-mutes something silenced on purpose. */
function isSilenced(channel) {
    return switchedOff.has(channel) || muted.has(channel) || (soloed.size > 0 && !soloed.has(channel));
}

/**
 *     Puts the mix on the page onto a player that has just been handed the song. Loading a song lets go of
 *     every lock the mixer set, and anything chosen before the first click had nowhere to go until now.
 *     Only what was actually changed here is applied: the song carries its own instruments and volumes,
 *     and locking a channel to what it already plays would stop the song setting it again on the way round.
 */
function applyMix(player) {
    for (const channel of songChannels) {
        if (chosen.has(channel)) player.setChannelInstrument(channel, instruments.get(channel));

        if (volumes.has(channel) || songVolume !== 1)
            player.setChannelVolume(channel, songVolume * (volumes.get(channel) ?? 1));

        player.setChannelMuted(channel, isSilenced(channel));
    }
}

/**
 *     Puts the whole mix back, which a new sound bank has just undone. Every channel is told what it
 *     plays, not only the ones chosen here: loading a bank resets them all to the first program, and
 *     what the song itself asked for is as lost as what anybody picked. Only a choice made here is
 *     locked, so a channel the song owns is still the song's to set when it starts over.
 */
function restoreMix(player) {
    for (const channel of songChannels) {
        if (channel === DRUM_CHANNEL) player.setChannelDrums(channel, true);

        const instrument = instruments.get(channel);
        if (instrument !== undefined) player.setChannelInstrument(channel, instrument, chosen.has(channel));

        if (volumes.has(channel) || songVolume !== 1)
            player.setChannelVolume(channel, songVolume * (volumes.get(channel) ?? 1));

        player.setChannelMuted(channel, isSilenced(channel));
    }
}

function applyMuting() {
    withPlayer((player) => {
        for (const channel of songChannels) player.setChannelMuted(channel, isSilenced(channel));
    }, "Could not silence the channel");
}

elements.songVolume.addEventListener("input", async () => {
    songVolume = Number(elements.songVolume.value);
    showSongVolume();

    await withPlayer((player) => {
        for (const channel of instruments.keys())
            player.setChannelVolume(channel, songVolume * (volumes.get(channel) ?? 1));
    }, "Could not set the volume of the song");

    scheduleDownloadRefresh();
});

function showSongVolume() {
    elements.songVolumeValue.textContent = formatPercent(songVolume);
}

// --- the download ----------------------------------------------------------

/**
 *     A mix is heard at once, but the file offered for download is the one the server wrote, so it is
 *     fetched again with the mix on the page. The seed is the same, so the song is the same song.
 */
function scheduleDownloadRefresh() {
    cancelDownloadRefresh();
    downloadTimer = setTimeout(refreshDownload, 400);
}

function cancelDownloadRefresh() {
    // a file already on its way is for a song, or a choice, that is no longer the one on the page
    downloadRequest++;

    if (downloadTimer === null) return;

    clearTimeout(downloadTimer);
    downloadTimer = null;
}

async function refreshDownload() {
    downloadTimer = null;

    const songSeed = elements.seed.value;
    if (songSeed === "") return;

    const request = ++downloadRequest;
    try {
        const response = await requestSong({ seed: Number(songSeed), ...describeMix() });
        if (!response.ok) throw new Error(await describeFailure(response));

        const song = await response.arrayBuffer();
        if (request !== downloadRequest) return; // a later choice has already asked for its own file

        offerDownload(song, songSeed);
    } catch (error) {
        setStatus(`The download does not carry the mix on this page: ${error.message}`, true);
    }
}

// --- ticking ---------------------------------------------------------------

/**
 *     The sequencer applies a new position in the worklet, so reading it straight back after a
 *     stop or a seek still gives the old one: callers that just moved it pass the position instead.
 */
function render(player, time = player.currentTime) {
    const duration = player.duration;
    if (!isSeeking && duration > 0) elements.seek.value = String(time / duration);

    elements.elapsed.textContent = formatTime(time);
    elements.total.textContent = formatTime(duration);

    for (const [channel, row] of rows) {
        const voices = player.paused ? 0 : player.getVoiceCount(channel);
        row.level.style.width = `${Math.min(100, voices * 12)}%`;
    }
}

function startTicking(player) {
    if (frame !== null) return;

    const tick = () => {
        if (player.paused) {
            frame = null;
            setPlayIcon(false);
            return;
        }

        render(player);
        frame = requestAnimationFrame(tick);
    };

    frame = requestAnimationFrame(tick);
}

function stopTicking() {
    if (frame === null) return;

    cancelAnimationFrame(frame);
    frame = null;
}

// closing the tab is the commonest way listening ends, and the last event is the one worth having
window.addEventListener("pagehide", reportListening);

// --- wiring ----------------------------------------------------------------

fillInstruments(elements.rollFirst);
fillInstruments(elements.rollLast);
elements.rollFirst.value = "0";
elements.rollLast.value = String(LAST_INSTRUMENT_BEFORE_EFFECTS);

updateTransport();
setPlayIcon(false);
start();

/**
 *     The page is worth something the moment it opens: a song and a soundfont are both fetched straight
 *     away and at the same time, since neither waits on the other. Playing them is all that needs a
 *     gesture, and pressing play is one.
 */
async function start() {
    const linked = linkedSeed();
    track("page_open", { detail: linked.wasAsked ? "link" : "fresh" });

    // said afterwards, since the generating itself has the status line until it is done with it
    const generating = generate(linked.seed);
    if (linked.wasAsked && linked.seed === null)
        generating.then(() => setStatus("That link did not carry a seed I could read, so here is another song.", true));


    // listed whatever is loaded, so the dropdown is ready for anyone who opens the panel
    const offered = await listLibrary();
    const kept = await recallKept();

    if (kept) {
        // the dropdown still points at it when what this browser kept is one the server offers too
        selectInLibrary(kept.name);

        // read back out of the store rather than held, for the same reason useSoundFont takes a reader
        await useSoundFont(kept.name, reread, "kept", true);
        return;
    }

    if (offered === null) {
        setSoundFontState("none — open this and choose one");
        return;
    }

    selectInLibrary(offered.dataset.name);
    await useSoundFont(offered.dataset.name, () => download(offered.value, offered.dataset.name), "server");
}

/** Points the dropdown at a soundfont by name, and at nothing when the server offers no such thing. */
function selectInLibrary(name) {
    const option = [...elements.library.options].find((candidate) => candidate.dataset.name === name);

    elements.library.value = option?.value ?? "";
    showLicense(option ?? null);
}
