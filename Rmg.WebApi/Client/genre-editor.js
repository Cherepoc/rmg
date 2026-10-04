import { MAX_GENRE_NAME_BYTES } from "./settings.js";

/**
 *     The genre editor, a popup over the page: a draft genre, a name and what it says of the choices the songs draw
 *     (/api/genres/choices), in sections that fold. A pool is a checkbox, unchecked for the choice as the songs have it,
 *     and once checked its options: a short pool's every option, a checkbox and a slider of its weight, and the
 *     instruments rows, each a list of them and a slider, swapped in their lists, removed or added; a pool keeping none is
 *     a problem. A chance and a value are sliders; the tempo is a range of beats a minute, between its ends. A section of
 *     several choices, the swing and half time, the parts playing and the parts, has a checkbox of its own. A slider goes
 *     back to its default on a double-click, and a scale and a chord shape say over their names how they sound. The parts
 *     playing come first, then a tab for every part: a pitched part's instruments, rhythm and feel, and the drums' kit,
 *     pieces and setup, and a tab for every role of them, its drums of the pieces chosen, its rhythm and its feel. The
 *     draft is played or saved as the page says (app.js), and exported to and imported from a JSON file. A value shows
 *     its lean from as it is, a chance its chance in a middling song, and a pool's weight its amount, on steps finer at
 *     the low end (/api/genres/choices).
 */

/** A pool's weight where an option is checked or added with none of its own, and where the instruments start from: half the heaviest's or more. */
const ADDED_WEIGHT = 0.5;

/** The last step of a value's or a range's 64. */
const LAST_STEP = 63;

const PITCHED = [["melody", "Melody"], ["chords", "Chords"], ["bass", "Bass"], ["pad", "Pad"], ["counterMelody", "Counter-melody"], ["riff", "Riff"], ["rhythm", "Rhythm"], ["solo", "Soloist"]];
const ROLES = [["ground", "Ground (kick)"], ["backbeat", "Backbeat (snare)"], ["time", "Time (hi-hat)"], ["colour", "Colour (toms)"]];

