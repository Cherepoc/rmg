import { endsOf, ownOf, written } from "./genres.js";
import { amountOf, byMiddle, centreOf, COLOURS, drawBand, drawShares, ENDS, isTime, leanOf, mark, numberInWords, numberOf, numberOfWords, rangeOf, svg, unitInWords } from "./genre-charts.js";

/**
 *     The genre editor's window over it for a choice, where the genre changes it: whether the genre overrides it, the
 *     options it keeps, picked from a list or removed by their tabs, and each one's amounts in the plainest songs, the
 *     middling and the experimental, drawn on a chart of them across how experimental a song is, the one chosen moved by
 *     its three handles, up and down or by the arrow keys, or by its numbers, a range's ends and lean on one band, every
 *     handle of the three at once; and, for a pool, what the songs draw of
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
    // how many times the chart was asked for, the latest answer the one drawn
    let previewsAsked = 0;
    // the choice as the window opened on it, which closing it without Done loses nothing of
    let opened = "";

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
    close.addEventListener("click", () => {
        if (mayLeave()) dialog.close();
    });
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
    // the option pointed at in the chances, named at once with its share where the pointer is
    const pointed = html("div", { class: "tune-pointed", "aria-hidden": "true" });
    pointed.hidden = true;
    const sharesBox = html("div", { class: "tune-shares-box" }, shares, pointed);
    const lists = html("div", { class: "tune-lists" });
    const grid = html("div", { class: "tune-grid" }, axis, plot, legend, fieldsRow, sharesHeading, sharesAxis, sharesBox, lists);
    // the charts' heights the stylesheet sizes them by, and the legend beside every row of the middle column
    grid.style.setProperty("--tune-height", `${HEIGHT}px`);
    grid.style.setProperty("--tune-shares-height", `${SHARES_HEIGHT}px`);
    legend.style.gridRow = `1 / span ${[plot, fieldsRow, sharesHeading, sharesBox, lists].length}`;
    const empty = html("p", { class: "tune-empty", text: "The genre keeps none of these: pick some under Options." });
    const main = html("div", { class: "tune-main" }, heading, grid, empty);
    const body = html("div", { class: "tune-body" }, list, main);

    const reset = html("button", { type: "button", text: "Restore defaults", title: "The options and their amounts by default, as with no genre, the choice no longer overridden" });
    const finish = html("button", { type: "button", class: "primary", text: "Done" });
    const footer = html("footer", {}, reset, html("span", { class: "tune-gap" }), finish);
    dialog.append(header, body, footer);

    override.addEventListener("change", () => (overridden = override.checked));
    // the songs' own, the choice no longer overridden
    reset.addEventListener("click", () => {
        amounts = start(choice);
        selected = kept()[0]?.name ?? null;
        renderList();
        overridden = override.checked = false;
        changed();
    });
    finish.addEventListener("click", () => {
        done?.(overridden ? structuredClone(amounts) : null);
        dialog.close();
    });
    // a time typed as minutes and seconds, or minutes alone; what is no number put back as it was
    fields.forEach(({ input }, end) => input.addEventListener("change", () => {
        const value = numberOfWords(choice, selected, input.value);
        if (Number.isFinite(value)) setEnd(end, value);
        else render();
    }));

    // the list of the options to pick, open under its button, closed by a click elsewhere or Escape, the window kept open
    pickOpen.addEventListener("click", () => showPicks(popup.hidden));
    search.addEventListener("input", filterPicks);
    dialog.addEventListener("pointerdown", (event) => {
        if (!popup.hidden && !pick.contains(event.target)) showPicks(false);
    });
    dialog.addEventListener("cancel", (event) => {
        if (popup.hidden) {
            if (!mayLeave()) event.preventDefault();
            return;
        }
        event.preventDefault();
        showPicks(false);
        pickOpen.focus();
    });

    const asOpened = () => JSON.stringify({ overridden, amounts });

    /** Whether the window may close without Done: nothing changed since it opened, or its loss confirmed. */
    function mayLeave() {
        return asOpened() === opened || confirm("Close the window and lose its changes to this choice?");
    }

    shares.addEventListener("pointermove", (event) => showPointed(pointAt(event), event));
    shares.addEventListener("pointerleave", () => showPointed(null));

    // the charts drawn again to the window's width as it changes
    new ResizeObserver(() => {
        if (dialog.open && choice) redraw();
    }).observe(plot);

    const optionOf = (name) => choice.options.find((x) => x.name === name);
    const kept = () => choice.options.filter((x) => x.name in amounts);
    const isPool = () => choice.kind === "pool";
    const isRange = () => choice.kind === "range";
    // the options drawn on one chart with the one chosen: those of its range, a range's ends together and its lean apart
    const alongside = () => kept().filter((x) => String(rangeOf(choice, x.name)) === String(rangeOf(choice, selected)));
    // the order the options are drawn and told in: a pool's commonest in a middling song first, at the bottom
    const order = () => (isPool() && preview?.shares && kept().every((x) => preview.shares[x.name])
        ? byMiddle(preview.shares).filter((x) => x in amounts)
        : kept().map((x) => x.name));
    // every option its grey, the one chosen too, which an outline in the accent marks instead
    const greyOf = (name) => COLOURS[order().indexOf(name) % COLOURS.length];

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
        // a range's other end following one set past it, so that its from is never above its to, which no genre may be
        const other = isRange() ? { from: "to", to: "from" }[selected] : undefined;
        if (other !== undefined && amounts[other] !== undefined) {
            const others = [...endsOf(amounts[other])];
            if (selected === "from" ? others[end] < points[end] : others[end] > points[end]) {
                others[end] = points[end];
                amounts[other] = written(others);
            }
        }
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
        if (isRange()) chart.querySelector(".genre-band").replaceWith(band(width));
        else chart.querySelector(".tune-line.chosen")?.setAttribute("d", `M${points.map((value, i) => `${(i * width) / 2},${yOf(selected, i, value).toFixed(1)}`).join(" L")}`);
        // a range's lean's handles follow its ends'
        for (const handle of chart.querySelectorAll(".tune-handle")) {
            const value = endsOf(amounts[handle.dataset.option])[Number(handle.dataset.end)];
            handle.setAttribute("cy", String(yOf(handle.dataset.option, Number(handle.dataset.end), value)));
            handle.setAttribute("aria-valuenow", String(value));
        }
        fields.forEach(({ input }, end) => (input.value = numberInWords(choice, selected, points[end])));
    }

    function changed() {
        render();
        clearTimeout(waiting);
        waiting = setTimeout(askPreview, PREVIEW_WAIT_MS);
    }

    async function askPreview() {
        // the latest asked for alone drawn, a slower earlier answer let go of
        const asked = ++previewsAsked;
        // a pool keeping none is no genre's, and draws nothing
        if (kept().length === 0) return;
        try {
            const response = await fetch(new URL("api/genres/preview", location.href), {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ choice: choice.name, options: amounts }),
            });
            if (!response.ok || asked !== previewsAsked) return;
            const answered = await response.json();
            if (asked !== previewsAsked) return;
            preview = answered;
            // the charts alone, so that what is pressed or focused meanwhile stays where it is
            redraw();
        } catch {
            // without the server's lines, the handles' straight ones stand
        }
    }

    /**
     *     The y on the chart of an option's number at an end: by its range, or a range's end by its amount, as its scale
     *     spaces it, and its lean where its draws centre between its ends.
     */
    function yOf(name, end, value) {
        if (!isRange()) {
            const [least, most] = rangeOf(choice, name);
            return HEIGHT - ((value - least) / (most - least)) * HEIGHT;
        }
        if (name !== "lean") return HEIGHT - amountOf(choice, value) * HEIGHT;
        const [low, high] = endsAt(end);
        return HEIGHT - (low + (high - low) * centreOf(choice, value)) * HEIGHT;
    }

    /** An option's number at an end of a y on the chart, as yOf places it; a lean between ends that meet as it is. */
    function valueAt(name, end, y) {
        const amount = (HEIGHT - y) / HEIGHT;
        if (!isRange()) {
            const [least, most] = rangeOf(choice, name);
            return least + amount * (most - least);
        }
        if (name !== "lean") return numberOf(choice, amount);
        const [low, high] = endsAt(end);
        return high - low < 1e-6 ? endsOf(amounts.lean)[end] : leanOf(choice, (amount - low) / (high - low));
    }

    /** A range's from and to at an end, as amounts from 0 to 1. */
    const endsAt = (end) => ["from", "to"].map((x) => amountOf(choice, endsOf(amounts[x])[end]));

    /**
     *     A range's band: the songs' own as the server draws it, or, while a handle is dragged or until it comes, straight
     *     between the handles.
     */
    function band(width) {
        const ownCurves = dragging === null && kept().every((x) => preview?.curves?.[x.name]);
        const straight = (points) => [...Array(21).keys()].map((j) => {
            const at = (j / 20) * 2;
            const i = Math.min(1, Math.floor(at));
            return points[i] + (at - i) * (points[i + 1] - points[i]);
        });
        const curves = ownCurves ? preview.curves : Object.fromEntries(kept().map((x) => [x.name, straight(endsOf(amounts[x.name]))]));
        return drawBand(svg("g"), choice, curves, width, HEIGHT);
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

    /** The window drawn again, the charts too but where a handle of the chart chooses its option, which stays as it is. */
    function render(charts = true) {
        pick.hidden = choice.kind === "range";
        renderPicks();
        for (const row of list.children) {
            row.querySelector(".tune-values").textContent = endsOf(amounts[row.dataset.option]).map((x) => numberInWords(choice, row.dataset.option, x)).join(" · ");
            if (row.dataset.option === selected) row.setAttribute("aria-current", "true");
            else row.removeAttribute("aria-current");
        }

        grid.hidden = selected === null;
        empty.hidden = selected !== null;
        if (selected === null) return;

        const [least, most] = rangeOf(choice, selected);
        const option = optionOf(selected);
        const points = endsOf(amounts[selected]);
        heading.textContent = isPool() ? `${option.label}: how common it is, against the others` : `${choice.label}: ${option.label}${unitInWords(choice, selected)}`;
        // a range's lean drawn on its ends' scale
        const scaled = isRange() ? "from" : selected;
        const [lowest, highest] = rangeOf(choice, scaled);
        axisHigh.textContent = numberInWords(choice, scaled, highest);
        axisLow.textContent = numberInWords(choice, scaled, lowest);
        fields.forEach(({ input }, end) => {
            input.type = isTime(choice, selected) ? "text" : "number";
            input.min = String(least);
            input.max = String(most);
            input.value = numberInWords(choice, selected, points[end]);
            input.setAttribute("aria-label", `${option.label}, ${ENDS[end].toLowerCase()}`);
        });
        // an attribute, which an svg has no property of
        for (const element of [sharesHeading, sharesAxis, sharesBox, lists]) element.toggleAttribute("hidden", !isPool());
        legend.hidden = choice.kind === "range";
        if (charts) renderCharts();
    }

    /** The chart of the amounts, its handles, a pool's chances and the legend, the handle focused kept so. */
    // whether the charts are to be drawn again once a drag ends, which a redraw would cut short by taking its handle away
    let isRedrawWaiting = false;

    /** The charts drawn again, or once the drag under way ends. */
    function redraw() {
        isRedrawWaiting = dragging !== null;
        if (!isRedrawWaiting) renderCharts();
    }

    function renderCharts() {
        if (selected === null) return;
        const width = plot.clientWidth;
        const focused = chart.contains(document.activeElement) ? document.activeElement : null;

        // drawn in pixels, the handles standing out of it by the margin, so that it is as wide as the chances under it
        chart.setAttribute("viewBox", `${-MARGIN} ${-MARGIN} ${width + 2 * MARGIN} ${HEIGHT + 2 * MARGIN}`);
        chart.setAttribute("width", String(width + 2 * MARGIN));
        chart.setAttribute("height", String(HEIGHT + 2 * MARGIN));
        chart.replaceChildren(
            ...[0, HEIGHT / 2, HEIGHT].map((y) => svg("line", { x1: 0, y1: y, x2: width, y2: y, class: "tune-gridline" })),
            ...[0, width / 2, width].map((x) => svg("line", { x1: x, y1: 0, x2: x, y2: HEIGHT, class: "tune-gridline" })),
        );
        chart.setAttribute("aria-label", `${choice.label}: every option's amounts from the plainest songs to the experimental`);
        // a range's ends and lean one band, every handle of the three on it
        if (isRange()) {
            chart.append(band(width));
            for (const option of kept()) endsOf(amounts[option.name]).forEach((value, end) => chart.append(handle(option.name, end, value, width)));
        } else drawLines(width);
        if (focused !== null) chart.querySelector(`[data-option="${CSS.escape(focused.dataset.option)}"][data-end="${focused.dataset.end}"]`)?.focus();

        renderLegend();
        if (isPool()) renderShares(width);
    }

    /** The options alongside the one chosen as lines, and its handles. */
    function drawLines(width) {
        // the one chosen drawn last, over the others
        for (const other of alongside().sort((a, b) => (a.name === selected) - (b.name === selected))) {
            const curve = dragging === null && preview?.curves?.[other.name];
            const along = curve
                ? curve.map((value, i) => [(i / (curve.length - 1)) * width, yOf(other.name, 0, value)])
                : endsOf(amounts[other.name]).map((value, i) => [(i * width) / 2, yOf(other.name, i, value)]);
            const line = mark({
                d: `M${along.map(([x, y]) => `${x.toFixed(1)},${y.toFixed(1)}`).join(" L")}`,
                class: other.name === selected ? "tune-line chosen" : "tune-line",
                "data-option": other.name,
            }, optionOf(other.name).label);
            if (other.name !== selected) line.style.stroke = greyOf(other.name);
            chart.append(line);
        }
        endsOf(amounts[selected]).forEach((value, end) => chart.append(handle(selected, end, value, width)));
    }

    /** The options' colours, the one at the top of the chances first, as they stack. */
    function renderLegend() {
        legend.replaceChildren(...order().reverse().map((name) => {
            const swatch = html("span", { class: "tune-swatch" });
            swatch.style.background = greyOf(name);
            return html("span", { class: name === selected ? "chosen" : "", "data-option": name, title: optionOf(name).label }, swatch, html("span", { text: optionOf(name).short }));
        }));
    }

    /** An option's share outlined, over the chances, by the class given: the one chosen or the one pointed at. */
    function outlineOf(name, className) {
        const outline = shares.querySelector(`path[data-option="${CSS.escape(name)}"]`).cloneNode(false);
        outline.removeAttribute("data-option");
        outline.removeAttribute("fill");
        outline.setAttribute("class", className);
        return outline;
    }

    /** The option whose share the pointer is over, and the point along the songs it is at; none off them. */
    function pointAt(event) {
        const share = preview?.shares;
        if (!share || selected === null || kept().some((x) => !share[x.name])) return null;
        const box = shares.getBoundingClientRect();
        const points = share[kept()[0].name].length;
        const j = Math.round(Math.min(1, Math.max(0, (event.clientX - box.left) / box.width)) * (points - 1));
        const at = 100 * (1 - (event.clientY - box.top) / box.height);
        let below = 0;
        for (const name of order()) {
            const above = below + share[name][j];
            if (at >= below && at <= above) return { name, j, points, amount: share[name][j] };
            below = above;
        }
        return null;
    }

    /** The option pointed at outlined and named, its share where the pointer is and, at an end, which; none shown for none. */
    function showPointed(at, event) {
        shares.querySelector(".share-pointed")?.remove();
        for (const entry of legend.children) entry.classList.toggle("pointed", entry.dataset.option === at?.name);
        pointed.hidden = at === null;
        if (at === null) return;
        shares.append(outlineOf(at.name, "share-pointed"));
        const where = at.j === 0 ? " at the plainest end" : at.j === at.points - 1 ? " at the experimental end" : at.j === (at.points - 1) / 2 ? " in a middling song" : "";
        pointed.textContent = `${optionOf(at.name).short} · ${Math.round(at.amount)}%${where}`;
        // beside the pointer, on the side with room, so that it never runs past the chart
        const box = sharesBox.getBoundingClientRect();
        const x = event.clientX - box.left;
        const isLeft = x > box.width / 2;
        pointed.style.left = `${isLeft ? x - 12 : x + 12}px`;
        pointed.style.top = `${Math.max(0, event.clientY - box.top - 30)}px`;
        pointed.style.transform = isLeft ? "translateX(-100%)" : "";
    }

    /**
     *     A handle of an option at an end: dragged up and down, or moved by the arrow keys, a whole number a step, its
     *     option chosen as it is taken.
     */
    function handle(name, end, value, width) {
        const [least, most] = rangeOf(choice, name);
        const element = svg("circle", {
            cx: (end * width) / 2,
            cy: yOf(name, end, value),
            r: 9,
            class: isRange() && name !== "lean" ? "tune-handle tune-handle-end" : "tune-handle",
            tabindex: 0,
            role: "slider",
            "aria-label": `${optionOf(name).label}, ${ENDS[end].toLowerCase()}`,
            "aria-valuemin": least,
            "aria-valuemax": most,
            "aria-valuenow": value,
            "data-option": name,
            "data-end": end,
        });
        const choose = () => {
            if (selected === name) return;
            selected = name;
            render(false);
        };
        element.addEventListener("focus", choose);
        element.addEventListener("keydown", (event) => {
            const by = { ArrowUp: 1, ArrowRight: 1, ArrowDown: -1, ArrowLeft: -1, PageUp: 10, PageDown: -10 }[event.key];
            if (by === undefined) return;
            event.preventDefault();
            choose();
            setEnd(end, endsOf(amounts[name])[end] + by);
            chart.querySelector(`[data-option="${CSS.escape(name)}"][data-end="${end}"]`)?.focus();
        });
        // a drag's amount before it, so that a click that moves nothing overrides nothing
        let before = null;
        element.addEventListener("pointerdown", (event) => {
            choose();
            dragging = end;
            before = JSON.stringify(amounts[name]);
            element.setPointerCapture(event.pointerId);
        });
        element.addEventListener("pointermove", (event) => {
            if (dragging !== end || selected !== name) return;
            setEnd(end, valueAt(name, end, event.clientY - chart.getBoundingClientRect().top - MARGIN));
        });
        // a drag let go of, or broken off by the browser, as a touch turned scroll, kept as far as it went, and the charts
        // drawn again if anything asked to meanwhile
        const letGo = () => {
            if (dragging !== end) return;
            dragging = null;
            if (JSON.stringify(amounts[name]) !== before) edited();
            if (isRedrawWaiting) redraw();
        };
        for (const type of ["pointerup", "pointercancel", "lostpointercapture"]) element.addEventListener(type, letGo);
        return element;
    }

    /** What the songs draw of every option, stacked from the bottom, the one chosen in the accent, and at each end in words. */
    function renderShares(width) {
        const share = preview?.shares;
        // until the server says what the songs draw of every option kept, the chart and its words as they were, so that
        // the window keeps its height as an option is picked
        if (!share || kept().some((x) => !share[x.name])) return;
        shares.replaceChildren();
        lists.replaceChildren();
        shares.setAttribute("viewBox", `0 0 ${width} ${SHARES_HEIGHT}`);

        drawShares(shares, share, order(), greyOf, (name) => optionOf(name).label, width, SHARES_HEIGHT);
        // the one chosen outlined over the others, so that it is whole
        shares.append(outlineOf(selected, "share-chosen"));
        showPointed(null);
        const points = share[kept()[0].name].length;
        const told = [0, (points - 1) / 2, points - 1].map((j) => order()
            .map((name) => ({ name, amount: Math.round(share[name][j]) }))
            .filter((x) => x.amount > 0)
            .sort((a, b) => b.amount - a.amount));
        lists.append(...told.map((items, end) => html("div", {},
            html("h4", { text: ENDS[end] }),
            html("ul", {}, ...items.map((x) => html("li", { title: optionOf(x.name).label }, html("span", { text: optionOf(x.name).short }), ` ${x.amount}%`))))));
        const said = (end) => told[end].map((x) => `${optionOf(x.name).label} ${x.amount}%`).join(", ");
        shares.setAttribute("aria-label", `The chance a song draws each: in the plainest song ${said(0)}; in a middling one ${said(1)}; in the experimental ${said(2)}`);
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
            // a range named by its ends alone leans to the middle, its lean's handles on the band all the same
            if (isRange()) amounts.lean ??= start(choice).lean;
            selected = kept()[0]?.name ?? null;
            preview = null;
            // none of the last choice's chances meanwhile
            shares.replaceChildren();
            lists.replaceChildren();
            done = onDone;
            showPicks(false);
            renderList();
            opened = asOpened();
            dialog.showModal();
            render();
            askPreview();
        },
    };
}
