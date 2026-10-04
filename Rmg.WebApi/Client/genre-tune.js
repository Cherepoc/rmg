import { endsOf, ownOf, written } from "./genres.js";
import { byMiddle, COLOURS, drawShares, ENDS, rangeOf, svg } from "./genre-charts.js";

/**
 *     The genre editor's window over it for a choice, where the genre changes it: whether the genre overrides it, the
 *     options it keeps, picked from a list or removed by their tabs, and each one's amounts in the plainest songs, the
 *     middling and the experimental, drawn on a chart of them across how experimental a song is, the one chosen moved by
 *     its three handles, up and down or by the arrow keys, or by its numbers; and, for a pool, what the songs draw of
 *     each, the chance of a song drawing it, beside, and at each end in words. Every line is the server's, as the songs
 *     draw it (/api/genres/preview), the handles' straight between them until it comes. Done gives the choice back to the
 *     editor, overridden or not; closing lets it go.
 */

// the charts' height in pixels, their width the window's, and how far the handles stand out of the chart of the amounts
const HEIGHT = 200;
const SHARES_HEIGHT = 150;
const MARGIN = 12;

/** How long the window waits after a change before it asks for the lines again. */
const PREVIEW_WAIT_MS = 150;

/** A pool's weight where an option is added that the songs never play. */
const ADDED_WEIGHT = 50;

function html(name, attributes = {}, ...children) {
    const element = document.createElement(name);
    for (const [key, value] of Object.entries(attributes)) {
        if (key === "text") element.textContent = value;
        else element.setAttribute(key, value);
    }
    element.append(...children);
    return element;
}

/** The option another plays only with, by its tag, "with:riff"; none for one that plays alone. */
const partnerOf = (option) => option.tags.find((x) => x.startsWith("with:"))?.slice("with:".length) ?? null;

