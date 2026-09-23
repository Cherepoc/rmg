import {
    DRUM_CHANNEL,
    DRUM_KITS,
    INSTRUMENT_FAMILIES,
    LAST_INSTRUMENT_BEFORE_EFFECTS,
} from "./instruments.js";
import { encodeMp3, renderSong } from "./export.js";
import { getPlayer } from "./player.js";
import { forget, isKeeping, keep, recall, setKeeping } from "./storage.js";

const PLAY_ICON = "M8 5v14l11-7z";
const PAUSE_ICON = "M7 5h3.5v14H7zM13.5 5H17v14h-3.5z";

const elements = {
    soundFont: document.getElementById("soundfont"),
    soundFontName: document.getElementById("soundfont-name"),
    libraryRow: document.getElementById("library-row"),
    library: document.getElementById("library"),
    keep: document.getElementById("keep"),
    seed: document.getElementById("seed"),
    generate: document.getElementById("generate"),
    showSeeded: document.getElementById("show-seeded"),
    seeded: document.getElementById("seeded"),
    seedInput: document.getElementById("seed-input"),
    generateSeeded: document.getElementById("generate-seeded"),
    download: document.getElementById("download"),
    exportMp3: document.getElementById("export-mp3"),
    bitrate: document.getElementById("bitrate"),
    play: document.getElementById("play"),
    stop: document.getElementById("stop"),
    seek: document.getElementById("seek"),
    elapsed: document.getElementById("elapsed"),
    total: document.getElementById("total"),
    volume: document.getElementById("volume"),
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
let songVolume = 1;

let hasSoundFont = false;
let source = null;
let loading = Promise.resolve();
let hasSong = false;
let isSeeking = false;
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
    player.onSongChange(() => buildMixer(player));
    player.onSongEnded(() => {
        setPlayIcon(false);
        render(player);
    });
    player.masterGain = Number(elements.volume.value);
}

// --- soundfont -------------------------------------------------------------

elements.keep.checked = isKeeping();

elements.soundFont.addEventListener("change", async () => {
    const file = elements.soundFont.files?.[0];
    if (!file) return;

    elements.library.value = "";
    await useSoundFont(file.name, () => file.arrayBuffer());
});

