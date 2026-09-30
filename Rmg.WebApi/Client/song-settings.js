import { DRUM_GROUPS, DRUM_SETUPS, FACETS, FORMAT_PARTS, LAST_STEP, MIDDLE_PAN, PARTS } from "./settings.js";

/**
 *     The settings a song is asked for with and heard with, as settings.js writes them, and what the server says of the
 *     song it made (X-Song-Settings): every value given, or as the song drew it. What changes the song's notes is asked
 *     of the server; what changes only how it sounds is heard at once.
 */

/** Nothing given, every value in the middle and every part and drum group at its fullest, as a first visit starts. */
export function defaultSettings() {
    return {
        unconventionality: { isGiven: false, value: 32 },
        facets: Object.fromEntries(FACETS.map((facet) => [facet, { isGiven: false, value: 32 }])),
        tempo: { isGiven: false, value: 0 },
        key: { isGiven: false, value: 0 },
        meter: { isGiven: false, value: 0 },
        parts: Object.fromEntries(FORMAT_PARTS.map((part) => [part, {
            plays: "random",
            instrument: { isGiven: false, value: 0 },
            volume: LAST_STEP,
            pan: { isGiven: false, value: MIDDLE_PAN },
            isOn: true,
        }])),
        drumSetup: null,
        drumGroups: Object.fromEntries(DRUM_GROUPS.map((group) => [group, { isOn: true, volume: LAST_STEP }])),
        volume: LAST_STEP,
    };
}

/** The settings with what the song drew let go of, as they are kept for the next visit and the next song. */
export function givenOnly(settings) {
    const fresh = defaultSettings();
    const keep = (given, drawn) => (given.isGiven ? { ...given } : drawn);
    return {
        unconventionality: keep(settings.unconventionality, fresh.unconventionality),
        facets: Object.fromEntries(FACETS.map((facet) => [facet, keep(settings.facets[facet], fresh.facets[facet])])),
        tempo: keep(settings.tempo, fresh.tempo),
        key: keep(settings.key, fresh.key),
        meter: keep(settings.meter, fresh.meter),
        parts: Object.fromEntries(FORMAT_PARTS.map((part) => {
            const given = settings.parts[part];
            return [part, { ...given, instrument: keep(given.instrument, fresh.parts[part].instrument), pan: keep(given.pan, fresh.parts[part].pan) }];
        })),
        drumSetup: settings.drumSetup,
        drumGroups: structuredClone(settings.drumGroups),
        volume: settings.volume,
    };
}

/** What the server is asked for: the seed, null for a random one, every value given, and the whole mix. */
export function requestFor(seed, settings) {
    const given = (setting) => (setting.isGiven ? setting.value : undefined);
    return {
        seed,
        unconventionality: given(settings.unconventionality),
        facets: Object.fromEntries(FACETS.filter((facet) => settings.facets[facet].isGiven).map((facet) => [facet, settings.facets[facet].value])),
        volume: settings.volume,
        parts: Object.fromEntries(PARTS.map((part) => {
            const mix = settings.parts[part];
            return [part, {
                plays: mix.plays === "random" ? undefined : mix.plays === "on",
                instrument: given(mix.instrument),
                volume: mix.volume,
                pan: given(mix.pan),
                isOn: mix.isOn,
            }];
        })),
        drumSetup: settings.drumSetup ?? undefined,
        tempo: given(settings.tempo),
        key: given(settings.key),
        meter: given(settings.meter),
        drumGroups: Object.fromEntries(DRUM_GROUPS.map((group) => [group, { ...settings.drumGroups[group] }])),
    };
}

/**
 *     The settings with what the song drew where nothing was given, from the server's report of it, and what the song
 *     is made of: every part's channel, from 0, as the file has it, whether it is in the song, its own instrument and
 *     pan, whatever the mix plays, and the drum groups it plays.
 */
export function applyReport(settings, report) {
    const next = structuredClone(settings);
    const drawn = (setting, reported) => (setting.isGiven ? setting : { isGiven: false, value: reported.value });
    next.unconventionality = drawn(settings.unconventionality, report.unconventionality);
    for (const facet of FACETS) next.facets[facet] = drawn(settings.facets[facet], report.facets[facet]);
    next.tempo = drawn(settings.tempo, report.tempo);
    next.key = drawn(settings.key, report.key);
    next.meter = drawn(settings.meter, report.meter);

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

    // a part's twin, the riff's, which plays on a channel of its own as its part is mixed, on the other side
    const twin = report.parts.riffTwin?.channel;
    const twins = { riff: twin == null ? null : twin - 1 };

    return {
        settings: next,
        channels,
        twins,
        plays,
        own,
        drumSetup: report.drumSetup.value,
        drumGroups: report.drumGroups.map((group) => group.toLowerCase()),
    };
}

/** Whether every part is given out, which leaves nothing to play. */
export function isEveryPartOff(settings) {
    return PARTS.every((part) => settings.parts[part].plays === "off");
}

export { DRUM_SETUPS };