export function createGenreTune() {
    let choice = null;
    let amounts = {};
    let overridden = false;
    let selected = null;
    let preview = null;
    let done = null;
    let waiting = null;
    // the options the genre may keep, and its amounts as the songs have them, as the editor gives them
    let allowed = () => true;
    let start = null;
    // a handle being dragged, and its end
    let dragging = null;

    const dialog = html("dialog", { class: "genre-tune" });
    const title = html("h2");
    const override = html("input", { type: "checkbox" });
    const overrideLabel = html("label", { class: "tune-override" }, override, "Override");
    const pickOpen = html("button", { type: "button", class: "tune-pick-open", "aria-haspopup": "true", "aria-expanded": "false" });
    const search = html("input", { type: "search", placeholder: "Find an option", "aria-label": "Find an option" });
    const picks = html("div", { class: "tune-picks" });
    const popup = html("div", { class: "tune-popup", role: "group", "aria-label": "Options the genre keeps" }, search, picks);
    popup.hidden = true;
    const pick = html("span", { class: "tune-pick" }, pickOpen, popup);
    const close = html("button", { type: "button", "aria-label": "Close without keeping", class: "genre-close", text: "×" });
    close.addEventListener("click", () => dialog.close());
    const header = html("header", {}, title, overrideLabel, pick, html("span", { class: "tune-gap" }), close);

    const list = html("div", { class: "tune-list", role: "group", "aria-label": "Options" });
    const heading = html("h3");
    const chart = svg("svg", { class: "tune-chart", role: "group" });
    const plot = html("div", { class: "tune-plot" }, chart);
    const axisHigh = html("span");
    const axisLow = html("span");
    const axis = html("div", { class: "tune-axis", "aria-hidden": "true" }, axisHigh, axisLow);
    const fields = ENDS.map((end) => {
        const input = html("input", { type: "number", step: "1" });
        const label = html("label", {}, end, input);
        return { label, input };
    });
    const fieldsRow = html("div", { class: "tune-fields" }, ...fields.map((x) => x.label));
    const legend = html("div", { class: "tune-legend" });
    const sharesHeading = html("h3", { class: "tune-shares-heading", text: "The chance a song draws each" });
    const sharesAxis = html("div", { class: "tune-axis tune-shares-axis", "aria-hidden": "true" }, html("span", { text: "100%" }), html("span", { text: "0%" }));
    const shares = svg("svg", { class: "tune-shares", role: "img", preserveAspectRatio: "none" });
    const lists = html("div", { class: "tune-lists" });
    const grid = html("div", { class: "tune-grid" }, axis, plot, legend, fieldsRow, sharesHeading, sharesAxis, shares, lists);
    const empty = html("p", { class: "tune-empty", text: "The genre keeps none of these: pick some under Options." });
    const main = html("div", { class: "tune-main" }, heading, grid, empty);
    const body = html("div", { class: "tune-body" }, list, main);

    const reset = html("button", { type: "button", text: "Restore defaults", title: "The options and their amounts as the songs have them" });
    const finish = html("button", { type: "button", class: "primary", text: "Done" });
    const footer = html("footer", {}, reset, html("span", { class: "tune-gap" }), finish);
    dialog.append(header, body, footer);

    override.addEventListener("change", () => (overridden = override.checked));
    reset.addEventListener("click", () => {
        amounts = start(choice);
        selected = kept()[0]?.name ?? null;
        renderList();
        edited();
    });
    finish.addEventListener("click", () => {
        done?.(overridden ? structuredClone(amounts) : null);
        dialog.close();
    });
    fields.forEach(({ input }, end) => input.addEventListener("change", () => setEnd(end, Number(input.value))));

    // the list of the options to pick, open under its button, closed by a click elsewhere or Escape, the window kept open
    pickOpen.addEventListener("click", () => showPicks(popup.hidden));
    search.addEventListener("input", filterPicks);
    dialog.addEventListener("pointerdown", (event) => {
        if (!popup.hidden && !pick.contains(event.target)) showPicks(false);
    });
    dialog.addEventListener("cancel", (event) => {
        if (popup.hidden) return;
        event.preventDefault();
        showPicks(false);
        pickOpen.focus();
    });

    // the charts drawn again to the window's width as it changes
    new ResizeObserver(() => {
        if (dialog.open && choice) renderCharts();
    }).observe(plot);

    const optionOf = (name) => choice.options.find((x) => x.name === name);
    const kept = () => choice.options.filter((x) => x.name in amounts);
    const isPool = () => choice.kind === "pool";
    // the options drawn on one chart with the one chosen: those of its range, the tempo's ends together and its lean apart
    const alongside = () => kept().filter((x) => String(rangeOf(choice, x.name)) === String(rangeOf(choice, selected)));
    // the order the options are drawn and told in: a pool's commonest in a middling song first, at the bottom
    const order = () => (isPool() && preview?.shares && kept().every((x) => preview.shares[x.name])
        ? byMiddle(preview.shares).filter((x) => x in amounts)
        : kept().map((x) => x.name));
    const colourOf = (name) => (name === selected ? "var(--accent)" : COLOURS[order().indexOf(name) % COLOURS.length]);

    /** The window's choice changed: overridden from now, drawn again, and the lines asked for again. */
    function edited() {
        overridden = override.checked = true;
        changed();
    }

    function setEnd(end, value) {
        const [least, most] = rangeOf(choice, selected);
        const points = [...endsOf(amounts[selected])];
        points[end] = Math.max(least, Math.min(most, Math.round(value)));
        amounts[selected] = written(points);
        if (dragging === null) edited();
        else {
            overridden = override.checked = true;
            follow(points);
        }
    }

    /**
     *     While a handle is dragged, the handles, the line chosen and the numbers alone follow it, the line straight between
     *     the handles, so that the handle held stays the one dragged; the whole is drawn again as it is let go.
     */
    function follow(points) {
        const width = plot.clientWidth;
        for (const handle of chart.querySelectorAll(".tune-handle")) {
            const value = points[Number(handle.dataset.end)];
            handle.setAttribute("cy", String(yOf(value)));
            handle.setAttribute("aria-valuenow", String(value));
        }
        chart.querySelector(".tune-line.chosen")?.setAttribute("d", `M${points.map((value, i) => `${(i * width) / 2},${yOf(value).toFixed(1)}`).join(" L")}`);
        fields.forEach(({ input }, end) => (input.value = String(points[end])));
    }

    function changed() {
        render();
        clearTimeout(waiting);
        waiting = setTimeout(askPreview, PREVIEW_WAIT_MS);
    }

    async function askPreview() {
        const asked = choice;
        // a pool keeping none is no genre's, and draws nothing
        if (kept().length === 0) return;
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

    /** The options' tabs, each its numbers and, but a range's, a button removing it; made again as the options kept change. */
    function renderList() {
        const isLast = isPool() && kept().length === 1;
        list.replaceChildren(...kept().map((x) => {
            const option = html("button", { type: "button", class: "tune-option", "data-option": x.name }, html("span", { text: x.label }), html("span", { class: "tune-values" }));
            option.addEventListener("click", () => {
                selected = x.name;
                render();
            });
            const row = html("div", { class: "tune-row", "data-option": x.name }, option);
            if (choice.kind !== "range") {
                const remove = html("button", { type: "button", class: "tune-remove", "aria-label": `Leave out ${x.label}`, text: "×" });
                // a pool keeps one at least
                remove.disabled = isLast;
                if (isLast) remove.title = "A pool keeps one option at least";
                remove.addEventListener("click", () => leaveOut(x.name));
                row.append(remove);
            }
            return row;
        }));
    }

    /** The options to pick from, those the genre may keep, a twin while its partner is out and a pool's last disabled. */
    function renderPicks() {
        const options = choice.options.filter((x) => allowed(x) || x.name in amounts);
        pickOpen.textContent = `Options: ${kept().length} of ${options.length}`;
        picks.replaceChildren(...options.map((x) => {
            const input = html("input", { type: "checkbox" });
            input.checked = x.name in amounts;
            const partner = partnerOf(x);
            input.disabled = (partner !== null && !(partner in amounts)) || (isPool() && input.checked && kept().length === 1);
            input.addEventListener("change", () => (input.checked ? keep(x) : leaveOut(x.name)));
            const label = html("label", { "data-option": x.name }, input, x.label);
            if (x.description) label.title = x.description;
            return label;
        }));
        filterPicks();
    }

    function filterPicks() {
        const text = search.value.trim().toLowerCase();
        for (const label of picks.children) label.hidden = text !== "" && !label.textContent.toLowerCase().includes(text);
    }

    function showPicks(open) {
        popup.hidden = !open;
        pickOpen.setAttribute("aria-expanded", String(open));
        if (open) {
            search.value = "";
            filterPicks();
            search.focus();
        }
    }

    /** An option kept: at its own amounts, or, a pool's the songs never play, at ADDED_WEIGHT; chosen. */
    function keep(option) {
        amounts[option.name] = isPool() && !option.own.some((x) => x > 0) ? ADDED_WEIGHT : ownOf(option);
        selected = option.name;
        renderList();
        edited();
    }

    /** An option left out, and those that play only with it. */
    function leaveOut(name) {
        delete amounts[name];
        for (const other of choice.options.filter((x) => partnerOf(x) === name)) delete amounts[other.name];
        if (!(selected in amounts)) selected = kept()[0]?.name ?? null;
        renderList();
        edited();
    }

    function render() {
        pick.hidden = choice.kind === "range";
        renderPicks();
        for (const row of list.children) {
            row.querySelector(".tune-values").textContent = endsOf(amounts[row.dataset.option]).join(" · ");
            if (row.dataset.option === selected) row.setAttribute("aria-current", "true");
            else row.removeAttribute("aria-current");
        }

        grid.hidden = selected === null;
        empty.hidden = selected !== null;
        if (selected === null) return;

        const [least, most] = rangeOf(choice, selected);
        const option = optionOf(selected);
        const points = endsOf(amounts[selected]);
        heading.textContent = isPool() ? `${option.label}: how common it is, against the others` : `${choice.label}: ${option.label}`;
        axisHigh.textContent = String(most);
        axisLow.textContent = String(least);
        fields.forEach(({ input }, end) => {
            input.min = String(least);
            input.max = String(most);
            input.value = String(points[end]);
            input.setAttribute("aria-label", `${option.label}, ${ENDS[end].toLowerCase()}`);
        });
        // an attribute, which an svg has no property of
        for (const element of [sharesHeading, sharesAxis, shares, lists]) element.toggleAttribute("hidden", !isPool());
        legend.hidden = choice.kind === "range";
        renderCharts();
    }

    /** The chart of the amounts, its handles, a pool's chances and the legend, the handle focused kept so. */
    function renderCharts() {
        if (selected === null) return;
        const width = plot.clientWidth;
        const [least, most] = rangeOf(choice, selected);
        const points = endsOf(amounts[selected]);
        const focused = chart.contains(document.activeElement) ? document.activeElement.dataset.end : undefined;

        // drawn in pixels, the handles standing out of it by the margin, so that it is as wide as the chances under it
        chart.setAttribute("viewBox", `${-MARGIN} ${-MARGIN} ${width + 2 * MARGIN} ${HEIGHT + 2 * MARGIN}`);
        chart.setAttribute("width", String(width + 2 * MARGIN));
        chart.setAttribute("height", String(HEIGHT + 2 * MARGIN));
        chart.replaceChildren(
            ...[0, HEIGHT / 2, HEIGHT].map((y) => svg("line", { x1: 0, y1: y, x2: width, y2: y, class: "tune-gridline" })),
            ...[0, width / 2, width].map((x) => svg("line", { x1: x, y1: 0, x2: x, y2: HEIGHT, class: "tune-gridline" })),
        );
        chart.setAttribute("aria-label", `${choice.label}: every option's amounts from the plainest songs to the experimental`);
        // the one chosen drawn last, over the others
        for (const other of alongside().sort((a, b) => (a.name === selected) - (b.name === selected))) {
            const curve = dragging === null && preview?.curves?.[other.name];
            const along = curve
                ? curve.map((value, i) => [(i / (curve.length - 1)) * width, yOf(value)])
                : endsOf(amounts[other.name]).map((value, i) => [(i * width) / 2, yOf(value)]);
            const line = svg("path", {
                d: `M${along.map(([x, y]) => `${x.toFixed(1)},${y.toFixed(1)}`).join(" L")}`,
                class: other.name === selected ? "tune-line chosen" : "tune-line",
                "data-option": other.name,
            });
            if (other.name !== selected) line.style.stroke = colourOf(other.name);
            chart.append(line);
        }
        points.forEach((value, end) => chart.append(handle(end, value, least, most, width)));
        if (focused !== undefined) chart.querySelector(`[data-end="${focused}"]`)?.focus();

        renderLegend();
        if (isPool()) renderShares(width);
    }

    /** The options' colours, the one at the top of the chances first, as they stack. */
    function renderLegend() {
        legend.replaceChildren(...order().reverse().map((name) => {
            const swatch = html("span", { class: "tune-swatch" });
            swatch.style.background = colourOf(name);
            return html("span", {}, swatch, optionOf(name).label);
        }));
    }

    /** A handle of the option chosen at an end: dragged up and down, or moved by the arrow keys, a whole number a step. */
    function handle(end, value, least, most, width) {
        const element = svg("circle", {
            cx: (end * width) / 2,
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
            const y = event.clientY - chart.getBoundingClientRect().top - MARGIN;
            setEnd(end, least + ((HEIGHT - y) / HEIGHT) * (most - least));
        });
        element.addEventListener("pointerup", () => {
            dragging = null;
            edited();
        });
        return element;
    }

    /** What the songs draw of every option, stacked from the bottom, the one chosen in the accent, and at each end in words. */
    function renderShares(width) {
        shares.replaceChildren();
        lists.replaceChildren();
        shares.setAttribute("viewBox", `0 0 ${width} ${SHARES_HEIGHT}`);
        const share = preview?.shares;
        if (!share || kept().some((x) => !share[x.name])) return;

        drawShares(shares, share, order(), colourOf, width, SHARES_HEIGHT);
        const points = share[kept()[0].name].length;
        const told = [0, (points - 1) / 2, points - 1].map((j) => order()
            .map((name) => ({ name, amount: Math.round(share[name][j]) }))
            .filter((x) => x.amount > 0)
            .sort((a, b) => b.amount - a.amount));
        lists.append(...told.map((items, end) => html("div", {},
            html("h4", { text: ENDS[end] }),
            html("ul", {}, ...items.map((x) => html("li", { text: `${optionOf(x.name).label} ${x.amount}%` }))))));
        const said = (end) => told[end].map((x) => `${optionOf(x.name).label} ${x.amount}%`).join(", ");
        shares.setAttribute("aria-label", `The chance a song draws each: in a middling song ${said(1)}; in the experimental ${said(2)}`);
    }

    return {
        element: dialog,
        /**
         *     Opens on a choice, its options' amounts as the genre gives them, none where it does not override it, which
         *     then start as the songs have them; the options it may keep, how the songs have them, its title, and what to do
         *     as it is done: the amounts given back, none where it no longer overrides it.
         */
        open(bound, given, { allowed: isAllowed, start: startOf, title: titled }, onDone) {
            choice = bound;
            title.textContent = titled;
            allowed = isAllowed;
            start = startOf;
            overridden = override.checked = given !== null;
            amounts = structuredClone(given ?? start(choice));
            selected = kept()[0]?.name ?? null;
            preview = null;
            done = onDone;
            showPicks(false);
            renderList();
            dialog.showModal();
            render();
            askPreview();
        },
    };
}
