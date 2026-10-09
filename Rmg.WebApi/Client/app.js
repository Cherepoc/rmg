import {
    DRUM_KITS,
    INSTRUMENT_FAMILIES,
} from "./instruments.js";
import { hideWork, showWork } from "./busy.js";
import { encodeMp3, renderSong } from "./export.js";
import { createMediaControls } from "./media-session.js";
import { getPlayer } from "./player.js";
import { createPulse } from "./pulse.js";
import {
    askToPersist,
    forget,
    isAutoplaying,
    isKeeping,
    keep,
    keepHistory,
    keepGenre,
    keepAnyGenre,
    recallAnyGenre,
    keepGenreCopies,
    keepGenres,
    keepRating,
    keepSettings,
    recall,
    recallHistory,
    recallRating,
    recallGenre,
    recallGenreCopies,
    recallGenres,
    recallSettings,
    setAutoplaying,
    setKeeping,
} from "./storage.js";
import {
    DRUM_GROUPS,
    DRUM_PIECES,
    FACETS,
    formatSettings,
    fromStep,
    identityOf,
    LAST_STEP,
    MIDDLE_STEP,
    parseSettings,
    PARTS,
    PLAYS,
    readLinkedSeed,
    readSeed,
    toStep,
} from "./settings.js";
import { applyReport, defaultSettings, givenOnly, isEveryPartOff, noGenre, requestFor } from "./song-settings.js";
import { asGenre, byName, hasAGenre, isSameGenre, nestedChoices, presetById, presetOf, PRESETS, sameName } from "./genres.js";
import { createGenreEditor } from "./genre-editor.js";
import { createGenreManager } from "./genre-manager.js";
import { since, tell, track } from "./tally.js";
import { createListeningIndex, createListeningStrip, listeningQuestion, loadQuestions, withoutDevice } from "./listening.js";

const PLAY_ICON = "M8 5v14l11-7z";
const PAUSE_ICON = "M7 5h3.5v14H7zM13.5 5H17v14h-3.5z";
// a row's mute, a speaker struck through, and its solo, headphones
const MUTE_ICON = "M16.5 12A4.5 4.5 0 0 0 14 7.97v2.21l2.45 2.45c.03-.2.05-.41.05-.63zM19 12c0 .94-.2 1.82-.54 2.64l1.51 1.51A8.8 8.8 0 0 0 21 12c0-4.28-2.99-7.86-7-8.77v2.06c2.89.86 5 3.54 5 6.71zM4.27 3 3 4.27 7.73 9H3v6h4l5 5v-6.73l4.25 4.25c-.67.52-1.42.93-2.25 1.18v2.06a8.99 8.99 0 0 0 3.69-1.81L19.73 21 21 19.73l-9-9L4.27 3zM12 4 9.91 6.09 12 8.18V4z";
const SOLO_ICON = "M12 3a9 9 0 0 0-9 9v7a2 2 0 0 0 2 2h3v-8H5v-1a7 7 0 0 1 14 0v1h-3v8h3a2 2 0 0 0 2-2v-7a9 9 0 0 0-9-9z";

const elements = {
    soundFont: document.getElementById("soundfont"),
    soundFontName: document.getElementById("soundfont-name"),
    soundFontSummary: document.querySelector("#soundfont-panel > summary"),
    nowPlaying: document.querySelector(".now-playing"),
    libraryRow: document.getElementById("library-row"),
    library: document.getElementById("library"),
    libraryLicense: document.getElementById("library-license"),
    keep: document.getElementById("keep"),
    seed: document.getElementById("seed"),
    generate: document.getElementById("generate"),
    settingsState: document.getElementById("settings-state"),
    allOff: document.getElementById("all-off"),
    songSettings: document.getElementById("song-settings"),
    genreRow: document.getElementById("genre-row"),
    seedInput: document.getElementById("seed-input"),
    generateSeeded: document.getElementById("generate-seeded"),
    history: document.getElementById("history"),
    clearHistory: document.getElementById("clear-history"),
    download: document.getElementById("download"),
    exportMp3: document.getElementById("export-mp3"),
    share: document.getElementById("share"),
    shareLabel: document.getElementById("share-label"),
    bitrate: document.getElementById("bitrate"),
    previous: document.getElementById("previous"),
    next: document.getElementById("next"),
    play: document.getElementById("play"),
    stop: document.getElementById("stop"),
    seek: document.getElementById("seek"),
    elapsed: document.getElementById("elapsed"),
    total: document.getElementById("total"),
    sections: document.getElementById("seek-sections"),
    volume: document.getElementById("volume"),
    volumeValue: document.getElementById("volume-value"),
    autoplay: document.getElementById("autoplay"),
    songVolume: document.getElementById("song-volume"),
    songVolumeValue: document.getElementById("song-volume-value"),
    mixer: document.getElementById("mixer"),
    status: document.getElementById("status"),
    version: document.getElementById("version"),
    like: document.getElementById("like"),
    dislike: document.getElementById("dislike"),
};

// the parts and drum groups muted or soloed here, which only this page hears, and every part's row of the mixer, by the part
const muted = new Set();
const soloed = new Set();
const rows = new Map();

// the settings every song is asked for with, and the one on the page heard with (song-settings.js): every value given,
// or as the song on the page drew it; kept in this browser for the next visit
let settings = restoreSettings();
/** Whether every new song is asked for in a preset drawn for it, the list's Any genre, which this browser keeps. */
let isAnyGenre = recallAnyGenre();

// the song on the page, as its seed, in letters and digits: null before the first
let songSeed = null;
// and the key its genre is kept by on the server, which a link to it carries: null for a song of no genre
let songGenreKey = null;

// what the song on the page is made of, as the server said: the channel every part plays on in the file the player
// has, null where it is not written, whether each part is in the song, the drum groups it plays and its drum pieces
let channels = {};
// a part's other channels, its second take's and the riff's twin's, which follow its mix, each on its side
let others = {};
let partsInSong = {};
let drumGroupsPresent = [];
let drumPiecesPlayed = [];

// every drum's MIDI note's group, as the server lists the pieces, and the hardest each group was struck since the last frame
const drumGroupOfNote = new Map();
const drumHits = new Map();

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
// the songs' version the server makes now, as it says it
let serverVersion = null;
// where the next song made starts playing, in seconds, a listening song's; none for its start
let startAt = null;
// the stages the song asked for next leaves out, as the listening mode hears one without a device; none otherwise
let skipped = [];
// and the stages the song on the page left out, which its remakes, its download and its MP3 leave out too
let songSkipped = [];
// the song's sections as its report gives them, where each starts in seconds, its role and its soloists, and its length
let songSections = [];
let songSeconds = 0;
let isSeeking = false;
let seekedFrom = null;
let isExporting = false;

// the new song on its way: what it was asked for with, which an ask for the same leaves to come, and the settings of
// the song on the page, which a Stop puts back
let arrival = null;
// how many songs are on their way, new or remade, which the song on the page shows it is waiting for
let songsComing = 0;

// whether the song on the page was rolled here rather than asked for by seed, which is the only kind
// that leads to another when it ends, and which request to start a song once it arrives is the last one
let isSongRandom = false;
let playRequest = 0;
let isListening = false;
// whether the page asks a listening question, whose songs, A and B and the next, keep the mutes and solos pressed
let asksQuestion = false;
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

/**
 *     A soundfont on its way, on the panel that names it and on Play, which waits for it: how much of it has arrived
 *     where its size is known, and the bar sweeping where it is not, or while it is read.
 */
function showSoundFontWork(name, fraction) {
    setSoundFontState(name);
    showWork(elements.soundFontSummary, `Loading ${name}`, fraction);
    showWork(elements.play, `Loading ${name}`, fraction);
}

function hideSoundFontWork() {
    hideWork(elements.soundFontSummary);
    hideWork(elements.play);
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
    // a drum struck lifts its group's pulse, as loud as it is struck and its group plays, which one channel's level cannot tell apart
    player.onNoteOn((channel, note, velocity) => {
        const group = drumGroupOfNote.get(note);
        if (channel !== channels.drum || group === undefined || isGroupSilenced(group)) return;
        const hit = (velocity / 127) * fromStep(settings.drumGroups[group].volume);
        drumHits.set(group, Math.max(drumHits.get(group) ?? 0, hit));
    });
    player.masterGain = Number(elements.volume.value);
}

/**
 *     What a song running out leads to: another one, rolled and played where the last left off, the way
 *     a video site goes on to the next. Only a song rolled here leads anywhere. One asked for by seed, or
 *     arrived at by a link, is the song that was asked for, and it ends where it ends.
 */
function advance() {
    if (!isAutoplayOn() || !isSongRandom || arrival !== null) return false;
    if (isEveryPartOff(settings)) {
        setStatus("Every part is off, so no next song plays. Turn a part on in the song settings.", true);
        return false;
    }

    playNew(null, "auto");
    return true;
}

/**
 *     Gets a song and starts it the moment it has reached the player: asking for a song is asking to hear
 *     it, whether by the button, by a seed, from the list, or by the song before it running out. The song is asked for
 *     with the settings given, which become the page's, and the latest ask is the one that plays.
 */
