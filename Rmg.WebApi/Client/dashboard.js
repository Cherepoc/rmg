/**
 *     The analytics dashboard. It asks the server for one summary and draws it; nothing here talks to
 *     anything else, and the charts are plain SVG rather than a library, for the same reason the rest of
 *     this page has no build step.
 */

const TOKEN_KEY = "rmg.dashboard-token";

const elements = {
    controls: document.getElementById("controls"),
    range: document.getElementById("range"),
    refresh: document.getElementById("refresh"),
    taken: document.getElementById("taken"),
    gate: document.getElementById("gate"),
    gateHint: document.getElementById("gate-hint"),
    token: document.getElementById("token"),
    unlock: document.getElementById("unlock"),
    forget: document.getElementById("forget"),
    status: document.getElementById("status"),
    headline: document.getElementById("headline"),
    timings: document.getElementById("timings"),
    funnelPanel: document.getElementById("funnel-panel"),
    funnel: document.getElementById("funnel"),
    dailyPanel: document.getElementById("daily-panel"),
    dailyLegend: document.getElementById("daily-legend"),
    daily: document.getElementById("daily"),
    dailyFigure: document.getElementById("daily-figure"),
    dailyTooltip: document.getElementById("daily-tooltip"),
    dailyTable: document.getElementById("daily-table"),
    dailyTableToggle: document.getElementById("daily-table-toggle"),
    seedsPanel: document.getElementById("seeds-panel"),
    seeds: document.getElementById("seeds"),
    failuresPanel: document.getElementById("failures-panel"),
    failures: document.getElementById("failures"),
};

const SERIES = [
    { key: "visitors", name: "Visitors", role: "--series-1" },
    { key: "songs", name: "Songs", role: "--series-2" },
    { key: "plays", name: "Plays", role: "--series-3" },
];

let summary = null;

// what the pointer over the day by day chart reads from, replaced on every draw
let chart = null;

// --- the token -------------------------------------------------------------

function readToken() {
    try {
        return localStorage.getItem(TOKEN_KEY) ?? "";
    } catch {
        return "";
    }
}

function writeToken(token) {
    try {
        if (token) localStorage.setItem(TOKEN_KEY, token);
        else localStorage.removeItem(TOKEN_KEY);
    } catch {
        // a browser that refuses storage simply asks again next time
    }
}

elements.unlock.addEventListener("click", async () => {
    const token = elements.token.value.trim();
    if (token === "") return;

    writeToken(token);
    await load();
});

elements.token.addEventListener("keydown", (event) => {
    if (event.key === "Enter") elements.unlock.click();
});

elements.forget.addEventListener("click", () => {
    writeToken("");
    elements.token.value = "";
    location.reload();
});

// --- loading ---------------------------------------------------------------

elements.refresh.addEventListener("click", () => load());
elements.range.addEventListener("change", () => load());

function setStatus(message, isError = false) {
    elements.status.textContent = message;
    elements.status.classList.toggle("error", isError);
}

function showPanels(isShown) {
    for (const panel of [elements.headline, elements.timings, elements.funnelPanel,
        elements.dailyPanel, elements.seedsPanel, elements.failuresPanel])
        panel.hidden = !isShown;

    elements.controls.hidden = !isShown;
}

async function load() {
    const token = readToken();
    if (token === "") {
        elements.gate.hidden = false;
        showPanels(false);
        return;
    }

    setStatus("Reading…");

    let response;
    try {
        response = await fetch(new URL(`api/tally/summary?days=${encodeURIComponent(elements.range.value)}`, location.href), {
            headers: { Authorization: `Bearer ${token}` },
        });
    } catch (error) {
        setStatus(`Could not reach the server: ${error.message}`, true);
        return;
    }

    if (response.status === 401) {
        elements.gate.hidden = false;
        elements.forget.hidden = false;
        showPanels(false);
        setStatus("That token was not accepted.", true);
        return;
    }

    if (response.status === 404) {
        elements.gate.hidden = false;
        elements.gateHint.textContent =
            "This server was started without RMG_DASHBOARDTOKEN, so it does not serve the summary at all.";
        showPanels(false);
        setStatus("The dashboard is switched off on this server.", true);
        return;
    }

    if (!response.ok) {
        setStatus(`The server answered ${response.status}.`, true);
        return;
    }

    summary = await response.json();
    elements.gate.hidden = true;
    elements.forget.hidden = false;
    showPanels(true);
    draw();
    setStatus("");
    elements.taken.textContent = `read ${new Date().toLocaleTimeString()}`;
}

