import { DRUM_GROUPS, FACETS, LAST_STEP, MIDDLE_STEP, PARTS } from "./settings.js";

/**
 *     The settings a song is asked for with and heard with, as settings.js writes them, and what the server says of the
 *     song it made (X-Song-Settings): every value given, or as the song drew it. What changes the song's notes is asked
 *     of the server; what changes only how it sounds is heard at once.
 */

/** The values given or drawn whole, each by its value before the song is made: the middle of its steps, or the first. */
const SCALARS = { unconventionality: MIDDLE_STEP, tempo: MIDDLE_STEP, key: 0, meter: 0, mode: 0, swing: 0, length: MIDDLE_STEP, darkness: MIDDLE_STEP, intensity: MIDDLE_STEP };

/** Nothing given, every value in the middle and every part and drum group at its fullest, as a first visit starts. */
export function defaultSettings() {
    return {
        ...Object.fromEntries(Object.entries(SCALARS).map(([name, value]) => [name, { isGiven: false, value }])),
        facets: Object.fromEntries(FACETS.map((facet) => [facet, { isGiven: false, value: MIDDLE_STEP }])),
        parts: Object.fromEntries(PARTS.map((part) => [part, {
            plays: "random",
            instrument: { isGiven: false, value: 0 },
            volume: LAST_STEP,
            pan: { isGiven: false, value: MIDDLE_STEP },
        }])),
        drumPieces: { isGiven: false, value: [] },
        drumGroups: Object.fromEntries(DRUM_GROUPS.map((group) => [group, { volume: LAST_STEP }])),
        volume: LAST_STEP,
        genre: noGenre(),
    };
}

/** No genre: every choice as the songs have it. */
export function noGenre() {
    return { name: "", choices: {} };
}

/** The settings with what the song drew let go of, as they are kept for the next visit and the next song. */
export function givenOnly(settings) {
    const fresh = defaultSettings();
    const keep = (given, drawn) => (given.isGiven ? { ...given } : drawn);
    return {
        ...Object.fromEntries(Object.keys(SCALARS).map((name) => [name, keep(settings[name], fresh[name])])),
        facets: Object.fromEntries(FACETS.map((facet) => [facet, keep(settings.facets[facet], fresh.facets[facet])])),
        parts: Object.fromEntries(PARTS.map((part) => {
            const given = settings.parts[part];
            return [part, { ...given, instrument: keep(given.instrument, fresh.parts[part].instrument), pan: keep(given.pan, fresh.parts[part].pan) }];
        })),
        drumPieces: keep(settings.drumPieces, fresh.drumPieces),
        drumGroups: structuredClone(settings.drumGroups),
        volume: settings.volume,
        genre: structuredClone(settings.genre),
    };
}

/** What the server is asked for: the seed, null for a random one, every value given, and the whole mix. */
export function requestFor(seed, settings) {
    const given = (setting) => (setting.isGiven ? setting.value : undefined);
    return {
        seed,
        ...Object.fromEntries(Object.keys(SCALARS).map((name) => [name, given(settings[name])])),
        facets: Object.fromEntries(FACETS.filter((facet) => settings.facets[facet].isGiven).map((facet) => [facet, settings.facets[facet].value])),
        volume: settings.volume,
        parts: Object.fromEntries(PARTS.map((part) => {
            const mix = settings.parts[part];
            return [part, {
                plays: mix.plays === "random" ? undefined : mix.plays === "on",
                instrument: given(mix.instrument),
                volume: mix.volume,
                pan: given(mix.pan),
            }];
        })),
        drumPieces: given(settings.drumPieces),
        drumGroups: Object.fromEntries(DRUM_GROUPS.map((group) => [group, { ...settings.drumGroups[group] }])),
        genre: Object.keys(settings.genre.choices).length > 0 ? settings.genre.choices : undefined,
        genreName: Object.keys(settings.genre.choices).length > 0 ? settings.genre.name : undefined,
    };
}

/** The General MIDI program a riser plays whatever its part's instrument, the reverse cymbal (the server's `Risers.Program`). */
const RISER_PROGRAM = 119;

/**
 *     The settings with what the song drew where nothing was given, from the server's report of it, and what the song
 *     is made of: every part's channel, from 0, as the file has it, whether it is in the song, its own instrument and
 *     pan, whatever the mix plays, the drum groups it plays and its drum pieces, given or drawn.
 */
export function applyReport(settings, report) {
    const next = structuredClone(settings);
    const drawn = (setting, reported) => (setting.isGiven ? setting : { isGiven: false, value: reported.value });
    for (const name of Object.keys(SCALARS)) next[name] = drawn(settings[name], report[name]);
    for (const facet of FACETS) next.facets[facet] = drawn(settings.facets[facet], report.facets[facet]);
    next.drumPieces = drawn(settings.drumPieces, report.drumPieces);

    const channels = {};
    const plays = {};
    const own = {};
    for (const part of PARTS) {
        const reported = report.parts[part];
        const mix = next.parts[part];
        if (!mix.instrument.isGiven) mix.instrument = { isGiven: false, value: reported.instrument };
        if (!mix.pan.isGiven) mix.pan = { isGiven: false, value: reported.pan };
        channels[part] = reported.channel === null ? null : reported.channel - 1;
        plays[part] = reported.plays.value;
        own[part] = { instrument: reported.instrument, pan: reported.pan };
    }

    // a part's other channels, mixed as it is: its second take, a doubled driven guitar's, on the other side, its riser,
    // and the riff's twin, on the other side too, with its own second take back on the riff's
    // each at its track's own level, as the file writes it, a doubled part's takes under the others' (a riser whole)
    const level = (part) => report.parts[part].level;
    const seconds = (part) => (report.parts[part]?.double == null ? [] : [report.parts[part].double - 1]);
    const riser = (part) => (report.parts[part]?.riser == null ? [] : [{ channel: report.parts[part].riser - 1, mirrored: false, program: RISER_PROGRAM, level: 1 }]);
    const others = Object.fromEntries(PARTS.map((part) => [part, [...seconds(part).map((channel) => ({ channel, mirrored: true, level: level(part) })), ...riser(part)]]));
    const twin = report.parts.riffTwin?.channel;
    if (twin != null)
        others.riff.push({ channel: twin - 1, mirrored: true, level: level("riffTwin") }, ...seconds("riffTwin").map((channel) => ({ channel, mirrored: false, level: level("riffTwin") })));
    const levels = Object.fromEntries(PARTS.map((part) => [part, level(part)]));

    return {
        settings: next,
        channels,
        levels,
        others,
        plays,
        own,
        drumGroups: report.drumGroups.map((group) => group.toLowerCase()),
        drumPieces: report.drumPieces.value,
    };
}

/** Whether every part is given out, which leaves nothing to play. */
export function isEveryPartOff(settings) {
    return PARTS.every((part) => settings.parts[part].plays === "off");
}

