import {
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
    isAutoplaying,
    isKeeping,
    keep,
    keepHistory,
    keepRating,
    keepSettings,
    recall,
    recallHistory,
    recallRating,
    recallSettings,
    setAutoplaying,
    setKeeping,
} from "./storage.js";
import {
    DRUM_GROUPS,
    DRUM_SETUPS,
    FACETS,
    formatSettings,
    identityOf,
    LAST_STEP,
    MIDDLE_PAN,
    parseSettings,
    PARTS,
    PLAYS,
    readSeed,
} from "./settings.js";
import { applyReport, defaultSettings, givenOnly, isEveryPartOff, requestFor } from "./song-settings.js";
import { since, tell, track } from "./tally.js";

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
    settingsPanel: document.getElementById("settings-panel"),
    settingsState: document.getElementById("settings-state"),
    allOff: document.getElementById("all-off"),
    songSettings: document.getElementById("song-settings"),
    harmonySettings: document.getElementById("harmony-settings"),
    melodySettings: document.getElementById("melody-settings"),
    soundSettings: document.getElementById("sound-settings"),
    drumSettings: document.getElementById("drum-settings"),
    drumGroups: document.getElementById("drum-groups"),
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
    songVolume: document.getElementById("song-volume"),
    songVolumeValue: document.getElementById("song-volume-value"),
    rollInstruments: document.getElementById("roll-instruments"),
    rollFirst: document.getElementById("roll-first"),
    rollLast: document.getElementById("roll-last"),
    mixer: document.getElementById("mixer"),
    status: document.getElementById("status"),
    version: document.getElementById("version"),
    like: document.getElementById("like"),
    dislike: document.getElementById("dislike"),
};

// the parts muted or soloed here, which only this page hears, and every part's row of the mixer, by the part
const muted = new Set();
const soloed = new Set();
const rows = new Map();

// the settings every song is asked for with, and the one on the page heard with (song-settings.js): every value given,
// or as the song on the page drew it; kept in this browser for the next visit
let settings = restoreSettings();

// the song on the page, as its seed, in letters and digits: null before the first
let songSeed = null;

// what the song on the page is made of, as the server said: the channel every part plays on in the file the player
// has, null where it is not written, whether each part is in the song, its drum setup and the drum groups it plays
let channels = {};
// the channel of a part's twin, the riff's, which follows the part's mix on the other side
let twins = {};
let partsInSong = {};
let drumSetup = null;
let drumGroupsPresent = [];

// the song asked for again with changed settings, while the one on the page plays on, and where it is to go on from
let regenerationTimer = null;
let regeneration = null;
let pendingSwap = false;

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
// the songs' version the song on the page was made by, which every event about the song carries: the same seed
// is another song in another version, and a page open across a deploy still plays the song it got
let songVersion = null;
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

    // nor is rating it, though it is mostly done while it plays
    elements.like.disabled = !hasSong || songVersion === null;
    elements.dislike.disabled = !hasSong || songVersion === null;
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
    if (isEveryPartOff(settings)) {
        setStatus("Every part is off, so no next song plays. Turn a part on in the song settings.", true);
        return false;
    }

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
            // a song made again with changed settings goes on from the same share of its length, playing if it was
            const swap = pendingSwap ? { share: player.duration > 0 ? player.currentTime / player.duration : 0, wasPlaying: !player.paused } : null;
            pendingSwap = false;
            const loaded = player.nextSong();
            player.loadSong(song);
            applyMix(player);
            if (swap !== null) {
                await loaded;
                player.currentTime = swap.share * player.duration;
                if (swap.wasPlaying && player.paused) await player.play();
                else if (!swap.wasPlaying && !player.paused) player.pause();
                render(player);
                showMedia(player);
            }
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

// no seed at all, so the server rolls one and reports it back in X-Song-Seed
elements.generate.addEventListener("click", () => playNew(null, "generate"));

// the seed typed, or a random one where none is, with the settings as they are
elements.generateSeeded.addEventListener("click", () => {
    const text = elements.seedInput.value.trim();
    if (text === "") {
        playNew(null, "generate");
        return;
    }

    const seed = readSeed(text);
    if (seed === null) {
        setStatus(`'${text}' is not a seed. A seed is up to six letters and digits.`, true);
        return;
    }

    playNew(seed, "seed");
});

elements.seedInput.addEventListener("keydown", (event) => {
    if (event.key === "Enter") elements.generateSeeded.click();
});

/**
 *     The song asked for in the address, as <c>?song=12Ab&settings=1…</c>: its seed and the settings it was heard with,
 *     and whether one was asked for at all. A link carrying something that is not a seed, or settings that cannot be
 *     read, is worth saying so about, as is a link from before seeds were written in letters (<c>?seed=12345</c>),
 *     rather than quietly playing a different song and letting whoever sent it wonder.
 *
 *     <c>wasRolled</c> tells a refresh from a link. The page writes the song it is playing into the address, so every
 *     refresh after the first arrives looking exactly like a song somebody asked for; what marks it as the page's own
 *     roll is the state left on the history entry, which a refresh keeps and a link followed from anywhere else does
 *     not have.
 */
function linkedSong() {
    const parameters = new URL(location.href).searchParams;
    const wasRolled = history.state?.rolled === true;
    const isOld = parameters.has("seed") && !parameters.has("song");
    const asked = parameters.get("song");
    if (asked === null) return { seed: null, wasAsked: false, isOld, wasRolled, settings: null, isSettingsBad: false };

    const linked = parameters.get("settings");
    const parsed = linked === null ? null : parseSettings(linked.trim());
    return { seed: readSeed(asked.trim()), wasAsked: true, isOld, wasRolled, settings: parsed, isSettingsBad: linked !== null && parsed === null };
}

