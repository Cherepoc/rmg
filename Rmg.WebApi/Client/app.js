import {
    DRUM_CHANNEL,
    DRUM_KITS,
    INSTRUMENT_FAMILIES,
    LAST_INSTRUMENT_BEFORE_EFFECTS,
} from "./instruments.js";
import { encodeMp3, renderSong } from "./export.js";
import { createMediaControls } from "./media-session.js";
import { getPlayer } from "./player.js";
import {
    askToPersist,
    forget,
    IS_SEED,
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
    seeded: document.getElementById("seeded"),
    seedInput: document.getElementById("seed-input"),
    generateSeeded: document.getElementById("generate-seeded"),
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
    volumeValue: document.getElementById("volume-value"),
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

// hasSoundFont and hasSong say that the bytes are here, not that the player has them: the page fetches
// both the moment it opens, and the audio stack cannot exist until the page has been interacted with
let hasSoundFont = false;

// the soundfont in use, by name and as a Blob, which the browser may keep on disk rather than in memory;
// an export or a later opt-in to keeping it reads it from here rather than fetching it again
let source = null;
let loading = Promise.resolve();
let hasSong = false;
let pendingSoundFont = null;
let pendingSong = null;
let delivery = Promise.resolve();
let startedListening = null;
let isSeeking = false;
let seekedFrom = null;
let isExporting = false;
let isGenerating = false;

// whether the song on the page was rolled here rather than asked for by seed, which is the only kind
// that leads to another when it ends, and which request to start a song once it arrives is the last one
let isSongRandom = false;
let playRequest = 0;
let isListening = false;
let downloadUrl = null;
let exportUrl = null;
let frame = null;
let downloadTimer = null;
let downloadRefresh = null;
let sayTimer = null;
let statusTimer = null;

function setStatus(message, isError = false) {
    clearTimeout(statusTimer);
    elements.status.textContent = message;
    elements.status.classList.toggle("error", isError);
}

/**
 *     Says something went as asked, then lets the line go quiet again. What is still going on, what went
 *     wrong, and anything to be read and copied stay until something else is said.
 */
function announce(message) {
    setStatus(message);
    statusTimer = setTimeout(() => setStatus(""), 4000);
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
    elements.exportMp3.disabled = !isReady || isExporting;

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
    // the length is only known once the sequencer has taken the song, and a new song starts from the top
    player.onSongChange(() => {
        render(player, 0);
        if (!player.paused) showMedia(player, 0);
    });
    player.onSongEnded(() => {
        setPlayIcon(false);
        reportListening();
        render(player);

        // one on its way keeps the controls playing across the gap, or a locked phone may let go of them
        if (!advance()) mediaControls.paused(elements.seed.value, player.duration, player.duration);
    });
    player.masterGain = Number(elements.volume.value);
}

/**
 *     What a song running out leads to: another one, rolled and played where the last left off, the way
 *     a video site goes on to the next. Only a song rolled here leads anywhere. One asked for by seed, or
 *     arrived at by a link, is the song that was asked for, and it ends where it ends.
 */
function advance() {
    if (!elements.autoplay.checked || !isSongRandom || isGenerating) return false;

    playNew(null, "auto");
    return true;
}

/**
 *     Gets a song and starts it the moment it has reached the player: asking for a song is asking to hear
 *     it, whether by the button, by a seed, from the list, or by the song before it running out.
 */
async function playNew(seed, origin) {
    // one already on its way is the one that will play: a second ask would only cancel it for nothing
    if (isGenerating) return;

    const request = ++playRequest;
    const generated = await generate(seed);

    // the player has to have been handed the song before it can be started on it
    await delivery;
    if (!generated || request !== playRequest) return;

    await withPlayer((player) => startPlaying(player, origin), "Playback failed");
}

/**
 *     Lets go of a song on its way to being started. Whatever was pressed since, Stop or Pause or another
 *     song, is the last word on what should be playing.
 */
function cancelPendingPlay() {
    playRequest++;
}

// --- soundfont -------------------------------------------------------------

elements.keep.checked = isKeeping();

elements.soundFont.addEventListener("change", async () => {
    const file = elements.soundFont.files?.[0];
    if (!file) return;

    elements.library.value = "";
    showLicense(null);
    await useSoundFont(file.name, () => file, "file");
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
        announce("Soundfont forgotten.");
        return;
    }

    // not waited for: whatever the browser says, the soundfont is saved the same way
    askToPersist();

    if (source === null) {
        announce("The next soundfont you load will be remembered.");
        return;
    }

    const saving = source;
    setStatus(`Saving ${saving.name}…`);

    const problem = await store(saving.name, saving.blob);
    if (problem !== null) {
        setStatus(`Could not save ${saving.name}: ${problem}.`, true);
        return;
    }

    saving.isStored = true;
    announce(`${saving.name} will load next time.`);
});