// --- formatting ------------------------------------------------------------

function count(value) {
    if (value >= 10_000) return `${(value / 1000).toFixed(1)}K`;
    return value.toLocaleString();
}

function duration(ms) {
    if (ms === null || ms === undefined) return "—";
    if (ms < 1000) return `${Math.round(ms)} ms`;
    if (ms < 60_000) return `${(ms / 1000).toFixed(ms < 10_000 ? 1 : 0)} s`;

    return `${Math.floor(ms / 60_000)}m ${Math.round((ms % 60_000) / 1000)}s`;
}

function seconds(value) {
    if (value === null || value === undefined) return "—";
    if (value < 60) return `${value.toFixed(0)} s`;

    return `${Math.floor(value / 60)}m ${Math.round(value % 60)}s`;
}

function percent(part, whole) {
    return whole > 0 ? `${Math.round((part / whole) * 100)}%` : "—";
}

function set(id, text) {
    document.getElementById(id).textContent = text;
}

// --- drawing ---------------------------------------------------------------

function draw() {
    const played = summary.funnel.find((step) => step.name === "Pressed play")?.visitors ?? 0;

    set("visitors", count(summary.visitors));
    set("visitors-note", `in the last ${summary.days} days`);
    set("opens", count(summary.pageOpens));
    set("play-rate", percent(played, summary.visitors));
    set("play-sub", `${count(played)} of ${count(summary.visitors)}`);
    set("events", count(summary.events));

    drawSpread("sf", summary.soundFontMs, duration);
    drawSpread("ag", summary.firstGestureMs, duration);
    drawSpread("ls", summary.listenedSeconds, seconds);

    drawFunnel();
    drawLegend();
    drawDaily();
    drawDailyTable();
    drawSeeds();
    drawFailures();
}

function drawSpread(prefix, spread, format) {
    set(`${prefix}-p50`, format(spread.median));
    set(`${prefix}-p75`, format(spread.upper));
    set(`${prefix}-p95`, format(spread.worst));
    set(`${prefix}-n`, count(spread.count));
}

/** One measure across ordered stages, so one hue and the value at the tip of each bar. */
function drawFunnel() {
    const top = summary.funnel[0]?.visitors ?? 0;
    elements.funnel.replaceChildren();

    if (top === 0) {
        elements.funnel.append(empty("Nobody has been counted yet."));
        return;
    }

    for (const step of summary.funnel) {
        const row = document.createElement("div");
        row.className = "funnel-row";

        const name = document.createElement("span");
        name.className = "funnel-name";
        name.textContent = step.name;

        const track = document.createElement("div");
        track.className = "funnel-track";
        const bar = document.createElement("div");
        bar.className = "funnel-bar";
        bar.style.width = `${(step.visitors / top) * 100}%`;
        bar.title = `${step.name}: ${step.visitors} of ${top}`;
        track.append(bar);

        const value = document.createElement("span");
        value.className = "funnel-value";
        value.innerHTML = `${count(step.visitors)} <span class="funnel-share">${percent(step.visitors, top)}</span>`;

        row.append(name, track, value);
        elements.funnel.append(row);
    }
}

function drawLegend() {
    elements.dailyLegend.replaceChildren(...SERIES.map((series) => {
        const item = document.createElement("span");
        item.style.color = `var(${series.role})`;

        const key = document.createElement("i");
        const label = document.createElement("span");
        label.style.color = "var(--muted)";
        label.textContent = series.name;

        item.append(key, label);
        return item;
    }));
}

function empty(message) {
    const paragraph = document.createElement("p");
    paragraph.className = "empty";
    paragraph.textContent = message;
    return paragraph;
}

// --- the day by day chart --------------------------------------------------

const SVG = "http://www.w3.org/2000/svg";
const BOX = { width: 900, height: 280, left: 44, right: 78, top: 16, bottom: 28 };

function node(name, attributes) {
    const element = document.createElementNS(SVG, name);
    for (const [key, value] of Object.entries(attributes)) element.setAttribute(key, String(value));

    return element;
}

/**
 *     Three series on one scale, which they share because they are all counts of people or things per
 *     day. Two scales on one chart would let the shapes be arranged to say whatever was wanted.
 */
