import { button, downloadJson, jsonFileInput } from "./dialog-parts.js";
import { isShowingGenrePrinciples, setShowingGenrePrinciples } from "./storage.js";
import { endsOf, genreProblem, isPresetName, nestedChoices, NOT_A_GENRE, ownOf, readImport, sameName } from "./genres.js";
import { ADDED_WEIGHT, createGenreTune } from "./genre-tune.js";
import { byMiddle, COLOURS, drawBand, drawLines, drawShares, ENDS, endsInWords, labelOf, svg } from "./genre-charts.js";

/**
 *     The genre editor, a popup over the page: a draft genre, a name and what it says of the choices the songs draw
 *     (/api/genres/choices), in sections that fold, every choice a graph of what the songs draw of it from the plainest
 *     to the experimental (/api/genres/previews) and its checkbox, Override: checked, the genre names the choice, and
 *     unchecked, it leaves it as the songs have it, the graph greyed. A graph clicked opens the window that changes it
 *     (genre-tune.js), the options the genre keeps and each one's amounts at the three ends. The parts playing come first,
 *     then a tab for every part: a pitched part's instruments, rhythm and feel, and the drums' kit, pieces and setup, and
 *     a tab for every role of them, its drums of the pieces chosen, its rhythm and its feel. The draft is played or saved
 *     as the page says (app.js), and exported to and imported from a JSON file. Every amount is a whole percent, an
 *     option's one alike at every end or three, at the plain end, in the middle and at the experimental.
 */

/** A pool's weight from which an instrument is among the commonest, which a part's instruments start from. */
const COMMON_WEIGHT = 50;

/** A genre's description's longest, in characters. */
const MAX_GENRE_DESCRIPTION = 200;

/** How long the editor waits after a change before it asks for the graphs again. */
const PREVIEW_WAIT_MS = 150;

// the graphs' size in their own units, drawn to the width of the editor
const GRAPH_WIDTH = 520;
const GRAPH_HEIGHT = 120;

const PITCHED = [["melody", "Melody"], ["chords", "Chords"], ["bass", "Bass"], ["pad", "Pad"], ["counterMelody", "Counter-melody"], ["riff", "Riff"], ["rhythm", "Rhythm"], ["solo", "Soloist"]];
const ROLES = [["ground", "Ground (kick)"], ["backbeat", "Backbeat (snare)"], ["time", "Time (hi-hat)"], ["colour", "Colour (toms)"]];

