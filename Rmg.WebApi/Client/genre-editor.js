import { MAX_GENRE_NAME_BYTES } from "./settings.js";
import { isShowingGenrePrinciples, setShowingGenrePrinciples } from "./storage.js";
import { endsOf, ownOf, written } from "./genres.js";
import { createGenreTune } from "./genre-tune.js";

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
 *     draft is played or saved as the page says (app.js), and exported to and imported from a JSON file. Every amount is
 *     a whole percent, an option's one alike at every end or three, at the plain end, in the middle and at the wild; a
 *     slider sets its middle, its ends moving alike, as the songs have them where the slider comes to its default: a value
 *     shows its lean from as it is, a chance its chance in a middling song, and a pool's weight its weight there.
 */

/** A pool's weight where an option is checked or added with none of its own, and where the instruments start from: half the heaviest's or more. */
const ADDED_WEIGHT = 50;

/** The most of any amount, a whole percent. */
const MOST = 100;

/**
 *     The scale a weight's or a chance's slider moves on: its amount, from 0 to 100, its position's share of the way to
 *     the power of CURVE, so that half the way is a third, the small amounts the finer; and the round amounts a drag
 *     within SNAP of comes to.
 */
export const SHARE_SCALE = {
    POSITIONS: 1000,
    CURVE: 1.6,
    SNAP: 2,
    ROUND: [10, 15, 20, 25, 30, 40, 50, 60, 70, 75, 80, 90, 100],
};

/** A position's amount on the share scale, a whole percent, snapped to a round one within reach. */
function shareAmountOf(position) {
    const amount = Math.round(MOST * (position / SHARE_SCALE.POSITIONS) ** SHARE_SCALE.CURVE);
    const round = SHARE_SCALE.ROUND.find((x) => Math.abs(x - amount) <= SHARE_SCALE.SNAP);
    return round ?? amount;
}

/** An amount's position on the share scale. */
function sharePositionOf(amount) {
    return Math.round(SHARE_SCALE.POSITIONS * (amount / MOST) ** (1 / SHARE_SCALE.CURVE));
}

const PITCHED = [["melody", "Melody"], ["chords", "Chords"], ["bass", "Bass"], ["pad", "Pad"], ["counterMelody", "Counter-melody"], ["riff", "Riff"], ["rhythm", "Rhythm"], ["solo", "Soloist"]];
const ROLES = [["ground", "Ground (kick)"], ["backbeat", "Backbeat (snare)"], ["time", "Time (hi-hat)"], ["colour", "Colour (toms)"]];