elements.library.addEventListener("change", async () => {
    const option = elements.library.selectedOptions[0];
    if (!option?.value) return;

    elements.soundFont.value = "";
    await useSoundFont(option.dataset.name, () => download(option.value, option.dataset.name));
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
function useSoundFont(name, read, isStored = false) {
    loading = loading.then(() => loadSoundFont(name, read, isStored));
    return loading;
}

/**
 *     Takes the soundfont <paramref name="read" /> returns, rather than its bytes, because the
 *     worklet may take the buffer with it: holding a second copy of a few hundred megabytes just
 *     to serve a later opt-in is not worth it, so a re-read is asked for instead.
 */
async function loadSoundFont(name, read, isStored = false) {
    elements.soundFontName.textContent = name;
    setStatus(`Loading ${name}…`);

    let soundFont;
    try {
        soundFont = await read();
    } catch (error) {
        clearSoundFont();
        setStatus(`Could not read ${name}: ${error.message}`, true);
        return;
    }

    // stored before the player is handed it, since loading detaches the buffer
    const problem = isKeeping() && !isStored ? await store(name, soundFont) : null;

    const loaded = await withPlayer(async (player) => {
        await player.loadSoundFont(soundFont);
        return true;
    }, "Could not load the soundfont");

    hasSoundFont = loaded === true;
    updateTransport();

    if (!hasSoundFont) {
        // whatever would not load is not worth keeping, and would only fail again on the next visit
        await forget().catch(() => {});
        clearSoundFont();
        return;
    }

    source = { name, read };
    setStatus(problem === null ? `${name} loaded.` : `${name} loaded, but not kept: ${problem}.`);
}

async function download(file, name) {
    setStatus(`Downloading ${name}…`);

    const response = await fetch(new URL(`soundfonts/${encodeURIComponent(file)}`, location.href));
    if (!response.ok) throw new Error(await describeFailure(response));

    return await response.arrayBuffer();
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
    elements.soundFontName.textContent = "No soundfont loaded";
    elements.soundFont.value = "";
    elements.library.value = "";
    source = null;
}

async function listLibrary() {
    let soundFonts;
    try {
        const response = await fetch(new URL("api/soundfonts", location.href));
        if (!response.ok) return;

        soundFonts = await response.json();
    } catch {
        return; // a server with nothing to offer just leaves the page to a file of your own
    }

    if (soundFonts.length === 0) return;

    const choose = document.createElement("option");
    choose.value = "";
    choose.textContent = "Choose…";

    elements.library.replaceChildren(choose, ...soundFonts.map(describeSoundFont));
    elements.libraryRow.hidden = false;
}

function describeSoundFont(soundFont) {
    const option = document.createElement("option");
    option.value = soundFont.file;
    option.dataset.name = soundFont.name;
    option.textContent = `${soundFont.name} (${formatSize(soundFont.size)})`;
    return option;
}

function formatSize(bytes) {
    const megabytes = bytes / 1024 ** 2;
    return megabytes >= 1024 ? `${(megabytes / 1024).toFixed(1)} GB` : `${Math.max(1, Math.round(megabytes))} MB`;
}

async function restoreSoundFont() {
    if (!isKeeping()) return;

    let saved;
    try {
        saved = await recall();
    } catch {
        return; // a browser that will not open the database simply has nothing kept
    }

    if (!saved) return;

    elements.soundFontName.textContent = saved.name;
    setStatus(`${saved.name} is kept in this browser, and loads as soon as you touch the page.`);

    // the audio stack only starts once the page has been interacted with, and a synthesizer built
    // before that never reports itself ready, so the kept soundfont waits for the first gesture
    await firstGesture();
    if (source !== null) return; // something else got loaded in the meantime

    // read back out of the store rather than held, for the same reason useSoundFont takes a reader
    await useSoundFont(saved.name, reread, true);
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
elements.generate.addEventListener("click", () => generate(null));

elements.generateSeeded.addEventListener("click", () => {
    const seed = readSeed(elements.seedInput.value.trim());
    if (seed === null) {
        setStatus(`'${elements.seedInput.value.trim()}' is not a valid seed. A seed is a 32-bit integer.`, true);
        return;
    }

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

async function generate(seed) {
    elements.generate.disabled = true;
    elements.generateSeeded.disabled = true;
    cancelDownloadRefresh();
    setStatus("Generating…");

    try {
        const response = await requestSong({ seed });
        if (!response.ok) {
            setStatus(`Could not generate a song: ${await describeFailure(response)}`, true);
            return;
        }

        const songSeed = response.headers.get("X-Song-Seed") ?? String(seed);
        readSong(response.headers.get("X-Song-Instruments"));
        const song = await response.arrayBuffer();

        elements.seed.value = songSeed;
        offerDownload(song, songSeed);
        setStatus(`Generated song ${songSeed}.`);

        // offered for download first, so it stays usable even if the audio stack cannot start
        const loaded = await withPlayer((player) => {
            player.loadSong(song);
            return true;
        }, "Generated, but the player could not load it");

        hasSong = loaded === true;
        updateTransport();
    } catch (error) {
        setStatus(`Could not generate a song: ${error.message}`, true);
    } finally {
        elements.generate.disabled = false;
        elements.generateSeeded.disabled = false;
    }
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

elements.play.addEventListener("click", async () => {
    await withPlayer(async (player) => {
        if (player.paused) {
            await player.play();
            startTicking(player);
        } else {
            stopTicking();
            player.pause();
        }

        setPlayIcon(!player.paused);
    }, "Playback failed");
});

elements.stop.addEventListener("click", async () => {
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

function buildMixer(player) {
    rows.clear();
    muted.clear();
    soloed.clear();
    elements.mixer.replaceChildren();

    const channels = player.songChannels;
    for (const channel of channels) {
        const number = document.createElement("td");
        number.textContent = String(channel + 1);

        const onCell = document.createElement("td");
        onCell.append(createSwitch(player, channels, channel));

        const instrumentCell = document.createElement("td");
        const instrument = createInstrumentPicker(player, channel);
        const roll = createRoll(player, channel);
        const instrumentField = document.createElement("div");
        instrumentField.className = "instrument-field";
        instrumentField.append(instrument, roll);
        instrumentCell.append(instrumentField);

        const volumeCell = document.createElement("td");
        volumeCell.append(createVolume(player, channel));

        const muteCell = document.createElement("td");
        const mute = createToggle("Mute", () => {
            toggle(muted, channel, mute);
            applyMuting(player, channels);
        });
        muteCell.append(mute);

        const soloCell = document.createElement("td");
        const solo = createToggle("Solo", () => {
            toggle(soloed, channel, solo);
            applyMuting(player, channels);
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
    applyMuting(player, channels);
}

/**
 *     The instruments of a channel: the General MIDI ones, or the drum kits, which is what an instrument
 *     means on the percussion channel.
 */
function createInstrumentPicker(player, channel) {
    const select = document.createElement("select");
    select.setAttribute("aria-label", `Instrument of channel ${channel + 1}`);

    if (channel === DRUM_CHANNEL)
        select.append(...DRUM_KITS.map((kit) => createInstrumentOption(kit.instrument, kit.name)));
    else
        fillInstruments(select);

    select.value = String(instruments.get(channel) ?? 0);
    select.addEventListener("change", () => setInstrument(player, channel, Number(select.value)));

    return select;
}

/** Takes one instrument at random, which is worth nothing if it is the one already playing. */
function createRoll(player, channel) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "roll";
    button.textContent = "Roll";
    button.setAttribute("aria-label", `Roll the instrument of channel ${channel + 1}`);
    button.addEventListener("click", () => rollInstrument(player, channel));
    return button;
}

function setInstrument(player, channel, instrument) {
    instruments.set(channel, instrument);
    rows.get(channel).instrument.value = String(instrument);

    player.setChannelInstrument(channel, instrument);
    scheduleDownloadRefresh();
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
function rollInstrument(player, channel) {
    const choices = rollable(channel).filter((instrument) => instrument !== instruments.get(channel));
    if (choices.length === 0) return;

    setInstrument(player, channel, choices[Math.floor(Math.random() * choices.length)]);
}

elements.rollInstruments.addEventListener("click", async () => {
    await withPlayer((player) => {
        for (const channel of instruments.keys()) rollInstrument(player, channel);
    }, "Could not roll the instruments");
});

/**
 *     Switches a track off, which leaves it out of the file and silences it here, so what is heard is what
 *     would be downloaded.
 */
function createSwitch(player, channels, channel) {
    const toggle = document.createElement("input");
    toggle.type = "checkbox";
    toggle.checked = !switchedOff.has(channel);
    toggle.setAttribute("aria-label", `Channel ${channel + 1} on`);

    toggle.addEventListener("change", () => {
        if (toggle.checked) switchedOff.delete(channel);
        else switchedOff.add(channel);

        applyMuting(player, channels);
        scheduleDownloadRefresh();
    });

    return toggle;
}

/** The volume of a track, which the song carries, rather than the volume this page is played at. */
function createVolume(player, channel) {
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

        player.setChannelVolume(channel, songVolume * volume);
        scheduleDownloadRefresh();
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

function applyMuting(player, channels) {
    // an explicit mute still wins over a solo, so soloing never un-mutes something silenced on purpose
    for (const channel of channels)
        player.setChannelMuted(
            channel,
            switchedOff.has(channel) || muted.has(channel) || (soloed.size > 0 && !soloed.has(channel))
        );
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

// --- wiring ----------------------------------------------------------------

fillInstruments(elements.rollFirst);
fillInstruments(elements.rollLast);
elements.rollFirst.value = "0";
elements.rollLast.value = String(LAST_INSTRUMENT_BEFORE_EFFECTS);

updateTransport();
setPlayIcon(false);
setStatus("Choose a soundfont, then generate a song.");
listLibrary();
restoreSoundFont();