/** Serialised, because two loads at once would race each other inside the sound bank manager. */
function useSoundFont(name, read, origin, isStored = false) {
    // caught here, so one load that throws cannot stop every load after it
    loading = loading
        .then(() => loadSoundFont(name, read, origin, isStored))
        .catch((error) => setSoundFontState(`Could not load ${name}: ${error.message}`, true));
    return loading;
}

/**
 *     <paramref name="read" /> answers with the soundfont as a Blob or an ArrayBuffer. It is held as a
 *     Blob, and the player is handed a copy of its bytes, since loading detaches the buffer it is given.
 */
async function loadSoundFont(name, read, origin, isStored = false) {
    setSoundFontState(`Loading ${name}…`);

    let blob;
    let soundFont;
    try {
        const data = await read();
        blob = data instanceof Blob ? data : new Blob([data]);
        soundFont = await blob.arrayBuffer();
    } catch (error) {
        setStatus(`Could not read ${name}: ${explain(error)}`, true);

        // one already loaded is still the one playing, so it is what the page goes on showing
        if (source === null) {
            clearSoundFont();
            setSoundFontState(`${name} could not be read`, true);
        } else {
            elements.soundFont.value = "";
            selectInLibrary(source.name);
            setSoundFontState(source.name);
        }

        // the byte count tells the two failures apart: nothing arrived, or the connection gave out partway
        track("soundfont_failed", { detail: error.name || "unreadable", bytes: error.got ?? null });
        return;
    }

    // kept only once the player has taken it, so a file that will not load never replaces one that does
    source = { name, blob, isStored };
    pendingSoundFont = soundFont;
    hasSoundFont = true;
    updateTransport();

    setSoundFontState(name);

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
    // caught here, so one delivery that throws cannot stop every delivery after it
    delivery = delivery
        .then(deliverPending)
        .catch((error) => setStatus(`The player could not take the song: ${error.message}`, true));
    return delivery;
}