export function createGenreEditor({ onPlay, onSave, onDelete, isOwn, tempoOf }) {
    let choices = new Map();
    let draft = { name: "", choices: {} };
    // every pool's options as it was unchecked, for it to come back to, by its choice's name
    let uncheckedPools = new Map();

    const dialog = document.createElement("dialog");
    dialog.className = "genre-dialog";
    dialog.setAttribute("aria-label", "Genre editor");

    // every field's showing of the draft, and every section's choices, which open it where the draft names one
    const shows = [];
    const sections = [];
    // what every section of several choices sets as the editor opens
    const resets = [];

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

    /**
     *     A slider of a step from its least to 63, back to its default on a double-click (app.js), its ends in words where it
     *     has them, and its step shown as the form given says, none where it shows elsewhere.
     */
    function slider(label, least, last, value, byDefault, onInput, form = null, ends = []) {
        const element = document.createElement("span");
        element.className = "genre-slider";
        const range = document.createElement("input");
        range.type = "range";
        range.min = String(least);
        range.max = String(last);
        range.step = "1";
        range.value = String(value);
        range.dataset.default = String(byDefault);
        range.setAttribute("aria-label", label);
        const shown = document.createElement("output");
        shown.className = "value";
        const show = () => {
            if (form === null) return;
            shown.textContent = form(Number(range.value));
            range.setAttribute("aria-valuetext", shown.textContent);
        };
        range.addEventListener("input", () => {
            show();
            onInput(Number(range.value));
        });
        const [lowEnd, highEnd] = ends.map((text) => {
            const end = document.createElement("span");
            end.className = "end";
            end.setAttribute("aria-hidden", "true");
            end.textContent = text;
            return end;
        });
        element.append(...(ends.length > 0 ? [lowEnd, range, highEnd] : [range]));
        if (form !== null) element.append(shown);
        show();
        return element;
    }

    /** A value's step from 0 to 100, step 32 at 50, a half of the steps either side. */
    const percentOf = (step) => Math.round(step <= 32 ? step * 50 / 32 : 50 + (step - 32) * 50 / 31);

    /** A pool's or a chance's step as its amount, by its choice's steps (/api/genres/choices), in percent. */
    const percentAt = (choice, step) => Math.round(choice.amounts[step] * 100);

    /** The step of a pool's or a chance's amount, the nearest. */
    const stepAt = (choice, amount) => choice.amounts.reduce((best, x, i) => (Math.abs(x - amount) < Math.abs(choice.amounts[best] - amount) ? i : best), 0);

    /** A pool's weight, from 0 to 100. */
    const asWeight = (choice) => (step) => String(percentAt(choice, step));

    /** A value's lean from as it is, at the middle, to its least, -100, or its most, +100. */
    const asLean = (step) => {
        if (step === 32) return "as is";
        const lean = 2 * percentOf(step) - 100;
        return `${lean > 0 ? "+" : "−"}${Math.abs(lean)}`;
    };

    /** A chance in a middling song, in percent. */
    const asChance = (choice) => (step) => `${percentAt(choice, step)}%`;

    function heading(title) {
        const element = document.createElement("h4");
        element.append(title);
        return element;
    }

    /**
     *     A section that folds, open where the draft names one of its choices as the editor opens; titled in words, or by
     *     the checkbox of the one choice it holds, which opens it once checked.
     */
    function section(title, names, ...content) {
        const element = document.createElement("details");
        element.className = "genre-section";
        const summary = document.createElement("summary");
        summary.append(title);
        element.dataset.section = summary.textContent;
        title.querySelector?.("input").addEventListener("change", (event) => {
            if (event.target.checked) element.open = true;
        });
        element.append(summary, ...content);
        sections.push({ element, names });
        return element;
    }

    /** A section of one choice, titled by its field's checkbox. */
    function choiceSection(made, choiceName) {
        return section(made.head, [choiceName], single(made, choiceName));
    }

    // --- the fields, each bound to a choice and shown again as the draft changes -----------------------------------------

    /**
     *     A choice's checkbox, its title: unchecked, the genre leaves the choice as the songs have it and its fields are
     *     hidden; checked, it names it, as it did before it was unchecked, or from the amounts given.
     */
    function chosen(title, content, start) {
        const check = checkbox(title);
        check.element.classList.add("genre-choose");
        let choice = null;
        check.input.addEventListener("change", () => {
            if (check.input.checked) draft.choices[choice.name] = uncheckedPools.get(choice.name) ?? start(choice);
            else {
                uncheckedPools.set(choice.name, draft.choices[choice.name]);
                delete draft.choices[choice.name];
            }
            changed();
        });
        return {
            element: check.element,
            bind(bound) {
                choice = bound;
                check.element.dataset.choice = choice.name;
                check.input.checked = choice.name in draft.choices;
                content.hidden = !check.input.checked;
            },
        };
    }

    /** A weight where an option is checked or added: its own, 0 for one the songs play only the wilder they are, or ADDED_WEIGHT for one they never play. */
    const weightOf = (choice, option) => (option.isPlayed ? option.default : stepAt(choice, ADDED_WEIGHT));

    /** A pool's options as the songs have them: every one they play, at its weight in the middle, 0 for one they play only the wilder they are. */
    const asTheSongsHaveThem = (options) => Object.fromEntries(options.filter((x) => x.isPlayed).map((x) => [x.name, x.default]));

    /**
     *     A short pool: its checkbox, its title, then every option it may keep, a checkbox and a slider of its weight;
     *     those it may keep, all of them, or as the draft allows them, such as the drums of the pieces chosen.
     */
    function checklistField(title, allowed = () => true) {
        const element = document.createElement("div");
        element.className = "genre-sliders";
        const rows = document.createElement("div");
        rows.className = "genre-sliders";
        const choose = chosen(title, rows, (choice) => asTheSongsHaveThem(choice.options.filter(allowed)));
        element.append(rows);
        return {
            head: choose.element,
            element,
            bind(choice) {
                element.dataset.choice = choice.name;
                choose.bind(choice);
                const options = draft.choices[choice.name];
                rows.replaceChildren(...choice.options.filter(allowed).map((option) => {
                    const row = document.createElement("div");
                    row.className = "genre-row";
                    row.dataset.option = option.name;
                    if (option.description) row.title = option.description;
                    const kept = options?.[option.name];
                    const check = checkbox(option.label);
                    check.input.checked = kept !== undefined;
                    check.input.addEventListener("change", () => {
                        if (check.input.checked) options[option.name] = weightOf(choice, option);
                        else delete options[option.name];
                        changed();
                    });
                    const weight = slider(`${choice.label}: ${option.label}`, 0, choice.amounts.length - 1, kept ?? weightOf(choice, option), weightOf(choice, option), (value) => {
                        options[option.name] = value;
                        showProblem();
                    }, asWeight(choice));
                    // an option left out its checkbox alone, its weight once it is in
                    weight.hidden = kept === undefined;
                    row.append(check.element, weight);
                    return row;
                }));
            },
        };
    }

    /** The instruments: their checkbox, their title, then rows, each a list of them and a slider of its weight, swapped, removed, or added. */
    function rowsField(title) {
        const element = document.createElement("div");
        element.className = "genre-pool";
        const pool = document.createElement("div");
        pool.className = "genre-pool";
        const rows = document.createElement("div");
        rows.className = "genre-rows";
        let choice = null;
        const add = button("Add", "Add the next instrument", () => {
            const options = draft.choices[choice.name];
            const unused = choice.options.find((x) => !(x.name in options));
            options[unused.name] = weightOf(choice, unused);
            changed();
        }, "genre-add");
        pool.append(rows, add);
        // the commonest the songs have, as they have them
        const choose = chosen(title, pool, (bound) => asTheSongsHaveThem(bound.options.filter((x) => bound.amounts[x.default] >= ADDED_WEIGHT)));
        element.append(pool);

        return {
            head: choose.element,
            element,
            bind(bound) {
                choice = bound;
                element.dataset.choice = choice.name;
                choose.bind(choice);
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
                    const weight = slider(`${choice.label}: ${option.label}`, 0, choice.amounts.length - 1, options[option.name], weightOf(choice, option), (value) => {
                        options[option.name] = value;
                        showProblem();
                    }, asWeight(choice));
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

    /** The option another plays only with, by its tag, "with:riff"; none for one that plays alone. */
    const partnerOf = (option) => option.tags.find((x) => x.startsWith("with:"))?.slice("with:".length) ?? null;

    /**
     *     Chances or values, each a checkbox and a slider: a value unchecked as it is, its slider its lean from it; a chance
     *     unchecked never, its slider its chance in a middling song, and one that plays only with another unchecked and
     *     disabled while that one is.
     */
    function slidersField() {
        const element = document.createElement("div");
        element.className = "genre-sliders";
        return {
            element,
            bind(choice) {
                const isChances = choice.kind === "chances";
                element.dataset.choice = choice.name;
                element.replaceChildren(...choice.options.map((option) => {
                    const row = document.createElement("div");
                    row.className = "genre-row";
                    row.dataset.option = option.name;
                    const value = draft.choices[choice.name]?.[option.name];
                    const partner = partnerOf(option);
                    const named = checkbox(option.label);
                    named.input.checked = value !== undefined;
                    named.input.disabled = partner !== null && !(partner in (draft.choices[choice.name] ?? {}));
                    named.input.addEventListener("change", () => {
                        const options = (draft.choices[choice.name] ??= {});
                        if (named.input.checked) options[option.name] = option.default;
                        else {
                            delete options[option.name];
                            for (const other of choice.options.filter((x) => partnerOf(x) === option.name)) delete options[other.name];
                        }
                        if (!isChances && Object.keys(options).length === 0) delete draft.choices[choice.name];
                        changed();
                    });
                    const range = slider(`${choice.label}: ${option.label}`, 0, isChances ? choice.amounts.length - 1 : LAST_STEP, value ?? option.default, option.default, (step) => {
                        draft.choices[choice.name][option.name] = step;
                        showProblem();
                    }, isChances ? asChance(choice) : asLean);
                    range.hidden = value === undefined;
                    row.append(named.element, range);
                    return row;
                }));
            },
        };
    }

    /**
     *     A section of several choices under one checkbox, its title: unchecked, the genre leaves them as the songs have them
     *     and its fields are hidden; checked, it names them as it did before it was unchecked, or where it named none, as
     *     the start gives each, none for a choice left to its own checkbox. It stays checked while it names none.
     */
    function groupSection(title, names, start, ...content) {
        const check = checkbox(title);
        check.element.classList.add("genre-choose");
        const fields = document.createElement("div");
        fields.className = "genre-group";
        fields.append(...content);
        // what it named as it was unchecked, by its choices' names
        let named = new Map();
        check.input.addEventListener("change", () => {
            if (check.input.checked) {
                for (const name of names) {
                    const options = named.size > 0 ? named.get(name) : start(choices.get(name));
                    if (options) draft.choices[name] = options;
                }
            } else {
                named = new Map(names.filter((x) => x in draft.choices).map((x) => [x, draft.choices[x]]));
                for (const name of names) delete draft.choices[name];
            }
            changed();
        });
        shows.push(() => (fields.hidden = !check.input.checked));
        resets.push(() => {
            check.input.checked = names.some((x) => x in draft.choices);
            named = new Map();
        });
        return section(check.element, names, fields);
    }

    /** Chances or values, every one named as the songs have it in the middle. */
    const asTheyAre = (choice) => Object.fromEntries(choice.options.map((x) => [x.name, x.default]));

    /** The tempo: its checkbox, its title, named or the songs', and its range, from and to, shown in beats a minute. */
    function rangeField(title) {
        const element = document.createElement("div");
        element.className = "genre-range";
        const range = document.createElement("div");
        range.className = "genre-range";
        const shown = document.createElement("output");
        let choice = null;
        // the ends kept in order, the other moving with the one moved past it, the sliders kept as they are dragged
        const [from, to] = ["from", "to"].map((end) => slider(`Tempo: ${end}`, 0, LAST_STEP, 0, 0, (step) => {
            const range = draft.choices[choice.name];
            range[end] = step;
            if (range.from > range.to) range[end === "from" ? "to" : "from"] = step;
            showRange();
            showProblem();
        }, null, ["60 bpm", "240 bpm"]));
        const ends = document.createElement("div");
        ends.className = "genre-sliders";
        ends.append(from, to);
        range.append(ends, shown);
        element.append(range);
        const named = chosen(title, range, asTheyAre);

        function showRange() {
            const range = draft.choices[choice.name] ?? { from: choice.options[0].default, to: choice.options[1].default };
            from.querySelector("input").value = String(range.from);
            to.querySelector("input").value = String(range.to);
            from.querySelector("input").dataset.default = String(choice.options[0].default);
            to.querySelector("input").dataset.default = String(choice.options[1].default);
            shown.textContent = `${tempoOf(range.from)}–${tempoOf(range.to)} bpm`;
        }

        return {
            head: named.element,
            element,
            bind(bound) {
                choice = bound;
                named.bind(choice);
                showRange();
            },
        };
    }

    /** A field shown under a heading, its checkbox where it has one, bound to the choice its part's name gives. */
    function bound(made, choiceOf) {
        return { head: made.head, element: made.element, show: (part) => made.bind(choices.get(choiceOf(part))) };
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

    /** A panel: fields under headings, a pool's its checkbox, each bound to its part's choice, shown for the part given. */
    function panel(...parts) {
        const element = document.createElement("div");
        const fields = [];
        for (const part of parts) {
            if (typeof part === "string") element.append(heading(part));
            else {
                if (part.head) element.append(heading(part.head));
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

    /** The setups and the roles' drums the pieces chosen no longer allow let go of; a choice they leave with none a problem. */
    function prune() {
        const keep = (choiceName, allowed) => {
            const options = draft.choices[choiceName];
            if (!options) return;
            for (const option of Object.keys(options)) if (!allowed({ name: option })) delete options[option];
        };
        keep("drumSetup", (x) => isSetupAllowed(x));
        for (const [role] of ROLES) keep(`drumRole.${role}`, isPieceAllowed);
    }

    // --- the body ----------------------------------------------------------------------------------------------------------

    const pitched = panel(
        bound(rowsField("Instruments"), (part) => `instrument.${part}`),
        "Rhythm", bound(slidersField(), (part) => `rhythm.${part}`),
        bound(checklistField("Feel"), (part) => `feel.${part}`),
    );
    const solo = panel(bound(rowsField("Instruments"), () => "instrument.solo"));
    const role = panel(
        bound(checklistField("Drums", isPieceAllowed), (x) => `drumRole.${x}`),
        "Rhythm", bound(slidersField(), (x) => `rhythm.${x}`),
        bound(checklistField("Feel"), (x) => `feel.${x}`),
    );
    const roles = tabs("Drums' roles", ROLES.map(([part, label]) => ({ label, part, names: [`drumRole.${part}`, `rhythm.${part}`, `feel.${part}`], panel: role })));
    const drums = panel(
        bound(checklistField("Kit"), () => "kit"),
        bound(checklistField("Pieces"), () => "drumPieces"),
        bound(checklistField("Setup", isSetupAllowed), () => "drumSetup"),
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
        choiceSection(rangeField("Tempo"), "tempo"),
        choiceSection(checklistField("Meter"), "meter"),
        choiceSection(checklistField("Feel"), "feel"),
        groupSection("Swing and half time", ["feelChances", "feelValues"], asTheyAre, single(slidersField(), "feelChances"), single(slidersField(), "feelValues")),
        choiceSection(checklistField("Scale"), "scale"),
        choiceSection(checklistField("Chord shapes"), "chordShape"),
        groupSection("Parts playing", ["parts"], asTheyAre, single(slidersField(), "parts")),
        groupSection("Parts", [...PITCHED.flatMap(([part]) => [`instrument.${part}`, `rhythm.${part}`, `feel.${part}`]), "kit", "drumPieces", "drumSetup",
            ...ROLES.flatMap(([role]) => [`drumRole.${role}`, `rhythm.${role}`, `feel.${role}`])], () => null, parts.element),
    );

    // --- the footer: the problem, as JSON, and what to do with the draft -----------------------------------------------------

    const problem = document.createElement("p");
    problem.className = "warning";
    problem.setAttribute("role", "alert");
    problem.hidden = true;

    const exported = button("Export JSON", "Save this genre as a JSON file", () => {
        const link = document.createElement("a");
        link.href = URL.createObjectURL(new Blob([JSON.stringify({ name: draft.name.trim(), choices: draft.choices }, null, 1)], { type: "application/json" }));
        link.download = `${draft.name.trim() || "genre"}.json`;
        link.click();
        setTimeout(() => URL.revokeObjectURL(link.href));
    }, "genre-export");

    const file = document.createElement("input");
    file.type = "file";
    file.accept = "application/json,.json";
    file.hidden = true;
    file.setAttribute("aria-label", "Genre file");
    file.addEventListener("change", async () => {
        const chosen = file.files[0];
        file.value = "";
        if (!chosen) return;
        try {
            const parsed = JSON.parse(await chosen.text());
            const isGenre = typeof parsed?.name === "string" && typeof parsed?.choices === "object" && parsed.choices !== null
                && Object.keys(parsed.choices).every((x) => choices.has(x));
            if (!isGenre) throw new Error();
            load(parsed);
        } catch {
            showProblem("That file is not a genre: a name and the choices the editor knows.");
        }
    });
    const imported = button("Import JSON", "Open a genre saved as a JSON file", () => file.click());

    const play = button("Play", "Ask for the song again in this genre", () => onPlay(finished()), "primary");
    const save = button("Save", "Keep this genre among yours, and play it", () => onSave(finished()));
    const remove = button("Delete", "Let go of this genre of yours", () => onDelete(draft.name.trim()));
    const footer = document.createElement("footer");
    footer.append(problem, file, imported, exported, remove, save, play);
    dialog.append(header, body, footer);

    // --- the draft ------------------------------------------------------------------------------------------------------------

    /** A genre as the draft, its sections open where it names a choice. */
    function load(genre) {
        draft = { name: genre.name, choices: structuredClone(genre.choices) };
        uncheckedPools = new Map();
        for (const { element, names } of sections) element.open = names.some((x) => x in draft.choices);
        for (const reset of resets) reset();
        changed();
    }

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

    /** A pool as the problem names it: a part's by its part and what it chooses, such as "Melody's feel". */
    function poolName(choiceName) {
        const label = choices.get(choiceName).label;
        const of = { instrument: "instruments", feel: "feel", drumRole: "drums" }[choiceName.split(".")[0]];
        return choiceName.includes(".") ? `${label}'s ${of}` : label;
    }

    function problemOf(genre) {
        if (new TextEncoder().encode(genre.name).length > MAX_GENRE_NAME_BYTES) return "The name is too long.";
        const empty = Object.entries(genre.choices).find(([name, options]) => choices.get(name)?.kind === "pool" && Object.keys(options).length === 0);
        return empty ? `${poolName(empty[0])} keeps none of its options.` : null;
    }

    function showProblem(text = problemOf(draft)) {
        problem.textContent = text ?? "";
        problem.hidden = text === null;
        play.disabled = save.disabled = text !== null;
        save.disabled ||= draft.name.trim() === "";
        remove.disabled = !isOwn(draft.name.trim());
    }

    return {
        element: dialog,
        /** Opens the editor on a genre, a copy of it the draft, by the choices the server lists; its sections open where it names a choice. */
        open(genre, choiceList) {
            choices = new Map(choiceList.map((x) => [x.name, x]));
            load(genre);
            dialog.showModal();
        },
        close() {
            dialog.close();
        },
    };
}