function drawDaily() {
    const days = summary.daily;
    elements.daily.replaceChildren();
    elements.daily.setAttribute("viewBox", `0 0 ${BOX.width} ${BOX.height}`);

    if (days.length === 0) {
        chart = null;
        hideDay();
        elements.dailyFigure.hidden = true;
        if (!elements.dailyPanel.querySelector(".empty"))
            elements.dailyPanel.append(empty("No days have been counted yet."));
        return;
    }

    elements.dailyFigure.hidden = false;
    elements.dailyPanel.querySelector(".empty")?.remove();

    const highest = Math.max(1, ...days.flatMap((day) => SERIES.map((series) => day[series.key])));
    const step = niceStep(highest);
    const top = Math.ceil(highest / step) * step;

    const plotWidth = BOX.width - BOX.left - BOX.right;
    const plotHeight = BOX.height - BOX.top - BOX.bottom;
    const x = (index) => BOX.left + (days.length === 1 ? plotWidth / 2 : (index / (days.length - 1)) * plotWidth);
    const y = (value) => BOX.top + plotHeight - (value / top) * plotHeight;

    // gridlines first, hairline and solid, so they sit under the data and stay out of its way
    for (const value of ticks(top, step)) {
        elements.daily.append(node("line", {
            x1: BOX.left, x2: BOX.left + plotWidth, y1: y(value), y2: y(value),
            stroke: "var(--grid)", "stroke-width": 1,
        }));

        const label = node("text", {
            x: BOX.left - 10, y: y(value) + 4, "text-anchor": "end",
            fill: "var(--muted)", "font-size": 12,
        });
        label.textContent = value.toLocaleString();
        elements.daily.append(label);
    }

    for (const [index, day] of dayLabels(days).entries()) {
        if (day === null) continue;

        const label = node("text", {
            x: x(index), y: BOX.height - 8, "text-anchor": "middle",
            fill: "var(--muted)", "font-size": 12,
        });
        label.textContent = day;
        elements.daily.append(label);
    }

    const last = days.length - 1;

    for (const series of SERIES) {
        const points = days.map((day, index) => `${x(index)},${y(day[series.key])}`).join(" ");

        elements.daily.append(node("polyline", {
            points, fill: "none", stroke: `var(${series.role})`,
            "stroke-width": 2, "stroke-linejoin": "round", "stroke-linecap": "round",
        }));

        // the end marker carries a 2px ring in the surface colour, so crossing lines stay legible
        elements.daily.append(node("circle", {
            cx: x(last), cy: y(days[last][series.key]), r: 4,
            fill: `var(${series.role})`, stroke: "var(--surface)", "stroke-width": 2,
        }));
    }

    // Direct end labels supplement the legend, but only where the lines have separated. Visitors and
    // songs land on the same number most days, so nudging labels apart would detach them from their
    // lines and read as noise; where they would collide, the legend and the tooltip carry it instead.
    const placed = [];
    for (const series of [...SERIES].sort((first, second) => days[last][first.key] - days[last][second.key])) {
        const at = y(days[last][series.key]);
        if (placed.some((taken) => Math.abs(taken - at) < 15)) continue;

        placed.push(at);
        const label = node("text", { x: x(last) + 10, y: at + 4, fill: "var(--muted)", "font-size": 12 });
        label.textContent = `${series.name} ${days[last][series.key].toLocaleString()}`;
        elements.daily.append(label);
    }

    const crosshair = node("line", {
        y1: BOX.top, y2: BOX.top + plotHeight, stroke: "var(--grid)", "stroke-width": 1, opacity: 0,
    });
    elements.daily.append(crosshair);

    const dots = SERIES.map((series) => {
        const dot = node("circle", {
            r: 4, fill: `var(${series.role})`, stroke: "var(--surface)", "stroke-width": 2, opacity: 0,
        });
        elements.daily.append(dot);
        return dot;
    });

    chart = { days, x, y, crosshair, dots, plotWidth };
    hideDay();
}

/**
 *     A crosshair and one tooltip for the whole day, rather than a hit target per dot. Listened for once,
 *     below, and read from whatever the last draw left in <c>chart</c>.
 */