/**
 *     Keeps the address on the song being heard, as it is heard, so a refresh gives the same one back and the address
 *     bar is itself a shareable link. Replaced rather than pushed: every roll of the dice is not a place to go back to.
 *
 *     Whether the page rolled this one goes on the entry with it. The address cannot say so by itself: a song the page
 *     put there and a song somebody sent read the same, and a refresh of a rolled song is still a rolled song, so it
 *     should go on rolling when it ends.
 */
function rememberSong(wasRolled = history.state?.rolled === true) {
    if (songSeed === null) return;

    try {
        history.replaceState({ rolled: wasRolled }, "", songLink());
    } catch {
        // an address that cannot be rewritten costs nothing here; the share button reads the song itself
    }
}

/** The link to the song on the page, which is this address with its seed and its settings as they are now. */
function songLink() {
    const address = new URL(location.href);
    address.searchParams.delete("seed");
    address.searchParams.set("song", songSeed);
    address.searchParams.set("settings", formatSettings(settings));

    return address.toString();
}

/** What every event about the song on the page carries: which song it is, and what it was heard with. */
function aboutSong(measurements = {}) {
    return {
        seed: songSeed,
        version: songVersion,
        settings: songSeed === null ? null : formatSettings(settings),
        ...measurements,
    };
}

/** A song as the page names it: its seed, and what of its settings names it too, which the same seed is another song by. */
function songName(seed, songSettings) {
    const identity = identityOf(songSettings);
    return identity === "" ? seed : `${seed}.${identity}`;
}

/** The song on the page, as the page names it; null before the first. */
function currentSong() {
    return songSeed === null ? null : songName(songSeed, settings);
}

/** The name the song on the page is downloaded under. */
function fileName() {
    return `song-${songSeed}${identityOf(settings)}`;
}

function requestSong(song, signal = undefined) {
    return fetch(new URL("api/songs/generate", location.href), {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(song),
        signal,
    });
}

/**
 *     Answers whether a song arrived, which is what tells a roll of the next one that it has something to play. A new
 *     song is asked for by its seed, null for a random one, with the settings as they are; <paramref name="isRolled" />
 *     says it was the page's own to roll rather than one asked for, which is what makes it lead to another when it
 *     ends, and a seed refreshed back into the address is still one the page rolled, so the caller is allowed to say
 *     so. A song remade (<paramref name="isRemade" />) is the one on the page asked for again with changed settings,
 *     which replaces it where it plays, and takes its place on the list.
 */
async function generate(seed, { isRolled = seed === null, isRemade = false, signal = undefined } = {}) {
    if (isEveryPartOff(settings)) {
        showEveryPartOff();
        return false;
    }

    if (!isRemade) {
        if (isGenerating) return false;
        setGenerating(true);
        cancelRegeneration();
        cancelDownloadRefresh();
        setStatus("Generating…");
    }

    try {
        const response = await requestSong(requestFor(seed, settings), signal);
        if (!response.ok) {
            setStatus(`Could not generate a song: ${await describeFailure(response)}`, true);
            track("song_failed", { detail: `http ${response.status}` });
            return false;
        }

        const song = await response.arrayBuffer();
        const report = JSON.parse(response.headers.get("X-Song-Settings"));

        // a song replaced while playing was listened to up to here, and the next one is from here
        if (!isRemade && startedListening !== null) {
            reportListening();
            startedListening = performance.now();
        }

        // only once the whole song is here: a body that fails halfway leaves the old song as it was
        const made = applyReport(settings, report);
        ({ settings, channels, twins, own } = made);
        partsInSong = made.plays;
        drumSetup = made.drumSetup;
        drumGroupsPresent = made.drumGroups;
        songSeed = response.headers.get("X-Song-Seed") ?? seed;
        songVersion = response.headers.get("X-Song-Version");
        elements.seed.value = songSeed;
        if (!isRemade) {
            muted.clear();
            soloed.clear();
        }

        showSettings();
        showRating(recallRating(songVersion, currentSong()));
        rememberSong(isRemade ? undefined : isRolled);
        addToHistory(isRemade);
        // offered for download first, and as a copy, so it stays usable whatever the audio stack does
        offerDownload(song);
        if (isRemade) {
            announce("The song is made again with the new settings.");
        } else {
            announce(`Generated song ${songSeed}.`);
            track("song_generated", aboutSong({ ms: since() }));
        }

        pendingSong = song;
        pendingSwap = isRemade;
        hasSong = true;
        if (!isRemade) isSongRandom = isRolled;
        updateTransport();
        deliver();

        return true;
    } catch (error) {
        // one let go of for a later change is not a failure: that change is asking for its own song
        if (signal?.aborted) return false;

        setStatus(`Could not generate a song: ${error.message}`, true);
        track("song_failed", { detail: error.name || "failed" });
        return false;
    } finally {
        if (!isRemade) setGenerating(false);
    }
}

/**
 *     The song on the page asked for again, once the settings have stopped changing for a moment, with them, to replace
 *     it where it plays; one on its way is let go of for the newer change.
 */
function scheduleRegeneration() {
    cancelRegeneration();
    if (songSeed === null || isEveryPartOff(settings)) return;

    regenerationTimer = setTimeout(() => {
        regenerationTimer = null;
        cancelDownloadRefresh();
        regeneration = new AbortController();
        const signal = regeneration.signal;
        generate(songSeed, { isRemade: true, signal }).finally(() => {
            if (regeneration?.signal === signal) regeneration = null;
        });
    }, 400);
}

