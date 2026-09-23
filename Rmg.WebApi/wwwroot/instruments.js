/** General MIDI keeps channel 10 for percussion, counted from 0 like the synthesizer counts channels. */
export const DRUM_CHANNEL = 9;

/** The 128 General MIDI instruments, in the sixteen families of eight the standard groups them into. */
export const INSTRUMENT_FAMILIES = [
    {
        name: "Piano",
        instruments: [
            "Acoustic Grand Piano", "Bright Acoustic Piano", "Electric Grand Piano", "Honky-tonk Piano",
            "Electric Piano 1", "Electric Piano 2", "Harpsichord", "Clavi",
        ],
    },
    {
        name: "Chromatic Percussion",
        instruments: [
            "Celesta", "Glockenspiel", "Music Box", "Vibraphone",
            "Marimba", "Xylophone", "Tubular Bells", "Dulcimer",
        ],
    },
    {
        name: "Organ",
        instruments: [
            "Drawbar Organ", "Percussive Organ", "Rock Organ", "Church Organ",
            "Reed Organ", "Accordion", "Harmonica", "Tango Accordion",
        ],
    },
    {
        name: "Guitar",
        instruments: [
            "Acoustic Guitar (nylon)", "Acoustic Guitar (steel)", "Electric Guitar (jazz)", "Electric Guitar (clean)",
            "Electric Guitar (muted)", "Overdriven Guitar", "Distortion Guitar", "Guitar Harmonics",
        ],
    },
    {
        name: "Bass",
        instruments: [
            "Acoustic Bass", "Electric Bass (finger)", "Electric Bass (pick)", "Fretless Bass",
            "Slap Bass 1", "Slap Bass 2", "Synth Bass 1", "Synth Bass 2",
        ],
    },
    {
        name: "Strings",
        instruments: [
            "Violin", "Viola", "Cello", "Contrabass",
            "Tremolo Strings", "Pizzicato Strings", "Orchestral Harp", "Timpani",
        ],
    },
    {
        name: "Ensemble",
        instruments: [
            "String Ensemble 1", "String Ensemble 2", "Synth Strings 1", "Synth Strings 2",
            "Choir Aahs", "Voice Oohs", "Synth Voice", "Orchestra Hit",
        ],
    },
    {
        name: "Brass",
        instruments: [
            "Trumpet", "Trombone", "Tuba", "Muted Trumpet",
            "French Horn", "Brass Section", "Synth Brass 1", "Synth Brass 2",
        ],
    },
    {
        name: "Reed",
        instruments: [
            "Soprano Sax", "Alto Sax", "Tenor Sax", "Baritone Sax",
            "Oboe", "English Horn", "Bassoon", "Clarinet",
        ],
    },
    {
        name: "Pipe",
        instruments: [
            "Piccolo", "Flute", "Recorder", "Pan Flute",
            "Blown Bottle", "Shakuhachi", "Whistle", "Ocarina",
        ],
    },
    {
        name: "Synth Lead",
        instruments: [
            "Lead 1 (square)", "Lead 2 (sawtooth)", "Lead 3 (calliope)", "Lead 4 (chiff)",
            "Lead 5 (charang)", "Lead 6 (voice)", "Lead 7 (fifths)", "Lead 8 (bass + lead)",
        ],
    },
    {
        name: "Synth Pad",
        instruments: [
            "Pad 1 (new age)", "Pad 2 (warm)", "Pad 3 (polysynth)", "Pad 4 (choir)",
            "Pad 5 (bowed)", "Pad 6 (metallic)", "Pad 7 (halo)", "Pad 8 (sweep)",
        ],
    },
    {
        name: "Synth Effects",
        instruments: [
            "FX 1 (rain)", "FX 2 (soundtrack)", "FX 3 (crystal)", "FX 4 (atmosphere)",
            "FX 5 (brightness)", "FX 6 (goblins)", "FX 7 (echoes)", "FX 8 (sci-fi)",
        ],
    },
    {
        name: "Ethnic",
        instruments: [
            "Sitar", "Banjo", "Shamisen", "Koto",
            "Kalimba", "Bag Pipe", "Fiddle", "Shanai",
        ],
    },
    {
        name: "Percussive",
        instruments: [
            "Tinkle Bell", "Agogo", "Steel Drums", "Woodblock",
            "Taiko Drum", "Melodic Tom", "Synth Drum", "Reverse Cymbal",
        ],
    },
    {
        name: "Sound Effects",
        instruments: [
            "Guitar Fret Noise", "Breath Noise", "Seashore", "Bird Tweet",
            "Telephone Ring", "Helicopter", "Applause", "Gunshot",
        ],
    },
];

/**
 *     Where a roll stops unless the settings say otherwise: everything but the family General MIDI ends
 *     with, the sound effects, which the generator leaves out of a song of its own accord too.
 */
export const LAST_INSTRUMENT_BEFORE_EFFECTS = INSTRUMENT_FAMILIES
    .slice(0, -1)
    .reduce((count, family) => count + family.instruments.length, 0) - 1;

/**
 *     The drum kits a program change on the percussion channel selects. They are the General MIDI 2 kits,
 *     so a soundfont that only carries the standard one answers every one of them with that.
 */
export const DRUM_KITS = [
    { instrument: 0, name: "Standard" },
    { instrument: 8, name: "Room" },
    { instrument: 16, name: "Power" },
    { instrument: 24, name: "Electronic" },
    { instrument: 25, name: "TR-808" },
    { instrument: 32, name: "Jazz" },
    { instrument: 40, name: "Brush" },
    { instrument: 48, name: "Orchestra" },
    { instrument: 56, name: "Sound Effects" },
];
