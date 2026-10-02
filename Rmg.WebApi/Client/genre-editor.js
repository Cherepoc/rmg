import { MAX_GENRE_NAME_BYTES } from "./settings.js";

/**
 *     The genre editor: a draft genre, a name and what it says of the choices the songs draw (/api/genres/choices), shown
 *     choice by choice in the server's groups, each option's step from 0 to 63 on a slider. A choice added starts from the
 *     songs' own weights, its options a pool's above 0, every chance and value as it is; a pool keeps the options above 0.
 *     The draft is played or saved as the page says (app.js), and read from or written as JSON.
 */
export function createGenreEditor({ onPlay, onSave, onDelete, isOwn }) {
    let choices = [];
    let draft = { name: "", choices: {} };

    const root = document.createElement("div");
    root.className = "genre-editor";
    root.hidden = true;

    const name = document.createElement("input");
    name.type = "text";
    name.placeholder = "Name";
    name.setAttribute("aria-label", "Genre name");
    name.addEventListener("input", () => {
        draft.name = name.value;
        showProblem();
    });

    const groups = document.createElement("div");
    groups.className = "genre-groups";

    const problem = document.createElement("p");
    problem.className = "warning";
    problem.setAttribute("role", "alert");
    problem.hidden = true;

    const play = button("Play", "Ask for the song again in this genre", () => onPlay(finished()));
    const save = button("Save", "Keep this genre among yours, and play it", () => onSave(finished()));
    const remove = button("Delete", "Let go of this genre of yours", () => onDelete(draft.name));
    const close = button("Close", "Close the editor", () => (root.hidden = true));

    const json = document.createElement("textarea");
    json.className = "genre-json";
    json.rows = 4;
    json.setAttribute("aria-label", "Genre as JSON");
    const read = button("Read JSON", "Take the genre written above", () => {
        try {
            const parsed = JSON.parse(json.value);
            if (typeof parsed?.name !== "string" || typeof parsed?.choices !== "object" || parsed.choices === null) throw new Error();
            draft = { name: parsed.name, choices: structuredClone(parsed.choices) };
            show();
        } catch {
            showProblem("That is not a genre: a name and its choices.");
        }
    });
    const jsonDetails = document.createElement("details");
    const jsonSummary = document.createElement("summary");
    jsonSummary.textContent = "As JSON";
    jsonDetails.append(jsonSummary, json, read);

    const actions = document.createElement("div");
    actions.className = "genre-actions";
    actions.append(play, save, remove, close);
    root.append(name, groups, problem, actions, jsonDetails);

    function button(text, title, action) {
        const element = document.createElement("button");
        element.type = "button";
        element.textContent = text;
        element.title = title;
        element.addEventListener("click", action);
        return element;
    }

    /** The draft as it is played or kept: a pool's options above 0, its choices that keep something; none if it is not a genre. */
    function finished() {
        const result = { name: draft.name.trim(), choices: {} };
        for (const [choice, options] of Object.entries(draft.choices)) {
            const kind = choices.find((x) => x.name === choice)?.kind;
            const kept = Object.fromEntries(Object.entries(options).filter(([, step]) => kind !== "pool" || step > 0));
            if (Object.keys(kept).length > 0) result.choices[choice] = kept;
        }
        return problemOf(result) === null ? result : null;
    }

    function problemOf(genre) {
        if (new TextEncoder().encode(genre.name).length > MAX_GENRE_NAME_BYTES) return "The name is too long.";
        for (const [choice, options] of Object.entries(draft.choices)) {
            const kind = choices.find((x) => x.name === choice)?.kind;
            if (kind === "pool" && Object.values(options).every((step) => step === 0)) return `${labelOf(choice)} keeps none of its options.`;
        }
        return null;
    }

    function showProblem(text = problemOf(draft)) {
        problem.textContent = text ?? "";
        problem.hidden = text === null;
        play.disabled = save.disabled = text !== null;
        save.disabled ||= draft.name.trim() === "";
        remove.disabled = !isOwn(draft.name.trim());
        json.value = JSON.stringify(draft, null, 1);
    }

    function labelOf(choice) {
        const found = choices.find((x) => x.name === choice);
        return found ? `${found.group}: ${found.label}` : choice;
    }

    /** A choice the draft names: its options' sliders, an option to add, and itself to let go of. */
    function choiceRow(choice) {
        const row = document.createElement("div");
        row.className = "genre-choice";
        row.dataset.choice = choice.name;

        const title = document.createElement("div");
        title.className = "genre-choice-title";
        const label = document.createElement("span");
        label.textContent = choice.label;
        title.append(label, button("Remove", `Leave ${choice.label} as the songs have it`, () => {
            delete draft.choices[choice.name];
            show();
        }));
        row.append(title);

        const options = draft.choices[choice.name];
        for (const option of choice.options.filter((x) => x.name in options)) {
            const line = document.createElement("label");
            line.className = "genre-option";
            const text = document.createElement("span");
            text.textContent = option.label;
            const range = document.createElement("input");
            range.type = "range";
            range.min = "0";
            range.max = "63";
            range.step = "1";
            range.value = String(options[option.name]);
            range.dataset.option = option.name;
            range.setAttribute("aria-label", `${choice.label}: ${option.label}`);
            const output = document.createElement("output");
            output.textContent = range.value;
            range.addEventListener("input", () => {
                options[option.name] = Number(range.value);
                output.textContent = range.value;
                showProblem();
            });
            const drop = button("×", `Leave out ${option.label}`, () => {
                delete options[option.name];
                show();
            });
            line.append(text, range, output, drop);
            row.append(line);
        }

        const missing = choice.options.filter((x) => !(x.name in options));
        if (missing.length > 0) {
            const add = document.createElement("select");
            add.setAttribute("aria-label", `Add to ${choice.label}`);
            add.append(new Option(`Add to ${choice.label.toLowerCase()}…`, ""), ...missing.map((x) => new Option(x.label, x.name)));
            add.addEventListener("change", () => {
                const option = choice.options.find((x) => x.name === add.value);
                if (option === undefined) return;
                options[option.name] = choice.kind === "pool" ? Math.max(1, option.default) : option.default;
                show();
            });
            row.append(add);
        }
        return row;
    }

    /** The draft's choices in the server's groups, with the choices it does not name yet to add. */
    function show() {
        name.value = draft.name;
        const byGroup = new Map();
        for (const choice of choices) {
            if (!byGroup.has(choice.group)) byGroup.set(choice.group, []);
            byGroup.get(choice.group).push(choice);
        }
        groups.replaceChildren(...[...byGroup].map(([group, members]) => {
            const fieldset = document.createElement("fieldset");
            fieldset.className = "settings-group";
            const legend = document.createElement("legend");
            legend.textContent = group;
            fieldset.append(legend, ...members.filter((x) => x.name in draft.choices).map(choiceRow));
            const unnamed = members.filter((x) => !(x.name in draft.choices));
            if (unnamed.length > 0) {
                const add = document.createElement("select");
                add.setAttribute("aria-label", `Add a choice of ${group.toLowerCase()}`);
                add.append(new Option("Add a choice…", ""), ...unnamed.map((x) => new Option(x.label, x.name)));
                add.addEventListener("change", () => {
                    const choice = choices.find((x) => x.name === add.value);
                    if (choice === undefined) return;
                    // as the songs have it: a pool's options above 0 at their weights, every chance and value as it is
                    draft.choices[choice.name] = Object.fromEntries(choice.options.filter((x) => choice.kind !== "pool" || x.default > 0).map((x) => [x.name, x.default]));
                    show();
                });
                fieldset.append(add);
            }
            return fieldset;
        }));
        showProblem();
    }

    return {
        element: root,
        /** Opens the editor on a genre, a copy of it the draft. */
        open(genre, choiceList) {
            choices = choiceList;
            draft = { name: genre.name, choices: structuredClone(genre.choices) };
            root.hidden = false;
            show();
        },
        close() {
            root.hidden = true;
        },
    };
}
