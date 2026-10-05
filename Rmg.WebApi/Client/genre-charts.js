/**
 *     The genre's charts, as the editor's graphs and its fine-tune window draw them, from what the songs draw of a choice
 *     (/api/genres/previews, /api/genres/preview): a pool's shares stacked, the commonest in a middling song at the
 *     bottom, another kind's amounts as lines, and the tempo's range as a band; and what each end of the songs draws, in
 *     words.
 */

const SVG = "http://www.w3.org/2000/svg";

/** The options' fills and lines, greys told apart by their lightness, the accent kept for the one chosen. */
export const COLOURS = ["#8f93a3", "#c4c7d3", "#a9adbb", "#e3e4ea", "#787c8c", "#d4d6df"];

/** The ends of the songs, as the editor names them. */
export const ENDS = ["Plainest", "Middling", "Experimental"];

export function svg(name, attributes = {}) {
    const element = document.createElementNS(SVG, name);
    for (const [key, value] of Object.entries(attributes)) element.setAttribute(key, String(value));
    return element;
}

/** The least and the most of an option's amounts, by its choice's kind and its name (GenreAmounts). */
export function rangeOf(choice, option) {
    if (choice.kind !== "range") return [0, 100];
    return option === "lean" ? [-100, 100] : [60, 240];
}

/** An option's amounts at the plainest point, the middle one and the last. */
const atEnds = (along) => [along[0], along[(along.length - 1) / 2], along[along.length - 1]];

/** The options a pool's shares name, the commonest in a middling song first, which is drawn at the bottom. */
export function byMiddle(shares) {
    return Object.keys(shares).sort((a, b) => atEnds(shares[b])[1] - atEnds(shares[a])[1]);
}

/** An option's name as its choice labels it. */
export const labelOf = (choice) => (name) => choice.options.find((x) => x.name === name)?.label ?? name;

/** An option's mark in a chart, named by its title where it is pointed at. */
export function mark(attributes, label) {
    const element = svg("path", attributes);
    const title = svg("title");
    title.textContent = label;
    element.append(title);
    return element;
}

/**
 *     A pool's shares, in percent, stacked from the bottom in the order given, each its fill and its label, in a chart of the
 *     size given.
 */
export function drawShares(element, shares, names, fillOf, titleOf, width, height) {
    let below = null;
    for (const name of names) {
        const along = shares[name];
        below ??= along.map(() => 0);
        const above = below.map((y, j) => y + along[j]);
        const xOf = (j) => ((j / (along.length - 1)) * width).toFixed(1);
        const yOf = (value) => (height - (value / 100) * height).toFixed(1);
        const top = above.map((y, j) => `${xOf(j)},${yOf(y)}`);
        const bottom = below.map((y, j) => `${xOf(j)},${yOf(y)}`).reverse();
        element.append(mark({ d: `M${[...top, ...bottom].join(" L")} Z`, fill: fillOf(name), class: "share", "data-option": name }, titleOf(name)));
        below = above;
    }
}

/** Amounts along the songs as lines, between the least and the most, each its stroke and its label. */
export function drawLines(element, curves, names, [least, most], strokeOf, titleOf, width, height) {
    for (const name of names) {
        const along = curves[name];
        const points = along.map((value, j) => `${((j / (along.length - 1)) * width).toFixed(1)},${(height - ((value - least) / (most - least)) * height).toFixed(1)}`);
        element.append(mark({ d: `M${points.join(" L")}`, fill: "none", stroke: strokeOf(name), "stroke-width": 3, "data-option": name }, titleOf(name)));
    }
}

/** The tempo's range as a band between its ends, in beats a minute. */
export function drawBand(element, from, to, [least, most], width, height) {
    const xOf = (j) => ((j / (from.length - 1)) * width).toFixed(1);
    const yOf = (value) => (height - ((value - least) / (most - least)) * height).toFixed(1);
    const top = to.map((value, j) => `${xOf(j)},${yOf(value)}`);
    const bottom = from.map((value, j) => `${xOf(j)},${yOf(value)}`).reverse();
    element.append(svg("path", { d: `M${[...top, ...bottom].join(" L")} Z`, class: "genre-band" }));
    for (const end of [top, from.map((value, j) => `${xOf(j)},${yOf(value)}`)])
        element.append(svg("path", { d: `M${end.join(" L")}`, class: "genre-band-end" }));
}

/**
 *     What each end of the songs draws, in words: a pool's options by their shares, a chance's by its chance and a value
 *     by how far it is from as it is, at 50, the greatest first, none at 0 or as it is, at most as many as given; the
 *     tempo its range.
 */
export function endsInWords(choice, preview, count = Infinity) {
    const label = labelOf(choice);
    if (choice.kind === "range") {
        const { from, to } = preview.curves;
        return [0, 1, 2].map((end) => [`${Math.round(atEnds(from)[end])}–${Math.round(atEnds(to)[end])} bpm`]);
    }

    const amounts = choice.kind === "pool" ? preview.shares : preview.curves;
    // a value by how far it is from as it is, at 50, in its own numbers, as the window shows them
    const sizeOf = (amount) => (choice.kind === "values" ? Math.abs(amount - 50) : amount);
    const said = (amount) => (choice.kind === "values" ? `${Math.round(amount)}` : `${Math.round(amount)}%`);
    return [0, 1, 2].map((end) => Object.keys(amounts)
        .map((name) => ({ name, amount: atEnds(amounts[name])[end] }))
        .filter((x) => Math.round(sizeOf(x.amount)) > 0)
        .sort((a, b) => sizeOf(b.amount) - sizeOf(a.amount))
        .slice(0, count)
        .map((x) => `${label(x.name)} ${said(x.amount)}`));
}