async function deliverPending() {
    if (pendingSoundFont === null && pendingSong === null) return;

    await firstGesture();

    // a player that would not start is no fault of the song or the soundfont: both wait for the next try
    const player = await withPlayer((player) => player, "The player could not start");
    if (player === null) return;

    // taken only now, since loading detaches the buffers and a second delivery must not resend them
    const soundFont = pendingSoundFont;
    const loaded = source;
    const song = pendingSong;
    pendingSoundFont = null;
    pendingSong = null;

    if (soundFont !== null) {
        try {
            await player.loadSoundFont(soundFont);
        } catch (error) {
            setStatus(`Could not load ${loaded.name}: ${error.message}`, true);

            // a kept one that would not load would only fail again on the next visit; any other kept one
            // is still good, and stays
            if (loaded.isStored) await forget().catch(() => {});
            if (source === loaded) clearSoundFont();

            // the song never reached the player, so it waits for the next soundfont
            if (song !== null) pendingSong ??= song;
            return;
        }

        // not waited for: saving hundreds of megabytes has no business holding up the first note
        if (isKeeping() && !loaded.isStored) keepLoaded(loaded);
    }

    try {
        if (song !== null) {
            player.loadSong(song);
            applyMix(player);
        } else if (soundFont !== null) {
            // a soundfont swapped under a song already playing: the bank took every channel back to
            // where it started, and the song will not say what it plays again until it comes round
            applyMix(player, true);
        }
    } catch (error) {
        setStatus(`The player could not take the song: ${error.message}`, true);
        return;
    }

    track("audio_ready", { ms: since() });
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
            if (!response.ok) {
                const refusal = new Error(await describeFailure(response));

                // a server that says no will say no again; only a failing server is worth asking twice
                refusal.isFinal = response.status < 500;
                throw refusal;
            }

            // a server that took no notice of the range is starting over, so what was kept is not the start
            if (got > 0 && response.status !== 206) {
                pieces.length = 0;
                got = 0;
            }

            // a missing length reads as 0, which would make whatever has arrived look like all of it
            const remaining = Number(response.headers.get("Content-Length"));
            if (total === 0 && remaining > 0) total = got + remaining;

            // read as it arrives, so an interruption keeps what came before it
            const reader = response.body.getReader();
            for (;;) {
                const { done, value } = await reader.read();
                if (done) break;

                pieces.push(value);
                got += value.length;
                setSoundFontState(`Downloading ${name}… ${sofar(got, total)}`);
            }

            return new Blob(pieces);
        } catch (error) {
            failure = error;
            if (attempt === DOWNLOAD_ATTEMPTS || error.isFinal) break;

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

function pause(ms) {
    return new Promise((resolve) => setTimeout(resolve, ms));
}

/** Saves a soundfont the player has taken, and says so beside its name only if it could not. */
async function keepLoaded(loaded) {
    const problem = await store(loaded.name, loaded.blob);

    // switched off while the save was still going: what was asked last is the last word
    if (!isKeeping()) {
        await forget().catch(() => {});
        return;
    }

    if (problem === null) {
        loaded.isStored = true;
        return;
    }

    if (source === loaded) setSoundFontState(`${loaded.name}, not saved: ${problem}`);
}

async function store(name, soundFont) {
    try {
        await keep(name, soundFont);
        return null;
    } catch (error) {
        return error.name === "QuotaExceededError" ? "not enough space" : error.message;
    }
}

function clearSoundFont() {
    setSoundFontState("None loaded");
    elements.soundFont.value = "";
    elements.library.value = "";
    showLicense(null);
    source = null;
    pendingSoundFont = null;
    hasSoundFont = false;
    updateTransport();
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

    // what the dropdown shows when the soundfont is not one of these, which is not itself a choice:
    // picking it would leave a soundfont playing under a dropdown that says there is none
    const choose = document.createElement("option");
    choose.value = "";
    choose.textContent = "Choose…";
    choose.disabled = true;
    choose.hidden = true;

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

        // a touch only allows audio once the finger lifts, so pointerdown is too early on a phone: a player
        // built then is never ready. A click comes after the lift, whatever did the clicking.
        const options = { once: true, capture: true };
        const done = () => {
            document.removeEventListener("click", done, options);
            document.removeEventListener("keydown", done, options);
            resolve();
        };

        document.addEventListener("click", done, options);
        document.addEventListener("keydown", done, options);
    });
}

// --- generating ------------------------------------------------------------

elements.seeded.addEventListener("toggle", () => {
    if (elements.seeded.open) elements.seedInput.focus();
});

// no seed at all, so the server rolls one and reports it back in X-Song-Seed
elements.generate.addEventListener("click", () => playNew(null, "generate"));

elements.generateSeeded.addEventListener("click", () => {
    const seed = readSeed(elements.seedInput.value.trim());
    if (seed === null) {
        setStatus(`'${elements.seedInput.value.trim()}' is not a valid seed. A seed is a 32-bit integer.`, true);
        return;
    }

    playNew(seed, "seed");
});

elements.seedInput.addEventListener("keydown", (event) => {
    if (event.key === "Enter") elements.generateSeeded.click();
});

/**
 *     A seed is a 32-bit integer, and anything else is worth saying so before it is sent. Written out in
 *     digits, too: Number takes "0x10" and "1e3" as well, which are not what anybody means by a seed.
 */
function readSeed(text) {
    const seed = Number(text);
    const isSeed = IS_SEED.test(text) && seed >= -(2 ** 31) && seed <= 2 ** 31 - 1;
    return isSeed ? seed : null;
}

