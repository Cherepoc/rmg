/**
 *     The analytics dashboard. It asks the server for one summary and draws it; nothing here talks to
 *     anything else, and the charts are plain SVG rather than a library, a page this small needing none.
 */

import { showStatus } from "./text.js";

const elements = {
    controls: document.getElementById("controls"),
    range: document.getElementById("range"),
    refresh: document.getElementById("refresh"),
    taken: document.getElementById("taken"),
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
    versionsPanel: document.getElementById("versions-panel"),
    versions: document.getElementById("versions"),
    ratedPanel: document.getElementById("rated-panel"),
    ratedHint: document.getElementById("rated-hint"),
    rated: document.getElementById("rated"),
    fifthsPanel: document.getElementById("fifths-panel"),
    fifths: document.getElementById("fifths"),
    genresPanel: document.getElementById("genres-panel"),
    genres: document.getElementById("genres"),
    verdictsPanel: document.getElementById("verdicts-panel"),
    verdicts: document.getElementById("verdicts"),
    failuresPanel: document.getElementById("failures-panel"),
    failures: document.getElementById("failures"),
    serverPanel: document.getElementById("server-panel"),
    outcomes: document.getElementById("outcomes"),
    speeds: document.getElementById("speeds"),
    speedFifthsTitle: document.getElementById("speed-fifths-title"),
    speedFifths: document.getElementById("speed-fifths"),
    serverDaily: document.getElementById("server-daily"),
};

const SERIES = [
    { key: "visitors", name: "Visitors", role: "--series-1" },
    { key: "songs", name: "Songs", role: "--series-2" },
    { key: "plays", name: "Plays", role: "--series-3" },
];

let summary = null;

// what the pointer over the day by day chart reads from, replaced on every draw
let chart = null;

// --- loading ---------------------------------------------------------------

elements.refresh.addEventListener("click", () => load());
elements.range.addEventListener("change", () => load());

function setStatus(message, isError = false) {
    showStatus(elements.status, message, isError);
}

function showPanels(isShown) {
    for (const panel of [elements.headline, elements.timings, elements.funnelPanel,
        elements.dailyPanel, elements.seedsPanel, elements.versionsPanel, elements.ratedPanel, elements.fifthsPanel, elements.genresPanel, elements.verdictsPanel, elements.failuresPanel, elements.serverPanel])
        panel.hidden = !isShown;

    elements.controls.hidden = !isShown;
}

// how many times the summary was asked for, the latest answer the one shown
let loads = 0;

/**
 *     Reads the summary from the admin area, whose credentials the browser asked for when the page was opened and sends
 *     on with every request under it.
 */
async function load() {
    setStatus("Reading…");
    // the latest asked for alone shown, a slower earlier answer let go of
    const asked = ++loads;

    let response;
    let read;
    try {
        response = await fetch(new URL(`api/summary?days=${encodeURIComponent(elements.range.value)}`, location.href));
        read = response.ok ? await response.json() : null;
    } catch (error) {
        if (asked !== loads) return;
        elements.controls.hidden = false;
        setStatus(`Could not read the summary: ${error.message}`, true);
        return;
    }
    if (asked !== loads) return;

    if (response.status === 404) {
        showPanels(false);
        setStatus("This server counts nothing, so it has no summary to show.", true);
        return;
    }

    if (!response.ok) {
        // the controls shown, so that a failed first read can be tried again
        elements.controls.hidden = false;
        setStatus(`The server answered ${response.status}.`, true);
        return;
    }

    summary = read;
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

    const whole = Math.round(ms / 1000);
    return `${Math.floor(whole / 60)}m ${whole % 60}s`;
}

function seconds(value) {
    if (value === null || value === undefined) return "—";
    if (value < 60) return `${value.toFixed(0)} s`;

    const whole = Math.round(value);
    return `${Math.floor(whole / 60)}m ${whole % 60}s`;
}

