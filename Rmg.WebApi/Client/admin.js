/**
 *     The admin area's index: a song described from its link, as the songs make it now, its text ready to copy for an
 *     agent to name the song by.
 */

import { showStatus } from "./text.js";
import { parseSettings, readLinkedSeed } from "./settings.js";

const elements = {
    link: document.getElementById("song-link"),
    describe: document.getElementById("describe"),
    copy: document.getElementById("copy"),
    status: document.getElementById("describe-status"),
    description: document.getElementById("description"),
};

function setStatus(message, isError = false) {
    showStatus(elements.status, message, isError);
}

/**
 *     The song a link asks for, its seed, its settings and its genre, or a seed given by itself, as the page reads them: an
 *     old link's seed in its old digits, and its settings, which the page no longer reads, left out.
 */
function songOf(text) {
    const trimmed = text.trim();
    try {
        const parameters = new URL(trimmed).searchParams;
        const [song, settings] = [parameters.get("song"), parameters.get("settings")];
        return { song: song === null ? null : readLinkedSeed(song, settings) ?? song, settings: parseSettings(settings) === null ? null : settings, genre: parameters.get("genre") };
    } catch {
        return { song: trimmed, settings: null, genre: null };
    }
}

async function describe() {
    const { song, settings, genre } = songOf(elements.link.value);
    if (!song) {
        setStatus("That link has no song in it.", true);
        return;
    }

    const query = new URLSearchParams({ song });
    if (settings) query.set("settings", settings);
    if (genre) query.set("genre", genre);
    elements.describe.disabled = true;
    // the song described before taken away, so that it is never copied for this one
    elements.description.hidden = true;
    elements.copy.disabled = true;
    setStatus("Making the song…");
    try {
        const response = await fetch(new URL(`api/songs/describe?${query}`, location.href));
        if (!response.ok) {
            const body = await response.json().catch(() => null);
            setStatus(body?.detail ?? body?.error ?? body?.title ?? `The server answered ${response.status}.`, true);
            return;
        }

        const described = await response.json();
        elements.description.value = described.text;
        elements.description.hidden = false;
        elements.copy.disabled = false;
        setStatus(`Song ${described.seed}, as songs ${described.songsVersion} make it.`);
    } catch (error) {
        setStatus(`Could not reach the server: ${error.message}`, true);
    } finally {
        elements.describe.disabled = false;
    }
}

elements.describe.addEventListener("click", describe);
// Enter as the button, and as it, not while a song is being described
elements.link.addEventListener("keydown", (event) => {
    if (event.key === "Enter" && !elements.describe.disabled) describe();
});

elements.copy.addEventListener("click", async () => {
    try {
        await navigator.clipboard.writeText(elements.description.value);
        setStatus("Copied.");
    } catch {
        elements.description.select();
        setStatus("Select the text and copy it.");
    }
});
