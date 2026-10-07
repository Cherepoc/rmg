import QUESTIONS from "./listening.json";

/**
 *     The page's listening mode, `?listen=` a question's id, on a server that listens (a test instance): a device heard
 *     with and without over a few songs, A and B swapped from song to song so that neither is always the one with, and a
 *     verdict on each. The questions are written by the tests (`ListeningQuestionsTest`), each its songs, the genre they
 *     are made in, and what the song without leaves out: stages of it and choices of its genre.
 */

/** The question of the id, none for none or one not asked. */
export function listeningQuestion(id) {
    return id === null ? null : QUESTIONS.find((x) => x.id === id) ?? null;
}

/** What a verdict says, as the server keeps it, by the button's choice. */
const CHOICES = [
    ["A", "A is better"],
    ["B", "B is better"],
    ["same", "The same"],
    ["unsure", "Can't tell"],
];

/**
 *     The strip asking the question: a song at a time, A and B played on asking, and a verdict, which goes on to the
 *     next song. `play` is given the song's seed and whether it is the one without; `verdict` the seed and the choice,
 *     "with", "without", "same" or "unsure".
 */
export function createListeningStrip(question, { play, verdict }) {
    let at = 0;
    const strip = document.createElement("section");
    strip.className = "panel listening";
    strip.setAttribute("aria-label", "Listening");

    const asked = document.createElement("p");
    asked.className = "listening-question";
    asked.textContent = question.question;
    const where = document.createElement("p");
    where.className = "listening-where";
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
        play(question.seeds[at], isWithout(letter));
    }, "primary"));
    const verdicts = CHOICES.map(([choice, text]) => button(text, () => {
        verdict(question.seeds[at], choice === "A" || choice === "B" ? (isWithout(choice) ? "without" : "with") : choice);
        at++;
        show();
    }));

    const controls = document.createElement("div");
    controls.className = "listening-controls";
    controls.append(...plays, ...verdicts);
    strip.append(asked, where, controls, heard);

    function show() {
        const done = at >= question.seeds.length;
        where.textContent = done ? `Done: ${question.seeds.length} songs heard. Thank you.` : `Song ${at + 1} of ${question.seeds.length}`;
        heard.textContent = "";
        for (const element of [...plays, ...verdicts]) element.disabled = done;
    }

    show();
    return strip;
}