async function playNew(seed, origin, asked = settings, { keepsPlace = false } = {}) {
    // a new song of Any genre in a preset drawn for it, the page's settings taking it only once the song is on its way,
    // so that a Stop puts back the genre of the song on the page
    if (seed === null && asked === settings && isAnyGenre) asked = { ...settings, genre: drawAnyGenre() };
    // the same song asked for again is the one already on its way: asking anew would only cancel it for nothing
    if (arrival !== null && arrival.wanted === wantedOf(seed, asked)) return;

    const request = ++playRequest;
    const generated = await generate(seed, { asked, keepsPlace });

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

/** A request leaving out the stages given, as the listening mode's song without its device asks; as it is for none. */
function withSkip(request, skip) {
    return skip.length > 0 ? { ...request, skip } : request;
}

/** What a new song is asked for with, as the request says it, which tells one ask for the same song from another. */
function wantedOf(seed, songSettings) {
    return JSON.stringify(requestFor(seed, songSettings));
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
    showWork(elements.soundFontSummary, `Saving ${saving.name}`);
    const problem = await store(saving.name, saving.blob);
    hideWork(elements.soundFontSummary);
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
    showSoundFontWork(name, null);
    // how long it takes to arrive, from its own start, whenever it is chosen
    const started = performance.now();

    let blob;
    let soundFont;
    try {
        const data = await read();
        blob = data instanceof Blob ? data : new Blob([data]);
        soundFont = await blob.arrayBuffer();
    } catch (error) {
        hideSoundFontWork();
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

    hideSoundFontWork();

    // kept only once the player has taken it, so a file that will not load never replaces one that does
    source = { name, blob, isStored };
    pendingSoundFont = soundFont;
    hasSoundFont = true;
    updateTransport();

    setSoundFontState(name);

    // the size and where it came from, never its name: a file of your own is yours, and its name can say
    // more about you than this has any business knowing
    track("soundfont_ready", { ms: Math.round(performance.now() - started), bytes: soundFont.byteLength, detail: origin });

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
            // a listening song started where its question says to listen
            const from = startAt;
            startAt = null;
            const loaded = player.nextSong();
            player.loadSong(song);
            applyMuting(player);
            // and the mix as the page has it now, a change made while the song was on its way, which its file was asked
            // for without, heard once the song is the player's
            loaded.then(() => applyMix(player));
            if (swap !== null) {
                await loaded;
                player.currentTime = swap.share * player.duration;
                if (swap.wasPlaying && player.paused) await player.play();
                else if (!swap.wasPlaying && !player.paused) player.pause();
                render(player);
                showMedia(player);
            }
            if (from !== null) {
                await loaded;
                player.currentTime = Math.min(from, player.duration);
                render(player);
                showMedia(player);
            }
        } else if (soundFont !== null) {
            // a soundfont swapped under a song already playing: the bank took every channel back to
            // where it started, and the song will not say what it plays again until it comes round
            applyMix(player);
        }
    } catch (error) {
        setStatus(`The player could not take the song: ${error.message}`, true);
        return;
    }

    // how long the page waited for its first sound, once a page
    if (!isAudioReadyTold) track("audio_ready", { ms: since() });
    isAudioReadyTold = true;
}

// whether the page has told how long its first sound took
let isAudioReadyTold = false;

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
                showSoundFontWork(name, total > 0 ? got / total : null);
            }

            return new Blob(pieces);
        } catch (error) {
            failure = error;
            if (attempt === DOWNLOAD_ATTEMPTS || error.isFinal) break;

            setSoundFontState(`${name}, trying again`);
            await pause(400 * attempt);
        }
    }

    // how far it got says which kind of failure this was: nothing at all, or a connection that gave out
    failure.got = got;
    throw failure;
}