/**
 *     The seed asked for in the address, as <c>?seed=12345</c>: the seed itself, and whether one was
 *     asked for at all. A link carrying something that is not a seed is worth saying so about, rather
 *     than quietly playing a different song and letting whoever sent it wonder.
 *
 *     <c>wasRolled</c> tells a refresh from a link. The page writes the seed it is playing into the
 *     address, so every refresh after the first arrives looking exactly like a seed somebody asked for;
 *     what marks it as the page's own roll is the state left on the history entry, which a refresh
 *     keeps and a link followed from anywhere else does not have.
 */
function linkedSeed() {
    const asked = new URL(location.href).searchParams.get("seed");
    const wasRolled = history.state?.rolled === true;

    if (asked === null) return { seed: null, wasAsked: false, wasRolled };

    return { seed: readSeed(asked.trim()), wasAsked: true, wasRolled };
}

/**
 *     Keeps the address on the song being heard, so a refresh gives the same one back and the address
 *     bar is itself a shareable link. Replaced rather than pushed: every roll of the dice is not a place
 *     to go back to.
 *
 *     Whether the page rolled this one goes on the entry with it. The address cannot say so by itself:
 *     a seed the page put there and a seed somebody sent read the same, and a refresh of a rolled song
 *     is still a rolled song, so it should go on rolling when it ends.
 */
function rememberSeed(songSeed, wasRolled) {
    try {
        history.replaceState({ rolled: wasRolled }, "", songLink(songSeed));
    } catch {
        // an address that cannot be rewritten costs nothing here; the share button reads the seed itself
    }
}

/** The link to a song, which is this address with its seed. */
function songLink(songSeed) {
    const address = new URL(location.href);
    address.searchParams.set("seed", songSeed);

    return address.toString();
}