export function createGenreEditor({ onPlay, onSave, onDelete, isOwn }) {
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
     *     A slider of a step from its least to its last, back to its default on a double-click (app.js), its ends in words
     *     where it has them, its step shown as the form given says, none where it shows elsewhere, and what it means, such
     *     as "experimental songs only", inside it, read again as any slider moves (showMeanings). On the share scale, a
     *     weight's or a chance's, it moves finer at the small amounts and snaps to round ones, the arrow keys stepping a
     *     whole percent; otherwise a step a position.
     */
    function slider(label, least, last, value, byDefault, onInput, form, meaning, ends, isShare) {
        const element = document.createElement("span");
        element.className = "genre-slider";
        const range = document.createElement("input");
        range.type = "range";
        range.min = String(isShare ? 0 : least);
        range.max = String(isShare ? SHARE_SCALE.POSITIONS : last);
        range.step = "1";
        range.value = String(isShare ? sharePositionOf(value) : value);
        range.dataset.default = String(isShare ? sharePositionOf(byDefault) : byDefault);
        if (isShare) range.dataset.scale = "share";
        // the amount the slider stands at, kept apart from its position on the share scale, where a key steps a whole
        // percent a drag would snap away from
        let amount = value;
        const amountOf = () => (isShare ? amount : Number(range.value));
        range.setAttribute("aria-label", label);
        const shown = document.createElement("output");
        shown.className = "value";
        const track = document.createElement("span");
        track.className = "genre-track";
        const said = document.createElement("span");
        said.className = "genre-meaning";
        said.setAttribute("aria-hidden", "true");
        track.append(range, said);
        const show = () => {
            const step = amountOf();
            const fill = (Number(range.value) - Number(range.min)) / (Number(range.max) - Number(range.min));
            track.style.setProperty("--fill", `${100 * fill}%`);
            // written on the wider side of the thumb, never under it
            said.classList.toggle("after", fill < 0.5);
            said.textContent = meaning?.(step) ?? "";
            if (form !== null) shown.textContent = form(step);
            const text = [form === null ? null : shown.textContent, said.textContent].filter(Boolean).join(", ");
            if (text) range.setAttribute("aria-valuetext", text);
        };
        said.show = show;
        const moved = () => {
            if (isShare) {
                // its default as it is, a double-click's, and otherwise the round amount within reach, the thumb on it
                amount = range.value === range.dataset.default ? byDefault : shareAmountOf(Number(range.value));
                range.value = String(sharePositionOf(amount));
            }
            onInput(amountOf());
            showMeanings();
        };
        range.addEventListener("input", moved);
        if (isShare)
            range.addEventListener("keydown", (event) => {
                const by = { ArrowRight: 1, ArrowUp: 1, ArrowLeft: -1, ArrowDown: -1 }[event.key];
                if (by === undefined) return;
                event.preventDefault();
                amount = Math.max(0, Math.min(MOST, amount + by));
                range.value = String(sharePositionOf(amount));
                // a whole percent at a time, snapping no further
                onInput(amount);
                showMeanings();
            });
        const [lowEnd, highEnd] = ends.map((text) => {
            const end = document.createElement("span");
            end.className = "end";
            end.setAttribute("aria-hidden", "true");
            end.textContent = text;
            return end;
        });
        element.append(...(ends.length > 0 ? [lowEnd, track, highEnd] : [track]));
        if (form !== null) element.append(shown);
        show();
        return element;
    }

    /** Every slider shown again, as what one means may follow another, such as a weight's share of its pool. */
    function showMeanings() {
        for (const said of dialog.querySelectorAll(".genre-meaning")) said.show();
    }

    // --- an option's amounts: one alike at every end, or three, at the plain end, in the middle and at the wild ---------

    /** Its amount in a middling song, which the slider sets. */
    const middleOf = (amount) => endsOf(amount)[1];

    /**
     *     The amounts with the middle moved and the ends moved alike, by its ratio, so that a curve keeps its shape; the
     *     ends as they are where the middle comes to 0 or comes from it, so that an option of the wild end alone stays so.
     */
    function withMiddle(amount, middle, most = MOST) {
        const [plain, was, wild] = endsOf(amount);
        if (was === 0 || middle === 0) return written([plain, middle, wild]);
        const moved = (x) => Math.min(most, Math.round((x * middle) / was));
        return written([moved(plain), middle, moved(wild)]);
    }

    /** The amounts with the middle moved and the ends moved by as much, as a lean's are, from -100 to 100. */
    function shifted(amount, middle) {
        const [plain, was, wild] = endsOf(amount);
        const moved = (x) => Math.max(-MOST, Math.min(MOST, x + middle - was));
        return written([moved(plain), middle, moved(wild)]);
    }

    /** An option's amounts with its slider moved: the curve it starts from where the slider comes to its default, as a double-click brings it, and otherwise the middle moved (withMiddle). */
    const moved = (amount, middle, start, move = withMiddle) => (middle === middleOf(start) ? start : move(amount, middle));

    /** Whether the songs play an option at all, at one end or another. */
    const isPlayed = (option) => option.own.some((x) => x > 0);

    const EXPERIMENTAL_ONLY = "experimental songs only";
    const EVERY_SONG = "every song";

    /** What an option at 0 in the middle means: experimental songs only where the wild end plays it, never otherwise. */
    const atNothing = (amount) => (endsOf(amount)[2] > 0 ? EXPERIMENTAL_ONLY : "never");

    /** What a pool's weight means: its share of the middling songs among the options kept, every song where it is alone. */
    const weightMeaning = (choice, option) => (middle) => {
        const options = draft.choices[choice.name] ?? {};
        const kept = Object.values(options);
        if (kept.length === 1) return EVERY_SONG;
        if (middle === 0) return atNothing(options[option.name]);
        const total = kept.reduce((sum, x) => sum + middleOf(x), 0);
        return `${Math.round((MOST * middle) / total)}% of middling songs`;
    };

    /** What a chance means: in middling songs, every song where it is so at every end. */
    const chanceMeaning = (choice, option) => (middle) => {
        const amount = draft.choices[choice.name]?.[option.name];
        if (middle === 0) return atNothing(amount);
        return endsOf(amount ?? middle).every((x) => x === MOST) ? EVERY_SONG : "of middling songs";
    };

    /** What a value means: as the songs have it at 50, more or less of it either side. */
    const valueMeaning = (middle) => (middle === MOST / 2 ? "as the songs have it" : middle > MOST / 2 ? "more" : "less");

    /** A pool's weight. */
    const asWeight = (middle) => String(middle);

    /** A value's lean from as it is, at 50, to its least, -100, or its most, +100. */
    const asLean = (middle) => {
        if (middle === MOST / 2) return "as is";
        const lean = 2 * middle - MOST;
        return `${lean > 0 ? "+" : "−"}${Math.abs(lean)}`;
    };

    /** A chance in a middling song, in percent. */
    const asChance = (middle) => `${middle}%`;

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

    // the window an option's amounts at its three ends are tuned in (genre-tune.js), over the editor
    const tune = createGenreTune();

    /** A choice's button to fine-tune its options, the choice the one bound as it is pressed, the amounts it gives back the draft's. */
    function tuneButton(choiceOf) {
        return button("Fine-tune…", "Set the options in the plainest, middling and most experimental songs", () => {
            const choice = choiceOf();
            tune.open(choice, draft.choices[choice.name] ?? {}, (amounts) => {
                draft.choices[choice.name] = amounts;
                changed();
            });
        }, "genre-tune-open");
    }

    /** A weight where an option is checked or added: its own, or ADDED_WEIGHT at every end for one the songs never play. */
    const weightOf = (option) => (isPlayed(option) ? ownOf(option) : ADDED_WEIGHT);

    /** A pool's options as the songs have them: every one they play, at its own weights. */
    const asTheSongsHaveThem = (options) => Object.fromEntries(options.filter(isPlayed).map((x) => [x.name, ownOf(x)]));

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
                rows.replaceChildren(tuneButton(() => choice), ...choice.options.filter(allowed).map((option) => {
                    const row = document.createElement("div");
                    row.className = "genre-row";
                    row.dataset.option = option.name;
                    if (option.description) row.title = option.description;
                    const kept = options?.[option.name];
                    const check = checkbox(option.label);
                    check.input.checked = kept !== undefined;
                    check.input.addEventListener("change", () => {
                        if (check.input.checked) options[option.name] = weightOf(option);
                        else delete options[option.name];
                        changed();
                    });
                    const weight = slider(`${choice.label}: ${option.label}`, 0, MOST, middleOf(kept ?? weightOf(option)), middleOf(weightOf(option)), (value) => {
                        options[option.name] = moved(options[option.name], value, weightOf(option));
                        showProblem();
                    }, asWeight, weightMeaning(choice, option), [], true);
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
            options[unused.name] = weightOf(unused);
            changed();
        }, "genre-add");
        pool.append(tuneButton(() => choice), rows, add);
        // the commonest the songs have, as they have them
        const choose = chosen(title, pool, (bound) => asTheSongsHaveThem(bound.options.filter((x) => middleOf(ownOf(x)) >= ADDED_WEIGHT)));
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
                    const weight = slider(`${choice.label}: ${option.label}`, 0, MOST, middleOf(options[option.name]), middleOf(weightOf(option)), (value) => {
                        options[option.name] = moved(options[option.name], value, weightOf(option));
                        showProblem();
                    }, asWeight, weightMeaning(choice, option), [], true);
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
                element.replaceChildren(...(isChances ? [tuneButton(() => choice)] : []), ...choice.options.map((option) => {
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
                        if (named.input.checked) options[option.name] = ownOf(option);
                        else {
                            delete options[option.name];
                            for (const other of choice.options.filter((x) => partnerOf(x) === option.name)) delete options[other.name];
                        }
                        if (!isChances && Object.keys(options).length === 0) delete draft.choices[choice.name];
                        changed();
                    });
                    const range = slider(`${choice.label}: ${option.label}`, 0, MOST, middleOf(value ?? ownOf(option)), middleOf(ownOf(option)), (middle) => {
                        const options = draft.choices[choice.name];
                        options[option.name] = moved(options[option.name], middle, ownOf(option));
                        showProblem();
                    }, isChances ? asChance : asLean, isChances ? chanceMeaning(choice, option) : valueMeaning, [], isChances);
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
    const asTheyAre = (choice) => Object.fromEntries(choice.options.map((x) => [x.name, ownOf(x)]));

    /**
     *     The tempo: its checkbox, its title, named or the songs', its range, from and to, in beats a minute, and its lean
     *     within it, to the slow end or the fast; each slider its middle, its ends moving alike.
     */
    function rangeField(title) {
        const element = document.createElement("div");
        element.className = "genre-range";
        const range = document.createElement("div");
        range.className = "genre-range";
        const shown = document.createElement("output");
        let choice = null;
        const option = (name) => choice.options.find((x) => x.name === name);
        // the ends kept in order, the other moving with the one moved past it, the sliders kept as they are dragged
        const [from, to] = ["from", "to"].map((end) => slider(`Tempo: ${end}`, 60, 240, 60, 60, (bpm) => {
            const range = draft.choices[choice.name];
            const inBpm = (amount, middle) => withMiddle(amount, middle, 240);
            range[end] = moved(range[end], bpm, ownOf(option(end)), inBpm);
            const other = end === "from" ? "to" : "from";
            if (middleOf(range.from) > middleOf(range.to)) range[other] = moved(range[other], bpm, ownOf(option(other)), inBpm);
            showRange();
            showProblem();
        }, null, null, ["60 bpm", "240 bpm"], false));
        const lean = slider("Tempo: lean", -100, 100, 0, 0, (value) => {
            const range = draft.choices[choice.name];
            range.lean = moved(range.lean ?? 0, value, ownOf(option("lean")), shifted);
            showProblem();
        }, null, null, ["Slower", "Faster"], false);
        const ends = document.createElement("div");
        ends.className = "genre-sliders";
        ends.append(from, to, lean);
        range.append(tuneButton(() => choice), ends, shown);
        element.append(range);
        const named = chosen(title, range, asTheyAre);

        function showRange() {
            const range = draft.choices[choice.name] ?? asTheyAre(choice);
            for (const [end, element] of [["from", from], ["to", to], ["lean", lean]]) {
                const input = element.querySelector("input");
                input.value = String(middleOf(range[end] ?? 0));
                input.dataset.default = String(middleOf(ownOf(option(end))));
            }
            shown.textContent = `${middleOf(range.from)}–${middleOf(range.to)} bpm in a middling song`;
            showMeanings();
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

    // how a genre works, open until it is closed, which this browser keeps
    const principles = document.createElement("details");
    principles.className = "genre-principles";
    principles.open = isShowingGenrePrinciples();
    principles.addEventListener("toggle", () => setShowingGenrePrinciples(principles.open));
    const principlesSummary = document.createElement("summary");
    principlesSummary.textContent = "How a genre works";
    const principlesList = document.createElement("ul");
    principlesList.append(...[
        "A genre sets what a middling song does. Plainer songs keep to its commonest options; more experimental ones stray from it, towards what it makes rare.",
        "A checked choice is the genre's; an unchecked one is as the songs have it without a genre.",
        "An option unchecked never plays. At 0 it plays only in the experimental songs, as its most experimental amount has it. A chance at 100% at every end, or the only option kept, plays in every song.",
        "A weight is a share among the options kept; a chance is out of 100 middling songs.",
        "Double-click a slider to put it back as the songs have it.",
    ].map((text) => {
        const item = document.createElement("li");
        item.textContent = text;
        return item;
    }));
    principles.append(principlesSummary, principlesList);

    const body = document.createElement("div");
    body.className = "genre-body";
    body.append(
        principles,
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
    dialog.append(header, body, footer, tune.element);

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