/** Waits, and stops waiting the moment the signal, if any, lets go, which it throws as the fetch it stands in for would. */
function pause(ms, signal = undefined) {
    return new Promise((resolve, reject) => {
        signal?.throwIfAborted();
        const timer = setTimeout(resolve, ms);
        signal?.addEventListener("abort", () => {
            clearTimeout(timer);
            reject(signal.reason);
        }, { once: true });
    });
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
    // beside the soundfont's name, which says whose it is
    elements.libraryLicense.textContent = "Licence";
    elements.libraryLicense.setAttribute("aria-label", `Licence for ${option.dataset.name}`);
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

// the seed typed, with the settings as they are
elements.generateSeeded.addEventListener("click", () => {
    const text = elements.seedInput.value.trim();
    const seed = readSeed(text);
    if (seed === null) {
        setStatus(`'${text}' is not a seed. A seed is up to eleven letters and digits.`, true);
        return;
    }

    playNew(seed, "seed");
});

elements.seedInput.addEventListener("input", updateGenerateButtons);

elements.seedInput.addEventListener("keydown", (event) => {
    if (event.key === "Enter") elements.generateSeeded.click();
});

/**
 *     The song asked for in the address, as <c>?song=12Ab&settings=1…</c>: its seed and the settings it was heard with,
 *     and whether one was asked for at all. A link carrying something that is not a seed, or settings that cannot be
 *     read, is worth saying so about, rather than quietly playing a different song and letting whoever sent it wonder.
 *
 *     <c>wasRolled</c> tells a refresh from a link. The page writes the song it is playing into the address, so every
 *     refresh after the first arrives looking exactly like a song somebody asked for; what marks it as the page's own
 *     roll is the state left on the history entry, which a refresh keeps and a link followed from anywhere else does
 *     not have.
 */
function linkedSong() {
    const parameters = new URL(location.href).searchParams;
    const wasRolled = history.state?.rolled === true;
    const asked = parameters.get("song");
    if (asked === null) return { seed: null, wasAsked: false, wasRolled, settings: null, isSettingsBad: false, genreKey: null };

    const linked = parameters.get("settings");
    const parsed = linked === null ? null : parseSettings(linked.trim());
    return {
        seed: readLinkedSeed(asked.trim(), linked?.trim() ?? null),
        wasAsked: true,
        wasRolled,
        settings: parsed,
        isSettingsBad: linked !== null && parsed === null,
        genreKey: parameters.get("genre")?.trim() || null,
    };
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
    if (songGenreKey === null) address.searchParams.delete("genre");
    else address.searchParams.set("genre", songGenreKey);

    return address.toString();
}

/** What every event about the song on the page carries: which song it is, and what it was heard with. */
function aboutSong(measurements = {}) {
    return {
        seed: songSeed,
        version: songVersion,
        settings: songSeed === null ? null : formatSettings(settings),
        genre: songSeed === null ? null : songGenreKey,
        ...measurements,
    };
}

/**
 *     A song as the page names it: its seed, and what of its settings names it too, and its genre's key, which the same
 *     seed is another song by.
 */
function songName(seed, songSettings, genreKey) {
    const identity = identityOf(songSettings);
    const named = identity === "" ? seed : `${seed}.${identity}`;
    return genreKey === null ? named : `${named}~${genreKey}`;
}

/** The song on the page, as the page names it; null before the first. */
function currentSong() {
    return songSeed === null ? null : songName(songSeed, settings, songGenreKey);
}

/** The name the song on the page is downloaded under: its name, as a link names it, its genre's key among it. */
function fileName() {
    return `song-${currentSong()}`;
}

const BUSY = "The server is busy. Trying again…";
const BUSY_RETRIES = 3;

/**
 *     Asks for a song, and again, a few times, while the server is too busy to make it, after as long as it says, and
 *     a little longer at random, so that the pages it turned away at once do not all come back at once.
 */
async function requestSong(song, signal = undefined) {
    try {
        for (let attempt = 0; ; attempt++) {
            const response = await fetch(new URL("api/songs/generate", location.href), {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(song),
                signal,
            });
            if ((response.status !== 503 && response.status !== 429) || attempt === BUSY_RETRIES) return response;

            setStatus(BUSY);
            const seconds = Math.min(Math.max(Number(response.headers.get("Retry-After")) || 1, 1), 10);
            await pause(seconds * 1000 * (1 + Math.random() / 2), signal);
        }
    } finally {
        // whatever came of it, the waiting is over
        if (elements.status.textContent === BUSY) setStatus("");
    }
}

/**
 *     Answers whether a song arrived, which is what tells a roll of the next one that it has something to play. A new
 *     song is asked for by its seed, null for a random one, with the settings as they are; <paramref name="isRolled" />
 *     says it was the page's own to roll rather than one asked for, which is what makes it lead to another when it
 *     ends, and a seed refreshed back into the address is still one the page rolled, so the caller is allowed to say
 *     so. A song remade (<paramref name="isRemade" />) is the one on the page asked for again with changed settings,
 *     which replaces it where it plays, and takes its place on the list, as one gone back to keeps its own
 *     (<paramref name="keepsPlace" />).
 */
async function generate(seed, { isRolled = seed === null, isRemade = false, keepsPlace = false, signal = undefined, asked = settings } = {}) {
    if (isEveryPartOff(asked)) {
        showEveryPartOff();
        return false;
    }

    // a new song lets go of the one on its way, the latest ask being the one that plays, and keeps the settings of the
    // song on the page for a Stop to put back
    let ask = null;
    if (!isRemade) {
        ask = { controller: new AbortController(), wanted: wantedOf(seed, asked), restore: arrival?.restore ?? settings };
        arrival?.controller.abort();
        arrival = ask;
        signal = ask.controller.signal;
        if (asked !== settings) {
            settings = asked;
            keepCurrentSettings();
            showSettings();
        }

        updateGenerateButtons();
        cancelRegeneration();
        cancelDownloadRefresh();
        setStatus("");
    }

    songsComing++;
    showWork(elements.nowPlaying, "Making the song");
    try {
        // a new song's stages as asked, a remake's as its song's
        const skip = isRemade ? songSkipped : skipped;
        const response = await requestSong(withSkip(requestFor(seed, settings), skip), signal);
        if (!response.ok) {
            if (signal?.aborted) return false;
            setStatus(`Could not generate a song: ${await describeFailure(response)}`, true);
            track("song_failed", { detail: `http ${response.status}` });
            return false;
        }

        const song = await response.arrayBuffer();
        // let go of once it was all here, which the reading no longer notices
        if (signal?.aborted) return false;

        const report = JSON.parse(response.headers.get("X-Song-Settings"));

        // a song replaced while playing was listened to up to here, and the next one is from here
        if (!isRemade && startedListening !== null) {
            reportListening();
            startedListening = performance.now();
        }

        // only once the whole song is here: a body that fails halfway leaves the old song as it was
        const made = applyReport(settings, report);
        ({ settings, channels, others, own } = made);
        partsInSong = made.plays;
        drumGroupsPresent = made.drumGroups;
        drumPiecesPlayed = made.drumPieces;
        songSeed = response.headers.get("X-Song-Seed") ?? seed;
        songSkipped = skip;
        // a preset by its id, which a link reads as it is, another genre by the key the server keeps it by
        songGenreKey = presetOf(settings.genre)?.id ?? response.headers.get("X-Genre-Key");
        songVersion = response.headers.get("X-Song-Version");
        elements.seed.value = songSeed;
        showSections(report);

        // a new song is heard whole, a song made again as it was being heard, and a listening question's as the last was
        if (!isRemade && !asksQuestion) {
            muted.clear();
            soloed.clear();
        }
        showSettings();
        showRating(recallRating(songVersion, currentSong()));
        rememberSong(isRemade ? undefined : isRolled);
        addToHistory(isRemade, keepsPlace);
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
        if (--songsComing === 0) hideWork(elements.nowPlaying);

        // one let go of for a newer song leaves the page to that one, and one stopped leaves it as it was, with the
        // settings of the song on it
        if (ask !== null && arrival === ask) {
            if (signal.aborted) {
                if (settings !== ask.restore) {
                    settings = ask.restore;
                    keepCurrentSettings();
                    showSettings();
                }
                setStatus("");
            }
            arrival = null;
            updateGenerateButtons();
        }
    }
}

// how long the settings stay as they are, in milliseconds, before the song is asked for again with them: short, as a
// remake let go of frees the server at once
const REMAKE_WAIT = 200;

/**
 *     The song on the page asked for again, once the settings have stopped changing for a moment, with them, to replace
 *     it where it plays; one on its way is let go of for the newer change.
 */
function scheduleRegeneration() {
    cancelRegeneration();
    if (songSeed === null || isEveryPartOff(settings)) return;

    regenerationTimer = setTimeout(() => {
        regenerationTimer = null;

        // a new song on its way is the one to remake once it is here, not the one it replaces
        if (arrival !== null) {
            scheduleRegeneration();
            return;
        }

        cancelDownloadRefresh();
        regeneration = new AbortController();
        const signal = regeneration.signal;
        generate(songSeed, { isRemade: true, signal }).finally(() => {
            if (regeneration?.signal === signal) regeneration = null;
        });
    }, REMAKE_WAIT);
}

function cancelRegeneration() {
    clearTimeout(regenerationTimer);
    regenerationTimer = null;
    regeneration?.abort();
    regeneration = null;
}

/**
 *     The buttons that ask for a song are disabled while every part is off, which leaves nothing to play, Generate while a
 *     song is on its way, as a second press would let go of a song for another just as random, unheard, and the seed's
 *     while none is typed. A seed, typed or from the list, is a song of its own asked for, which takes the place of the
 *     one on its way.
 */
function updateGenerateButtons() {
    elements.generate.disabled = arrival !== null || isEveryPartOff(settings);
    elements.generateSeeded.disabled = isEveryPartOff(settings) || elements.seedInput.value.trim() === "";
    updateWalkButtons();
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
    if (response.status === 503) return "the server is busy making other songs. Try again in a moment.";
    if (response.status === 429) return "songs are being asked for faster than they can be made. Try again in a moment.";
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

/** The settings and the genre this browser left last time, every value given as it was; none given on a first visit. */
function restoreSettings() {
    const kept = parseSettings(recallSettings());
    return { ...(kept === null ? defaultSettings() : givenOnly(kept)), genre: recallGenre() ?? noGenre() };
}

/** Keeps the settings for the next visit, and says how many of them change the song. */
function keepCurrentSettings() {
    keepSettings(formatSettings(givenOnly(settings)));
    keepGenre(hasAGenre(settings.genre) ? settings.genre : null);
    const given = [settings.unconventionality, settings.tempo, settings.key, settings.mode, settings.meter, settings.swing, settings.length, ...FACETS.map((facet) => settings.facets[facet])].filter((x) => x.isGiven).length
        + PARTS.filter((part) => settings.parts[part].plays !== "random").length
        + (settings.drumPieces.isGiven ? 1 : 0);
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
    // a song whose settings are of no format the page reads any more is let go of, rather than played with none
    const entries = recallHistory()
        .filter((entry) => readSeed(entry.split(":")[0]) !== null && parseSettings(entry.split(":")[1]) !== null)
        .slice(0, SONGS_KEPT);
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
    updateWalkButtons();
});

/**
 *     Puts the song on the page on top of the list, however it came about: rolled, typed, followed from a link, rolled
 *     by the song before it running out, or pressed on the list itself. The list is the order songs were heard in, so a
 *     song heard again moves to the top, and wherever it was before is taken off; but a song gone back to with Previous
 *     keeps its place, so that Previous goes on back down the list, and a song remade with changed settings takes the
 *     place of the one it was made from, so that trying settings does not fill the list.
 */
function addToHistory(isRemade, keepsPlace) {
    const name = currentSong();
    const entry = `${songSeed}:${formatSettings(givenOnly(settings))}${songGenreKey === null ? "" : `:${songGenreKey}`}`;
    const current = elements.history.querySelector("[aria-current]");
    const earlier = [...elements.history.children].filter((item) => item.dataset.song === name || (isRemade && item === current));
    const place = isRemade || keepsPlace ? (earlier.includes(current) ? current : earlier[0]) : undefined;

    // the keyboard follows a pressed song to its place, rather than being dropped with the button it was on
    const wasFocused = earlier.includes(document.activeElement);
    const item = createHistoryItem(entry);
    if (place === undefined) {
        for (const other of earlier) other.remove();
        elements.history.prepend(item);
        // the song on the page is the top of the list, so that is where the list is scrolled to
        elements.history.scrollTop = 0;
    } else {
        place.replaceWith(item);
        for (const other of earlier) other.remove();
        showInList(item);
    }

    while (elements.history.children.length > SONGS_KEPT) elements.history.lastElementChild.remove();
    if (wasFocused) item.focus({ preventScroll: true });

    keepHistory(historyEntries());
    keepGenreCopy(songGenreKey, settings.genre);
    markCurrentSong(name);
    elements.clearHistory.disabled = false;
}

/** Marks the song on the page, which is where the list is being read from. */
function markCurrentSong(name) {
    for (const item of elements.history.children) {
        if (item.dataset.song === name) item.setAttribute("aria-current", "true");
        else item.removeAttribute("aria-current");
    }

    updateWalkButtons();
}

/**
 *     Scrolls the list, and the list alone, to show a song in it, as little as it takes: the page stays where it is, as
 *     whoever changed a setting further down is still there.
 */
function showInList(item) {
    const list = elements.history.getBoundingClientRect();
    const shown = item.getBoundingClientRect();
    if (shown.top < list.top) elements.history.scrollTop -= list.top - shown.top;
    else if (shown.bottom > list.bottom) elements.history.scrollTop += shown.bottom - list.bottom;
}

/** The song heard before the one on the page, the next down the list, null where there is none. */
function previousSong() {
    return elements.history.querySelector("[aria-current]")?.nextElementSibling ?? null;
}

/** The song heard after the one on the page, the next up the list, null where it is the latest. */
function nextSong() {
    return elements.history.querySelector("[aria-current]")?.previousElementSibling ?? null;
}

/** Previous wants a song before; Next, past the latest, a new one, which wants what Generate does. */
function updateWalkButtons() {
    elements.previous.disabled = previousSong() === null;
    elements.next.disabled = nextSong() === null && elements.generate.disabled;
}

/** A song of the list, asked for as it was heard, in its genre, keeping its place on it. */
async function playFromList(item, origin) {
    const { seed, entrySettings, genreKey } = readEntry(item.dataset.entry);
    playNew(seed, origin, await withGenre(entrySettings, genreKey), { keepsPlace: true });
}

/**
 *     Keeps a copy of the genre a song of the list was made in, by its key, and lets go of those of songs no longer on
 *     it, so that a song played again from the list has its genre without asking the server.
 */
function keepGenreCopy(key, genre) {
    const keys = new Set(historyEntries().map((entry) => readEntry(entry).genreKey).filter((x) => x !== null));
    const copies = Object.fromEntries(Object.entries(recallGenreCopies()).filter(([kept]) => keys.has(kept)));
    if (key !== null && hasAGenre(genre)) copies[key] = asGenre(genre);
    keepGenreCopies(copies);
}

/**
 *     The genre kept by a key, as this browser kept a copy of it, as the preset of that id or as the server keeps it;
 *     null where none has it.
 */
async function genreOf(key) {
    const copy = recallGenreCopies()[key];
    if (copy !== undefined) return copy;
    const preset = presetById(key);
    if (preset !== null) return asGenre(preset);
    try {
        const response = await fetch(new URL(`api/genres/${encodeURIComponent(key)}`, location.href));
        if (!response.ok) return null;
        const kept = await response.json();
        return { name: kept.name, choices: kept.choices };
    } catch {
        return null;
    }
}

/** Settings with the genre of a key, or with none where there is no key or none is found by it. */
async function withGenre(songSettings, genreKey) {
    const genre = genreKey === null ? null : await genreOf(genreKey);
    return { ...songSettings, genre: genre ?? noGenre() };
}

function playPrevious(origin = "previous") {
    const item = previousSong();
    if (item !== null) playFromList(item, origin);
}

/** The song heard after this one, or a new one from the latest, the way a song running out leads to one. */
function playNext(origin = "next") {
    const item = nextSong();
    if (item !== null) playFromList(item, origin);
    else if (!elements.generate.disabled) playNew(null, origin);
}

elements.previous.addEventListener("click", () => playPrevious());
elements.next.addEventListener("click", () => playNext());

/** A song of the list, as its entry keeps it: its seed, the settings it was asked for with, and its genre's key, if it has one. */
function readEntry(entry) {
    const [seedText, settingsText, genreKey] = entry.split(":");
    return { seedText, seed: readSeed(seedText), entrySettings: parseSettings(settingsText) ?? defaultSettings(), genreKey: genreKey ?? null };
}

/**
 *     One song to come back to: its seed, marked where it was asked for with settings that change it, which it is asked
 *     for with again, the settings becoming the page's. What comes back is the song as it was generated and heard, and
 *     it is the song that was asked for: it leads to no other when it ends.
 */
function createHistoryItem(entry) {
    const { seedText, seed, entrySettings, genreKey } = readEntry(entry);
    const identity = identityOf(entrySettings);

    const button = document.createElement("button");
    button.type = "button";
    button.dataset.entry = entry;
    button.dataset.seed = seedText;
    button.dataset.song = songName(seed, entrySettings, genreKey);
    const isGiven = identity !== "" || genreKey !== null;
    button.textContent = isGiven ? `${seedText} ⚙` : seedText;
    button.setAttribute("aria-label", isGiven ? `Song ${seedText}, with its settings` : `Song ${seedText}`);

    button.addEventListener("click", async () => playNew(seed, "history", await withGenre(entrySettings, genreKey)));

    return button;
}

// --- sharing ---------------------------------------------------------------

elements.share.addEventListener("click", share);

/**
 *     Hands over a link to the song on the page: on a touch screen by its share sheet, as a phone shares, and everywhere
 *     else by the clipboard, where a browser may offer a share sheet with nothing behind it, as one on Linux does, which
 *     fails as if it were closed; each falling back on the other, and on the link shown to copy by hand. The link is the
 *     seed and the settings, so what arrives is the song with the mix as it was left.
 */
async function share() {
    const seed = elements.seed.value;
    const link = songLink();
    const isSheetFirst = navigator.share !== undefined && matchMedia("(pointer: coarse)").matches;

    // closing a phone's share sheet halfway is thinking better of it, not a failure worth reporting
    if (isSheetFirst && (await shareBySheet(seed, link)) !== "failed") return;

    if (await copy(link)) {
        say("Copied");
        announce(`Link to song ${seed} copied.`);
        track("shared", aboutSong({ detail: "clipboard" }));
        return;
    }

    if (!isSheetFirst && navigator.share !== undefined && (await shareBySheet(seed, link)) === "shared") return;

    // nothing worked, so the link goes where it can at least be read and copied by hand
    setStatus(link);
    say("Above");
}

/** The link handed to the share sheet: shared, the sheet closed, or failed. */
async function shareBySheet(seed, link) {
    try {
        await navigator.share({ title: `RMG song ${seed}`, url: link });
        track("shared", aboutSong({ detail: "sheet" }));
        return "shared";
    } catch (error) {
        return error.name === "AbortError" ? "closed" : "failed";
    }
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
    elements.shareLabel.textContent = word;
    // where the button is only its icon, the word shows beside it for as long as it is said
    elements.share.classList.add("saying");

    // a second press starts the wait over, rather than the first one cutting the second short
    clearTimeout(sayTimer);
    sayTimer = setTimeout(() => {
        elements.shareLabel.textContent = "Share";
        elements.share.classList.remove("saying");
    }, 1600);
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
    setStatus("");
    // the song fetched, then rendered through the soundfont for the first half and encoded for the second
    showWork(elements.exportMp3, `Exporting ${name}`);

    try {
        const response = await requestSong(withSkip(requestFor(songSeed, settings), songSkipped));
        if (!response.ok) throw new Error(await describeFailure(response));

        const song = await response.arrayBuffer();
        const soundFont = await exporting.blob.arrayBuffer();

        const audio = await renderSong(song, soundFont, (progress) => showWork(elements.exportMp3, `Exporting ${name}`, progress / 2));

        const mp3 = await encodeMp3(audio, Number(elements.bitrate.value), (progress) =>
            showWork(elements.exportMp3, `Exporting ${name}`, 0.5 + progress / 2));

        offerExport(mp3, name);
        announce(`Exported ${name}, ${formatSize(mp3.size)}.`);
        track("export_mp3", aboutSong({ ms: since(), detail: `${elements.bitrate.value} kbps` }));
    } catch (error) {
        setStatus(`Could not export ${name}: ${error.message}`, true);
    } finally {
        hideWork(elements.exportMp3);
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

// a switch pressed in or not, as the rating is
const isAutoplayOn = () => elements.autoplay.getAttribute("aria-pressed") === "true";

elements.autoplay.setAttribute("aria-pressed", String(isAutoplaying()));

elements.autoplay.addEventListener("click", () => {
    elements.autoplay.setAttribute("aria-pressed", String(!isAutoplayOn()));
    setAutoplaying(isAutoplayOn());
});

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
        render(player);
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
 *     The lock screen and media keys, which drive the page the way its own buttons do, Previous and Next walking the
 *     list as theirs do.
 */
const mediaControls = createMediaControls({
    play: () => playOrPause(true),
    pause: () => playOrPause(false),
    stop: () => elements.stop.click(),
    next: () => playNext("lockscreen"),
    previous: () => playPrevious("lockscreen"),
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
    // a song on its way is let go of: Stop is the last word on what plays, and the page keeps the song it has
    arrival?.controller.abort();
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
// every value's name and what it does, which is shown under the pointer rather than taking a line of its own
const SETTING_NAMES = {
    unconventionality: ["How experimental", "How far the whole song strays from convention. Every facet below is drawn around it, so that a song can be plain in one way and experimental in another; a facet given is set outright."],
    tempo: ["Tempo", "The song's tempo, in beats a minute: 60 at the left, 120 in the middle and 240 at the right. Its sections may change it, as its feel allows, and an ending may slow it."],
    key: ["Key", "The key the song starts in. Its key changes, as its scale allows, move on from it."],
    mode: ["Mode", "The scale the song starts in, on its key: major, minor, a mode or a stranger scale. Its sections may change it, as its scale allows."],
    meter: ["Meter", "The beats of the first section's bars. Its later sections may change the meter, as its feel allows."],
    swing: ["Swing", "How late the second of every pair of notes plays: straight at the left, a triplet's swing at the right. A meter that does not pair its notes, such as 6/8, plays straight."],
    length: ["Length", "How long the song plays, from half a minute to fifteen. Its form comes as near as it can, more and longer sections for a long song; a minute's song may run a little longer."],
    feel: ["Feel", "How the time moves: odd meters and meter changes, tuplets and polymeters, swing, and tempo changes between sections."],
    form: ["Form", "How the song is put together: its sections and their order, which parts play where, and its solos."],
    scale: ["Scale", "The notes the song draws on: modes beyond major and minor, key changes, and the scales its lines take."],
    progression: ["Progression", "How freely the chords move, from the common progressions to ones that wander."],
    chords: ["Chords", "The chords' colours, from plain triads to extended and altered ones, and how they are voiced."],
    melody: ["Melody", "How the lines move, their leaps, rhythms and ranges, and how freely the soloists play."],
    sound: ["Sound", "The instruments over the song, and how they play: articulation, bends and effects."],
    groove: ["Groove", "The drums' patterns, from the common beats to unusual ones."],
    fills: ["Fills", "The drums' fills between phrases and sections, and their solos."],
};

// a slider's two ends, where they are not the plain and the experimental
const SCALE_ENDS = { tempo: ["Slow", "Fast"], swing: ["Straight", "Triplet"], length: ["Short", "Long"] };

const PART_NAMES = { melody: "Melody", chords: "Chords", bass: "Bass", pad: "Pad", counterMelody: "Counter-melody", drum: "Drums", riff: "Riff", rhythm: "Rhythm", solo: "Soloist" };

const DRUM_GROUP_NAMES = {
    kick: "Kick", snare: "Snare", timekeepers: "Timekeepers", toms: "Toms", accents: "Cymbals", percussion: "Percussion", calls: "Calls",
};



/** The values chosen from a list rather than on a slider. */
const CHOICES = ["key", "mode", "meter"];

const KEY_NAMES = ["C", "C♯", "D", "E♭", "E", "F", "F♯", "G", "A♭", "A", "B♭", "B"];

/** The list of genres and its editor's button, which wait for the choices a genre is written by, and the choices. */
let genreSelect = null;
/** The chosen genre's sentence, under the list: a preset's, or one of one's own's description. */
let genreAbout = null;
let genreManage = null;
let genreChoices = null;

/** The editor of a genre of one's own (genre-editor.js), its draft played, kept here or let go of. */
const genreEditor = createGenreEditor({
    onPlay: (genre) => genre !== null && useGenre(genre),
    // in place of the genre it was opened from, renamed or not, and of any other of its name, which the editor has
    // asked about; the lists show them by name
    onSave: (genre, replacing) => {
        if (genre === null) return;
        keepGenres([...recallGenres().filter((x) => !(replacing !== null && sameName(x.name, replacing)) && !sameName(x.name, genre.name)), genre]);
        useGenre(genre);
    },
    onDelete: (name) => {
        keepGenres(recallGenres().filter((x) => !sameName(x.name, name)));
        genreEditor.close();
        showGenre();
    },
    isOwn: (name) => recallGenres().some((x) => sameName(x.name, name)),
    // every genre of a list kept, each under its name or the first number free after it; how many, in words
    onKeepAll: keepAllGenres,
});
document.body.append(genreEditor.element);

/** The window of every genre (genre-manager.js): one's own edited, duplicated or removed, a preset duplicated, one made new. */
const genreManager = createGenreManager({
    presets: () => PRESETS,
    own: () => recallGenres(),
    // the song's genre where it is neither a preset nor one's own
    current: () => hasAGenre(settings.genre) && presetOf(settings.genre) === null && !recallGenres().some((x) => isSameGenre(x, settings.genre)) ? settings.genre : null,
    onKeep: keepSongsGenre,
    // the editor in place of the window, which closing it leaves for the page
    onNew: () => {
        genreManager.close();
        genreEditor.open(noGenre(), genreChoices, false);
    },
    // a copy in the editor, kept only once it is saved; a preset's without its sentence, which says what the preset sets
    // and would no longer once the copy changes it
    onDuplicate: (genre) => {
        const isPreset = PRESETS.includes(genre);
        const copy = { name: freeName(genre.name), ...(genre.description && !isPreset ? { description: genre.description } : {}), choices: structuredClone(genre.choices) };
        genreManager.close();
        genreEditor.open(copy, genreChoices, false);
    },
    onEdit: (genre) => {
        genreManager.close();
        // one's own only where it is, the song's of the same name otherwise
        genreEditor.open(genre, genreChoices, recallGenres().some((x) => isSameGenre(x, genre)));
    },
    onRemove: (name) => {
        keepGenres(recallGenres().filter((x) => !sameName(x.name, name)));
        showGenre();
    },
    onKeepAll: keepAllGenres,
    knows: (name) => genreChoices.some((x) => x.name === name),
    asFile: (genre) => ({ ...genre, choices: nestedChoices(genre.choices, genreChoices.map((x) => x.name)) }),
});
document.body.append(genreManager.element);

// every choice's row, by its key: the key, the mode and the meter, listed as the server lists them
const choiceRows = new Map();

// the tempos a song may be given, in beats a minute, and its lengths, in seconds, by their steps, as the server lists them
let tempos = [];
let lengths = [];

/** A length in seconds as minutes and seconds, "2:49". */
function formatLength(seconds) {
    const whole = Math.round(seconds);
    return `${Math.floor(whole / 60)}:${String(whole % 60).padStart(2, "0")}`;
}

/** What a setting's step shows: its tempo in beats a minute, its length in minutes, or its share. */
function settingText(key, step) {
    if (key === "tempo") return tempos.length > 0 ? `${Math.round(tempos[step])} bpm` : "";
    if (key === "length") return lengths.length > 0 ? formatLength(lengths[step]) : "";
    return formatPercent(fromStep(step));
}

// every value's row, by its key, and every drum group's
const settingRows = new Map();
const groupRows = new Map();

// the song's own instrument and pan of every part, whatever the mix plays, as the server said
let own = {};

buildSettings();

/** The settings panel: the values that change the song, every part's row of the mixer, and the drums'. */
function buildSettings() {
    // the genre first, then in the order they are named, the key, the mode and the meter chosen from a
    // list and every other on a slider
    elements.genreRow.append(...createGenreRow(), createGenreAbout());
    elements.songSettings.append(...Object.keys(SETTING_NAMES).map((key) => (CHOICES.includes(key) ? createChoiceRow(key) : createSettingRow(key))));
    // the drums last, their groups under them, each with its pieces
    elements.mixer.append(...PARTS.filter((part) => part !== "drum").map(createPartRow), createPartRow("drum"), ...DRUM_GROUPS.map(createDrumGroupRow));

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
 *     A value that changes the song and is one of a list, the key, the mode or the meter: checked, it is given, and the list sets
 *     it; unchecked, the list shows what the song on the page drew.
 */
function createChoiceRow(key) {
    const [name, description] = SETTING_NAMES[key];
    const given = createGiven(name);

    const label = document.createElement("span");
    label.className = "setting-name";
    label.textContent = name;

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

    select.setAttribute("aria-description", description);
    // where a slider's row has its ends and its value, nothing, so that the list is as wide as a slider
    const place = document.createElement("span");
    place.className = "setting-slider";
    const [end, otherEnd] = [0, 1].map(() => {
        const element = document.createElement("span");
        element.className = "end";
        element.setAttribute("aria-hidden", "true");
        return element;
    });
    place.append(end, select, otherEnd, emptyValue());
    const row = document.createElement("div");
    row.className = "setting";
    row.title = description;
    row.append(given, label, place);
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

// --- the genre ---------------------------------------------------------------


const GENRE_DESCRIPTION = "What the song's draws keep to: its tempo and feel, its harmony, its instruments, its drums and its rhythm. "
    + "A preset, or one of your own; none for the songs as they are.";

/**
 *     The genre's row, on the song's panel under the transport: a list of none, the presets and the genres made here, and
 *     the song's own where it is none of them; the button acting on the genre chosen, and the window of every genre.
 */
function createGenreRow() {
    const label = document.createElement("label");
    label.className = "field genre-field";
    const name = document.createElement("span");
    name.textContent = "Genre";

    genreSelect = document.createElement("select");
    genreSelect.setAttribute("aria-label", "Genre");
    genreSelect.setAttribute("aria-description", GENRE_DESCRIPTION);
    genreSelect.title = GENRE_DESCRIPTION;
    genreSelect.disabled = true;
    genreSelect.addEventListener("change", () => chooseGenre(genreSelect.value));
    label.append(name, genreSelect);


    genreManage = document.createElement("button");
    genreManage.type = "button";
    genreManage.className = "secondary genre-manage";
    genreManage.textContent = "Manage";
    genreManage.title = "Every genre: make one, duplicate any, edit or remove your own, import and export them";
    genreManage.disabled = true;
    genreManage.addEventListener("click", () => genreManager.open());
    return [label, genreManage];
}

/** The chosen genre's sentence, a line of its row's own under it: a preset's, or one of one's own's description. */
function createGenreAbout() {
    genreAbout = document.createElement("p");
    genreAbout.className = "genre-about";
    genreAbout.hidden = true;
    return genreAbout;
}

/** Every genre of a list kept among one's own, each under its name or the first number free after it; how many, in words. */
function keepAllGenres(genres) {
    for (const genre of genres) keepGenres([...recallGenres(), { ...genre, name: nameFree(genre.name.trim() || "Untitled") }]);
    showGenre();
    return genres.length === 1 ? "One genre" : `${genres.length} genres`;
}

/** The song's genre, a link's or a draft played, kept among one's own under its name or the first number free, the song as it is. */
function keepSongsGenre(genre) {
    const kept = { name: nameFree(genre.name.trim() || "Untitled"), choices: structuredClone(genre.choices) };
    keepGenres([...recallGenres(), kept]);
    settings.genre = asGenre(kept);
    keepCurrentSettings();
    showGenre();
}

/** A name as it is where no genre of one's own nor a preset has it, else with the first number free after it. */
function nameFree(name) {
    return [...recallGenres(), ...PRESETS].some((x) => sameName(x.name, name)) ? freeName(name) : name;
}

/** A name and the first number after it that no genre of one's own nor a preset has, whatever its case: "Rock 1", "Rock 2". */
function freeName(name) {
    const taken = new Set([...recallGenres(), ...PRESETS].map((x) => x.name.trim().toLowerCase()));
    let number = 1;
    while (taken.has(`${name} ${number}`.toLowerCase())) number++;
    return `${name} ${number}`;
}

/** Nothing where a slider's row shows its value, as wide, so that a list is as wide as a slider. */
function emptyValue() {
    const value = document.createElement("output");
    value.className = "percent";
    value.setAttribute("aria-hidden", "true");
    return value;
}

/** Asks for the song again in a genre the editor made. */
function useGenre(genre) {
    isAnyGenre = false;
    keepAnyGenre(false);
    settings.genre = asGenre(genre);
    track("genre_chosen", { detail: "own" });
    showGenre();
    settingsChanged(true);
}

/** A preset drawn for the next song, of Any genre, other than the song's where there is another. */
function drawAnyGenre() {
    const others = PRESETS.filter((x) => !isSameGenre(asGenre(x), settings.genre));
    return asGenre(others[Math.floor(Math.random() * others.length)]);
}

/** Every genre the list offers, by its value in it: the presets, then the genres made here, each by name; one's own by its place as kept. */
function listedGenres() {
    const byItsName = (first, second) => byName(first.genre, second.genre);
    return [
        ...PRESETS.map((preset) => ({ value: `preset:${preset.id}`, genre: asGenre(preset), group: "Presets", description: preset.description })).sort(byItsName),
        ...recallGenres().map((genre, i) => ({ value: `own:${i}`, genre: asGenre(genre), group: "Yours", description: genre.description ?? "" })).sort(byItsName),
    ];
}

/** The list as the settings have it: the genre chosen, or the song's own, as a link brought it, under its name. */
function showGenre() {
    if (genreSelect === null) return;

    const listed = listedGenres();
    const chosen = hasAGenre(settings.genre) ? listed.find((x) => isSameGenre(x.genre, settings.genre)) : null;
    const groups = new Map();
    for (const item of listed) {
        if (!groups.has(item.group)) {
            const group = document.createElement("optgroup");
            group.label = item.group;
            groups.set(item.group, group);
        }
        groups.get(item.group).append(createOption(item.value, item.genre.name || "Unnamed"));
    }
    const options = [createOption("none", "None"), createOption("any", "Any genre"), ...groups.values()];
    if (hasAGenre(settings.genre) && chosen === undefined) options.splice(2, 0, createOption("song", settings.genre.name || "This song's genre"));
    genreSelect.replaceChildren(...options);
    genreSelect.value = isAnyGenre ? "any" : !hasAGenre(settings.genre) ? "none" : chosen?.value ?? "song";
    // of Any genre, the preset this song plays
    const about = chosen?.description ?? "";
    genreAbout.textContent = isAnyGenre && hasAGenre(settings.genre) ? `This song: ${settings.genre.name}. ${about}` : about;
    genreAbout.hidden = genreAbout.textContent === "";
    genreManager.refresh();
}

/** Asks for the song again in the genre chosen, or in none. */
function chooseGenre(value) {
    if (value === "song") return;
    isAnyGenre = value === "any";
    keepAnyGenre(isAnyGenre);
    settings.genre = isAnyGenre ? drawAnyGenre() : value === "none" ? noGenre() : asGenre(listedGenres().find((x) => x.value === value).genre);
    track("genre_chosen", { detail: value.startsWith("preset:") ? value.slice("preset:".length) : value.startsWith("own:") ? "own" : value });
    showGenre();
    settingsChanged(true);
}

/** The tempos, meters, drum pieces and scales a song may be given, from the server, which only it knows. */
async function listChoices() {
    try {
        const response = await fetch(new URL("api/songs/options", location.href));
        if (!response.ok) return;

        const options = await response.json();
        tempos = options.tempos;
        lengths = options.lengths;
        // a piece of a group the page shows no row for left off its chips, the lists after it filled all the same
        for (const piece of options.drumPieces) {
            groupRows.get(piece.group.toLowerCase())?.chips.append(createDrumPieceChip(piece));
            for (const note of piece.notes) drumGroupOfNote.set(note, piece.group.toLowerCase());
        }
        evenDrumGroupRows();
        showSettings();
        choiceRows.get("meter").select.append(...options.meters.map((x, i) => createOption(String(i), x)));
        choiceRows.get("mode").select.append(...options.modes.map((x, i) => createOption(String(i), x)));
        showSetting("tempo");
        showSetting("length");
        showChoice("meter");
        showChoice("mode");
    } catch {
        // without the lists, the tempo, the meter and the mode are the songs' own
    }
}

/** A part's channels, its own and its others', whether each sits on the other side of the part's pan, and the program of one that keeps its own. */
function channelsOf(part) {
    return [...(channels[part] == null ? [] : [{ channel: channels[part], mirrored: false }]), ...(others[part] ?? [])];
}

/** A pan step on the other side of the middle, as far from it. */
function mirrorPan(step) {
    return toStep(1 - fromStep(step));
}

/** A pan step as MIDI's, 64 in the middle, as the steps have theirs. */
function toMidiPan(step) {
    const amount = fromStep(step);
    return Math.round(amount <= 0.5 ? amount * 128 : 64 + (amount - 0.5) * 126);
}

/** Whether a value is given, rather than shown as the song drew it. */
function createGiven(name) {
    const given = document.createElement("input");
    given.type = "checkbox";
    given.setAttribute("aria-label", `Give the ${name.toLowerCase()}`);
    given.title = "Check to override, uncheck to randomize";
    return given;
}

/**
 *     A value that changes the song: checked, it is given, and the slider sets it; unchecked, the slider shows what the
 *     song on the page drew. A change asks for the song again once the slider is let go of.
 */
function createSettingRow(key) {
    const [name, description] = SETTING_NAMES[key];
    const [low, high] = SCALE_ENDS[key] ?? ["Plain", "Experimental"];
    const given = createGiven(name);

    const label = document.createElement("span");
    label.className = "setting-name";
    label.textContent = name;

    const range = document.createElement("input");
    range.type = "range";
    range.dataset.setting = key;
    range.min = "0";
    range.max = String(LAST_STEP);
    range.step = "1";
    // its middle, 50% or 120 beats a minute, which a double-click puts it back to
    range.dataset.default = String(MIDDLE_STEP);
    range.setAttribute("aria-label", `${name}, from ${low.toLowerCase()} to ${high.toLowerCase()}`);
    range.setAttribute("aria-description", description);

    const plain = document.createElement("span");
    plain.className = "end";
    plain.setAttribute("aria-hidden", "true");
    plain.textContent = low;
    const experimental = document.createElement("span");
    experimental.className = "end";
    experimental.setAttribute("aria-hidden", "true");
    experimental.textContent = high;

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
    row.title = description;
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
    row.output.textContent = settingText(key, setting.value);
}

/**
 *     A part's row: given or as the song drew it, whether it plays, which changes the song, and its instrument and pan,
 *     which are heard at once; its volume, which the song never draws; and how loud it plays as it is heard.
 */
function createPartRow(part) {
    const name = PART_NAMES[part];

    const given = createGiven(name);
    given.addEventListener("change", () => setPartGiven(part, given.checked));

    const plays = document.createElement("input");
    plays.type = "checkbox";
    plays.setAttribute("aria-label", `The ${name.toLowerCase()} plays`);
    plays.addEventListener("change", () => {
        settings.parts[part].plays = plays.checked ? "on" : "off";
        showEveryPartOff();
        settingsChanged(true);
    });

    const instrument = document.createElement("select");
    instrument.setAttribute("aria-label", `Instrument of the ${name.toLowerCase()}`);
    if (part === "drum") instrument.append(...DRUM_KITS.map((kit) => createOption(String(kit.instrument), kit.name)));
    else fillInstruments(instrument);
    instrument.addEventListener("change", () => setPartInstrument(part, { isGiven: true, value: Number(instrument.value) }, true));

    const volume = createFader(`Volume of the ${name.toLowerCase()}`, (step) => setPartVolume(part, step));
    const pan = createFader(`Pan of the ${name.toLowerCase()}, from left to right`, (step) => setPartPan(part, { isGiven: true, value: step }), true);

    // every part a colour of its own, the same in every song, spread round the wheel
    const pulse = createPulse(Math.round((PARTS.indexOf(part) * 360) / PARTS.length));

    const heading = document.createElement("th");
    heading.scope = "row";
    heading.textContent = name;

    const row = document.createElement("tr");
    row.className = "part";
    // the drums sit in the middle, as a kit does, and have no pan
    const [mute, solo] = createListening(part, name);
    row.append(cellOf(given, "given"), heading, cellOf(plays, "on"), cellOf(instrument, "instrument"), cellOf(part === "drum" ? null : pan.field, "pan"), cellOf(volume.field, "volume"),
        cellOf(mute, "listen"), cellOf(solo, "listen"), cellOf(pulse.canvas, "pulse"));
    rows.set(part, { row, given, plays, instrument, volume, pan, mute, solo, pulse });
    return row;
}

/** Whether a part's row is given: its playing, instrument or pan, and the drums' pieces. */
function isPartGiven(part) {
    const mix = settings.parts[part];
    return mix.plays !== "random" || mix.instrument.isGiven || mix.pan.isGiven || (part === "drum" && settings.drumPieces.isGiven);
}

/**
 *     Gives a part's playing, instrument and pan together, as the song drew them, the drums' pieces and their groups'
 *     volumes with them, or lets the song have them again, its volumes at the fullest, as one switch has them all.
 */
function setPartGiven(part, isGiven) {
    const mix = settings.parts[part];
    const drawn = own[part] ?? { instrument: mix.instrument.value, pan: mix.pan.value };
    mix.plays = !isGiven ? "random" : partsInSong[part] === false ? "off" : "on";
    // the song's own volume, the fullest, once it is let go of
    if (!isGiven) setPartVolume(part, LAST_STEP);
    setPartInstrument(part, { isGiven, value: isGiven ? mix.instrument.value : drawn.instrument }, false);
    if (part === "drum") {
        // the pieces the song played, none given before a song has played any
        settings.drumPieces = { isGiven: isGiven && drumPiecesPlayed.length > 0, value: [...drumPiecesPlayed] };
        if (!isGiven) for (const group of DRUM_GROUPS) settings.drumGroups[group].volume = LAST_STEP;
    } else setPartPan(part, { isGiven, value: isGiven ? mix.pan.value : drawn.pan });
    showSettings();
    settingsChanged(true);
}

/** Puts a drum piece in the drums given, or takes it out, which makes the song again. */
function toggleDrumPiece(piece) {
    const pieces = settings.drumPieces.value;
    const next = pieces.includes(piece) ? pieces.filter((x) => x !== piece) : [...pieces, piece];
    settings.drumPieces = { isGiven: true, value: DRUM_PIECES.filter((x) => next.includes(x)) };
    showSettings();
    track("mix_changed", { detail: "drum piece" });
    settingsChanged(true);
}

/**
 *     A drum group's row, under the drums: its pieces, in the song or out, which the drums' switch gives with them, and how
 *     loud it plays, which the server makes; its pulse as its drums are struck.
 */
function createDrumGroupRow(group) {
    const name = DRUM_GROUP_NAMES[group];

    // the group's pieces, which come with the options the server lists, where a part has its instrument and its pan
    const chips = document.createElement("div");
    chips.className = "chips";
    chips.setAttribute("role", "group");
    chips.setAttribute("aria-label", `${name} pieces`);

    const volume = createFader(`Volume of the ${name.toLowerCase()}`, (step) => (settings.drumGroups[group].volume = step), false, () => settingsChanged(true));

    // every group a colour of its own, between the parts' round the wheel
    const pulse = createPulse(Math.round(((DRUM_GROUPS.indexOf(group) + 0.5) * 360) / DRUM_GROUPS.length));

    // its name over the column of whether a part plays, which a group has not, so that the parts' names can be narrower
    const heading = document.createElement("th");
    heading.scope = "row";
    heading.colSpan = 2;
    heading.textContent = name;

    const row = document.createElement("tr");
    row.className = "drum-group";
    const [mute, solo] = createListening(`drums:${group}`, name);
    const chipsCell = cellOf(chips, "pieces");
    chipsCell.colSpan = 2;
    row.append(cellOf(null, "given"), heading, chipsCell, cellOf(volume.field, "volume"), cellOf(mute, "listen"), cellOf(solo, "listen"), cellOf(pulse.canvas, "pulse"));
    groupRows.set(group, { row, volume, chips, mute, solo, pulse });
    return row;
}


/**
 *     A row's mute and solo, as icons pressed in, which only this page hears: a part's by its name, a drum group's as
 *     "drums:" and its own.
 */
function createListening(key, name) {
    const button = (label, icon, set) => {
        const toggle = document.createElement("button");
        toggle.type = "button";
        toggle.className = "icon listen";
        toggle.setAttribute("aria-pressed", "false");
        toggle.setAttribute("aria-label", `${label} the ${name.toLowerCase()}`);
        toggle.title = `${label} the ${name.toLowerCase()}, here only`;
        toggle.innerHTML = `<svg viewBox="0 0 24 24" aria-hidden="true"><path d="${icon}" /></svg>`;
        toggle.addEventListener("click", () => {
            if (set.has(key)) set.delete(key);
            else set.add(key);
            showListening();
            applyMuting();
            track("mix_changed", { detail: label.toLowerCase() });
        });
        return toggle;
    };
    return [button("Mute", MUTE_ICON, muted), button("Solo", SOLO_ICON, soloed)];
}

/** Every mute and solo pressed as it is. */
function showListening() {
    for (const [part, row] of rows) {
        row.mute.setAttribute("aria-pressed", String(muted.has(part)));
        row.solo.setAttribute("aria-pressed", String(soloed.has(part)));
    }
    for (const [group, row] of groupRows) {
        row.mute.setAttribute("aria-pressed", String(muted.has(`drums:${group}`)));
        row.solo.setAttribute("aria-pressed", String(soloed.has(`drums:${group}`)));
    }
}

/**
 *     Muted, or left out of a solo: a part where another is soloed and it is not, the drums with every group of theirs but
 *     where one of their groups is soloed, as that solo is the drums'.
 */
function isSilenced(part) {
    const soloedGroups = DRUM_GROUPS.some((group) => soloed.has(`drums:${group}`));
    const isSoloed = soloed.has(part) || (part === "drum" && soloedGroups);
    return muted.has(part) || (soloed.size > 0 && !isSoloed);
}

/** A drum group muted, or left out where another group of the drums is soloed, or with the drums silenced. */
function isGroupSilenced(group) {
    const key = `drums:${group}`;
    const soloedGroups = DRUM_GROUPS.some((other) => soloed.has(`drums:${other}`));
    return isSilenced("drum") || muted.has(key) || (soloedGroups && !soloed.has(key));
}

/** Puts the mutes and solos on the player: a part's channel, a drum group's notes on the drums'. */
function applyMuting(on = null) {
    const apply = (player) => {
        for (const part of PARTS) for (const { channel } of channelsOf(part)) player.setChannelMuted(channel, isSilenced(part));
        // a song's drums may be on another channel than the last's
        player.unmuteNotes();
        if (channels.drum == null) return;
        for (const [note, group] of drumGroupOfNote) if (isGroupSilenced(group)) player.muteNote(channels.drum, note);
    };
    if (on !== null) apply(on);
    else withPlayer(apply, "Could not silence the part");
}

/**
 *     Every drum group's row as high as the highest, whose pieces take the most lines, so that the groups read as a block;
 *     measured again when the page is narrowed or widened, and when the panel opens, as a closed one has no height.
 */
function evenDrumGroupRows() {
    const groups = [...groupRows.values()].map((x) => x.row);
    for (const row of groups) row.style.height = "";
    // a phone's rows are laid out on two lines each rather than as a table's, and are as high as they need
    if (groups.length === 0 || getComputedStyle(groups[0]).display !== "table-row") return;
    const highest = Math.max(...groups.map((row) => row.getBoundingClientRect().height));
    if (highest > 0) for (const row of groups) row.style.height = `${highest}px`;
}

window.addEventListener("resize", evenDrumGroupRows);
document.getElementById("settings-panel").addEventListener("toggle", evenDrumGroupRows);

/** A table cell holding a control, or nothing, of the column its class names, where it has one. */
function cellOf(content, column = "") {
    const cell = document.createElement("td");
    if (column !== "") cell.className = column;
    if (content !== null) cell.append(content);
    return cell;
}

/** A drum piece's switch in its group's row, in the song when pressed. */
function createDrumPieceChip(piece) {
    const chip = document.createElement("button");
    chip.type = "button";
    chip.className = "chip";
    chip.dataset.piece = piece.name;
    // its group's name left out of it, as its row says it: "Acoustic" among the snares
    chip.textContent = piece.label.split(" ").filter((word) => word !== piece.group).join(" ") || piece.label;
    chip.setAttribute("aria-pressed", "false");
    chip.addEventListener("click", () => toggleDrumPiece(piece.name));
    return chip;
}

/** A fader from 0 to 61, saying where it is as a share, or where a pan sits. */
function createFader(label, onInput, isPan = false, onChange = null) {
    const input = document.createElement("input");
    input.type = "range";
    input.min = "0";
    input.max = String(LAST_STEP);
    input.step = "1";
    input.className = "fader";
    input.setAttribute("aria-label", label);
    // a pan in the middle and a volume as loud as it is made, which a double-click puts it back to
    input.dataset.default = String(isPan ? MIDDLE_STEP : LAST_STEP);

    const output = document.createElement("output");
    output.className = "percent";
    const show = () => (output.textContent = isPan ? formatPan(Number(input.value)) : formatPercent(fromStep(Number(input.value))));
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

/** A slider that has a default, and is set rather than showing what the song drew, which is left alone, as it is to a drag. */
function isResettable(target) {
    return target instanceof HTMLInputElement && target.type === "range" && target.dataset.default !== undefined && !target.disabled;
}

/** A slider back to its default, as if it were dragged there, so that its own handlers apply it. */
function resetSlider(slider) {
    slider.value = slider.dataset.default;
    slider.dispatchEvent(new Event("input", { bubbles: true }));
    slider.dispatchEvent(new Event("change", { bubbles: true }));
}

// a slider back to its default on a double-click
document.addEventListener("dblclick", (event) => {
    if (isResettable(event.target)) resetSlider(event.target);
});

// and on a double-tap, which a touch screen never makes a double-click of on a slider, its touches being drags: a second
// press of the same slider soon after the first and near it, the reset made once the press lets go, after the thumb's
// own move to where it touched
const DOUBLE_TAP_MS = 350;
const DOUBLE_TAP_REACH = 24;
let lastTap = null;
document.addEventListener("pointerdown", (event) => {
    if (event.pointerType !== "touch") return;
    const slider = event.target;
    const tap = { slider, time: event.timeStamp, x: event.clientX, y: event.clientY };
    const isDouble = lastTap !== null && lastTap.slider === slider && tap.time - lastTap.time < DOUBLE_TAP_MS
        && Math.hypot(tap.x - lastTap.x, tap.y - lastTap.y) < DOUBLE_TAP_REACH;
    lastTap = isDouble || !isResettable(slider) ? null : tap;
    if (isDouble && isResettable(slider)) slider.addEventListener("pointerup", () => resetSlider(slider), { once: true });
});

/** Puts the settings, and what the song on the page drew and is made of, on the panel. */
function showSettings() {
    showGenre();
    for (const key of settingRows.keys()) showSetting(key);
    for (const key of choiceRows.keys()) showChoice(key);

    for (const part of PARTS) {
        const row = rows.get(part);
        const mix = settings.parts[part];
        const isGiven = isPartGiven(part);
        row.given.checked = isGiven;
        row.plays.checked = mix.plays === "random" ? partsInSong[part] !== false : mix.plays === "on";
        row.instrument.value = String(mix.instrument.value);
        for (const control of [row.plays, row.instrument, row.volume.input, row.pan.input]) control.disabled = !isGiven;
        row.volume.input.value = String(mix.volume);
        row.volume.show();
        row.pan.input.value = String(mix.pan.value);
        row.pan.show();
        // a part the song does not play is still set here, for the songs after it
        row.row.classList.toggle("absent", channels[part] == null);
    }

    // the drums' groups and pieces are given with the drums, a piece kept while it is the last
    const areDrumsGiven = isPartGiven("drum");
    const pieces = settings.drumPieces.value;
    for (const group of DRUM_GROUPS) {
        const row = groupRows.get(group);
        row.volume.input.value = String(settings.drumGroups[group].volume);
        row.volume.input.disabled = !areDrumsGiven;
        row.volume.show();
        row.row.classList.toggle("absent", !drumGroupsPresent.includes(group));
        for (const chip of row.chips.children) {
            const isIn = pieces.includes(chip.dataset.piece);
            chip.setAttribute("aria-pressed", String(isIn));
            chip.disabled = !areDrumsGiven || (isIn && pieces.length === 1);
        }
    }

    showListening();
    elements.songVolume.value = String(settings.volume);
    showSongVolume();
    elements.allOff.hidden = !isEveryPartOff(settings);
    updateGenerateButtons();
}

/**
 *     After a change of the settings: kept for the next visit and in the address, and then either the song asked for
 *     again, for a change of what it is made of, or its file fetched again with the mix, for one only heard.
 */
function settingsChanged(isRemade) {
    keepCurrentSettings();
    // the address once a slider rests, as a browser takes only so many rewrites of it in a while
    clearTimeout(rememberTimer);
    rememberTimer = setTimeout(() => rememberSong(), REMEMBER_WAIT);
    if (isRemade) scheduleRegeneration();
    else scheduleDownloadRefresh();
}

// how long the settings rest, in milliseconds, before the address takes them, and the wait under way
const REMEMBER_WAIT = 250;
let rememberTimer = 0;

/** A part's instrument set, a change of it told where somebody made one, not where giving the part set it. */
function setPartInstrument(part, instrument, isChange) {
    settings.parts[part].instrument = instrument;
    rows.get(part).instrument.value = String(instrument.value);
    if (isChange) track("mix_changed", { detail: "instrument" });
    settingsChanged(false);

    // the page and the download are right whatever the audio does, so the player is told separately
    // a riser keeps its own sound
    for (const { channel } of channelsOf(part).filter((x) => x.program == null))
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

/** A part's volume under the song's, on the player. */
function applyVolume(part) {
    const volume = fromStep(settings.volume) * fromStep(settings.parts[part].volume);
    for (const { channel } of channelsOf(part))
        withPlayer((player) => player.setChannelVolume(channel, volume), "Could not set the volume");
}

function formatPercent(volume) {
    return `${Math.round(volume * 100)}%`;
}

/** Where a pan sits: in the middle, or how far left or right. */
function formatPan(step) {
    const pan = 2 * fromStep(step) - 1;
    if (pan === 0) return "C";
    return pan < 0 ? `L${Math.round(-pan * 100)}` : `R${Math.round(pan * 100)}`;
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

/**
 *     Puts the mix on the page back onto the player after a new sound bank, which a new song's file needs no help with,
 *     carrying its instruments, volumes and pans. A bank resets every channel to the first program, so what the song
 *     asked for is as lost as what anybody picked, and the drum channel forgets it is one. Every channel is told what it
 *     plays, how loud and where, but only a choice made here is locked, so a channel the song owns is still the song's
 *     to set when it starts over.
 */
function applyMix(player) {
    for (const part of PARTS)
        for (const { channel, mirrored, program } of channelsOf(part)) {
            const mix = settings.parts[part];
            if (part === "drum") player.setChannelDrums(channel, true);
            player.setChannelInstrument(channel, program ?? mix.instrument.value, program == null && mix.instrument.isGiven);
            player.setChannelVolume(channel, fromStep(settings.volume) * fromStep(mix.volume));
            player.setChannelPan(channel, toMidiPan(mirrored ? mirrorPan(mix.pan.value) : mix.pan.value));
        }
}

elements.songVolume.addEventListener("input", () => {
    settings.volume = Number(elements.songVolume.value);
    showSongVolume();
    for (const part of PARTS) applyVolume(part);
    settingsChanged(false);
});

function showSongVolume() {
    elements.songVolumeValue.textContent = formatPercent(fromStep(settings.volume));
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

    // a song on its way, new or remade, would replace the file this fetches with one of an older mix or another song
    if (arrival !== null || regeneration !== null) {
        scheduleDownloadRefresh();
        return;
    }

    const refresh = new AbortController();
    downloadRefresh = refresh;
    try {
        const response = await requestSong(withSkip(requestFor(songSeed, settings), songSkipped), refresh.signal);
        if (!response.ok) throw new Error(await describeFailure(response));

        offerDownload(await response.arrayBuffer());
    } catch (error) {
        // one let go of for a later choice is not a failure: that choice is asking for its own file
        if (!refresh.signal.aborted) setStatus(`The download does not include your mix: ${error.message}`, true);
    } finally {
        if (downloadRefresh === refresh) downloadRefresh = null;
    }
}

// --- sections --------------------------------------------------------------

/** A section's name as the seek bar tells it: its role in words, a solo said, and where it starts. */
function sectionName(section) {
    const role = section.role.replace(/[A-Z]/g, (x) => `-${x.toLowerCase()}`);
    const solo = section.soloists.length > 0 ? `, a ${section.soloists.includes("drum") ? "drum " : ""}solo` : "";
    return `${role[0].toUpperCase()}${role.slice(1)}${solo}, ${formatTime(section.start)}`;
}

/** The song's sections marked on the seek bar where each starts but the first, and its length shown before it plays, which playing keeps. */
function showSections(report) {
    songSections = report.sections;
    songSeconds = report.seconds;
    elements.sections.replaceChildren(...songSections.filter((x) => x.start > 0).map((section) => {
        const mark = document.createElement("span");
        mark.style.left = `${(100 * section.start) / songSeconds}%`;
        mark.dataset.name = sectionName(section);
        return mark;
    }));
    elements.total.textContent = formatTime(songSeconds);
}

// how far in from the bar's ends its thumb's middle travels, in pixels, as the marks are set in (app.css)
const SEEK_INSET = 8;

// the bar named by the section under the pointer, as a hover shows it
elements.seek.addEventListener("pointermove", (event) => {
    const box = elements.seek.getBoundingClientRect();
    const at = Math.min(Math.max((event.clientX - box.left - SEEK_INSET) / (box.width - 2 * SEEK_INSET), 0), 1) * songSeconds;
    const section = songSections.findLast((x) => x.start <= at + 1e-6);
    elements.seek.title = section ? sectionName(section) : "";
});

// --- ticking ---------------------------------------------------------------

/**
 *     The sequencer applies a new position in the worklet, so reading it straight back after a
 *     stop or a seek still gives the old one: callers that just moved it pass the position instead.
 */
function render(player, time = player.currentTime) {
    const duration = player.duration;
    elements.total.textContent = formatTime(duration > 0 ? duration : songSeconds);

    // the bar and the time under it are the seek's own while it lasts
    if (!isSeeking) {
        if (duration > 0) elements.seek.value = String(time / duration);
        elements.elapsed.textContent = formatTime(time);
    }

    // every part's level, the loudest of its channels, rising at once and falling back steadily, as a meter's does
    const now = performance.now();
    const elapsed = (now - lastRendered) / 1000;
    lastRendered = now;
    for (const [part, row] of rows) {
        const level = player.paused ? 0 : Math.max(0, ...channelsOf(part).map(({ channel }) => player.getLevel(channel)));
        const reading = level > 0 ? Math.min(1, Math.max(0, (20 * Math.log10(level) - METER_FLOOR) / (METER_TOP - METER_FLOOR))) : 0;
        row.shown = player.paused ? 0 : Math.max(reading, (row.shown ?? 0) - (METER_FALL * elapsed) / (METER_TOP - METER_FLOOR));
        row.pulse.update(row.shown, !player.paused, now / 1000);
    }
    // and every drum group's, by its hits, falling back as the parts' do
    for (const [group, row] of groupRows) {
        const hit = player.paused ? 0 : drumHits.get(group) ?? 0;
        row.shown = player.paused ? 0 : Math.max(hit, (row.shown ?? 0) - (METER_FALL * elapsed) / (METER_TOP - METER_FLOOR));
        row.pulse.update(row.shown, !player.paused, now / 1000);
    }
    drumHits.clear();
}

// the quietest and the loudest a part's meter shows, in decibels under full scale, a channel's own output before reverb
// playing at about -40, and how fast it falls back, in decibels a second
const METER_FLOOR = -60;
const METER_TOP = -18;
const METER_FALL = 24;
let lastRendered = performance.now();

function startTicking(player) {
    if (frame !== null) return;

    const tick = () => {
        if (player.paused) {
            frame = null;
            setPlayIcon(false);
            render(player);
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

updateTransport();
setPlayIcon(false);
start();

/**
 *     The listening mode where the address asks a question (listening.js): each of its songs in its genre, made with its
 *     device or without, the stages and the genre's choices the one without leaves out, and a verdict on it told; the
 *     index of the questions where it asks none, or one not asked. Whether the address asks for the listening mode,
 *     whose questions then come as they are fetched.
 */
function startListening() {
    const asked = new URL(location.href).searchParams.get("listen");
    if (asked === null) return false;
    loadQuestions().then((questions) => askQuestion(questions, asked));
    return true;
}

/** The question asked of the questions, or their index where it is none of them. */
function askQuestion(questions, asked) {
    const question = listeningQuestion(questions, asked);
    if (question === null) {
        showListeningIndex(questions);
        return;
    }

    asksQuestion = true;
    const strip = createListeningStrip(question, {
        play: (seed, isWithout, from) => {
            const preset = question.genre === null ? null : presetById(question.genre);
            const genre = preset === null ? noGenre() : asGenre(preset);
            skipped = isWithout ? question.without.skip : [];
            const choices = isWithout ? withoutDevice(genre.choices, question.without.genre) : genre.choices;
            // the song as the question picked it, of the settings as by default but its genre, the volume as heard; read as
            // the song is asked for, before generate waits on anything
            startAt = from;
            const arrived = generate(seed, { isRolled: false, asked: { ...defaultSettings(), volume: settings.volume, genre: { name: genre.name, choices } } });
            skipped = [];
            // and played at once, from where to listen, the button pressed the gesture the player waits for
            arrived.then((isThere) => isThere && playOrPause(true));
        },
        // the version of the song heard, or, of one not heard yet, the server's, which makes it
        verdict: (seed, choice) => tell("verdict", { seed, version: songSeed === seed && songVersion !== null ? songVersion : serverVersion, detail: `${question.id}:${choice}` }),
    });
    document.querySelector("main").prepend(strip);
}

/** The index of the questions, with their verdicts as the server that listens counts them, none where it does not. */
async function showListeningIndex(questions) {
    let verdicts = null;
    try {
        const response = await fetch(new URL("api/listening/verdicts", location.href));
        if (response.ok) verdicts = await response.json();
    } catch {
        // the index as a server that does not listen shows it
    }
    document.querySelector("main").prepend(createListeningIndex(questions, verdicts, (id) => presetById(id)?.name ?? id));
}

/**
 *     The page is worth something the moment it opens: a song and a soundfont are both fetched straight
 *     away and at the same time, since neither waits on the other. Playing them is all that needs a
 *     gesture, and pressing play is one.
 */
async function start() {
    // the choices a genre may name, which its editor needs and nothing else waits for
    listGenreChoices();
    // the listening mode asks for its own songs, none rolled as the page opens
    const isListening = startListening();

    const linked = linkedSong();
    track("page_open", { detail: linked.wasAsked ? "link" : "fresh" });
    showVersion();

    // a linked song is asked for with the settings it was heard with, which become the page's; a song rolled here
    // with the settings this browser kept
    if (linked.settings !== null) {
        settings = givenOnly(await withGenre(linked.settings, linked.genreKey));
        keepCurrentSettings();
        showSettings();
    }

    // said afterwards, since the generating itself has the status line until it is done with it, and
    // only once a song has arrived: a failure has the status line to itself
    const generating = isListening ? Promise.resolve(false) : generate(linked.seed, { isRolled: linked.seed === null || linked.wasRolled });
    const complaint = linked.wasAsked && linked.seed === null
        ? "That link has no valid song, so here is a random one."
        : linked.isSettingsBad
            ? "That link's settings could not be read, so here is its song with the settings you had."
            : linked.settings !== null && linked.genreKey !== null && !hasAGenre(settings.genre)
                ? "That link's genre is not kept here, so here is its song in no genre."
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

/** The choices a genre may weigh, from the server, which only it knows; none where it cannot say. */
async function listGenreChoices() {
    try {
        const response = await fetch(new URL("api/genres/choices", location.href));
        if (!response.ok) return null;
        const choices = await response.json();
        genreChoices = choices;
        genreSelect.disabled = genreManage.disabled = false;
        return choices;
    } catch {
        return null;
    }
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
        serverVersion = version;
        elements.version.textContent = commit ? `RMG ${version} (${commit.slice(0, 7)})` : `RMG ${version}`;
        elements.version.hidden = false;
    } catch {
        // a footer without its version is a footer all the same
    }
}
