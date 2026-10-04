import { endsOf, ownOf, written } from "./genres.js";

/**
 *     The genre editor's fine-tune window, over the editor: one choice's options kept, each its amounts in the plainest
 *     songs, the middling and the wildest, drawn on a chart of them across how experimental a song is, the one chosen
 *     moved by its three handles, up and down or by the arrow keys, or by its numbers, or shaped at once; and, for a pool,
 *     what the songs draw of each, the chance of a song drawing it, beside. Every line is the server's, as the songs
 *     draw it (/api/genres/preview), the handles' straight between them until it comes. Done gives the amounts back to
 *     the editor; closing lets them go.
 */

// the least and the most of an option's amounts, by its choice's kind and its name (GenreAmounts)
function rangeOf(choice, option) {
    if (choice.kind !== "range") return [0, 100];
    return option === "lean" ? [-100, 100] : [60, 240];
}

// the chart's size in its own units, the plainest at the left and the wildest at the right
const WIDTH = 520;
const HEIGHT = 200;

// the other options' lines and areas, in greys told apart by their lightness, the one chosen in the accent
const GREYS = ["#8f93a3", "#c4c7d3", "#a9adbb", "#e3e4ea", "#787c8c", "#d4d6df"];

const SVG = "http://www.w3.org/2000/svg";

/** How long the window waits after a change before it asks for the lines again. */
const PREVIEW_WAIT_MS = 150;

const ENDS = ["Plainest", "Middling", "Wildest"];

function svg(name, attributes = {}) {
    const element = document.createElementNS(SVG, name);
    for (const [key, value] of Object.entries(attributes)) element.setAttribute(key, String(value));
    return element;
}

function html(name, attributes = {}, ...children) {
    const element = document.createElement(name);
    for (const [key, value] of Object.entries(attributes)) {
        if (key === "text") element.textContent = value;
        else element.setAttribute(key, value);
    }
    element.append(...children);
    return element;
}

/**
 *     The shapes an option's amounts take at once, from its amount in a middling song, or its greatest where that is 0:
 *     the plain end's alone, leaning plainer, alike everywhere, leaning wilder, and the wild end's alone, each leaning
 *     four times either way.
 */
function shapesOf([plain, middle, wild], most) {
    const base = middle || Math.max(plain, wild) || Math.round(most / 2);
    const capped = (x) => Math.min(most, Math.round(x));
    return [
        ["Plain only", [base, base, 0]],
        ["Plainer", [capped(base * 4), base, capped(base / 4)]],
        ["Everywhere", [base, base, base]],
        ["Wilder", [capped(base / 4), base, capped(base * 4)]],
        ["Experimental only", [0, 0, Math.max(wild, base)]],
    ];
}