export function createGenreEditor({ onPlay, onSave, onDelete, isOwn, onKeepAll }) {
    let choices = new Map();
    let draft = { name: "", description: "", choices: {} };
    // every choice as it was before its override was unchecked, for it to come back to, by its name
    let unchecked = new Map();
    // what the songs draw of every choice, as the server last said (/api/genres/previews)
    let previews = null;
    // how many tab panels are named so far, each by its number
    let tabPanels = 0;
    let waiting = null;
    // the draft as it was opened, played or saved last, which closing the editor loses nothing of
    let kept = "";
    // the name of one's own genre the draft is, which Save keeps it in place of and Delete lets go of; none for a draft of
    // none, a preset or a song's
    let origin = null;
    // how many times the graphs were asked for, the latest answer the one drawn
    let previewsAsked = 0;

    const dialog = document.createElement("dialog");
    dialog.className = "genre-dialog";
    dialog.setAttribute("aria-label", "Genre editor");

    // every tile's showing of the draft, and every section's choices, which open it where the draft names one
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
    // what a genre of one's own is, shown under it when it is chosen; never a preset's, which says what the preset sets
    // and would no longer once the draft changes it
    const description = document.createElement("textarea");
    description.className = "genre-description";
    description.rows = 2;
    description.maxLength = MAX_GENRE_DESCRIPTION;
    description.placeholder = "What it is, in a sentence, shown under it when it is chosen";
    description.setAttribute("aria-label", "Genre description");
    description.addEventListener("input", () => (draft.description = description.value));
    const header = document.createElement("header");
    header.append(name, button("×", "Close the editor", () => {
        if (mayLeave()) dialog.close();
    }, "genre-close"));
    // Escape as the × is, the window over the editor's own Escape left to it
    dialog.addEventListener("cancel", (event) => {
        if (event.target === dialog && !mayLeave()) event.preventDefault();
    });

    /** Whether the editor may close: nothing changed since the draft was opened, played or saved, or its loss confirmed. */
    function mayLeave() {
        return asKept() === kept || confirm("Close the editor and lose the changes not played or saved?");
    }

    const asKept = () => JSON.stringify({ name: draft.name, description: draft.description, choices: draft.choices });

    // --- the parts --------------------------------------------------------------------------------------------------------

    function html(tag, className, text) {
        const element = document.createElement(tag);
        if (className) element.className = className;
        if (text !== undefined) element.textContent = text;
        return element;
    }

    /** A section that folds, open where the draft names one of its choices as the editor opens. */
    function section(title, names, ...content) {
        const element = document.createElement("details");
        element.className = "genre-section";
        const summary = document.createElement("summary");
        summary.textContent = title;
        element.dataset.section = title;
        element.append(summary, ...content);
        sections.push({ element, names });
        return element;
    }

    // the window a choice is changed in (genre-tune.js), over the editor
    const tune = createGenreTune();

    /** Every option of a choice, named as the songs have it. */
    const asTheyAre = (choice) => Object.fromEntries(choice.options.map((x) => [x.name, ownOf(x)]));

    /**
     *     A choice as the genre starts it, as the songs have it: a pool every option the songs play and it may keep, at
     *     their own weights, a part's instruments the commonest of them alone; every other kind each of its options.
     */
    function startOf(allowed) {
        return (choice) => {
            if (choice.kind !== "pool") return asTheyAre(choice);
            const common = choice.name.startsWith("instrument.") ? (x) => endsOf(ownOf(x))[1] >= COMMON_WEIGHT : (x) => x.own.some((y) => y > 0);
            const kept = choice.options.filter((x) => allowed(x) && common(x));
            // a pool the songs play none of, a genre's room, starts from every option it allows, as one added is kept
            if (kept.length === 0) return Object.fromEntries(choice.options.filter(allowed).map((x) => [x.name, ADDED_WEIGHT]));
            return Object.fromEntries(kept.map((x) => [x.name, ownOf(x)]));
        };
    }

    /**
     *     A choice's tile: its name, how many options the genre keeps, its Override checkbox, and its graph, which opens the
     *     window that changes it, with what each end of the songs draws most under it; those options the genre may keep,
     *     all of them, or as the draft allows them, such as the drums of the pieces chosen; and, a part's, what of it it
     *     sets, such as its instruments, its title, the window's after the part's.
     */
    function tile(allowed = () => true, what = null) {
        const element = html("div", "genre-tile");
        const title = html("h4");
        const count = html("span", "genre-count");
        const check = html("label", "genre-override");
        const input = document.createElement("input");
        input.type = "checkbox";
        check.append(input, "Override");
        const head = html("div", "genre-tile-head");
        head.append(title, count, check);
        // what the choice does, under its name
        const about = html("p", "genre-tile-about");
        const graph = html("button", "genre-graph");
        graph.type = "button";
        const chart = svg("svg", { viewBox: `0 0 ${GRAPH_WIDTH} ${GRAPH_HEIGHT}`, preserveAspectRatio: "none", "aria-hidden": "true" });
        graph.append(chart);
        const ends = html("div", "genre-ends");
        element.append(head, about, graph, ends);
        let choice = null;
        const start = startOf(allowed);
        // a part's choice titled by the part and what of it it sets, as its window is
        const titleOf = (x) => (what ? `${x.label} – ${what}` : x.label);

        input.addEventListener("change", () => {
            if (input.checked) draft.choices[choice.name] = unchecked.get(choice.name) ?? start(choice);
            else {
                unchecked.set(choice.name, draft.choices[choice.name]);
                delete draft.choices[choice.name];
            }
            changed();
        });
        graph.addEventListener("click", () => tune.open(choice, draft.choices[choice.name] ?? null, { allowed, start, title: titleOf(choice) }, (amounts) => {
            // values leaning none are as the songs have them
            if (amounts === null || (choice.kind === "values" && Object.keys(amounts).length === 0)) {
                if (choice.name in draft.choices) unchecked.set(choice.name, draft.choices[choice.name]);
                delete draft.choices[choice.name];
            } else draft.choices[choice.name] = amounts;
            changed();
        }));

        function draw() {
            // a tile of a tab not shown yet is bound as it is
            if (choice === null) return;
            chart.replaceChildren();
            ends.replaceChildren();
            // named by its choice before its graph comes, or where none does, and by its ends once it has them
            graph.setAttribute("aria-label", `${input.checked ? "Change" : "Override"} ${choice.label}`);
            const preview = previews?.choices?.[choice.name];
            if (!preview) return;
            if (choice.kind === "pool") {
                const names = byMiddle(preview.shares);
                drawShares(chart, preview.shares, names, (x) => COLOURS[names.indexOf(x) % COLOURS.length], labelOf(choice), GRAPH_WIDTH, GRAPH_HEIGHT);
            } else if (choice.kind === "range") drawBand(chart, choice, preview.curves, GRAPH_WIDTH, GRAPH_HEIGHT);
            else {
                const names = Object.keys(preview.curves);
                drawLines(chart, preview.curves, names, [0, 100], (x) => COLOURS[names.indexOf(x) % COLOURS.length], labelOf(choice), GRAPH_WIDTH, GRAPH_HEIGHT);
            }
            // the most of each end alone, on one line
            const words = endsInWords(choice, preview, 1);
            // the words cut short where the three do not fit, the number never
            ends.append(...ENDS.map((end, i) => {
                const told = words[i][0] ?? { words: choice.kind === "values" ? "default" : "none", number: "" };
                const span = html("span", "");
                span.append(html("span", "genre-end-words", `${end}: ${told.words}`), html("span", "genre-end-number", told.number === "" ? "" : ` ${told.number}`));
                span.title = span.textContent;
                return span;
            }));
            graph.setAttribute("aria-label", `${input.checked ? "Change" : "Override"} ${choice.label}: ${[...ends.children].map((x) => x.textContent).join("; ")}`);
        }

        return {
            element,
            draw,
            bind(bound) {
                choice = bound;
                element.dataset.choice = choice.name;
                title.textContent = titleOf(choice);
                about.textContent = choice.description;
                input.checked = choice.name in draft.choices;
                input.setAttribute("aria-label", `Override ${choice.label}`);
                element.classList.toggle("own", !input.checked);
                const kept = Object.keys(draft.choices[choice.name] ?? {}).length;
                const noun = { pool: "option", chances: "chance", values: "value" }[choice.kind];
                count.textContent = input.checked && noun ? `${kept} ${noun}${kept === 1 ? "" : "s"}` : "";
                draw();
            },
        };
    }

    // every tile shown, whose graph is drawn again as the previews come
    const tiles = [];

    /** A tile bound to a choice by the part its panel shows; the choice's name given by the part. */
    function bound(made, choiceOf) {
        tiles.push(made);
        return { element: made.element, show: (part) => made.bind(choices.get(choiceOf(part))) };
    }

    /** A tile of one choice, shown as the draft changes. */
    function single(made, choiceName) {
        tiles.push(made);
        shows.push(() => made.bind(choices.get(choiceName)));
        return made.element;
    }

    /**
     *     Tabs, every tab a part, over panels of fields: tabs of the same panel share one set of fields, bound to the tab's
     *     part, so that switching tabs keeps every field where it is. A tab marked where the draft names one of its choices,
     *     and shown only while its part may play, the first shown in place of one that no longer is.
     */
    function tabs(label, items) {
        const element = document.createElement("div");
        element.className = "genre-tabs";
        const strip = document.createElement("div");
        strip.className = "tab-strip";
        strip.setAttribute("role", "tablist");
        strip.setAttribute("aria-label", label);
        let current = 0;
        const panels = [...new Set(items.map((x) => x.panel))];
        for (const panel of panels) {
            panel.element.classList.add("tab-panel");
            panel.element.setAttribute("role", "tabpanel");
            panel.element.id ||= `genre-panel-${++tabPanels}`;
        }
        const buttons = items.map((item, i) => {
            const tab = document.createElement("button");
            tab.type = "button";
            tab.setAttribute("role", "tab");
            tab.id = `${item.panel.element.id}-tab-${i}`;
            tab.setAttribute("aria-controls", item.panel.element.id);
            tab.textContent = item.label;
            tab.addEventListener("click", () => {
                current = i;
                show();
            });
            return tab;
        });
        // the arrows, Home and End move among the tabs shown, as a tab strip's keys do, the tab taking its panel
        strip.addEventListener("keydown", (event) => {
            const shown = buttons.map((_, i) => i).filter((i) => !buttons[i].hidden);
            const at = shown.indexOf(current);
            const to = { ArrowRight: shown[(at + 1) % shown.length], ArrowLeft: shown[(at - 1 + shown.length) % shown.length], Home: shown[0], End: shown[shown.length - 1] }[event.key];
            if (to === undefined) return;
            event.preventDefault();
            current = to;
            show();
            buttons[current].focus();
        });
        strip.append(...buttons);
        element.append(strip, ...panels.map((x) => x.element));

        function show() {
            const shown = items.map((x) => x.isShown());
            if (!shown[current]) current = shown.indexOf(true);
            for (const panel of panels) panel.element.hidden = current < 0 || panel !== items[current].panel;
            if (current >= 0) items[current].panel.show(items[current].part);
            if (current >= 0) items[current].panel.element.setAttribute("aria-labelledby", buttons[current].id);
            buttons.forEach((tab, i) => {
                tab.hidden = !shown[i];
                tab.setAttribute("aria-selected", String(i === current));
                // the one chosen alone in the page's tab order, the others reached by the arrows
                tab.tabIndex = i === current ? 0 : -1;
                tab.classList.toggle("named", items[i].names.some((x) => x in draft.choices));
            });
        }

        return { element, show };
    }

    /** A panel: tiles, each bound to its part's choice, shown for the part given. */
    function panel(...parts) {
        const element = html("div", "genre-tiles");
        element.append(...parts.map((x) => x.element));
        return { element, show: (part) => parts.forEach((x) => x.show(part)) };
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

    /** Whether a setup is one the pieces chosen make, as the server tags them: the kit's or percussion's with one of its own, both with one of each. */
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

    // the choices about one part alone, which its tab shows after its instruments, rhythm and feel
    const PART_CHOICES = {
        melody: ["melodyRange", "sung", "hook", "sequence", "voice", "ornaments", "tension", "appoggiatura"],
        bass: ["bass", "octaveBass", "alternatingBass", "glide", "glideRate", "bassLanding", "figure.bass"],
        riff: ["riff", "riffScale", "figure.riff"],
        chords: ["figure.chords", "arpeggio.chords"],
        rhythm: ["arpeggio.rhythm"],
        solo: ["soloAnswers"],
        drums: ["fills", "kickOut", "fillFeet", "hatRolls", "hatRollRate", "ghosts", "ghostNotes", "rudiments", "drumJob", "marks", "drumSounds", "drumStrokes", "figure.ground", "figure.backbeat", "figure.time", "figure.claves", "figure.comp"],
    };
    /** A part's own choice's tile in its tab, bound as the draft changes whatever the part. */
    const own = (name) => ({ element: single(tile(), name), show: () => {} });
    const pitchedTiles = () => [
        bound(tile(undefined, "Instruments"), (part) => `instrument.${part}`),
        bound(tile(undefined, "Rhythm"), (part) => `rhythm.${part}`),
        bound(tile(undefined, "Feel"), (part) => `feel.${part}`),
    ];
    const pitched = panel(...pitchedTiles());
    // a part with choices of its own a panel of its own, the others sharing one
    const panels = Object.fromEntries(["melody", "bass", "riff", "chords", "rhythm"].map((part) => [part, panel(...pitchedTiles(), ...PART_CHOICES[part].map(own))]));
    const solo = panel(bound(tile(undefined, "Instruments"), () => "instrument.solo"), ...PART_CHOICES.solo.map(own));
    const role = panel(
        bound(tile(isPieceAllowed, "Drums"), (x) => `drumRole.${x}`),
        bound(tile(undefined, "Rhythm"), (x) => `rhythm.${x}`),
        bound(tile(undefined, "Feel"), (x) => `feel.${x}`),
    );
    const roles = tabs("Drums' roles", ROLES.map(([part, label]) => ({ label, part, names: [`drumRole.${part}`, `rhythm.${part}`, `feel.${part}`], panel: role, isShown: () => true })));
    const drums = panel(
        bound(tile(), () => "kit"),
        bound(tile(), () => "drumPieces"),
        bound(tile(isSetupAllowed), () => "drumSetup"),
        ...PART_CHOICES.drums.map(own),
        { element: roles.element, show: () => roles.show() },
    );
    /** Whether the lineup may play a part: every part where the genre leaves it as by default, else one it keeps above 0. */
    function mayPlay(option) {
        const lineup = draft.choices.parts;
        return !lineup || (option in lineup && endsOf(lineup[option]).some((x) => x > 0));
    }

    const parts = tabs("Parts", [
        ...PITCHED.map(([part, label]) => ({
            label,
            part,
            // a soloist's line is its own, with no rhythm or feel of a part's
            names: part === "solo" ? ["instrument.solo", ...PART_CHOICES.solo] : [`instrument.${part}`, `rhythm.${part}`, `feel.${part}`, ...(PART_CHOICES[part] ?? [])],
            panel: part === "solo" ? solo : panels[part] ?? pitched,
            isShown: () => mayPlay(part),
        })),
        {
            label: "Drums",
            part: "drums",
            names: ["kit", "drumPieces", "drumSetup", ...PART_CHOICES.drums, ...ROLES.flatMap(([role]) => [`drumRole.${role}`, `rhythm.${role}`, `feel.${role}`])],
            panel: drums,
            isShown: () => mayPlay("drum"),
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
        "A choice overridden is the genre's; one that is not keeps its default, as with no genre.",
        "Click a graph to change its choice: the options the genre keeps, and each one's amounts in the plainest, middling and experimental songs.",
        "An option left out never plays. At 0 in the plainest and middling songs it plays only in the experimental ones. A chance at 100% everywhere, or the only option kept, plays in every song.",
        "A weight is a share among the options kept; a chance is out of 100 songs.",
        "A part the lineup leaves out, or keeps at 0 everywhere, never plays, so it has no tab under Parts.",
    ].map((text) => {
        const item = document.createElement("li");
        item.textContent = text;
        return item;
    }));
    principles.append(principlesSummary, principlesList);

    const body = document.createElement("div");
    body.className = "genre-body";
    // every name of the parts' choices, a name the server lacks never among a draft's, as a soloist has an instrument alone
    const partNames = [...PITCHED.flatMap(([part]) => [`instrument.${part}`, `rhythm.${part}`, `feel.${part}`]), ...Object.values(PART_CHOICES).flat(), "kit", "drumPieces", "drumSetup",
        ...ROLES.flatMap(([role]) => [`drumRole.${role}`, `rhythm.${role}`, `feel.${role}`])];
    body.append(
        description,
        principles,
        section("Time and feel", ["tempo", "tempoBand", "length", "darkness", "intensity", "meter", "feel", "feelChances", "timeChanges", "feelValues", "grooveHold", "pins", "pocket", "sectionChanges", "polymeter", "polymeterCell"],
            single(tile(), "tempo"), single(tile(), "tempoBand"), single(tile(), "length"), single(tile(), "darkness"), single(tile(), "intensity"), single(tile(), "meter"), single(tile(), "feel"), single(tile(), "feelChances"), single(tile(), "timeChanges"), single(tile(), "feelValues"), single(tile(), "grooveHold"), single(tile(), "pins"), single(tile(), "pocket"), single(tile(), "sectionChanges"), single(tile(), "polymeter"), single(tile(), "polymeterCell")),
        section("Form", ["formChances", "form", "phrases", "phraseBars", "sectionLength", "roleLength", "phraseScheme", "texture", "alone", "loop", "drop", "dropKind", "mutation", "mixFilter", "breakdown", "callAndResponse", "lineRun", "stopTime", "stops", "ensembleHits", "surfDrop", "push", "rubato", "keyChanges", "keyLift", "sectionKey", "intro", "introWindow", "openingAlone", "openingPart", "ending", "lastRepeat", "riffSection", "solos", "soloCount", "soloShare", "soloists", "soloLanding", "soloStyle", "drumSoloFeet", "sticking"], single(tile(), "formChances"), single(tile(), "form"), single(tile(), "phrases"), single(tile(), "phraseBars"), single(tile(), "sectionLength"), single(tile(), "roleLength"), single(tile(), "phraseScheme"), single(tile(), "texture"), single(tile(), "alone"), single(tile(), "loop"), single(tile(), "drop"), single(tile(), "dropKind"), single(tile(), "mutation"), single(tile(), "mixFilter"), single(tile(), "breakdown"), single(tile(), "callAndResponse"), single(tile(), "lineRun"), single(tile(), "stopTime"), single(tile(), "stops"), single(tile(), "ensembleHits"), single(tile(), "surfDrop"), single(tile(), "push"), single(tile(), "rubato"), single(tile(), "keyChanges"), single(tile(), "keyLift"), single(tile(), "sectionKey"), single(tile(), "intro"), single(tile(), "introWindow"), single(tile(), "openingAlone"), single(tile(), "openingPart"), single(tile(), "ending"), single(tile(), "lastRepeat"), single(tile(), "riffSection"), single(tile(), "solos"), single(tile(), "soloCount"), single(tile(), "soloShare"), single(tile(), "soloists"), single(tile(), "soloLanding"), single(tile(), "soloStyle"), single(tile(), "drumSoloFeet"), single(tile(), "sticking")),
        section("Harmony", ["scale", "chordShape", "arpeggio", "colour", "rootMotion", "rootDegree", "strictness", "sectionHome", "cadenceDegree", "cadenceRaise", "chordScales", "ownMode", "harmonicRhythm", "vamp", "turnaround", "passingChords", "passingKind", "planing", "harmonyDevices", "reharmonisation", "sectionPedal", "lineScale"], single(tile(), "scale"), single(tile(), "chordShape"), single(tile(), "arpeggio"), single(tile(), "colour"), single(tile(), "rootMotion"), single(tile(), "rootDegree"), single(tile(), "strictness"), single(tile(), "sectionHome"), single(tile(), "cadenceDegree"), single(tile(), "cadenceRaise"), single(tile(), "chordScales"), single(tile(), "ownMode"), single(tile(), "harmonicRhythm"), single(tile(), "vamp"), single(tile(), "turnaround"), single(tile(), "passingChords"), single(tile(), "passingKind"), single(tile(), "planing"), single(tile(), "harmonyDevices"), single(tile(), "reharmonisation"), single(tile(), "sectionPedal"), single(tile(), "lineScale")),
        section("Sound", ["effects", "bendTiming", "vibratoWidth", "gateParts", "articulation", "tremoloSpeed", "articulationModes", "strongEffect", "echo", "room", "insertion", "pan", "register", "levels", "rests", "doubling", "lineChords", "harmonyInterval", "instrumentSwitches", "louderForms", "space", "dynamics"], single(tile(), "effects"), single(tile(), "bendTiming"), single(tile(), "vibratoWidth"), single(tile(), "gateParts"), single(tile(), "articulation"), single(tile(), "tremoloSpeed"), single(tile(), "articulationModes"), single(tile(), "strongEffect"), single(tile(), "echo"), single(tile(), "room"), single(tile(), "insertion"), single(tile(), "pan"), single(tile(), "register"), single(tile(), "levels"), single(tile(), "rests"), single(tile(), "doubling"), single(tile(), "lineChords"), single(tile(), "harmonyInterval"), single(tile(), "instrumentSwitches"), single(tile(), "louderForms"), single(tile(), "space"), single(tile(), "dynamics")),
        section("Parts", ["parts", ...partNames], single(tile(), "parts"), parts.element),
    );

    // --- the footer: the problem, as JSON, and what to do with the draft -----------------------------------------------------

    const problem = document.createElement("p");
    problem.className = "warning";
    problem.setAttribute("role", "alert");
    problem.hidden = true;

    const exported = button("Export JSON", "Save this genre as a JSON file", () => {
        const genre = finishedOrDraft();
        // a choice keeping none beside its parts' own, which a file of families cannot hold apart from them, refused
        // rather than written as another genre
        const lost = Object.keys(genre.choices).find((x) => !x.includes(".") && Object.keys(genre.choices[x]).length === 0 && Object.keys(genre.choices).some((y) => y.startsWith(`${x}.`)));
        if (lost !== undefined) showProblem(`${choices.get(lost)?.label ?? lost} keeps none of its options beside its parts' own, which a file cannot hold: give it one or take it out before exporting.`, false);
        else downloadJson(`${genre.name.trim() || "genre"}.json`, asFile(genre));
    }, "genre-export");

    const file = jsonFileInput((text) => {
        // a choice the server knows, and an option of it
        const read = readImport(text, (choice, option) => choices.has(choice) && (option === undefined || choices.get(choice).options.some((x) => x.name === option)), (name) => choices.get(name));
        // a list of genres, every one of them, kept among one's own, none where one is no genre the server plays, the
        // earlier word of an import let go of; one genre opened as the draft, its problem said
        if (read?.genres && read.refusal === null) {
            told.textContent = `${onKeepAll(read.genres)} kept among yours.`;
            told.hidden = false;
            showProblem();
            return;
        }
        if (read?.genre) {
            // a genre of its own, which a save keeps beside the one the editor was opened on
            origin = null;
            load(read.genre);
            return;
        }
        told.hidden = true;
        showProblem(read === null ? NOT_A_GENRE : read.refusal, false);
    });
    const imported = button("Import JSON", "Open a genre saved as a JSON file, or keep every one of a list of them", () => file.click());

    /** A genre as a file writes it, its choices in families, in the editor's order. */
    const asFile = (genre) => ({ ...genre, choices: nestedChoices(genre.choices, [...choices.keys()]) });

    // what was done that is no problem, such as genres imported
    const told = document.createElement("p");
    told.className = "genre-told";
    told.setAttribute("role", "status");
    told.hidden = true;

    const play = button("Play", "Ask for the song again in this genre", () => keep(onPlay), "primary");
    // in place of the genre the draft was opened from, renamed as it now is; another of one's own under its new name
    // replaced only once that is confirmed
    const save = button("Save", "Keep this genre among yours, and play it", () => {
        const named = draft.name.trim();
        if (isOwn(named) && !(origin !== null && sameName(origin, named)) && !confirm(`Replace your genre ${named}?`)) return;
        keep((genre) => onSave(genre, origin));
        origin = named;
        showProblem();
    });

    /** The draft played or saved, as the page says, and kept so. */
    function keep(action) {
        action(finished());
        kept = asKept();
    }

    // under a name asked for, one's own of that name replaced only once that is confirmed, a preset's never; the genre
    // the draft came from kept as it was
    const saveAs = button("Save as…", "Keep this genre among yours under another name, and play it", () => {
        const asked = prompt("Save as", draft.name.trim())?.trim();
        if (!asked) return;
        draft.name = asked;
        name.value = asked;
        showProblem();
        // a name the draft cannot take, too long say, saves nothing
        if (problemOf(draft) !== null || isPresetName(asked) || (isOwn(asked) && !confirm(`Replace your genre ${asked}?`))) return;
        keep((genre) => onSave(genre, null));
        origin = asked;
        showProblem();
    });

    // the genre the draft was opened from, which nothing brings back, once that is confirmed
    const remove = button("Delete", "Let go of this genre of yours", () => {
        if (confirm(`Delete your genre ${origin}? Nothing brings it back.`)) onDelete(origin);
    });
    const footer = document.createElement("footer");
    footer.append(problem, told, file, imported, exported, remove, saveAs, save, play);
    dialog.append(header, body, footer, tune.element);

    // --- the draft ------------------------------------------------------------------------------------------------------------

    /** A genre as the draft, its sections open where it names a choice. */
    function load(genre) {
        draft = { name: genre.name, description: genre.description ?? "", choices: structuredClone(genre.choices) };
        unchecked = new Map();
        // and any previews still on their way let go of, which were of the draft before
        previews = null;
        previewsAsked++;
        for (const { element, names } of sections) element.open = names.some((x) => x in draft.choices);
        changed();
    }

    function changed() {
        prune();
        name.value = draft.name;
        description.value = draft.description;
        for (const show of shows) show();
        showProblem();
        clearTimeout(waiting);
        waiting = setTimeout(askPreviews, PREVIEW_WAIT_MS);
    }

    /** What the songs draw of every choice of the draft, the graphs drawn again as it comes; none for a draft that is no genre. */
    async function askPreviews() {
        if (problemOf(draft) !== null) return;
        // the latest asked for alone drawn, a slower earlier answer let go of
        const asked = ++previewsAsked;
        try {
            const response = await fetch(new URL("api/genres/previews", location.href), {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(draft.choices),
            });
            if (!response.ok || asked !== previewsAsked) return;
            const answered = await response.json();
            if (asked !== previewsAsked) return;
            previews = answered;
        } catch {
            // the graphs as they were
            return;
        }
        for (const made of tiles) made.draw();
    }

    /** The draft as it is played or kept; none if it is not a genre. */
    function finished() {
        const result = finishedOrDraft();
        return problemOf(result) === null ? result : null;
    }

    /** The draft as it is kept and exported: its name and description trimmed, the latter only where it says something. */
    function finishedOrDraft() {
        const said = draft.description.trim();
        return { name: draft.name.trim(), ...(said === "" ? {} : { description: said }), choices: structuredClone(draft.choices) };
    }

    // what the server would refuse the genre for (genreProblem), by its choices
    const problemOf = (genre) => genreProblem(genre, (name) => choices.get(name));

    /**
     * Says the draft's problem, or another, a file refused, which leaves the draft as it was: the buttons as the draft's
     * own problem has them where the text is not the draft's.
     */
    function showProblem(text = problemOf(draft), isTheDrafts = true) {
        const drafts = isTheDrafts ? text : problemOf(draft);
        // a preset's name kept for the preset, the draft played under it but saved under another
        const named = isPresetName(draft.name) ? `${draft.name.trim()} is a preset's name: save yours under another.` : null;
        const said = text ?? named;
        problem.textContent = said ?? "";
        problem.hidden = said === null;
        play.disabled = saveAs.disabled = drafts !== null;
        save.disabled = (drafts ?? named) !== null || draft.name.trim() === "";
        remove.disabled = origin === null;
    }

    return {
        element: dialog,
        /**
         * Opens the editor on a genre, a copy of it the draft, by the choices the server lists; its sections open where it
         * names a choice. One of one's own is saved in its place, any other, whatever its name, as a genre of its own.
         */
        open(genre, choiceList, isOwnGenre) {
            choices = new Map(choiceList.map((x) => [x.name, x]));
            origin = isOwnGenre ? genre.name.trim() : null;
            told.hidden = true;
            load(genre);
            kept = asKept();
            dialog.showModal();
        },
        close() {
            dialog.close();
        },
    };
}