function percent(part, whole) {
    return whole > 0 ? `${Math.round((part / whole) * 100)}%` : "—";
}

function set(id, text) {
    document.getElementById(id).textContent = text;
}

// --- drawing ---------------------------------------------------------------

function draw() {
    const played = summary.funnel.find((step) => step.key === "played")?.visitors ?? 0;

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
    drawVersions();
    drawRated();
    drawFifths();
    drawGenres();
    drawVerdicts();
    drawFailures();
    drawServer();
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

    // the chart where the table is not the one shown
    elements.dailyFigure.hidden = !elements.dailyTable.hidden;
    elements.dailyPanel.querySelector(".empty")?.remove();

    const highest = Math.max(1, ...days.flatMap((day) => SERIES.map((series) => day[series.key])));
    // a whole one at the least, as a day of a visitor or two counts no fractions
    const step = Math.max(1, niceStep(highest));
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
    // the chart where the table is not shown, but where there are no days, whose empty chart stays hidden
    elements.dailyFigure.hidden = isShown || summary.daily.length === 0;
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

    // in a box of its own that scrolls sideways, so that a wide table on a phone moves within its panel, not the page
    const box = document.createElement("div");
    box.className = "grid-scroll";
    box.append(element);
    return box;
}

function drawSeeds() {
    if (summary.seeds.length === 0) {
        elements.seeds.replaceChildren(empty("Nothing has been listened to yet."));
        return;
    }

    const longest = summary.seeds[0].seconds;

    elements.seeds.replaceChildren(table(
        ["Seed", "Settings", "Version", "Genre", "Listened", "Stretches", ""],
        summary.seeds.map((seed) => {
            const bar = document.createElement("div");
            bar.className = "bar";
            bar.style.width = `${Math.max(2, (seed.seconds / longest) * 100)}%`;

            return [seed.seed, seed.identity === "" ? "drawn" : seed.identity, seed.version ?? "", seed.genre ?? "", seconds(seed.seconds), String(seed.plays), bar];
        })
    ));
}

function drawVersions() {
    if (summary.versions.length === 0) {
        elements.versions.replaceChildren(empty("No event has named its version yet."));
        return;
    }

    elements.versions.replaceChildren(table(
        ["Version", "Since", "Songs", "Plays", "Listens", "Past 30s", "Median", "Liked", "Disliked"],
        summary.versions.map((version) => [
            version.version,
            version.firstDay,
            String(version.songs),
            String(version.plays),
            String(version.listens),
            percent(version.listensPast30s, version.listens),
            seconds(version.medianSeconds),
            String(version.likes),
            String(version.dislikes),
        ])
    ));
}

function drawRated() {
    if (summary.rated.length === 0) {
        elements.ratedHint.textContent = "The songs of the latest version rated, the most liked first.";
        elements.rated.replaceChildren(empty("No song has been rated yet."));
        return;
    }

    elements.ratedHint.textContent = `The songs of ${summary.ratedVersion} rated, the most liked first. A seed is this song only in this version.`;
    elements.rated.replaceChildren(table(
        ["Song", "Settings given", "Genre", "Liked", "Disliked"],
        summary.rated.map((seed) => [seed.seed, seed.identity === "" ? "drawn" : seed.identity, seed.genre === "" ? "none" : seed.genre, String(seed.likes), String(seed.dislikes)])
    ));
}

const FIFTHS = ["Plainest", "Plain", "Middle", "Experimental", "Most experimental"];

/** Ratings by fifths of how plain or experimental the songs are, with how many songs each is of, as a share of few is little. */
function drawFifths() {
    if (summary.ratedFifths.length === 0) {
        elements.fifths.replaceChildren(empty("No song has been rated with its settings yet."));
        return;
    }

    elements.fifths.replaceChildren(table(
        ["Songs", "Fifth", "Rated", "Liked", "Disliked", "Share liked"],
        summary.ratedFifths.map((fifth) => [
            fifth.isGiven ? "asked for" : "drawn",
            `${FIFTHS[fifth.fifth]} (${fifth.fifth * 20}–${fifth.fifth * 20 + 20}%)`,
            String(fifth.songs),
            String(fifth.likes),
            String(fifth.dislikes),
            percent(fifth.likes, fifth.likes + fifth.dislikes),
        ])
    ));
}

