/**
 *     A part's pulse: a dot in the part's own colour, as large and as bright as the part plays, which throws off a ring
 *     where its level jumps, a note struck; so that a held pad glows, a bass breathes and drums and plucked parts
 *     ripple. It is only looked at, as a meter is, and says nothing to a reader.
 */

/** How wide and high the pulse is, in CSS pixels. */
const SIZE = 28;

/** How far a level must jump from one frame to the next to throw off a ring: about 5 dB of the meter's span. */
const ATTACK = 0.12;

/** How long a ring takes to spread and fade, in seconds, and how many there are at most at once. */
const RING_SECONDS = 0.6;
const MOST_RINGS = 3;

// a ring is motion only, which a visitor who asked for less of it does without
const calm = matchMedia("(prefers-reduced-motion: reduce)");

/** A pulse in the colour of <paramref name="hue" />, drawn idle until it is told a level. */
export function createPulse(hue) {
    const canvas = document.createElement("canvas");
    canvas.className = "pulse";
    canvas.setAttribute("aria-hidden", "true");
    const scale = window.devicePixelRatio || 1;
    canvas.width = canvas.height = Math.round(SIZE * scale);
    const context = canvas.getContext("2d");
    context.scale(scale, scale);

    let last = 0;
    let rings = [];

    /** Draws the level, from 0 to 1, as it is at <paramref name="now" />, in seconds; idle when nothing plays. */
    function update(level, isPlaying, now) {
        if (isPlaying && level - last > ATTACK && rings.length < MOST_RINGS && !calm.matches) rings.push(now);
        last = isPlaying ? level : 0;
        rings = rings.filter((born) => isPlaying && now - born < RING_SECONDS);

        const middle = SIZE / 2;
        const radius = 4 + last * (middle - 5);
        context.clearRect(0, 0, SIZE, SIZE);

        // bright in the middle and clear at the edge, a glow rather than a disc
        const strength = 0.4 + 0.6 * last;
        const glow = context.createRadialGradient(middle, middle, 0, middle, middle, radius);
        glow.addColorStop(0, `hsl(${hue} 90% 78% / ${strength})`);
        glow.addColorStop(0.45, `hsl(${hue} 85% 60% / ${0.6 * strength})`);
        glow.addColorStop(1, `hsl(${hue} 80% 55% / 0)`);
        context.fillStyle = glow;
        context.beginPath();
        context.arc(middle, middle, radius, 0, 2 * Math.PI);
        context.fill();

        for (const born of rings) {
            const age = (now - born) / RING_SECONDS;
            context.strokeStyle = `hsl(${hue} 85% 62% / ${1 - age})`;
            context.lineWidth = 1.5;
            context.beginPath();
            context.arc(middle, middle, radius + age * (middle - radius - 1), 0, 2 * Math.PI);
            context.stroke();
        }
    }

    update(0, false, 0);
    return { canvas, update };
}
