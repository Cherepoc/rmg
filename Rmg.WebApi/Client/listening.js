/**
 *     The page's listening mode, `?listen=` a question's id, on a server that listens (a test instance): a device heard
 *     with and without over a few songs, A and B swapped from song to song so that neither is always the one with, and a
 *     verdict on each. The questions are written by the tests (`ListeningQuestionsTest`), each its songs, where in each
 *     the device plays and where to start, the bar before, the genre they are made in, the choices of it the song with
 *     adds, what the song without leaves out, stages of it and choices of its genre, and the settings each side gives its
 *     songs, a mood given. They are served beside the page, `listening.json`, and fetched only where the page listens.
 */

import { minutesAndSeconds } from "./text.js";

/** The questions, as the server serves them beside the page. */
export async function loadQuestions() {
    const response = await fetch(new URL("listening.json", location.href));
    if (!response.ok) throw new Error(`The listening questions could not be fetched: ${response.status}`);
    return response.json();
}

/**
 *     A genre's choices with a question's patch of one side laid over them, the device taken out for the song without it
 *     or added for the song with it: every option the patch names its amount, and the genre's other options of the
 *     choice as they are, so that the two songs differ by the device alone.
 */
export function patchedChoices(choices, patch) {
    const patched = { ...choices };
    for (const [choice, options] of Object.entries(patch)) patched[choice] = { ...(choices[choice] ?? {}), ...options };
    return patched;
}

/** The settings a question's side, `with` or `without`, gives its songs, each given at its value; none for a side giving none. */
export function givenSettings(side) {
    return Object.fromEntries(Object.entries(side?.settings ?? {}).map(([name, value]) => [name, { isGiven: true, value }]));
}

/** The question of the id, none for none or one not asked. */
export function listeningQuestion(questions, id) {
    return id === null ? null : questions.find((x) => x.id === id) ?? null;
}

/**
 *     The index of every question, `?listen` with no question or one not asked: each its question, its genre, a link
 *     asking it, and its verdicts so far, as the server that listens counts them (`/api/listening/verdicts`), the latest
 *     version's; `verdicts` none where it does not listen, which the index says.
 */
export function createListeningIndex(questions, verdicts, genreName) {
    const index = document.createElement("section");
    index.className = "panel listening listening-index";
    index.setAttribute("aria-label", "Listening");
    const heading = document.createElement("h2");
    heading.textContent = "Listening";
    const hint = document.createElement("p");
    hint.className = "listening-where";
    hint.textContent = verdicts === null
        ? "This server makes songs whole: the listening mode is the test instance's."
        : `A device heard with and without over a few songs. Verdicts of version ${verdicts.version ?? "none yet"}.`;
    const list = document.createElement("ul");
    list.className = "listening-questions";
    const counted = new Map((verdicts?.verdicts ?? []).map((x) => [x.question, x]));
    list.append(...questions.map((question) => {
        const item = document.createElement("li");
        item.dataset.question = question.id;
        const link = document.createElement("a");
        link.href = `?listen=${encodeURIComponent(question.id)}`;
        link.textContent = question.question;
        const about = document.createElement("span");
        about.className = "listening-where";
        const count = counted.get(question.id);
        const said = count === undefined ? "no verdicts yet" : `with better ${count.with}, without ${count.without}, the same ${count.same}, can't tell ${count.unsure}`;
        about.textContent = `${question.genre === null ? "No genre" : genreName(question.genre)} · ${said}`;
        item.append(link, about);
        return item;
    }));
    index.append(heading, hint, list);
    return index;
}

/** Seconds as minutes and seconds, 1:05. */
const clock = minutesAndSeconds;

/** What a verdict says, as the server keeps it, by the button's choice. */
const CHOICES = [
    ["A", "A is better"],
    ["B", "B is better"],
    ["same", "The same"],
    ["unsure", "Can't tell"],
];

/**
 *     The strip asking the question: a song at a time, the parts to listen to named, A and B played on asking, an answer chosen, shown pressed and
 *     changed at will, and Next, which tells it and goes on to the next song, each started a moment before where its device
 *     plays; and back to the song before, its answer pressed, to change it, which the server counts in place of the
 *     first. `play` is given the song's seed, whether it is the one without and the second it starts at; `verdict` the
 *     seed and the choice, "with", "without", "same" or "unsure".
 */
export function createListeningStrip(question, { play, verdict }) {
    let at = 0;
    // the answer chosen on each song, by its button's choice, and the one told
    const chosen = [];
    const told = [];
    const strip = document.createElement("section");
    strip.className = "panel listening";
    strip.setAttribute("aria-label", "Listening");

    const asked = document.createElement("p");
    asked.className = "listening-question";
    asked.textContent = question.question;
    const where = document.createElement("p");
    where.className = "listening-where";
    // the parts that differ the most where to listen, as the questions' measure found them
    const focus = document.createElement("p");
    focus.className = "listening-focus";
    const heard = document.createElement("p");
    heard.className = "listening-heard";
    heard.setAttribute("role", "status");

    const button = (text, action, className = "secondary") => {
        const element = document.createElement("button");
        element.type = "button";
        element.textContent = text;
        element.className = className;
        element.addEventListener("click", action);
        return element;
    };
    // A is the song with the device on even songs and without it on odd ones
    const isWithout = (letter) => (letter === "B") === (at % 2 === 0);
    const plays = ["A", "B"].map((letter) => button(`Play ${letter}`, () => {
        heard.textContent = `Playing ${letter}`;
        play(question.seeds[at].seed, isWithout(letter), question.seeds[at].from);
    }, "primary"));
    const answers = CHOICES.map(([choice, text]) => {
        const element = button(text, () => {
            chosen[at] = choice;
            show();
        });
        element.dataset.choice = choice;
        return element;
    });
    const back = button("Back", () => {
        at--;
        show();
    });
    const next = button("Next", () => {
        const choice = chosen[at];
        // told once, and again only where it was changed
        if (told[at] !== choice) {
            verdict(question.seeds[at].seed, choice === "A" || choice === "B" ? (isWithout(choice) ? "without" : "with") : choice);
            told[at] = choice;
        }
        at++;
        show();
    }, "primary");

    const row = (...elements) => {
        const controls = document.createElement("div");
        controls.className = "listening-controls";
        controls.append(...elements);
        return controls;
    };
    const choices = row(...answers);
    choices.setAttribute("role", "group");
    choices.setAttribute("aria-label", "Your answer");
    strip.append(asked, where, focus, row(...plays), choices, row(back, next), heard);

    function show() {
        const done = at >= question.seeds.length;
        where.textContent = done
            ? `Done: ${question.seeds.length} songs heard. Thank you.`
            : `Song ${at + 1} of ${question.seeds.length} · listen at ${clock(question.seeds[at].at)}, A and B starting a bar before`;
        focus.textContent = done || !question.seeds[at].listen?.length ? "" : `Listen to: ${question.seeds[at].listen.join(", ")}`;
        heard.textContent = "";
        for (const element of [...plays, ...answers]) element.disabled = done;
        for (const element of answers) element.setAttribute("aria-pressed", String(!done && chosen[at] === element.dataset.choice));
        next.disabled = done || chosen[at] === undefined;
        back.disabled = at === 0;
    }

    show();
    return strip;
}