function cancelRegeneration() {
    clearTimeout(regenerationTimer);
    regenerationTimer = null;
    regeneration?.abort();
    regeneration = null;
}

/**
 *     The buttons up top are disabled while a song is on its way, or while every part is off, which leaves nothing to
 *     play. The list is not, since a disabled button drops the keyboard; generate simply ignores a press that arrives
 *     in the meantime.
 */
function setGenerating(isOn) {
    isGenerating = isOn;
    updateGenerateButtons();
}

function updateGenerateButtons() {
    const isBlocked = isGenerating || isEveryPartOff(settings);
    elements.generate.disabled = isBlocked;
    elements.generateSeeded.disabled = isBlocked;
}

/** Says, where the settings are and on the status line, that every part is off, and why nothing can be generated. */
const EVERY_PART_OFF = "Every part is off, so there is nothing to play. Turn a part on in the song settings.";

function showEveryPartOff() {
    const isOff = isEveryPartOff(settings);
    elements.allOff.hidden = !isOff;
    updateGenerateButtons();
    if (isOff) setStatus(EVERY_PART_OFF, true);
    else if (elements.status.textContent === EVERY_PART_OFF) setStatus("");
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

function offerDownload(song) {
    if (downloadUrl) URL.revokeObjectURL(downloadUrl);

    downloadUrl = URL.createObjectURL(new Blob([song], { type: "audio/midi" }));
    elements.download.href = downloadUrl;
    elements.download.download = `${fileName()}.mid`;
    elements.download.removeAttribute("aria-disabled");
}

// the download is a plain anchor, so the click is the only place it can be noticed
elements.download.addEventListener("click", () => {
    track("download_mid", aboutSong());
});

// --- the settings kept -----------------------------------------------------

/** The settings this browser left last time, every value given as it was; none given on a first visit. */
function restoreSettings() {
    const kept = parseSettings(recallSettings());
    return kept === null ? defaultSettings() : givenOnly(kept);
}

/** Keeps the settings for the next visit, and says how many of them change the song. */
function keepCurrentSettings() {
    keepSettings(formatSettings(givenOnly(settings)));
    const given = [settings.unconventionality, settings.tempo, settings.key, settings.meter, ...FACETS.map((facet) => settings.facets[facet])].filter((x) => x.isGiven).length
        + PARTS.filter((part) => settings.parts[part].plays !== "random").length
        + (settings.drumSetup === null ? 0 : 1);
    elements.settingsState.textContent = given === 0 ? "All drawn" : `${given} given`;
}

// --- the songs so far ------------------------------------------------------

/** How many songs the page keeps before the oldest is let go of. */
const SONGS_KEPT = 50;

restoreHistory();

/**
 *     Puts back the list this browser kept, before a song is asked for, so the one this visit opens
 *     with goes on top of what came before rather than starting the list over.
 */
function restoreHistory() {
    const entries = recallHistory().slice(0, SONGS_KEPT);
    elements.history.append(...entries.map(createHistoryItem));
    elements.clearHistory.disabled = elements.history.children.length === 0;
}

/** The songs in the list, newest first, as the page keeps them: the buttons are the list, so it is read off them. */
function historyEntries() {
    return [...elements.history.children].map((item) => item.dataset.entry);
}

/** The list is this browser's to be rid of, being the one thing here that says what anybody listened to. */
elements.clearHistory.addEventListener("click", () => {
    elements.history.replaceChildren();
    elements.clearHistory.disabled = true;
    keepHistory([]);
});

/**
 *     Puts the song on the page on top of the list, however it came about: rolled, typed, followed from a link, rolled
 *     by the song before it running out, or pressed on the list itself. The list is the order songs were heard in, so a
 *     song heard again moves to the top, and wherever it was before is taken off; a song remade with changed settings
 *     takes the place of the one it was made from, so that trying settings does not fill the list.
 */
function addToHistory(isRemade) {
    const name = currentSong();
    const entry = `${songSeed}:${formatSettings(givenOnly(settings))}`;
    const top = elements.history.firstElementChild;
    const earlier = [...elements.history.children].filter((item) => item.dataset.song === name || (isRemade && item === top && top.dataset.seed === songSeed));

    // the keyboard follows a pressed song to the top, rather than being dropped with the button it was on
    const wasFocused = earlier.includes(document.activeElement);
    for (const item of earlier) item.remove();

    const item = createHistoryItem(entry);
    elements.history.prepend(item);
    while (elements.history.children.length > SONGS_KEPT) elements.history.lastElementChild.remove();

    // the song on the page is the top of the list, so that is where the list is scrolled to
    elements.history.scrollTop = 0;
    if (wasFocused) item.focus({ preventScroll: true });

    keepHistory(historyEntries());
    markCurrentSong(name);
    elements.clearHistory.disabled = false;
}

/** Marks the song on the page, which is where the list is being read from. */
function markCurrentSong(name) {
    for (const item of elements.history.children) {
        if (item.dataset.song === name) item.setAttribute("aria-current", "true");
        else item.removeAttribute("aria-current");
    }
}

/**
 *     One song to come back to: its seed, marked where it was asked for with settings that change it, which it is asked
 *     for with again, the settings becoming the page's. What comes back is the song as it was generated and heard, and
 *     it is the song that was asked for: it leads to no other when it ends.
 */
function createHistoryItem(entry) {
    const [seedText, settingsText] = entry.split(":");
    const entrySettings = parseSettings(settingsText) ?? defaultSettings();
    const seed = readSeed(seedText);
    const identity = identityOf(entrySettings);

    const button = document.createElement("button");
    button.type = "button";
    button.dataset.entry = entry;
    button.dataset.seed = seedText;
    button.dataset.song = songName(seed, entrySettings);
    button.textContent = identity === "" ? seedText : `${seedText} ⚙`;
    button.setAttribute("aria-label", identity === "" ? `Song ${seedText}` : `Song ${seedText}, with its settings`);

    button.addEventListener("click", () => {
        if (isGenerating) return;

        settings = entrySettings;
        keepCurrentSettings();
        showSettings();
        playNew(seed, "history");
    });

    return button;
}

// --- sharing ---------------------------------------------------------------

elements.share.addEventListener("click", share);

/**
 *     Hands over a link to the song on the page. The share sheet where there is one, which is phones and
 *     little else, and the clipboard everywhere else. The link is the seed and the settings, so what arrives is the
 *     song with the mix as it was left.
 */
async function share() {
    const seed = elements.seed.value;
    const link = songLink();

    if (navigator.share !== undefined) {
        try {
            await navigator.share({ title: `RMG song ${seed}`, url: link });
            track("shared", aboutSong({ detail: "sheet" }));
            return;
        } catch (error) {
            // thinking better of it halfway through a share sheet is not a failure worth reporting
            if (error.name === "AbortError") return;
        }
    }

    if (await copy(link)) {
        say("Copied");
        announce(`Link to song ${seed} copied.`);
        track("shared", aboutSong({ detail: "clipboard" }));
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
    const name = `${fileName()}.mp3`;

    // taken now: a soundfont that fails to load while the song is fetched takes `source` with it
    const exporting = source;

    isExporting = true;
    updateTransport();
    setStatus("Fetching the song…");

    try {
        const response = await requestSong(requestFor(songSeed, settings));
        if (!response.ok) throw new Error(await describeFailure(response));

        const song = await response.arrayBuffer();
        const soundFont = await exporting.blob.arrayBuffer();

        const audio = await renderSong(song, soundFont, (progress) =>
            setStatus(`Rendering ${formatPercent(progress)}…`));

        const mp3 = await encodeMp3(audio, Number(elements.bitrate.value), (progress) =>
            setStatus(`Encoding ${formatPercent(progress)}…`));

        offerExport(mp3, name);
        announce(`Exported ${name}, ${formatSize(mp3.size)}.`);
        track("export_mp3", aboutSong({ ms: since(), detail: `${elements.bitrate.value} kbps` }));
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
    track("play", aboutSong({ detail: origin }));
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

    if (seconds >= 1) track("listened", aboutSong({ seconds }));
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

// --- the settings ----------------------------------------------------------

/** What every value is called on the page, and what it moves, by its key: the unconventionality or a facet. */
const SETTING_NAMES = {
    unconventionality: ["How plain or experimental", "everything below strays around it"],
    feel: ["Feel", "meter, tuplets and swing"],
    form: ["Form", "song form, intro and ending"],
    scale: ["Scale", "modes and key changes"],
    progression: ["Progression", "how freely the chords move"],
    chords: ["Chords", "colours and voicings"],
    melody: ["Melody", "improvisation and shape"],
    sound: ["Sound", "instruments over the song, articulation, bends and effects"],
    groove: ["Groove", "the drums' patterns"],
    fills: ["Fills", "the drums' fills"],
};

const PART_NAMES = { melody: "Melody", chords: "Chords", bass: "Bass", pad: "Pad", counterMelody: "Counter-melody", drum: "Drums", riff: "Riff", rhythm: "Rhythm" };

const DRUM_GROUP_NAMES = {
    kick: "Kick", snare: "Snare", timekeepers: "Hi-hat and ride", toms: "Toms", accents: "Cymbals", percussion: "Percussion", calls: "Calls",
};

const SETUP_NAMES = { kit: "Kit", kitAndPercussion: "Kit and percussion", percussion: "Percussion" };

// the General MIDI instruments by their number, and the kits the drums play
const INSTRUMENT_NAMES = INSTRUMENT_FAMILIES.flatMap((family) => family.instruments);

const KEY_NAMES = ["C", "C♯", "D", "E♭", "E", "F", "F♯", "G", "A♭", "A", "B♭", "B"];

// every choice's row, by its key: the tempo, the key and the meter, listed as the server lists them
const choiceRows = new Map();

// every value's row, by its key, and every drum group's
const settingRows = new Map();
const groupRows = new Map();
let setupSelect = null;

// the song's own instrument and pan of every part, whatever the mix plays, as the server said
let own = {};

buildSettings();

/** The settings panel: the values that change the song, every part's row of the mixer, and the drums'. */
function buildSettings() {
    elements.songSettings.append(
        createSettingRow("unconventionality"),
        createChoiceRow("tempo", "Tempo", "beats a minute"),
        createChoiceRow("key", "Key", "the key it starts in"),
        createChoiceRow("meter", "Meter", "the beats of a bar"),
        createSettingRow("feel"),
        createSettingRow("form"),
    );
    elements.harmonySettings.append(createSettingRow("scale"), createSettingRow("progression"), createSettingRow("chords"));
    elements.melodySettings.append(createSettingRow("melody"));
    elements.soundSettings.append(createSettingRow("sound"));
    elements.drumSettings.append(createSetupRow(), createSettingRow("groove"), createSettingRow("fills"));
    elements.mixer.append(...PARTS.map(createPartRow));
    elements.drumGroups.append(...DRUM_GROUPS.map(createDrumGroupRow));

    showSettings();
    keepCurrentSettings();
    listChoices();
}

function settingOf(key) {
    return FACETS.includes(key) ? settings.facets[key] : settings[key];
}

function setSetting(key, setting) {
    if (FACETS.includes(key)) settings.facets[key] = setting;
    else settings[key] = setting;
}

/**
 *     A value that changes the song and is one of a list, the tempo, the key or the meter: checked, it is given, and the
 *     list sets it; unchecked, the list shows what the song on the page drew.
 */
function createChoiceRow(key, name, hint) {
    const given = document.createElement("input");
    given.type = "checkbox";
    given.setAttribute("aria-label", `Give the ${name.toLowerCase()}`);

    const label = document.createElement("span");
    label.className = "setting-name";
    label.textContent = name;
    const explained = document.createElement("small");
    explained.textContent = hint;
    label.append(explained);

    const select = document.createElement("select");
    select.setAttribute("aria-label", name);
    if (key === "key") select.append(...KEY_NAMES.map((x, i) => createOption(String(i), x)));

    given.addEventListener("change", () => {
        setSetting(key, { isGiven: given.checked, value: Number(select.value || 0) });
        showChoice(key);
        settingsChanged(true);
    });
    select.addEventListener("change", () => {
        setSetting(key, { isGiven: true, value: Number(select.value) });
        showChoice(key);
        settingsChanged(true);
    });

    const row = document.createElement("div");
    row.className = "setting";
    row.append(given, label, select);
    choiceRows.set(key, { given, select });
    return row;
}

function showChoice(key) {
    const row = choiceRows.get(key);
    const setting = settingOf(key);
    row.given.checked = setting.isGiven;
    row.select.disabled = !setting.isGiven;
    if (row.select.options.length > setting.value) row.select.value = String(setting.value);
}

/** The tempos and meters a song may be given, from the server, which only it knows. */
async function listChoices() {
    try {
        const response = await fetch(new URL("api/songs/options", location.href));
        if (!response.ok) return;

        const { tempos, meters } = await response.json();
        choiceRows.get("tempo").select.append(...tempos.map((x, i) => createOption(String(i), `${Math.round(x)}`)));
        choiceRows.get("meter").select.append(...meters.map((x, i) => createOption(String(i), x)));
        showChoice("tempo");
        showChoice("meter");
    } catch {
        // without the lists, the tempo and the meter are the songs' own
    }
}

/** A pan's step, from 0 to 63, as MIDI's, from 0 to 127, the middles together. */
/** A part's channels, its own and its twin's, and whether each sits on the other side of the part's pan. */
function channelsOf(part) {
    return [
        ...(channels[part] == null ? [] : [{ channel: channels[part], mirrored: false }]),
        ...(twins[part] == null ? [] : [{ channel: twins[part], mirrored: true }]),
    ];
}

/** A pan step on the other side of the middle. */
function mirrorPan(step) {
    return Math.min(LAST_STEP, 2 * MIDDLE_PAN - step);
}

function toMidiPan(step) {
    return step <= MIDDLE_PAN ? step * 2 : Math.round(64 + ((step - MIDDLE_PAN) * 63) / (LAST_STEP - MIDDLE_PAN));
}

/**
 *     A value that changes the song: checked, it is given, and the slider sets it; unchecked, the slider shows what the
 *     song on the page drew. A change asks for the song again once the slider is let go of.
 */
function createSettingRow(key) {
    const [name, hint] = SETTING_NAMES[key];
    const isBase = key === "unconventionality";

    const given = document.createElement("input");
    given.type = "checkbox";
    given.setAttribute("aria-label", `Give the ${name.toLowerCase()}`);

    const label = document.createElement("span");
    label.className = "setting-name";
    label.textContent = name;
    const explained = document.createElement("small");
    explained.textContent = hint;
    label.append(explained);

    const range = document.createElement("input");
    range.type = "range";
    range.min = "0";
    range.max = String(LAST_STEP);
    range.step = "1";
    range.setAttribute("aria-label", `${name}, from ${isBase ? "plainest" : "plain"} to ${isBase ? "most experimental" : "experimental"}`);

    const plain = document.createElement("span");
    plain.className = "end";
    plain.setAttribute("aria-hidden", "true");
    plain.textContent = isBase ? "Plainest" : "Plain";
    const experimental = document.createElement("span");
    experimental.className = "end";
    experimental.setAttribute("aria-hidden", "true");
    experimental.textContent = isBase ? "Most experimental" : "Experimental";

    const output = document.createElement("output");
    output.className = "percent";

    given.addEventListener("change", () => {
        setSetting(key, { isGiven: given.checked, value: Number(range.value) });
        showSetting(key);
        settingsChanged(true);
    });
    range.addEventListener("input", () => {
        setSetting(key, { isGiven: true, value: Number(range.value) });
        showSetting(key);
    });
    range.addEventListener("change", () => settingsChanged(true));

    const row = document.createElement("div");
    row.className = "setting";
    const slider = document.createElement("span");
    slider.className = "setting-slider";
    slider.append(plain, range, experimental, output);
    row.append(given, label, slider);
    settingRows.set(key, { given, range, output });
    return row;
}

function showSetting(key) {
    const row = settingRows.get(key);
    const setting = settingOf(key);
    row.given.checked = setting.isGiven;
    row.range.disabled = !setting.isGiven;
    row.range.value = String(setting.value);
    row.output.textContent = formatPercent(setting.value / LAST_STEP);
}

/** The drums' setup: the song's own, or one given. */
function createSetupRow() {
    setupSelect = document.createElement("select");
    setupSelect.setAttribute("aria-label", "The drums' setup");
    setupSelect.append(createOption("", "Song's own"), ...DRUM_SETUPS.map((setup) => createOption(setup, SETUP_NAMES[setup])));
    setupSelect.addEventListener("change", () => {
        settings.drumSetup = setupSelect.value === "" ? null : setupSelect.value;
        settingsChanged(true);
    });

    const label = document.createElement("span");
    label.className = "setting-name";
    label.textContent = "Setup";

    const row = document.createElement("div");
    row.className = "setting";
    row.append(document.createElement("span"), label, setupSelect);
    return row;
}

/**
 *     A part's row: whether it plays, given or drawn, which changes the song; the instrument it plays, its volume and its
 *     pan, which are heard at once; whether it is written in the file; and muting and soloing, which only this page
 *     hears.
 */
function createPartRow(part) {
    const name = PART_NAMES[part];

    const plays = document.createElement("select");
    plays.setAttribute("aria-label", `Whether the ${name.toLowerCase()} plays`);
    const random = createOption("random", "Random");
    plays.append(random, createOption("on", "On"), createOption("off", "Off"));
    plays.addEventListener("change", () => {
        settings.parts[part].plays = plays.value;
        showEveryPartOff();
        settingsChanged(true);
    });

    const instrument = document.createElement("select");
    instrument.setAttribute("aria-label", `Instrument of the ${name.toLowerCase()}`);
    const ownInstrument = createOption("", "Song's own");
    instrument.append(ownInstrument);
    if (part === "drum") instrument.append(...DRUM_KITS.map((kit) => createOption(String(kit.instrument), kit.name)));
    else fillInstruments(instrument);
    instrument.addEventListener("change", () =>
        setPartInstrument(part, instrument.value === "" ? { isGiven: false, value: own[part]?.instrument ?? 0 } : { isGiven: true, value: Number(instrument.value) }));

    const roll = document.createElement("button");
    roll.type = "button";
    roll.className = "roll";
    roll.textContent = "Roll";
    roll.setAttribute("aria-label", `Roll the instrument of the ${name.toLowerCase()}`);
    roll.addEventListener("click", () => rollInstrument(part));

    const instrumentField = document.createElement("div");
    instrumentField.className = "instrument-field";
    instrumentField.append(instrument, roll);

    const volume = createFader(`Volume of the ${name.toLowerCase()}`, (step) => setPartVolume(part, step));
    // a double click puts the pan back where the song sits the part
    const pan = createFader(`Pan of the ${name.toLowerCase()}, from left to right`, (step) => setPartPan(part, { isGiven: true, value: step }), true);
    pan.input.addEventListener("dblclick", () => setPartPan(part, { isGiven: false, value: own[part]?.pan ?? MIDDLE_PAN }));
    const levels = document.createElement("div");
    levels.className = "fader-pair";
    levels.append(volume.field, pan.field);

    const inFile = document.createElement("input");
    inFile.type = "checkbox";
    inFile.setAttribute("aria-label", `The ${name.toLowerCase()} in the file`);
    inFile.addEventListener("change", () => setPartInFile(part, inFile.checked));

    const mute = createToggle("Mute", () => {
        toggle(muted, part, mute);
        applyMuting();
        track("mix_changed", { detail: "mute" });
    });
    const solo = createToggle("Solo", () => {
        toggle(soloed, part, solo);
        applyMuting();
        track("mix_changed", { detail: "solo" });
    });

    const meter = document.createElement("div");
    meter.className = "meter";
    const level = document.createElement("span");
    meter.append(level);

    const heading = document.createElement("th");
    heading.scope = "row";
    heading.textContent = name;

    const row = document.createElement("tr");
    row.append(heading, ...[plays, instrumentField, levels, inFile, mute, solo, meter].map((x) => {
        const cell = document.createElement("td");
        cell.append(x);
        return cell;
    }));
    rows.set(part, { row, plays, random, instrument, ownInstrument, volume, pan, inFile, mute, solo, level });
    return row;
}

/** A drum group's row: whether it is in the file and how loud it plays, which are made by the server. */
function createDrumGroupRow(group) {
    const name = DRUM_GROUP_NAMES[group];

    const inFile = document.createElement("input");
    inFile.type = "checkbox";
    inFile.setAttribute("aria-label", `${name} in the file`);
    inFile.addEventListener("change", () => {
        settings.drumGroups[group].isOn = inFile.checked;
        settingsChanged(true);
    });

    const volume = createFader(`Volume of the ${name.toLowerCase()}`, (step) => (settings.drumGroups[group].volume = step), false, () => settingsChanged(true));

    const heading = document.createElement("th");
    heading.scope = "row";
    heading.textContent = name;

    const row = document.createElement("tr");
    row.append(heading, ...[inFile, volume.field].map((x) => {
        const cell = document.createElement("td");
        cell.append(x);
        return cell;
    }));
    groupRows.set(group, { row, inFile, volume });
    return row;
}

/** A fader from 0 to 63, saying where it is as a share, or where a pan sits. */
function createFader(label, onInput, isPan = false, onChange = null) {
    const input = document.createElement("input");
    input.type = "range";
    input.min = "0";
    input.max = String(LAST_STEP);
    input.step = "1";
    input.className = "fader";
    input.setAttribute("aria-label", label);

    const output = document.createElement("output");
    output.className = "percent";
    const show = () => (output.textContent = isPan ? formatPan(Number(input.value)) : formatPercent(Number(input.value) / LAST_STEP));
    input.addEventListener("input", () => {
        show();
        onInput(Number(input.value));
    });
    if (onChange !== null) input.addEventListener("change", onChange);

    const field = document.createElement("div");
    field.className = "fader-field";
    field.append(input, output);
    return { field, input, show };
}

/** Puts the settings, and what the song on the page drew and is made of, on the panel. */
function showSettings() {
    for (const key of settingRows.keys()) showSetting(key);
    for (const key of choiceRows.keys()) showChoice(key);

    setupSelect.value = settings.drumSetup ?? "";
    setupSelect.options[0].textContent = drumSetup === null ? "Song's own" : `Song's own: ${SETUP_NAMES[drumSetup]}`;

    for (const part of PARTS) {
        const row = rows.get(part);
        const mix = settings.parts[part];
        row.plays.value = mix.plays;
        row.random.textContent = part in partsInSong ? `Random: ${partsInSong[part] ? "in" : "out"}` : "Random";
        row.ownInstrument.textContent = part in own ? `Song's own: ${instrumentName(part, own[part].instrument)}` : "Song's own";
        row.instrument.value = mix.instrument.isGiven ? String(mix.instrument.value) : "";
        row.volume.input.value = String(mix.volume);
        row.volume.show();
        row.pan.input.value = String(mix.pan.value);
        row.pan.show();
        row.inFile.checked = mix.isOn;
        // a part the song does not play is still set here, for the songs after it
        row.row.classList.toggle("absent", channels[part] == null);
    }

    for (const group of DRUM_GROUPS) {
        const row = groupRows.get(group);
        row.inFile.checked = settings.drumGroups[group].isOn;
        row.volume.input.value = String(settings.drumGroups[group].volume);
        row.volume.show();
        row.row.classList.toggle("absent", !drumGroupsPresent.includes(group));
    }

    elements.songVolume.value = String(settings.volume);
    showSongVolume();
    elements.allOff.hidden = !isEveryPartOff(settings);
    updateGenerateButtons();
}

function instrumentName(part, instrument) {
    return part === "drum"
        ? DRUM_KITS.find((kit) => kit.instrument === instrument)?.name ?? `Kit ${instrument}`
        : INSTRUMENT_NAMES[instrument] ?? `Instrument ${instrument}`;
}

/**
 *     After a change of the settings: kept for the next visit and in the address, and then either the song asked for
 *     again, for a change of what it is made of, or its file fetched again with the mix, for one only heard.
 */
function settingsChanged(isRemade) {
    keepCurrentSettings();
    rememberSong();
    if (isRemade) scheduleRegeneration();
    else scheduleDownloadRefresh();
}

function setPartInstrument(part, instrument) {
    settings.parts[part].instrument = instrument;
    rows.get(part).instrument.value = instrument.isGiven ? String(instrument.value) : "";
    track("mix_changed", { detail: "instrument" });
    settingsChanged(false);

    // the page and the download are right whatever the audio does, so the player is told separately
    for (const { channel } of channelsOf(part))
        withPlayer((player) => player.setChannelInstrument(channel, instrument.value, instrument.isGiven), "Could not set the instrument");
}

function setPartVolume(part, step) {
    settings.parts[part].volume = step;
    applyVolume(part);
    settingsChanged(false);
}

function setPartPan(part, pan) {
    settings.parts[part].pan = pan;
    rows.get(part).pan.input.value = String(pan.value);
    rows.get(part).pan.show();
    settingsChanged(false);

    for (const { channel, mirrored } of channelsOf(part))
        withPlayer((player) => player.setChannelPan(channel, toMidiPan(mirrored ? mirrorPan(pan.value) : pan.value)), "Could not set the pan");
}

/** Switches a part off, which leaves it out of the file and silences it here, so what is heard is what is downloaded. */
function setPartInFile(part, isOn) {
    settings.parts[part].isOn = isOn;
    applyMuting();
    track("mix_changed", { detail: "channel" });
    settingsChanged(false);
}

/** A part's volume under the song's, on the player. */
function applyVolume(part) {
    const volume = (settings.volume / LAST_STEP) * (settings.parts[part].volume / LAST_STEP);
    for (const { channel } of channelsOf(part))
        withPlayer((player) => player.setChannelVolume(channel, volume), "Could not set the volume");
}

/**
 *     What a roll of a part may land on: the drum kits for the drums, and the range the settings hold everywhere else,
 *     whichever way round it was set.
 */
function rollable(part) {
    if (part === "drum") return DRUM_KITS.map((kit) => kit.instrument);

    const bounds = [Number(elements.rollFirst.value), Number(elements.rollLast.value)];
    const first = Math.min(...bounds);
    return Array.from({ length: Math.max(...bounds) - first + 1 }, (_, index) => first + index);
}

/** Rolls one instrument, never landing on the one already playing while there is anything else to land on. */
function rollInstrument(part) {
    const choices = rollable(part).filter((instrument) => instrument !== settings.parts[part].instrument.value);
    if (choices.length === 0) return;

    setPartInstrument(part, { isGiven: true, value: choices[Math.floor(Math.random() * choices.length)] });
}

elements.rollInstruments.addEventListener("click", () => {
    for (const part of PARTS) rollInstrument(part);
});

function formatPercent(volume) {
    return `${Math.round(volume * 100)}%`;
}

/** Where a pan sits: in the middle, or how far left or right. */
function formatPan(step) {
    if (step === MIDDLE_PAN) return "C";
    return step < MIDDLE_PAN ? `L${Math.round(((MIDDLE_PAN - step) / MIDDLE_PAN) * 100)}` : `R${Math.round(((step - MIDDLE_PAN) / (LAST_STEP - MIDDLE_PAN)) * 100)}`;
}

/** The General MIDI instruments, grouped by family and numbered by counting through them in order. */
function fillInstruments(select) {
    let instrument = 0;

    select.append(...INSTRUMENT_FAMILIES.map((family) => {
        const group = document.createElement("optgroup");
        group.label = family.name;
        group.append(...family.instruments.map((name) => createOption(String(instrument++), name)));
        return group;
    }));
}

function createOption(value, name) {
    const option = document.createElement("option");
    option.value = value;
    option.textContent = name;
    return option;
}

function createToggle(label, onToggle) {
    const button = document.createElement("button");
    button.type = "button";
    button.textContent = label;
    button.setAttribute("aria-pressed", "false");
    button.addEventListener("click", onToggle);
    return button;
}

function toggle(set, part, button) {
    const isOn = !set.has(part);
    if (isOn) set.add(part);
    else set.delete(part);
    button.setAttribute("aria-pressed", String(isOn));
}

/** Switched off, or muted, or another part soloed; an explicit mute still wins over a solo. */
function isSilenced(part) {
    return !settings.parts[part].isOn || muted.has(part) || (soloed.size > 0 && !soloed.has(part));
}

/**
 *     Puts the mix on the page onto the player. A new song's file carries its instruments, volumes and pans, and loading
 *     it lets go of every lock the mixer set, so only what this page alone hears is left to apply.
 *
 *     A new sound bank undoes more than that: it resets every channel to the first program, so what the song asked for
 *     is as lost as what anybody picked, and the drum channel forgets it is one. After a bank, every channel is told
 *     what it plays, how loud and where, but only a choice made here is locked, so a channel the song owns is still the
 *     song's to set when it starts over.
 */
function applyMix(player, isBankReset = false) {
    for (const part of PARTS)
        for (const { channel, mirrored } of channelsOf(part)) {
            if (isBankReset) {
                const mix = settings.parts[part];
                if (part === "drum") player.setChannelDrums(channel, true);
                player.setChannelInstrument(channel, mix.instrument.value, mix.instrument.isGiven);
                player.setChannelVolume(channel, (settings.volume / LAST_STEP) * (mix.volume / LAST_STEP));
                player.setChannelPan(channel, toMidiPan(mirrored ? mirrorPan(mix.pan.value) : mix.pan.value));
            }

            player.setChannelMuted(channel, isSilenced(part));
        }
}

function applyMuting() {
    withPlayer((player) => {
        for (const part of PARTS) for (const { channel } of channelsOf(part)) player.setChannelMuted(channel, isSilenced(part));
    }, "Could not silence the part");
}

elements.songVolume.addEventListener("input", () => {
    settings.volume = Number(elements.songVolume.value);
    showSongVolume();
    for (const part of PARTS) applyVolume(part);
    settingsChanged(false);
});

function showSongVolume() {
    elements.songVolumeValue.textContent = formatPercent(settings.volume / LAST_STEP);
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
    if (songSeed === null) return;

    const refresh = new AbortController();
    downloadRefresh = refresh;
    try {
        const response = await requestSong(requestFor(songSeed, settings), refresh.signal);
        if (!response.ok) throw new Error(await describeFailure(response));

        offerDownload(await response.arrayBuffer());
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

    for (const [part, row] of rows) {
        const voices = player.paused || channels[part] == null ? 0 : player.getVoiceCount(channels[part]);
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

/**
 *     The song rated, liked or not, which is what tells one version of the songs from another better than how long
 *     they were listened to. Pressing the pressed one takes the rating back. Sent as the change from the rating this
 *     browser had, whether the page counts or not, since pressing it is saying so on purpose.
 */
function rate(rating) {
    const song = currentSong();
    if (song === null || songVersion === null) return;

    const current = recallRating(songVersion, song);
    const next = current === rating ? null : rating;
    keepRating(songVersion, song, next);
    showRating(next);
    // the change, rather than the rating, since the server has no one to tell today's rating from yesterday's by, and
    // with the settings it was heard with, which say what of them names the song
    tell("rated", aboutSong({ detail: `${current ?? "none"}>${next ?? "none"}` }));
    announce(next === null ? "Rating taken back." : next === "up" ? "Liked." : "Disliked.");
}

function showRating(rating) {
    elements.like.setAttribute("aria-pressed", String(rating === "up"));
    elements.dislike.setAttribute("aria-pressed", String(rating === "down"));
}

elements.like.addEventListener("click", () => rate("up"));
elements.dislike.addEventListener("click", () => rate("down"));

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
    const linked = linkedSong();
    track("page_open", { detail: linked.wasAsked ? "link" : "fresh" });
    showVersion();

    // a linked song is asked for with the settings it was heard with, which become the page's; a song rolled here
    // with the settings this browser kept
    if (linked.settings !== null) {
        settings = givenOnly(linked.settings);
        keepCurrentSettings();
        showSettings();
    }

    // said afterwards, since the generating itself has the status line until it is done with it, and
    // only once a song has arrived: a failure has the status line to itself
    const generating = generate(linked.seed, { isRolled: linked.seed === null || linked.wasRolled });
    const complaint = linked.isOld
        ? "That link is from an older version of RMG, whose songs sound different now, so here is a random song."
        : linked.wasAsked && linked.seed === null
            ? "That link has no valid song, so here is a random one."
            : linked.isSettingsBad
                ? "That link's settings could not be read, so here is its song with the settings you had."
                : null;
    if (complaint !== null)
        generating.then((isGenerated) => {
            if (isGenerated) setStatus(complaint, true);
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

/**
 *     RMG's version in the footer: the songs' number, which goes up when the songs change, and the commit. Best
 *     effort, as nothing else on the page depends on it.
 */
async function showVersion() {
    try {
        const response = await fetch(new URL("api/version", location.href));
        if (!response.ok) return;

        const { version, commit } = await response.json();
        elements.version.textContent = commit ? `RMG ${version} (${commit.slice(0, 7)})` : `RMG ${version}`;
        elements.version.hidden = false;
    } catch {
        // a footer without its version is a footer all the same
    }
}
