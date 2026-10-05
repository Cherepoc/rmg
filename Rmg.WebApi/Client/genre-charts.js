/**
 *     The genre's charts, as the editor's graphs and its fine-tune window draw them, from what the songs draw of a choice
 *     (/api/genres/previews, /api/genres/preview): a pool's shares stacked, the commonest in a middling song at the
 *     bottom, another kind's amounts as lines, and a range (the tempo, the length) as a band, shaded where its songs land;
 *     and what each end of the songs draws, in words.
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
    return option === "lean" ? [-100, 100] : [choice.scale.least, choice.scale.most];
}

/** A range's ends in words by its unit: a length's as minutes and seconds, "1:33–3:28", another's as "110–150 bpm". */
export function rangeInWords(choice, from, to) {
    return choice.scale.unit === "seconds" ? `${time(from)}–${time(to)}` : `${Math.round(from)}–${Math.round(to)} ${choice.scale.unit}`;
}

/** Seconds as minutes and seconds, "3:30". */
const time = (seconds) => `${Math.floor(Math.round(seconds) / 60)}:${String(Math.round(seconds) % 60).padStart(2, "0")}`;

/** Whether an option's numbers are times, a length's ends, shown and given as minutes and seconds. */
export const isTime = (choice, option) => choice.kind === "range" && option !== "lean" && choice.scale.unit === "seconds";

/** An option's number as the page shows it: a time as minutes and seconds, "3:30", another as it is. */
export function numberInWords(choice, option, number) {
    return isTime(choice, option) ? time(number) : String(number);
}

/** An option's number of what was typed: a time as minutes and seconds, "3:30", or minutes alone, "3.5"; NaN for no number. */
export function numberOfWords(choice, option, text) {
    if (!isTime(choice, option)) return text.trim() === "" ? NaN : Number(text);
    const [minutes, seconds, more] = text.trim().split(":");
    if (more !== undefined || minutes === "") return NaN;
    return seconds === undefined ? Number(minutes) * 60 : Number(minutes) * 60 + Number(seconds);
}