function showDay(event) {
    if (chart === null) return;

    const { days, x, y, crosshair, dots, plotWidth } = chart;
    const box = elements.daily.getBoundingClientRect();
    const atPixel = ((event.clientX - box.left) / box.width) * BOX.width;
    const part = (atPixel - BOX.left) / plotWidth;
    const index = Math.max(0, Math.min(days.length - 1, Math.round(part * (days.length - 1))));
    const day = days[index];

    crosshair.setAttribute("x1", x(index));
    crosshair.setAttribute("x2", x(index));
    crosshair.setAttribute("opacity", 1);

    dots.forEach((dot, slot) => {
        dot.setAttribute("cx", x(index));
        dot.setAttribute("cy", y(day[SERIES[slot].key]));
        dot.setAttribute("opacity", 1);
    });

    elements.dailyTooltip.innerHTML =
        `<b>${day.day}</b>` +
        SERIES.map((series) =>
            `<span style="color: var(${series.role})"><i></i></span>` +
            `<span style="color: var(--text)">${series.name} ${day[series.key].toLocaleString()}</span>`)
            .join("<br />");

    elements.dailyTooltip.hidden = false;

    const left = (x(index) / BOX.width) * box.width;
    elements.dailyTooltip.style.left = `${Math.min(box.width - 150, Math.max(0, left + 12))}px`;
    elements.dailyTooltip.style.top = "8px";
}

function hideDay() {
    elements.dailyTooltip.hidden = true;
    if (chart === null) return;

    chart.crosshair.setAttribute("opacity", 0);
    for (const dot of chart.dots) dot.setAttribute("opacity", 0);
}

elements.daily.addEventListener("pointermove", showDay);
elements.daily.addEventListener("pointerleave", hideDay);

/** A step the axis can count in without fractions, so the ticks read 0 / 20 / 40, never 0 / 13 / 25. */
function niceStep(highest, wanted = 4) {
    const rough = highest / wanted;
    const size = 10 ** Math.floor(Math.log10(rough));

    return (([1, 2, 2.5, 5].find((multiple) => multiple * size >= rough) ?? 10) * size);
}

function ticks(top, step) {
    const values = [];
    for (let value = 0; value <= top + step / 2; value += step) values.push(Math.round(value));

    return values;
}

/** At most seven dates on the axis, however long the window is. */
function dayLabels(days) {
    const every = Math.max(1, Math.ceil(days.length / 7));
    return days.map((day, index) =>
        index % every === 0 || index === days.length - 1 ? day.day.slice(5) : null);
}

elements.dailyTableToggle.addEventListener("click", () => {
    const isShown = elements.dailyTable.hidden;
    elements.dailyTable.hidden = !isShown;
    elements.dailyFigure.hidden = isShown;
    elements.dailyTableToggle.setAttribute("aria-expanded", String(isShown));
    elements.dailyTableToggle.textContent = isShown ? "Chart" : "Table";
});

function drawDailyTable() {
    elements.dailyTable.replaceChildren(table(
        ["Day", ...SERIES.map((series) => series.name)],
        summary.daily.map((day) => [day.day, ...SERIES.map((series) => day[series.key].toLocaleString())])
    ));
}

// --- the remaining tables --------------------------------------------------

function table(headings, rows) {
    const head = document.createElement("tr");
    for (const heading of headings) {
        const cell = document.createElement("th");
        cell.scope = "col";
        cell.textContent = heading;
        head.append(cell);
    }

    const body = document.createElement("tbody");
    for (const row of rows) {
        const line = document.createElement("tr");
        for (const value of row) {
            const cell = document.createElement("td");
            if (value instanceof Node) cell.append(value);
            else cell.textContent = value;
            line.append(cell);
        }
        body.append(line);
    }

    const element = document.createElement("table");
    element.className = "grid";
    const header = document.createElement("thead");
    header.append(head);
    element.append(header, body);

    return element;
}

function drawSeeds() {
    if (summary.seeds.length === 0) {
        elements.seeds.replaceChildren(empty("Nothing has been listened to yet."));
        return;
    }

    const longest = summary.seeds[0].seconds;

    elements.seeds.replaceChildren(table(
        ["Seed", "Listened", "Plays", ""],
        summary.seeds.map((seed) => {
            const bar = document.createElement("div");
            bar.className = "bar";
            bar.style.width = `${Math.max(2, (seed.seconds / longest) * 100)}%`;

            return [String(seed.seed), seconds(seed.seconds), String(seed.plays), bar];
        })
    ));
}

function drawFailures() {
    if (summary.failures.length === 0) {
        elements.failures.replaceChildren(empty("Nothing has failed. That is either good news or no news."));
        return;
    }

    elements.failures.replaceChildren(table(
        ["What", "Why", "Times"],
        summary.failures.map((failure) => [failure.name, failure.detail, String(failure.count)])
    ));
}

// --- wiring ----------------------------------------------------------------

elements.token.value = readToken();
elements.forget.hidden = readToken() === "";
load();
