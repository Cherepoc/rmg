/**
 *     The admin area's index: a song described from its link, as the songs make it now, its text ready to copy for an
 *     agent to name the song by.
 */

const elements = {
    link: document.getElementById("song-link"),
    describe: document.getElementById("describe"),
    copy: document.getElementById("copy"),
    status: document.getElementById("describe-status"),
    description: document.getElementById("description"),
};

function setStatus(message, isError = false) {
    elements.status.textContent = message;
    elements.status.classList.toggle("error", isError);
}

/** The song a link asks for, its seed and its settings, or a seed given by itself. */
function songOf(text) {
    const trimmed = text.trim();
    try {
        const parameters = new URL(trimmed).searchParams;
        return { song: parameters.get("song"), settings: parameters.get("settings") };
    } catch {
        return { song: trimmed, settings: null };
    }
}

async function describe() {
    const { song, settings } = songOf(elements.link.value);
    if (!song) {
        setStatus("That link has no song in it.", true);
        return;
    }

    const query = new URLSearchParams({ song });
    if (settings) query.set("settings", settings);
    elements.describe.disabled = true;
    setStatus("Making the song…");
    try {
        const response = await fetch(new URL(`api/songs/describe?${query}`, location.href));
        if (!response.ok) {
            const body = await response.json().catch(() => null);
            setStatus(body?.error ?? body?.title ?? `The server answered ${response.status}.`, true);
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
elements.link.addEventListener("keydown", (event) => {
    if (event.key === "Enter") describe();
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
