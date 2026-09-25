/**
 *     The song as the rest of the device sees it: the lock screen and notification on a phone, the media
 *     keys and the browser's own media controls elsewhere, with the title, a progress bar and buttons.
 *
 *     A browser only offers those for a page that is playing media, and the synthesizer plays through Web
 *     Audio, which does not count. So while a song plays, a silent clip of audio loops beside it: the
 *     sound is the synthesizer's as before, and the clip is only there to make the page media. Anything a
 *     browser refuses here is let go of quietly, since the song plays the same without any of it.
 */

/** Chrome on Android shows no controls for media shorter than five seconds. */
const SILENCE_SECONDS = 6;

/** How far the lock screen's skip buttons move, when the device does not say. */
const SKIP_SECONDS = 10;

/**
 *     <paramref name="actions" /> is what the buttons do: play, pause, stop, next, and seek, which is
 *     given a time to go to, or null and an offset to move by.
 */
export function createMediaControls(actions) {
    if (!("mediaSession" in navigator)) return { playing() {}, paused() {} };

    const session = navigator.mediaSession;
    let silence = null;
    let artwork = null;
    let shownSeed = null;

    handle("play", () => actions.play());
    handle("pause", () => actions.pause());
    handle("stop", () => actions.stop());
    handle("nexttrack", () => actions.next());
    handle("seekto", (details) => actions.seek(details.seekTime, 0));
    handle("seekbackward", (details) => actions.seek(null, -(details.seekOffset ?? SKIP_SECONDS)));
    handle("seekforward", (details) => actions.seek(null, details.seekOffset ?? SKIP_SECONDS));

    function show(seed, position, duration, state) {
        if (seed !== shownSeed) {
            shownSeed = seed;
            session.metadata = new MediaMetadata({
                title: seed ? `Song ${seed}` : "RMG",
                artist: "RMG",
                album: "Random Music Generator",
                artwork: artwork ?? [],
            });
        }

        session.playbackState = state;

        // without a length there is no bar to draw, and a position past the end is refused outright
        if (!(duration > 0)) return;
        try {
            session.setPositionState({ duration, position: Math.min(Math.max(position, 0), duration), playbackRate: 1 });
        } catch {
            // an older browser with a session but no progress bar
        }
    }

    // drawn once, in the background: the metadata picks it up with the next song shown
    drawArtwork().then((images) => {
        artwork = images;
        if (session.metadata) session.metadata.artwork = images;
    }, () => {});

    return {
        /** A song is playing, or has just moved; the device's clock runs the bar on from here. */
        playing(seed, position, duration) {
            silence ??= createSilence();

            // refused without a gesture on some browsers, which only costs the controls, not the sound
            silence.play().catch(() => {});
            show(seed, position, duration, "playing");
        },

        /** Paused, stopped or ended: the controls stay, so the song can be started again from them. */
        paused(seed, position, duration) {
            silence?.pause();
            show(seed, position, duration, "paused");
        },
    };

    function handle(action, handler) {
        try {
            session.setActionHandler(action, handler);
        } catch {
            // an action this browser does not know, which simply has no button
        }
    }
}

function createSilence() {
    const element = new Audio(silentWave(SILENCE_SECONDS));
    element.loop = true;
    return element;
}

/** Eight-bit mono PCM at 8 kHz, every sample at the midpoint, which is silence; about 48 KB in memory. */
function silentWave(seconds) {
    const rate = 8000;
    const length = rate * seconds;
    const bytes = new Uint8Array(44 + length).fill(128, 44);
    const view = new DataView(bytes.buffer);
    const text = (offset, value) => [...value].forEach((character, index) => view.setUint8(offset + index, character.charCodeAt(0)));

    text(0, "RIFF");
    view.setUint32(4, 36 + length, true);
    text(8, "WAVE");
    text(12, "fmt ");
    view.setUint32(16, 16, true);
    view.setUint16(20, 1, true); // PCM
    view.setUint16(22, 1, true); // mono
    view.setUint32(24, rate, true);
    view.setUint32(28, rate, true); // bytes a second
    view.setUint16(32, 1, true); // bytes a sample
    view.setUint16(34, 8, true); // bits a sample
    text(36, "data");
    view.setUint32(40, length, true);

    return URL.createObjectURL(new Blob([bytes], { type: "audio/wav" }));
}

/** The favicon, as the SVG it is and as a PNG drawn from it, for a device that will not take an SVG. */
async function drawArtwork() {
    const svg = new URL("favicon.svg", location.href).href;
    const image = new Image();
    image.src = svg;
    await image.decode();

    const canvas = document.createElement("canvas");
    canvas.width = canvas.height = 512;
    canvas.getContext("2d").drawImage(image, 0, 0, 512, 512);

    return [
        { src: canvas.toDataURL("image/png"), sizes: "512x512", type: "image/png" },
        { src: svg, sizes: "any", type: "image/svg+xml" },
    ];
}