/** Ratings by genre, every genre made on the page together, with how many songs each is of. */
function drawGenres() {
    if (summary.ratedGenres.length === 0) {
        elements.genres.replaceChildren(empty("No song has been rated yet."));
        return;
    }

    elements.genres.replaceChildren(table(
        ["Genre", "Rated", "Liked", "Disliked", "Share liked"],
        summary.ratedGenres.map((genre) => [
            genre.genre === "" ? "none" : genre.genre === "own" ? "made on the page" : genre.genre,
            String(genre.songs),
            String(genre.likes),
            String(genre.dislikes),
            percent(genre.likes, genre.likes + genre.dislikes),
        ])
    ));
}

/** The listening mode's verdicts by question, of the latest version given any. */
function drawVerdicts() {
    if (summary.verdicts.length === 0) {
        elements.verdicts.replaceChildren(empty("No verdict has been given yet."));
        return;
    }

    elements.verdicts.replaceChildren(table(
        [`Question (${summary.verdictVersion})`, "With better", "Without better", "The same", "Can't tell"],
        summary.verdicts.map((x) => [x.question, String(x.with), String(x.without), String(x.same), String(x.unsure)])
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

const OUTCOMES = {
    made: "Made",
    cancelled_waiting: "Given up while waiting",
    cancelled_making: "Given up while being made",
    refused_client: "Turned away, too many of one client's waiting",
    refused_full: "Turned away, the queue full",
    failed: "Failed",
};

function drawServer() {
    const server = summary.server;
    drawSpread("mk", server.makeMs, duration);
    drawSpread("rn", server.renderMs, duration);
    drawSpread("wt", server.waitMs, duration);

    const asked = server.outcomes.reduce((sum, outcome) => sum + outcome.count, 0);
    elements.outcomes.replaceChildren(asked === 0
        ? empty("No song has been asked of the server yet.")
        : table(
            ["How it ended", "Songs", "Share"],
            server.outcomes.map((outcome) => [OUTCOMES[outcome.outcome] ?? outcome.outcome, count(outcome.count), percent(outcome.count, asked)])
        ));

    elements.speeds.replaceChildren(server.versions.length === 0
        ? empty("No song has been made yet.")
        : table(
            ["Version", "Made", "Median", "19 in 20"],
            server.versions.map((version) => [version.version, count(version.songs), duration(version.medianMs), duration(version.worstMs)])
        ));

    elements.speedFifthsTitle.textContent = server.fifthsVersion === null
        ? "By how plain or experimental"
        : `By how plain or experimental, in ${server.fifthsVersion}`;
    elements.speedFifths.replaceChildren(server.fifths.length === 0
        ? empty("No song has been made yet.")
        : table(
            ["Fifth", "Made", "Median", "19 in 20"],
            server.fifths.map((fifth) => [
                `${FIFTHS[fifth.fifth]} (${fifth.fifth * 20}–${fifth.fifth * 20 + 20}%)`,
                count(fifth.songs),
                duration(fifth.medianMs),
                duration(fifth.worstMs),
            ])
        ));

    elements.serverDaily.replaceChildren(server.daily.length === 0
        ? empty("No song has been asked of the server yet.")
        : table(
            ["Day", "Made", "Given up", "Turned away", "Failed", "Most waiting"],
            server.daily.map((day) => [day.day, count(day.made), count(day.cancelled), count(day.refused), count(day.failed), count(day.mostWaiting)])
        ));
}

// --- wiring ----------------------------------------------------------------

load();