/** What a range's ends count, said after its name in a window's heading; nothing for a lean or another kind. */
export function unitInWords(choice, option) {
    if (choice.kind !== "range" || option === "lean") return "";
    return choice.scale.unit === "seconds" ? ", in minutes and seconds" : choice.scale.unit === "bpm" ? ", in beats a minute" : `, in ${choice.scale.unit}`;
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

/** A range's number as its amount, from 0, its least, to 1, its most, between the numbers its scale is told by. */
export function amountOf(choice, number) {
    const numbers = choice.scale.numbers;
    // the step it is in, the last for the most and beyond
    const above = numbers.findIndex((x) => x > number);
    const i = above === -1 ? numbers.length - 2 : Math.max(0, above - 1);
    const at = (number - numbers[i]) / (numbers[i + 1] - numbers[i]);
    return Math.max(0, Math.min(1, (i + at) / (numbers.length - 1)));
}

/** A range's amount, from 0 to 1, as its number (amountOf). */
export function numberOf(choice, amount) {
    const numbers = choice.scale.numbers;
    const at = Math.max(0, Math.min(1, amount)) * (numbers.length - 1);
    const i = Math.min(numbers.length - 2, Math.floor(at));
    return numbers[i] + (at - i) * (numbers[i + 1] - numbers[i]);
}

/** The power a range's place is raised to at a lean, from -100 to 100, the place the mean of two evens (SongGenerator.Across). */
const powerOf = (choice, lean) => Math.exp((-choice.scale.leanSkew * lean) / 100);

/** Where a range's draws centre at a lean, from 0, its from, to 1, its to: the median, (1/2) to the power. */
export const centreOf = (choice, lean) => 0.5 ** powerOf(choice, lean);

/** The lean whose draws centre where given, from 0, the from, to 1, the to, as a whole number from -100 to 100. */
export function leanOf(choice, centre) {
    const lean = (-100 * Math.log(Math.log(Math.max(1e-6, Math.min(1 - 1e-6, centre))) / Math.log(0.5))) / choice.scale.leanSkew;
    return Math.round(Math.max(-100, Math.min(100, lean)));
}

/** How likely a draw lands at a place in a range, from 0 to 1, at a power: the mean of two evens to the power. */
function likelihood(place, power) {
    const even = place ** (1 / power);
    return (even < 0.5 ? 4 * even : 4 * (1 - even)) * even / (power * place);
}

// the masks' names, every band's its own, so that two on a page never take each other's
let bands = 0;

/**
 *     A range as a band between its ends along the songs, on its amounts' own scale, such as a length's every like
 *     ratio a like height: every column of it shaded by how likely a song lands at each height in it, the likeliest the
 *     brightest, a line where its draws centre, and its ends. Its curves are its from's, its to's and its lean's numbers,
 *     the lean's none where the genre names its ends alone.
 */
export function drawBand(element, choice, { from, to, lean: leaning }, width, height) {
    // a genre naming its ends alone leans to the middle (Genre.Range)
    const lean = leaning ?? from.map(() => 0);
    const band = svg("g", { class: "genre-band" });
    const xOf = (j) => (j / (from.length - 1)) * width;
    const yOf = (amount) => height - amount * height;
    const low = from.map((x) => amountOf(choice, x));
    const high = to.map((x) => amountOf(choice, x));
    const centre = lean.map((x, j) => low[j] + (high[j] - low[j]) * centreOf(choice, x));

    // the shading a mask over the accent, a pixel a point, each column's likeliest the brightest
    const canvas = document.createElement("canvas");
    canvas.width = Math.max(2, Math.round(width));
    canvas.height = Math.max(1, Math.round(height));
    const image = canvas.getContext("2d").createImageData(canvas.width, canvas.height);
    for (let x = 0; x < canvas.width; x++) {
        const at = (x / (canvas.width - 1)) * (from.length - 1);
        const j = Math.min(from.length - 2, Math.floor(at));
        const along = (values) => values[j] + (at - j) * (values[j + 1] - values[j]);
        const top = yOf(along(high)) * (canvas.height / height);
        const bottom = yOf(along(low)) * (canvas.height / height);
        const power = powerOf(choice, along(lean));
        const column = [];
        for (let y = Math.max(0, Math.ceil(top)); y <= Math.min(canvas.height - 1, Math.floor(bottom)); y++)
            column.push([y, bottom - top < 1 ? 1 : likelihood(Math.min(0.995, Math.max(0.005, (bottom - y) / (bottom - top))), power)]);
        const most = Math.max(...column.map(([, likely]) => likely));
        for (const [y, likely] of column) {
            const i = 4 * (y * canvas.width + x);
            image.data.fill(255, i, i + 3);
            image.data[i + 3] = Math.round((255 * 0.85 * likely) / most);
        }
    }
    canvas.getContext("2d").putImageData(image, 0, 0);
    const id = `band${bands++}`;
    const mask = svg("mask", { id, maskUnits: "userSpaceOnUse", x: 0, y: 0, width, height });
    mask.append(svg("image", { href: canvas.toDataURL(), x: 0, y: 0, width, height, preserveAspectRatio: "none" }));
    band.append(mask, svg("rect", { x: 0, y: 0, width, height, mask: `url(#${id})`, class: "genre-band-shading" }));

    const line = (amounts, className) => svg("path", { d: `M${amounts.map((x, j) => `${xOf(j).toFixed(1)},${yOf(x).toFixed(1)}`).join(" L")}`, class: className });
    band.append(line(high, "genre-band-end"), line(low, "genre-band-end"), line(centre, "genre-band-centre"));
    element.append(band);
    return band;
}

/**
 *     What each end of the songs draws, in words: a pool's options by their shares, a chance's by its chance and a value
 *     by how far it is from as it is, at 50, the greatest first, none at 0 or as it is, at most as many as given, the last
 *     with how many more are as much; a range its ends by its unit. Each as its words and its number apart, "" for a
 *     range's, so that a page short of room cuts the words and never the number.
 */
export function endsInWords(choice, preview, count = Infinity) {
    const label = labelOf(choice);
    if (choice.kind === "range") {
        const { from, to } = preview.curves;
        return [0, 1, 2].map((end) => [{ words: rangeInWords(choice, atEnds(from)[end], atEnds(to)[end]), number: "" }]);
    }

    const amounts = choice.kind === "pool" ? preview.shares : preview.curves;
    // a value by how far it is from as it is, at 50, in its own numbers, as the window shows them
    const sizeOf = (amount) => (choice.kind === "values" ? Math.abs(amount - 50) : amount);
    const said = (amount) => (choice.kind === "values" ? `${Math.round(amount)}` : `${Math.round(amount)}%`);
    return [0, 1, 2].map((end) => {
        const told = Object.keys(amounts)
            .map((name) => ({ name, amount: atEnds(amounts[name])[end] }))
            .filter((x) => Math.round(sizeOf(x.amount)) > 0)
            .sort((a, b) => sizeOf(b.amount) - sizeOf(a.amount));
        // the last one said with those left out that are said as it is, so that none seems the only one so
        return told.slice(0, count).map((x, i) => {
            const tied = i === count - 1 ? told.slice(count).filter((y) => said(y.amount) === said(x.amount)).length : 0;
            return { words: `${label(x.name)}${tied > 0 ? ` and ${tied} more` : ""}`, number: said(x.amount) };
        });
    });
}
