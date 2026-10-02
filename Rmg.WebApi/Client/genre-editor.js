import { MAX_GENRE_NAME_BYTES } from "./settings.js";

/**
 *     The genre editor, a popup over the page: a draft genre, a name and what it says of the choices the songs draw
 *     (/api/genres/choices), in sections that fold. A short pool lists every option, a checkbox and a slider of its
 *     weight, none checked for the choice as the songs have it; the instruments are rows, each a list of them and a
 *     slider, swapped in their lists, removed or added; a chance and a value are sliders between their ends; the tempo is
 *     a range of beats a minute. The parts playing come first, then a tab for every part: a pitched part's instruments,
 *     rhythm and feel, and the drums' kit, pieces and setup, and a tab for every role of them, its drums of the pieces
 *     chosen, its rhythm and its feel. The draft is played or saved as the page says (app.js), and read or written as JSON.
 */

/** A pool's weight where an option is checked or added with none of its own, and where the instruments start from: half the heaviest's or more. */
const ADDED_WEIGHT = 32;

const PITCHED = [["melody", "Melody"], ["chords", "Chords"], ["bass", "Bass"], ["pad", "Pad"], ["counterMelody", "Counter-melody"], ["riff", "Riff"], ["rhythm", "Rhythm"], ["solo", "Soloist"]];
const ROLES = [["ground", "Ground (kick)"], ["backbeat", "Backbeat (snare)"], ["time", "Time (hi-hat)"], ["colour", "Colour (toms)"]];

