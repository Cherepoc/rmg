import { button, downloadJson, jsonFileInput } from "./dialog-parts.js";
import { byName, genreProblem, NOT_A_GENRE, readGenreFile } from "./genres.js";

/**
 *     The window of every genre, a popup over the page: the presets, each to be duplicated, and one's own, each to be
 *     edited, duplicated or removed, and the song's own where it is neither, a link's or a draft played, to be edited or
 *     kept among one's own; a genre made new, genres imported from a JSON file and one's own exported as one. What
 *     is done with a genre is the page's (app.js), the editor (genre-editor.js) opening over this window; the list shown
 *     again whenever the genres change (refresh).
 */
export function createGenreManager({ presets, own, current, onKeep, onNew, onDuplicate, onEdit, onRemove, onKeepAll, knows, choiceOf, asFile }) {
    const dialog = document.createElement("dialog");
    dialog.className = "genre-manager";
    dialog.setAttribute("aria-label", "Genres");

    const title = document.createElement("h2");
    title.textContent = "Genres";
    const header = document.createElement("header");
    header.append(title, button("×", "Close", () => dialog.close(), "genre-close"));

    const body = document.createElement("div");
    body.className = "genre-list";

    // what was done, such as genres imported, or a file that is no genre
    const told = document.createElement("p");
    told.className = "genre-told";
    told.setAttribute("role", "status");
    told.hidden = true;
    const tell = (text) => {
        told.textContent = text;
        told.hidden = false;
    };

    const file = jsonFileInput((text) => {
        const read = readGenreFile(text, knows);
        // a genre the server would refuse every song of told, as the editor's import tells it, and none kept
        const problem = read === null ? null : (read.genres ?? [read.genre]).map((x) => genreProblem(x, choiceOf)).find((x) => x !== null) ?? null;
        if (read === null) tell(NOT_A_GENRE);
        else if (problem !== null) tell(problem);
        else tell(`${onKeepAll(read.genres ?? [read.genre])} kept among yours.`);
    });
    const imported = button("Import JSON", "Keep a genre saved as a JSON file, or every one of a list of them, among yours", () => file.click(), "genre-export");
    // every genre of one's own as one file, so that they outlive this browser
    const exportedAll = button("Export all", "Save every genre of yours as one JSON file", () => downloadJson("genres.json", [...own()].sort(byName).map(asFile)));
    const made = button("New genre", "Make a genre of your own from none", () => onNew(), "primary");
    const footer = document.createElement("footer");
    footer.append(told, file, imported, exportedAll, made);
    dialog.append(header, body, footer);

    /** A genre's line: its name, its sentence, and what may be done with it. */
    function line(genre, description, actions) {
        const item = document.createElement("li");
        const text = document.createElement("div");
        text.className = "genre-line-text";
        const name = document.createElement("span");
        name.className = "genre-line-name";
        name.textContent = genre.name || "Unnamed";
        text.append(name);
        if (description) {
            const about = document.createElement("span");
            about.className = "genre-line-about";
            about.textContent = description;
            text.append(about);
        }
        const buttons = document.createElement("div");
        buttons.className = "genre-line-actions";
        buttons.append(...actions);
        item.append(text, buttons);
        return item;
    }

    function group(label, items, empty) {
        const heading = document.createElement("h3");
        heading.textContent = label;
        const list = document.createElement("ul");
        list.setAttribute("aria-label", label);
        list.append(...items);
        if (items.length === 0) {
            const none = document.createElement("p");
            none.className = "genre-line-about";
            none.textContent = empty;
            return [heading, none];
        }
        return [heading, list];
    }

    function refresh() {
        const yours = [...own()].sort(byName).map((genre) => line(genre, genre.description ?? "", [
            button("Edit", `Change ${genre.name}`, () => onEdit(genre)),
            button("Duplicate", `Open a copy of ${genre.name} in the editor, yours once you save it`, () => onDuplicate(genre)),
            button("Remove", `Let go of ${genre.name}`, () => {
                if (confirm(`Remove your genre ${genre.name}? Nothing brings it back.`)) onRemove(genre.name);
            }),
        ]));
        const shipped = [...presets()].sort(byName).map((preset) => line(preset, preset.description, [
            button("Duplicate", `Open a copy of ${preset.name} in the editor, yours once you save it`, () => onDuplicate(preset)),
        ]));
        // the song's genre where it is no preset and none of one's own, a link's or a draft played
        const song = current();
        const songs = song === null ? [] : group("This song's", [line(song, "", [
            button("Edit", `Change ${song.name || "this song's genre"}`, () => onEdit(song)),
            button("Keep", `Keep ${song.name || "this song's genre"} among yours`, () => onKeep(song)),
        ])], "");
        body.replaceChildren(...songs, ...group("Yours", yours, "None yet: make one new, or duplicate any."), ...group("Presets", shipped, ""));
        exportedAll.disabled = yours.length === 0;
    }

    return {
        element: dialog,
        open() {
            told.hidden = true;
            refresh();
            dialog.showModal();
        },
        close() {
            dialog.close();
        },
        /** The list as the genres now are, wherever it is open. */
        refresh() {
            if (dialog.open) refresh();
        },
    };
}