export function createGenreTune() {
    let choice = null;
    let amounts = {};
    let selected = null;
    let preview = null;
    let done = null;
    let waiting = null;
    // a handle being dragged, and its end
    let dragging = null;

    const dialog = html("dialog", { class: "genre-tune" });
    const title = html("h2");
    const close = html("button", { type: "button", "aria-label": "Close without keeping", class: "genre-close", text: "×" });
    close.addEventListener("click", () => dialog.close());
    const header = html("header", {}, title, close);

    const list = html("div", { class: "tune-list", role: "group", "aria-label": "Options" });
    const heading = html("h3");
    const chart = svg("svg", { viewBox: `-12 -12 ${WIDTH + 24} ${HEIGHT + 24}`, class: "tune-chart", role: "group" });
    const axisHigh = html("span");
    const axisLow = html("span");
    const axis = html("div", { class: "tune-axis", "aria-hidden": "true" }, axisHigh, axisLow);
    const ends = html("div", { class: "tune-ends", "aria-hidden": "true" }, ...ENDS.map((x) => html("span", { text: x })));
    const fields = ENDS.map((end) => {
        const input = html("input", { type: "number", step: "1" });
        const label = html("label", {}, end, input);
        return { label, input };
    });
    const shapes = html("div", { class: "tune-shapes", role: "group", "aria-label": "Shapes" });
    const sharesHeading = html("h3", { text: "The chance a song draws each" });
    const shares = svg("svg", { viewBox: `0 0 ${WIDTH} ${HEIGHT}`, preserveAspectRatio: "none", class: "tune-shares", role: "img" });
    const legend = html("div", { class: "tune-legend" });
    const sharesNote = html("p", { class: "tune-note", text: "As the songs draw it, before a section or a role leans it." });
    const sharesBox = html("div", { class: "tune-shares-box" }, sharesHeading, html("div", { class: "tune-shares-row" }, shares, legend), sharesNote);
    const main = html("div", { class: "tune-main" },
        heading,
        html("div", { class: "tune-chart-row" }, axis, chart),
        ends,
        html("div", { class: "tune-fields" }, ...fields.map((x) => x.label), shapes),
        sharesBox,
    );
    const body = html("div", { class: "tune-body" }, list, main);

    const reset = html("button", { type: "button", text: "Back to the songs' own" });
    const finish = html("button", { type: "button", class: "primary", text: "Done" });
    const footer = html("footer", {}, reset, html("span", { class: "tune-gap" }), finish);
    dialog.append(header, body, footer);

    reset.addEventListener("click", () => {
        for (const name of Object.keys(amounts)) amounts[name] = ownOf(optionOf(name));
        changed();
    });
    finish.addEventListener("click", () => {
        done?.(structuredClone(amounts));
        dialog.close();
    });
    fields.forEach(({ input }, end) => input.addEventListener("change", () => setEnd(end, Number(input.value))));

    const optionOf = (name) => choice.options.find((x) => x.name === name);
    const kept = () => choice.options.filter((x) => x.name in amounts);
    // the options drawn on one chart with the one chosen: those of its range, the tempo's ends together and its lean apart
    const alongside = () => kept().filter((x) => String(rangeOf(choice, x.name)) === String(rangeOf(choice, selected)));

    function setEnd(end, value) {
        const [least, most] = rangeOf(choice, selected);
        const points = [...endsOf(amounts[selected])];
        points[end] = Math.max(least, Math.min(most, Math.round(value)));
        amounts[selected] = written(points);
        if (dragging === null) changed();
        else follow(points);
    }

    /**
     *     While a handle is dragged, the handles, the line chosen and the numbers alone follow it, the line straight between
     *     the handles, so that the handle held stays the one dragged; the whole is drawn again as it is let go.
     */
    function follow(points) {
        for (const handle of chart.querySelectorAll(".tune-handle")) {
            const value = points[Number(handle.dataset.end)];
            handle.setAttribute("cy", String(yOf(value)));
            handle.setAttribute("aria-valuenow", String(value));
        }
        chart.querySelector(".tune-line.chosen")?.setAttribute("d", `M${points.map((value, i) => `${(i * WIDTH) / 2},${yOf(value).toFixed(1)}`).join(" L")}`);
        fields.forEach(({ input }, end) => (input.value = String(points[end])));
    }

    function changed() {
        render();
        clearTimeout(waiting);
        waiting = setTimeout(askPreview, PREVIEW_WAIT_MS);
    }

    async function askPreview() {
        const asked = choice;
        try {
            const response = await fetch(new URL("api/genres/preview", location.href), {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ choice: choice.name, options: amounts }),
            });
            if (!response.ok || asked !== choice) return;
            preview = await response.json();
            // the charts alone, so that what is pressed or focused meanwhile stays where it is
            renderCharts();
        } catch {
            // without the server's lines, the handles' straight ones stand
        }
    }

    /** The y of an amount on the chart, by the range of the option chosen. */
    function yOf(value) {
        const [least, most] = rangeOf(choice, selected);
        return HEIGHT - ((value - least) / (most - least)) * HEIGHT;
    }

    function render() {
        title.textContent = `Fine-tune: ${choice.label}`;
        const [least, most] = rangeOf(choice, selected);
        const option = optionOf(selected);
        const points = endsOf(amounts[selected]);

        // the list and the shapes made once a window and changed in place, so that a click on one is never on one made
        // again under it, as a number's change does as it is left
        for (const row of list.children) {
            const [plain, middle, wild] = endsOf(amounts[row.dataset.option]);
            row.querySelector(".tune-values").textContent = middle === 0 && plain === 0 && wild > 0 ? `experimental songs only · ${wild}` : `${plain} · ${middle} · ${wild}`;
            if (row.dataset.option === selected) row.setAttribute("aria-current", "true");
            else row.removeAttribute("aria-current");
        }

        heading.textContent = choice.kind === "pool" ? `${option.label}: how common it is, against the others` : `${choice.label}: ${option.label}`;
        axisHigh.textContent = String(most);
        axisLow.textContent = String(least);

        fields.forEach(({ input }, end) => {
            input.min = String(least);
            input.max = String(most);
            input.value = String(points[end]);
            input.setAttribute("aria-label", `${option.label}, ${ENDS[end].toLowerCase()}`);
        });

        shapes.hidden = choice.kind === "range";
        shapesOf(points, most).forEach(([, shape], i) => shapes.children[i].setAttribute("aria-pressed", String(shape.every((x, end) => x === points[end]))));

        sharesBox.hidden = choice.kind !== "pool";
        renderCharts();
    }

    /** The chart of the amounts, its handles, and a pool's chances, the handle focused kept so. */
    function renderCharts() {
        const [least, most] = rangeOf(choice, selected);
        const points = endsOf(amounts[selected]);
        const focused = chart.contains(document.activeElement) ? document.activeElement.dataset.end : undefined;

        // the grid, the others' lines, the one chosen's, and its handles
        chart.replaceChildren(
            ...[0, HEIGHT / 2, HEIGHT].map((y) => svg("line", { x1: 0, y1: y, x2: WIDTH, y2: y, class: "tune-grid" })),
            ...[0, WIDTH / 2, WIDTH].map((x) => svg("line", { x1: x, y1: 0, x2: x, y2: HEIGHT, class: "tune-grid" })),
        );
        chart.setAttribute("aria-label", `${choice.label}: every option's amounts from the plainest songs to the wildest`);
        for (const other of alongside()) {
            const curve = dragging === null && preview?.curves?.[other.name];
            const along = curve
                ? curve.map((value, i) => [(i / (curve.length - 1)) * WIDTH, yOf(value)])
                : endsOf(amounts[other.name]).map((value, i) => [(i * WIDTH) / 2, yOf(value)]);
            chart.append(svg("path", {
                d: `M${along.map(([x, y]) => `${x.toFixed(1)},${y.toFixed(1)}`).join(" L")}`,
                class: other.name === selected ? "tune-line chosen" : "tune-line",
                "data-option": other.name,
            }));
        }
        points.forEach((value, end) => chart.append(handle(end, value, least, most)));
        if (focused !== undefined) chart.querySelector(`[data-end="${focused}"]`)?.focus();

        if (choice.kind === "pool") renderShares();
    }

    /** A handle of the option chosen at an end: dragged up and down, or moved by the arrow keys, a whole number a step. */
    function handle(end, value, least, most) {
        const element = svg("circle", {
            cx: (end * WIDTH) / 2,
            cy: yOf(value),
            r: 9,
            class: "tune-handle",
            tabindex: 0,
            role: "slider",
            "aria-label": `${optionOf(selected).label}, ${ENDS[end].toLowerCase()}`,
            "aria-valuemin": least,
            "aria-valuemax": most,
            "aria-valuenow": value,
            "data-end": end,
        });
        element.addEventListener("keydown", (event) => {
            const by = { ArrowUp: 1, ArrowRight: 1, ArrowDown: -1, ArrowLeft: -1, PageUp: 10, PageDown: -10 }[event.key];
            if (by === undefined) return;
            event.preventDefault();
            setEnd(end, value + by);
            chart.querySelector(`[data-end="${end}"]`)?.focus();
        });
        element.addEventListener("pointerdown", (event) => {
            dragging = end;
            element.setPointerCapture(event.pointerId);
        });
        element.addEventListener("pointermove", (event) => {
            if (dragging !== end) return;
            const box = chart.getBoundingClientRect();
            // the chart's own units are 24 more than its height, 12 above and below it
            const y = ((event.clientY - box.top) / box.height) * (HEIGHT + 24) - 12;
            setEnd(end, least + ((HEIGHT - y) / HEIGHT) * (most - least));
        });
        element.addEventListener("pointerup", () => {
            dragging = null;
            changed();
        });
        return element;
    }

    /** What the songs draw of every option, stacked from the bottom, the one chosen in the accent. */
    function renderShares() {
        shares.replaceChildren();
        legend.replaceChildren();
        const options = kept();
        const share = preview?.shares;
        if (!share || options.some((x) => !share[x.name])) return;

        const points = share[options[0].name].length;
        let below = Array(points).fill(0);
        options.forEach((x, i) => {
            const above = below.map((y, j) => y + share[x.name][j]);
            const xOf = (j) => ((j / (points - 1)) * WIDTH).toFixed(1);
            const yOf = (value) => (HEIGHT - (value / 100) * HEIGHT).toFixed(1);
            const top = above.map((y, j) => `${xOf(j)},${yOf(y)}`);
            const bottom = below.map((y, j) => `${xOf(j)},${yOf(y)}`).reverse();
            const fill = x.name === selected ? "var(--accent)" : GREYS[i % GREYS.length];
            shares.append(svg("path", { d: `M${[...top, ...bottom].join(" L")} Z`, fill, "data-option": x.name }));
            const swatch = html("span", { class: "tune-swatch" });
            swatch.style.background = fill;
            legend.prepend(html("span", {}, swatch, x.label));
            below = above;
        });
        const wildest = options.map((x) => `${x.label} ${Math.round(share[x.name][points - 1])}%`).join(", ");
        const middling = options.map((x) => `${x.label} ${Math.round(share[x.name][(points - 1) / 2])}%`).join(", ");
        shares.setAttribute("aria-label", `The chance a song draws each: in a middling song ${middling}; in the wildest ${wildest}`);
    }

    // the shapes' buttons, each shaping the option chosen as it is pressed
    shapes.append(...shapesOf([0, 0, 0], 100).map(([name], i) => {
        const button = html("button", { type: "button", text: name, "aria-pressed": "false" });
        button.addEventListener("click", () => {
            amounts[selected] = written(shapesOf(endsOf(amounts[selected]), rangeOf(choice, selected)[1])[i][1]);
            changed();
        });
        return button;
    }));

    return {
        element: dialog,
        /** Opens on a choice and its options' amounts, the first kept chosen; done takes the amounts back. */
        open(bound, given, onDone) {
            choice = bound;
            amounts = structuredClone(given);
            selected = kept()[0]?.name ?? null;
            preview = null;
            done = onDone;
            if (selected === null) return;
            list.replaceChildren(...kept().map((x) => {
                const row = html("button", { type: "button", class: "tune-option", "data-option": x.name }, html("span", { text: x.label }), html("span", { class: "tune-values" }));
                row.addEventListener("click", () => {
                    selected = x.name;
                    render();
                });
                return row;
            }));
            render();
            askPreview();
            dialog.showModal();
        },
    };
}