export function createGenreEditor({ onPlay, onSave, onDelete, isOwn, tempoOf }) {
    let choices = new Map();
    let draft = { name: "", choices: {} };

    const dialog = document.createElement("dialog");
    dialog.className = "genre-dialog";
    dialog.setAttribute("aria-label", "Genre editor");

    // every field's showing of the draft, and every section's choices, which open it where the draft names one
    const shows = [];
    const sections = [];

    // --- the header: the name, and closing ------------------------------------------------------------------------------

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

    // --- the parts --------------------------------------------------------------------------------------------------------

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

    function note(text) {
        const element = document.createElement("p");
        element.className = "genre-note";
        element.textContent = text;
        return element;
    }

    function heading(text) {
        const element = document.createElement("h4");
        element.textContent = text;
        return element;
    }

    /** A section that folds, open where the draft names one of its choices as the editor opens. */
    function section(title, names, ...content) {
        const element = document.createElement("details");
        element.className = "genre-section";
        element.dataset.section = title;
        const summary = document.createElement("summary");
        summary.textContent = title;
        element.append(summary, ...content);
        sections.push({ element, names });
        return element;
    }

    // --- the fields, each bound to a choice and shown again as the draft changes -----------------------------------------

    /**
     *     A short pool: every option it may keep, a checkbox and a slider of its weight, none checked as the songs have
     *     it; those it may keep, all of them, or as the draft allows them, such as the drums of the pieces chosen.
     */
    function checklistField(allowed = () => true) {
        const element = document.createElement("div");
        element.className = "genre-sliders";
        return {
            element,
            bind(choice) {
                element.dataset.choice = choice.name;
                const options = draft.choices[choice.name];
                element.replaceChildren(...choice.options.filter(allowed).map((option) => {
                    const row = document.createElement("div");
                    row.className = "genre-row";
                    row.dataset.option = option.name;
                    const kept = options?.[option.name];
                    const check = checkbox(option.label);
                    check.input.checked = kept !== undefined;
                    check.input.addEventListener("change", () => {
                        const pool = (draft.choices[choice.name] ??= {});
                        if (check.input.checked) pool[option.name] = option.default > 0 ? option.default : ADDED_WEIGHT;
                        else delete pool[option.name];
                        if (Object.keys(pool).length === 0) delete draft.choices[choice.name];
                        changed();
                    });
                    const weight = slider(`${choice.label}: ${option.label}`, 1, kept ?? Math.max(1, option.default), "rarer", "commoner", (value) => {
                        draft.choices[choice.name][option.name] = value;
                        showProblem();
                    });
                    // an option left out its checkbox alone, its weight once it is in
                    weight.hidden = kept === undefined;
                    row.append(check.element, weight);
                    return row;
                }), note("None checked: as the songs have it."));
            },
        };
    }

    /** The instruments: rows, each a list of them and a slider of its weight, swapped, removed, or added; none as the songs have them. */
    function rowsField() {
        const element = document.createElement("div");
        element.className = "genre-pool";
        const rows = document.createElement("div");
        rows.className = "genre-rows";
        let choice = null;
        const add = button("Add", "Add the next instrument", () => {
            const options = (draft.choices[choice.name] ??= {});
            // the first the songs have, the commonest of them first
            const unused = Object.keys(options).length === 0
                ? choice.options.filter((x) => x.default >= ADDED_WEIGHT)
                : [choice.options.find((x) => !(x.name in options))].filter((x) => x !== undefined);
            for (const option of unused) options[option.name] = option.default > 0 ? option.default : ADDED_WEIGHT;
            changed();
        }, "genre-add");
        element.append(rows, add, note("None: as the songs have them."));

        return {
            element,
            bind(bound) {
                choice = bound;
                const options = draft.choices[choice.name] ?? {};
                const kept = choice.options.filter((x) => x.name in options);
                rows.replaceChildren(...kept.map((option) => {
                    const row = document.createElement("div");
                    row.className = "genre-row";
                    row.dataset.option = option.name;
                    const select = document.createElement("select");
                    select.setAttribute("aria-label", `${choice.label}: instrument`);
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
                    row.append(select, weight, button("×", `Leave out ${option.label}`, () => {
                        delete options[option.name];
                        if (Object.keys(options).length === 0) delete draft.choices[choice.name];
                        changed();
                    }, "genre-remove"));
                    return row;
                }));
                add.textContent = kept.length === 0 ? "Choose instruments" : "Add";
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
                element.dataset.choice = choice.name;
                const [low, high] = choice.kind === "chances" ? ["never", "always"] : ["less", "more"];
                element.replaceChildren(...choice.options.map((option) => {
                    const row = document.createElement("div");
                    row.className = "genre-row";
                    row.dataset.option = option.name;
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
                    range.hidden = value === undefined;
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

    /** A field shown under a heading, bound to the choice its part's name gives. */
    function bound(made, choiceOf) {
        return { element: made.element, show: (part) => made.bind(choices.get(choiceOf(part))) };
    }

    /** A field of one choice, shown as the draft changes. */
    function single(made, choiceName) {
        shows.push(() => made.bind(choices.get(choiceName)));
        return made.element;
    }

    /**
     *     Tabs, every tab a part, over panels of fields: tabs of the same panel share one set of fields, bound to the tab's
     *     part, so that switching tabs keeps every field where it is. A tab marked where the draft names one of its choices.
     */
    function tabs(label, items) {
        const element = document.createElement("div");
        element.className = "genre-tabs";
        const strip = document.createElement("div");
        strip.className = "tab-strip";
        strip.setAttribute("role", "tablist");
        strip.setAttribute("aria-label", label);
        let current = 0;
        const buttons = items.map((item, i) => {
            const tab = document.createElement("button");
            tab.type = "button";
            tab.setAttribute("role", "tab");
            tab.textContent = item.label;
            tab.addEventListener("click", () => {
                current = i;
                show();
            });
            return tab;
        });
        strip.append(...buttons);
        const panels = [...new Set(items.map((x) => x.panel))];
        for (const panel of panels) panel.element.classList.add("tab-panel");
        element.append(strip, ...panels.map((x) => x.element));

        function show() {
            for (const panel of panels) panel.element.hidden = panel !== items[current].panel;
            items[current].panel.show(items[current].part);
            buttons.forEach((tab, i) => {
                tab.setAttribute("aria-selected", String(i === current));
                tab.classList.toggle("named", items[i].names.some((x) => x in draft.choices));
            });
        }

        return { element, show };
    }

    /** A panel: fields under headings, each bound to its part's choice, shown for the part given. */
    function panel(...parts) {
        const element = document.createElement("div");
        const fields = [];
        for (const part of parts) {
            if (typeof part === "string") element.append(heading(part));
            else {
                element.append(part.element);
                fields.push(part);
            }
        }
        return { element, show: (part) => fields.forEach((x) => x.show(part)) };
    }

    // --- the drums: the pieces chosen, which the setups and the roles' drums keep to ---------------------------------------

    /** The drum pieces the draft keeps to, none for every one. */
    const piecesChosen = () => (draft.choices.drumPieces ? new Set(Object.keys(draft.choices.drumPieces)) : null);

    /** The families of the pieces chosen, every one where none is. */
    function familiesChosen() {
        const pieces = piecesChosen();
        const all = choices.get("drumPieces").options;
        return new Set(all.filter((x) => pieces === null || pieces.has(x.name)).flatMap((x) => x.tags));
    }

    /** Whether a setup is one the pieces chosen make: the kit's or percussion's with one of its family, both with one of each. */
    function isSetupAllowed(option) {
        const families = familiesChosen();
        return option.name === "kitAndPercussion" ? families.has("kit") && families.has("percussion") : families.has(option.name);
    }

    const isPieceAllowed = (option) => piecesChosen()?.has(option.name) ?? true;

    /** The setups and the roles' drums the pieces chosen no longer allow let go of, and a choice keeping nothing with them. */
    function prune() {
        const keep = (choiceName, allowed) => {
            const options = draft.choices[choiceName];
            if (!options) return;
            for (const option of Object.keys(options)) if (!allowed({ name: option })) delete options[option];
            if (Object.keys(options).length === 0) delete draft.choices[choiceName];
        };
        keep("drumSetup", (x) => isSetupAllowed(x));
        for (const [role] of ROLES) keep(`drumRole.${role}`, isPieceAllowed);
    }

    // --- the body ----------------------------------------------------------------------------------------------------------

    const pitched = panel(
        "Instruments", bound(rowsField(), (part) => `instrument.${part}`),
        "Rhythm", bound(slidersField(), (part) => `rhythm.${part}`),
        "Feel", bound(checklistField(), (part) => `feel.${part}`),
    );
    const solo = panel("Instruments", bound(rowsField(), () => "instrument.solo"));
    const role = panel(
        "Drums", bound(checklistField(isPieceAllowed), (x) => `drumRole.${x}`),
        "Rhythm", bound(slidersField(), (x) => `rhythm.${x}`),
        "Feel", bound(checklistField(), (x) => `feel.${x}`),
    );
    const roles = tabs("Drums' roles", ROLES.map(([part, label]) => ({ label, part, names: [`drumRole.${part}`, `rhythm.${part}`, `feel.${part}`], panel: role })));
    const drums = panel(
        "Kit", bound(checklistField(), () => "kit"),
        "Pieces", bound(checklistField(), () => "drumPieces"),
        "Setup", bound(checklistField(isSetupAllowed), () => "drumSetup"),
        { element: roles.element, show: () => roles.show() },
    );
    const parts = tabs("Parts", [
        ...PITCHED.map(([part, label]) => ({
            label,
            part,
            names: [`instrument.${part}`, `rhythm.${part}`, `feel.${part}`],
            panel: part === "solo" ? solo : pitched,
        })),
        {
            label: "Drums",
            part: "drums",
            names: ["kit", "drumPieces", "drumSetup", ...ROLES.flatMap(([role]) => [`drumRole.${role}`, `rhythm.${role}`, `feel.${role}`])],
            panel: drums,
        },
    ]);
    shows.push(parts.show);

    const body = document.createElement("div");
    body.className = "genre-body";
    body.append(
        section("Tempo", ["tempo"], single(rangeField(), "tempo")),
        section("Meter", ["meter"], single(checklistField(), "meter")),
        section("Feel", ["feel"], single(checklistField(), "feel")),
        section("Swing and half time", ["feelChances", "feelValues"], single(slidersField(), "feelChances"), single(slidersField(), "feelValues")),
        section("Scale", ["scale"], single(checklistField(), "scale")),
        section("Chord shapes", ["chordShape"], single(checklistField(), "chordShape")),
        section("Parts playing", ["parts"], single(slidersField(), "parts")),
        section("Parts", [...PITCHED.flatMap(([part]) => [`instrument.${part}`, `rhythm.${part}`, `feel.${part}`]), "kit", "drumPieces", "drumSetup",
            ...ROLES.flatMap(([role]) => [`drumRole.${role}`, `rhythm.${role}`, `feel.${role}`])], parts.element),
    );

    // --- the footer: the problem, as JSON, and what to do with the draft -----------------------------------------------------

    const problem = document.createElement("p");
    problem.className = "warning";
    problem.setAttribute("role", "alert");
    problem.hidden = true;

    const json = document.createElement("textarea");
    json.className = "genre-json";
    json.rows = 4;
    json.setAttribute("aria-label", "Genre as JSON");
    const jsonDetails = document.createElement("details");
    jsonDetails.className = "genre-section";
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

    // --- the draft ------------------------------------------------------------------------------------------------------------

    function changed() {
        prune();
        name.value = draft.name;
        for (const show of shows) show();
        showProblem();
    }

    /** The draft as it is played or kept; none if it is not a genre. */
    function finished() {
        const result = { name: draft.name.trim(), choices: structuredClone(draft.choices) };
        return problemOf(result) === null ? result : null;
    }

    function problemOf(genre) {
        return new TextEncoder().encode(genre.name).length > MAX_GENRE_NAME_BYTES ? "The name is too long." : null;
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
        /** Opens the editor on a genre, a copy of it the draft, by the choices the server lists; its sections open where it names a choice. */
        open(genre, choiceList) {
            choices = new Map(choiceList.map((x) => [x.name, x]));
            draft = { name: genre.name, choices: structuredClone(genre.choices) };
            for (const { element, names } of sections) element.open = names.some((x) => x in draft.choices);
            changed();
            dialog.showModal();
        },
        close() {
            dialog.close();
        },
    };
}