function requestSong(song, signal = undefined) {
    return fetch(new URL("api/songs/generate", location.href), {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(song),
        signal,
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

/**
 *     Answers whether a song arrived, which is what tells a roll of the next one that it has something
 *     to play. <paramref name="isRolled" /> says the song was the page's own to roll rather than one
 *     asked for, which is what makes it lead to another when it ends; a seed refreshed back into the
 *     address is still one the page rolled, so the caller is allowed to say so.
 */
async function generate(seed, isRolled = seed === null) {
    if (isGenerating) return false;

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

        const song = await response.arrayBuffer();
        const songSeed = response.headers.get("X-Song-Seed") ?? String(seed);

        // only once the whole song is here: a body that fails halfway leaves the old song and its mix
        readSong(response.headers.get("X-Song-Instruments"));

        // built from what the server reported rather than from the loaded song, so the mix is on the
        // page as soon as there is a song at all, without waiting for the audio stack a click builds
        buildMixer([...instruments.keys()].sort((first, second) => first - second));

        // a song replaced while playing was listened to up to here, and the next one is from here
        if (startedListening !== null) {
            reportListening();
            startedListening = performance.now();
        }

        elements.seed.value = songSeed;
        rememberSeed(songSeed, isRolled);
        addToHistory(songSeed);
        // offered for download first, and as a copy, so it stays usable whatever the audio stack does
        offerDownload(song, songSeed);
        announce(`Generated song ${songSeed}.`);
        track("song_generated", { ms: since(), seed: Number(songSeed) });

        pendingSong = song;
        hasSong = true;
        isSongRandom = isRolled;
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

/**
 *     The buttons up top are disabled while a song is on its way. The list is not, since a disabled
 *     button drops the keyboard; generate simply ignores a press that arrives in the meantime.
 */
function setGenerating(isOn) {
    isGenerating = isOn;
    elements.generate.disabled = isOn;
    elements.generateSeeded.disabled = isOn;
}

/**
 *     Safari says "Load failed" and Chrome "Failed to fetch"; neither says what to do next. A soundfont
 *     is tens of megabytes, so the answer is usually to try it again or to take a smaller one.
 */
function explain(error) {
    if (error.name !== "TypeError") return error.message;

    return error.got > 0
        ? "the download stopped. Choose it again or pick a smaller one."
        : "the download would not start. Try again.";
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
    elements.download.removeAttribute("aria-disabled");
}

// the download is a plain anchor, so the click is the only place it can be noticed
elements.download.addEventListener("click", () => {
    track("download_mid", { seed: Number(elements.seed.value) || null });
});

// --- the songs so far ------------------------------------------------------

/** How many seeds the page keeps before the oldest is let go of. */
const SONGS_KEPT = 50;

restoreHistory();

/**
 *     Puts back the list this browser kept, before a song is asked for, so the one this visit opens
 *     with goes on top of what came before rather than starting the list over.
 */
function restoreHistory() {
    // a seed is on the list once, where it was last heard, which a list kept before that rule may not say
    const seeds = [...new Set(recallHistory())].slice(0, SONGS_KEPT);
    elements.history.append(...seeds.map(createHistoryItem));
    elements.clearHistory.disabled = elements.history.children.length === 0;
}

/** The seeds in the list, newest first: the buttons are the list, so it is read off them. */
function historySeeds() {
    return [...elements.history.children].map((item) => item.textContent);
}

/** The list is this browser's to be rid of, being the one thing here that says what anybody listened to. */
elements.clearHistory.addEventListener("click", () => {
    elements.history.replaceChildren();
    elements.clearHistory.disabled = true;
    keepHistory([]);
});

/**
 *     Puts a seed on top of the list, however it came about: rolled, typed, followed from a link, rolled
 *     by the song before it running out, or pressed on the list itself. The list is the order songs were
 *     heard in, so a seed heard again moves to the top, and wherever it was before is taken off.
 */
function addToHistory(songSeed) {
    const earlier = [...elements.history.children].filter((item) => item.textContent === songSeed);

    // the keyboard follows a pressed seed to the top, rather than being dropped with the button it was on
    const wasFocused = earlier.includes(document.activeElement);
    for (const item of earlier) item.remove();

    const item = createHistoryItem(songSeed);
    elements.history.prepend(item);
    while (elements.history.children.length > SONGS_KEPT) elements.history.lastElementChild.remove();

    // the song on the page is the top of the list, so that is where the list is scrolled to
    elements.history.scrollTop = 0;
    if (wasFocused) item.focus({ preventScroll: true });

    keepHistory(historySeeds());
    markCurrentSong(songSeed);
    elements.clearHistory.disabled = false;
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

    button.addEventListener("click", () => {
        if (isGenerating) return;

        playNew(Number(songSeed), "history");
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
    const seed = elements.seed.value;
    const link = songLink(seed);

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
        announce(`Link to song ${seed} copied.`);
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

    // a second press starts the wait over, rather than the first one cutting the second short
    clearTimeout(sayTimer);
    sayTimer = setTimeout(() => (elements.share.textContent = "Share"), 1600);
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

    // taken now: a soundfont that fails to load while the song is fetched takes `source` with it
    const exporting = source;

    isExporting = true;
    updateTransport();
    setStatus("Fetching the song…");

    try {
        const response = await requestSong({ seed: Number(songSeed), ...describeMix() });
        if (!response.ok) throw new Error(await describeFailure(response));

        const song = await response.arrayBuffer();
        const soundFont = await exporting.blob.arrayBuffer();

        const audio = await renderSong(song, soundFont, (progress) =>
            setStatus(`Rendering ${formatPercent(progress)}…`));

        const mp3 = await encodeMp3(audio, Number(elements.bitrate.value), (progress) =>
            setStatus(`Encoding ${formatPercent(progress)}…`));

        offerExport(mp3, name);
        announce(`Exported ${name}, ${formatSize(mp3.size)}.`);
        track("export_mp3", { ms: since(), seed: Number(songSeed) || null, detail: `${elements.bitrate.value} kbps` });
    } catch (error) {
        setStatus(`Could not export ${name}: ${error.message}`, true);
    } finally {
        isExporting = false;
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

elements.play.addEventListener("click", () => playOrPause());

/**
 *     The play button, which toggles, and the lock screen's play and pause, which each only go one way:
 *     <paramref name="wanted" /> is true to play, false to pause, and left out to toggle.
 */
async function playOrPause(wanted) {
    cancelPendingPlay();

    // this click is very likely the gesture the fetched song and soundfont have been waiting for
    await deliver();

    await withPlayer(async (player) => {
        if (player.paused) {
            if (wanted !== false) await startPlaying(player);
            return;
        }

        if (wanted === true) return;

        stopTicking();
        player.pause();
        reportListening();
        setPlayIcon(false);
        showMedia(player);
    }, "Playback failed");
}

/** Starts the song, and everything that follows it while it plays. */
async function startPlaying(player, origin = null) {
    await player.play();
    startTicking(player);
    setPlayIcon(true);
    showMedia(player);
    startedListening = performance.now();
    track("play", { seed: Number(elements.seed.value) || null, detail: origin });
}

/**
 *     The lock screen and media keys, which drive the page the way its own buttons do. Next is a new
 *     random song, the way the song after this one would be.
 */
const mediaControls = createMediaControls({
    play: () => playOrPause(true),
    pause: () => playOrPause(false),
    stop: () => elements.stop.click(),
    next: () => playNew(null, "lockscreen"),
    seek: (time, offset) => withPlayer((player) => {
        const duration = player.duration;
        const target = Math.min(Math.max(time ?? player.currentTime + offset, 0), duration);
        player.currentTime = target;
        render(player, target);
        showMedia(player, target);
    }, "Seeking failed"),
});

/** Tells the lock screen where the song is, and whether it is playing. */
function showMedia(player, position = player.currentTime) {
    const seed = elements.seed.value;
    if (player.paused) mediaControls.paused(seed, position, player.duration);
    else mediaControls.playing(seed, position, player.duration);
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
    cancelPendingPlay();
    reportListening();

    await withPlayer((player) => {
        stopTicking();
        player.stop();
        setPlayIcon(false);
        render(player, 0);
        mediaControls.paused(elements.seed.value, 0, player.duration);
    }, "Playback failed");
});

// a Tab away from the bar is let go of on the next element, so leaving the bar ends the seek too
elements.seek.addEventListener("pointerdown", () => {
    isSeeking = true;
    seekedFrom = { value: elements.seek.value, elapsed: elements.elapsed.textContent };
});
elements.seek.addEventListener("keydown", () => (isSeeking = true));
elements.seek.addEventListener("keyup", () => (isSeeking = false));
elements.seek.addEventListener("blur", () => (isSeeking = false));
window.addEventListener("pointerup", () => (isSeeking = false));

// a touch that turns into a scroll is cancelled rather than let go of, and no change follows it, so the
// bar goes back to where the song is rather than staying where the finger happened to leave it
elements.seek.addEventListener("pointercancel", () => {
    isSeeking = false;
    if (seekedFrom === null) return;

    elements.seek.value = seekedFrom.value;
    elements.elapsed.textContent = seekedFrom.elapsed;
    seekedFrom = null;
});

// a seek replays the song up to the new place, so dragging only shows where it would go, and the
// sequencer is moved once, when the bar is let go of
elements.seek.addEventListener("input", async () => {
    await withPlayer((player) => {
        elements.elapsed.textContent = formatTime(Number(elements.seek.value) * player.duration);
    }, "Seeking failed");
});

elements.seek.addEventListener("change", async () => {
    await withPlayer((player) => {
        const time = Number(elements.seek.value) * player.duration;
        player.currentTime = time;
        render(player, time);
        showMedia(player, time);
    }, "Seeking failed");
});

elements.volume.addEventListener("input", async () => {
    elements.volumeValue.textContent = formatPercent(Number(elements.volume.value));
    await withPlayer((player) => (player.masterGain = Number(elements.volume.value)), "Could not set the volume");
});

// --- mixer -----------------------------------------------------------------

function buildMixer(channels) {
    rows.clear();
    muted.clear();
    soloed.clear();
    elements.mixer.replaceChildren();

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
    for (const channel of instruments.keys()) rollInstrument(channel);
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

        withPlayer((player) => player.setChannelVolume(channel, songVolume * volume), "Could not set the volume");
    });

    // counted once it is let go of: a drag is one change, not one for every step it passed through
    fader.addEventListener("change", () => track("mix_changed", { detail: "volume" }));

    const field = document.createElement("div");
    field.className = "fader-field";
    field.append(fader, shown);
    return field;
}

function formatPercent(volume) {
    return `${Math.round(volume * 100)}%`;
}

/** The General MIDI instruments, grouped by family and numbered by counting through them in order. */
function fillInstruments(select) {
    let instrument = 0;

    select.append(...INSTRUMENT_FAMILIES.map((family) => {
        const group = document.createElement("optgroup");
        group.label = family.name;
        group.append(...family.instruments.map((name) => createInstrumentOption(instrument++, name)));
        return group;
    }));
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
 *     Puts the mix on the page onto the player. Loading a song lets go of every lock the mixer set, and
 *     anything chosen before the first click had nowhere to go until now. Only what was actually changed
 *     here is applied: the song carries its own instruments and volumes, and locking a channel to what it
 *     already plays would stop the song setting it again on the way round.
 *
 *     A new sound bank undoes more than that: it resets every channel to the first program, so what the
 *     song itself asked for is as lost as what anybody picked, and the drum channel forgets it is one.
 *     After a bank, every channel is told what it plays, but only a choice made here is locked, so a
 *     channel the song owns is still the song's to set when it starts over.
 */
function applyMix(player, isBankReset = false) {
    for (const [channel, instrument] of instruments) {
        if (isBankReset && channel === DRUM_CHANNEL) player.setChannelDrums(channel, true);

        if (isBankReset || chosen.has(channel)) player.setChannelInstrument(channel, instrument, chosen.has(channel));

        if (volumes.has(channel) || songVolume !== 1)
            player.setChannelVolume(channel, songVolume * (volumes.get(channel) ?? 1));

        player.setChannelMuted(channel, isSilenced(channel));
    }
}

function applyMuting() {
    withPlayer((player) => {
        for (const channel of instruments.keys()) player.setChannelMuted(channel, isSilenced(channel));
    }, "Could not silence the channel");
}

elements.songVolume.addEventListener("input", async () => {
    songVolume = Number(elements.songVolume.value);
    showSongVolume();
    scheduleDownloadRefresh();

    await withPlayer((player) => {
        for (const channel of instruments.keys())
            player.setChannelVolume(channel, songVolume * (volumes.get(channel) ?? 1));
    }, "Could not set the volume of the song");
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
    downloadRefresh?.abort();
    downloadRefresh = null;

    clearTimeout(downloadTimer);
    downloadTimer = null;
}

async function refreshDownload() {
    downloadTimer = null;

    const songSeed = elements.seed.value;
    if (songSeed === "") return;

    const refresh = new AbortController();
    downloadRefresh = refresh;
    try {
        const response = await requestSong({ seed: Number(songSeed), ...describeMix() }, refresh.signal);
        if (!response.ok) throw new Error(await describeFailure(response));

        offerDownload(await response.arrayBuffer(), songSeed);
    } catch (error) {
        // one let go of for a later choice is not a failure: that choice is asking for its own file
        if (!refresh.signal.aborted) setStatus(`The download does not include your mix: ${error.message}`, true);
    } finally {
        if (downloadRefresh === refresh) downloadRefresh = null;
    }
}

// --- ticking ---------------------------------------------------------------

/**
 *     The sequencer applies a new position in the worklet, so reading it straight back after a
 *     stop or a seek still gives the old one: callers that just moved it pass the position instead.
 */
function render(player, time = player.currentTime) {
    const duration = player.duration;
    elements.total.textContent = formatTime(duration);

    // the bar and the time under it are the seek's own while it lasts
    if (!isSeeking) {
        if (duration > 0) elements.seek.value = String(time / duration);
        elements.elapsed.textContent = formatTime(time);
    }

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

    // said afterwards, since the generating itself has the status line until it is done with it, and
    // only once a song has arrived: a failure has the status line to itself
    const generating = generate(linked.seed, linked.seed === null || linked.wasRolled);
    if (linked.wasAsked && linked.seed === null)
        generating.then((isGenerated) => {
            if (isGenerated) setStatus("That link has no valid seed, so here is a random song.", true);
        });

    // listed whatever is loaded, so the dropdown is ready for anyone who opens the panel, and asked for
    // together, since neither needs the other
    const [offered, kept] = await Promise.all([listLibrary(), recallKept()]);

    if (kept) {
        // the dropdown still points at it when what this browser kept is one the server offers too
        selectInLibrary(kept.name);

        // a Blob out of IndexedDB is backed by the disk, so holding it costs no memory until it is read
        await useSoundFont(kept.name, () => kept.soundFont, "kept", true);
        return;
    }

    if (offered === null) {
        setSoundFontState("None — choose one");
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
