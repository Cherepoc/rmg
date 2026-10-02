import { MAX_GENRE_NAME_BYTES } from "./settings.js";

/**
 *     The genre editor, a popup over the page: a draft genre, a name and what it says of the choices the songs draw
 *     (/api/genres/choices). A choice is named or left as the songs have it by a checkbox. A pool is rows, each a list of
 *     its options and a slider of its weight, swapped in its list, removed, or added as the next unused option; a chance
 *     and a value are sliders between their ends; the tempo is a range of beats a minute. Choices of the same fields for
 *     every part are tabs over one set of fields: the instruments, the drums' roles, and every part's rhythm with its
 *     feel. The draft is played or saved as the page says (app.js), and read from or written as JSON.
 */

/** A pool's weight where an option is added with none of its own, and where a pool named starts from: half the heaviest's or more. */
const ADDED_WEIGHT = 32;

export function createGenreEditor({ onPlay, onSave, onDelete, isOwn, tempoOf }) {
    let choices = new Map();
    let draft = { name: "", choices: {} };

    const dialog = document.createElement("dialog");
    dialog.className = "genre-dialog";
    dialog.setAttribute("aria-label", "Genre editor");

    // --- the header: the name, and closing ----------------------------------------------------------------------

    const name = document.createElement("input");
    name.type = "text";
    name.placeholder = "Name";
    name.setAttribute("aria-label", "Genre name");
    name.addEventListener("input", () => {
        draft.name = name.value;
        showProblem();
    });
    const header = document.createElement("header");
    header.append(name, button("×", "Close the editor", () => dialog.close(), "genre-close"));

    // --- the body: every choice -----------------------------------------------------------------------------------

    const body = document.createElement("div");
    body.className = "genre-body";
    const fields = [];

    function section(title, ...content) {
        const element = document.createElement("section");
        element.className = "genre-section";
        const heading = document.createElement("h3");
        heading.textContent = title;
        element.append(heading, ...content);
        return element;
    }

    /** A field bound to a choice, shown again whenever the draft changes. */
    function field(create, choiceName) {
        const made = create();
        fields.push(() => made.bind(choices.get(choiceName)));
        return made.element;
    }

    /** Tabs of choices of the same fields, one set of them, bound to the tab's choices. */
    function tabs(label, parts) {
        const element = document.createElement("div");
        element.className = "genre-tabs";
        const strip = document.createElement("div");
        strip.className = "tab-strip";
        strip.setAttribute("role", "tablist");
        strip.setAttribute("aria-label", label);
        const madeFields = parts[0].choices.map(({ create }) => create());
        let current = 0;
        const buttons = parts.map((part, i) => {
            const tab = document.createElement("button");
            tab.type = "button";
            tab.setAttribute("role", "tab");
            tab.textContent = part.label;
            tab.addEventListener("click", () => {
                current = i;
                show();
            });
            return tab;
        });
        strip.append(...buttons);
        const panel = document.createElement("div");
        panel.className = "tab-panel";
        panel.setAttribute("role", "tabpanel");
        panel.append(...madeFields.map((x) => x.element));
        element.append(strip, panel);

        function show() {
            parts[current].choices.forEach(({ name: choiceName }, i) => {
                const choice = choices.get(choiceName);
                madeFields[i].element.hidden = choice === undefined;
                if (choice !== undefined) madeFields[i].bind(choice);
            });
            buttons.forEach((tab, i) => {
                tab.setAttribute("aria-selected", String(i === current));
                tab.classList.toggle("named", parts[i].choices.some((x) => x.name in draft.choices));
            });
        }

        fields.push(show);
        return element;
    }

    /** A pool: whether it is named, and its rows, each an option's list, its weight and its removal, and one to add. */
    function poolField(nameLabel = "Keep to these") {
        const element = document.createElement("div");
        element.className = "genre-pool";
        const named = checkbox(nameLabel);
        const rows = document.createElement("div");
        rows.className = "genre-rows";
        const add = button("Add", "Add the next option", () => {
            const options = draft.choices[choice.name];
            const unused = choice.options.find((x) => !(x.name in options));
            if (unused === undefined) return;
            options[unused.name] = unused.default > 0 ? unused.default : ADDED_WEIGHT;
            changed();
        }, "genre-add");
        element.append(named.element, rows, add);
        let choice = null;

        named.input.addEventListener("change", () => {
            if (named.input.checked)
                draft.choices[choice.name] = Object.fromEntries(choice.options.filter((x) => x.default >= ADDED_WEIGHT).map((x) => [x.name, x.default]));
            else delete draft.choices[choice.name];
            changed();
        });

        return {
            element,
            bind(bound) {
                choice = bound;
                const options = draft.choices[choice.name];
                named.input.checked = options !== undefined;
                rows.hidden = add.hidden = options === undefined;
                if (options === undefined) {
                    rows.replaceChildren();
                    return;
                }
                const kept = choice.options.filter((x) => x.name in options);
                rows.replaceChildren(...kept.map((option) => {
                    const row = document.createElement("div");
                    row.className = "genre-row";
                    const select = document.createElement("select");
                    select.setAttribute("aria-label", `${choice.label}: option`);
                    select.append(...choice.options.filter((x) => x === option || !(x.name in options)).map((x) => new Option(x.label, x.name)));
                    select.value = option.name;
                    select.addEventListener("change", () => {
                        const weight = options[option.name];
                        delete options[option.name];
                        options[select.value] = weight;
                        changed();
                    });
                    const weight = slider(`${choice.label}: ${option.label}`, 1, options[option.name], "rarer", "commoner", (value) => {
                        options[option.name] = value;
                        showProblem();
                    });
                    row.dataset.option = option.name;
                    row.append(select, weight, button("×", `Leave out ${option.label}`, () => {
                        delete options[option.name];
                        changed();
                    }, "genre-remove"));
                    return row;
                }));
                add.disabled = kept.length === choice.options.length;
            },
        };
    }

    /** Chances or values: each a checkbox, named or as it is, and a slider between its ends, its middle as it is. */
    function slidersField() {
        const element = document.createElement("div");
        element.className = "genre-sliders";
        return {
            element,
            bind(choice) {
                const [low, high] = choice.kind === "chances" ? ["never", "always"] : ["less", "more"];
                element.replaceChildren(...choice.options.map((option) => {
                    const row = document.createElement("div");
                    row.className = "genre-row";
                    const value = draft.choices[choice.name]?.[option.name];
                    const named = checkbox(option.label);
                    named.input.checked = value !== undefined;
                    named.input.addEventListener("change", () => {
                        const options = (draft.choices[choice.name] ??= {});
                        if (named.input.checked) options[option.name] = option.default;
                        else delete options[option.name];
                        if (Object.keys(options).length === 0) delete draft.choices[choice.name];
                        changed();
                    });
                    const range = slider(`${choice.label}: ${option.label}`, 0, value ?? option.default, low, high, (step) => {
                        draft.choices[choice.name][option.name] = step;
                        showProblem();
                    });
                    range.querySelector("input").disabled = value === undefined;
                    row.dataset.option = option.name;
                    row.append(named.element, range);
                    return row;
                }));
            },
        };
    }

    /** The tempo: named or the songs', and its range, from and to, shown in beats a minute. */
    function rangeField() {
        const element = document.createElement("div");
        element.className = "genre-range";
        const named = checkbox("Keep to a range");
        const shown = document.createElement("output");
        let choice = null;
        // the ends kept in order, the other moving with the one moved past it, the sliders kept as they are dragged
        const [from, to] = ["from", "to"].map((end) => slider(`Tempo: ${end}`, 0, 0, "slower", "faster", (step) => {
            const range = draft.choices[choice.name];
            range[end] = step;
            if (range.from > range.to) range[end === "from" ? "to" : "from"] = step;
            showRange();
            showProblem();
        }));
        const ends = document.createElement("div");
        ends.className = "genre-sliders";
        ends.append(from, to);
        element.append(named.element, ends, shown);
        named.input.addEventListener("change", () => {
            if (named.input.checked) draft.choices[choice.name] = Object.fromEntries(choice.options.map((x) => [x.name, x.default]));
            else delete draft.choices[choice.name];
            changed();
        });

        function showRange() {
            const range = draft.choices[choice.name] ?? { from: choice.options[0].default, to: choice.options[1].default };
            from.querySelector("input").value = String(range.from);
            to.querySelector("input").value = String(range.to);
            shown.textContent = `${tempoOf(range.from)}–${tempoOf(range.to)} bpm`;
        }

        return {
            element,
            bind(bound) {
                choice = bound;
                named.input.checked = choice.name in draft.choices;
                ends.hidden = !named.input.checked;
                showRange();
            },
        };
    }

    // the choices in the order the editor shows them, a family of every part's as tabs
    const instruments = [["melody", "Melody"], ["chords", "Chords"], ["bass", "Bass"], ["pad", "Pad"], ["counterMelody", "Counter-melody"], ["riff", "Riff"], ["rhythm", "Rhythm"], ["solo", "Soloist"]];
    const drumRoles = [["ground", "Ground (kick)"], ["backbeat", "Backbeat (snare)"], ["time", "Time (hi-hat)"], ["colour", "Colour (toms)"]];
    const rhythms = [...instruments.filter(([x]) => x !== "solo"), ...drumRoles];
    body.append(
        section("Tempo", field(rangeField, "tempo")),
        section("Meter", field(poolField, "meter")),
        section("Feel", field(poolField, "feel")),
        section("Swing and half time", field(slidersField, "feelChances"), field(slidersField, "feelValues")),
        section("Scale", field(poolField, "scale")),
        section("Chord shapes", field(poolField, "chordShape")),
        section("Instruments", tabs("Instruments", instruments.map(([part, label]) => ({ label, choices: [{ name: `instrument.${part}`, create: poolField }] })))),
        section("Drum kit", field(poolField, "kit")),
        section("Drum setup", field(poolField, "drumSetup")),
        section("Drum pieces", field(poolField, "drumPieces")),
        section("Parts playing", field(slidersField, "parts")),
        section("Drums' roles", tabs("Drums' roles", drumRoles.map(([role, label]) => ({ label, choices: [{ name: `drumRole.${role}`, create: () => poolField("Keep to these drums") }] })))),
        section("Rhythm", tabs("Rhythm", rhythms.map(([part, label]) => ({
            label,
            choices: [{ name: `rhythm.${part}`, create: slidersField }, { name: `feel.${part}`, create: () => poolField("Keep to these feels") }],
        })))),
    );

    // --- the footer: the problem, as JSON, and what to do with the draft ----------------------------------------------

    const problem = document.createElement("p");
    problem.className = "warning";
    problem.setAttribute("role", "alert");
    problem.hidden = true;

    const json = document.createElement("textarea");
    json.className = "genre-json";
    json.rows = 4;
    json.setAttribute("aria-label", "Genre as JSON");
    const jsonDetails = document.createElement("details");
    const jsonSummary = document.createElement("summary");
    jsonSummary.textContent = "As JSON";
    jsonDetails.append(jsonSummary, json, button("Read JSON", "Take the genre written above", () => {
        try {
            const parsed = JSON.parse(json.value);
            if (typeof parsed?.name !== "string" || typeof parsed?.choices !== "object" || parsed.choices === null) throw new Error();
            draft = { name: parsed.name, choices: structuredClone(parsed.choices) };
            changed();
        } catch {
            showProblem("That is not a genre: a name and its choices.");
        }
    }));
    body.append(jsonDetails);

    const play = button("Play", "Ask for the song again in this genre", () => onPlay(finished()), "primary");
    const save = button("Save", "Keep this genre among yours, and play it", () => onSave(finished()));
    const remove = button("Delete", "Let go of this genre of yours", () => onDelete(draft.name.trim()));
    const footer = document.createElement("footer");
    footer.append(problem, remove, save, play);
    dialog.append(header, body, footer);

    // --- the parts ----------------------------------------------------------------------------------------------------

    function button(text, title, action, className = "") {
        const element = document.createElement("button");
        element.type = "button";
        element.textContent = text;
        element.title = title;
        if (className) element.className = className;
        element.addEventListener("click", action);
        return element;
    }

    function checkbox(text) {
        const element = document.createElement("label");
        element.className = "genre-check";
        const input = document.createElement("input");
        input.type = "checkbox";
        const span = document.createElement("span");
        span.textContent = text;
        element.append(input, span);
        return { element, input };
    }

    /** A slider of a step from its least to 63, its ends in words and no number. */
    function slider(label, least, value, low, high, onInput) {
        const element = document.createElement("span");
        element.className = "genre-slider";
        const range = document.createElement("input");
        range.type = "range";
        range.min = String(least);
        range.max = "63";
        range.step = "1";
        range.value = String(value);
        range.setAttribute("aria-label", label);
        range.addEventListener("input", () => onInput(Number(range.value)));
        const [lowEnd, highEnd] = [low, high].map((text) => {
            const end = document.createElement("span");
            end.className = "end";
            end.setAttribute("aria-hidden", "true");
            end.textContent = text;
            return end;
        });
        element.append(lowEnd, range, highEnd);
        return element;
    }

    // --- the draft ------------------------------------------------------------------------------------------------------

    function changed() {
        name.value = draft.name;
        for (const show of fields) show();
        showProblem();
    }

    /** The draft as it is played or kept, the choices that keep something; none if it is not a genre. */
    function finished() {
        const result = { name: draft.name.trim(), choices: structuredClone(draft.choices) };
        return problemOf(result) === null ? result : null;
    }

    function problemOf(genre) {
        if (new TextEncoder().encode(genre.name).length > MAX_GENRE_NAME_BYTES) return "The name is too long.";
        for (const [choiceName, options] of Object.entries(genre.choices))
            if (choices.get(choiceName)?.kind === "pool" && Object.keys(options).length === 0) return `${choices.get(choiceName).label} keeps none of its options.`;
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

    return {
        element: dialog,
        /** Opens the editor on a genre, a copy of it the draft, by the choices the server lists. */
        open(genre, choiceList) {
            choices = new Map(choiceList.map((x) => [x.name, x]));
            draft = { name: genre.name, choices: structuredClone(genre.choices) };
            changed();
            dialog.showModal();
        },
        close() {
            dialog.close();
        },
    };
}
