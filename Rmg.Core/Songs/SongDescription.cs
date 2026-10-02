using System.Collections.Immutable;

namespace Rmg.Core.Songs;

/// <summary>
///     A song as a listener would tell it, in what makes its character rather than its notes: its tempo, meter, feel and
///     key, its parts and drums, how plain or wild it is, its form section by section, and its melody; for an agent to
///     name it by, or a person to read (<see cref="Composition.SongDescriptions" />).
/// </summary>
/// <param name="Seconds">How long it plays, its ending included.</param>
/// <param name="Bars">How many bars its sections play, its intro and ending left out.</param>
/// <param name="Bpm">Its tempo where its first section starts, in beats a minute.</param>
/// <param name="TimeSignature">Its meter, as a time signature, such as "4/4".</param>
/// <param name="Feel">The feel it plays in: "straight", or a tuplet's or grouping's, such as "triplets".</param>
/// <param name="Swing">How it swings, or "none".</param>
/// <param name="Key">The key it starts in, its tonic and scale, such as "D Dorian".</param>
/// <param name="KeyChanges">Where its key changes, by how much, and to what.</param>
/// <param name="Unconventionality">How plain or wild it is, as a whole and facet by facet.</param>
/// <param name="Parts">The parts that play, by role, and their instruments.</param>
/// <param name="Drums">Its drums, or none for a song without them.</param>
/// <param name="Intro">How it starts.</param>
/// <param name="Ending">How it ends.</param>
/// <param name="Sections">Its sections, in the order they play.</param>
/// <param name="Melody">Its melody, or none for a song without one.</param>
public sealed record SongDescription(
    double Seconds,
    int Bars,
    double Bpm,
    string TimeSignature,
    string Feel,
    string Swing,
    string Key,
    ImmutableArray<KeyChangeDescription> KeyChanges,
    UnconventionalityDescription Unconventionality,
    ImmutableArray<PartDescription> Parts,
    DrumsDescription? Drums,
    string Intro,
    string Ending,
    ImmutableArray<SectionDescription> Sections,
    MelodyDescription? Melody
);

/// <param name="Section">The place among the sections, from 1, of the first the new key plays in.</param>
/// <param name="Semitones">By how many semitones, up or down.</param>
/// <param name="Key">The key it changes to, its tonic and scale.</param>
public sealed record KeyChangeDescription(int Section, int Semitones, string Key);

/// <param name="Value">The song's, from 0, the plainest, to 1, the wildest.</param>
/// <param name="Word">The song's in a word: plain, middling or wild.</param>
/// <param name="Facets">Every facet's, by its name, in a word and as a value.</param>
public sealed record UnconventionalityDescription(double Value, string Word, ImmutableArray<FacetDescription> Facets);

/// <param name="Facet">The facet's name, such as "Feel".</param>
/// <param name="Value">From 0, the plainest, to 1, the wildest.</param>
/// <param name="Word">In a word: plain, middling or wild.</param>
public sealed record FacetDescription(string Facet, double Value, string Word);

/// <param name="Role">The part's role, such as "melody".</param>
/// <param name="Instrument">The instrument it starts on.</param>
/// <param name="Others">The other instruments it plays over the song, where its sound changes.</param>
public sealed record PartDescription(string Role, string Instrument, ImmutableArray<string> Others);

/// <param name="Setup">The kit, the kit and percussion, or percussion.</param>
/// <param name="Kit">The drum kit's sound, such as "Standard".</param>
/// <param name="Pieces">The drums it plays, by their names.</param>
public sealed record DrumsDescription(string Setup, string Kit, ImmutableArray<string> Pieces);

/// <param name="Place">Its place among the song's sections, from 1.</param>
/// <param name="Name">What it is, such as "Chorus", or "Section B" in a form of the song's own.</param>
/// <param name="Bars">How many bars it plays.</param>
/// <param name="Energy">How loud and busy it is against the song's other sections: quiet, medium or loud.</param>
/// <param name="Key">Its key, its tonic and scale, where it differs from the song's.</param>
/// <param name="TimeSignature">Its meter, where it differs from the song's.</param>
/// <param name="Bpm">Its tempo, where it differs from the song's.</param>
/// <param name="Feel">Its feel, where it differs from the song's.</param>
/// <param name="Drums">How its drums play where it differs from a groove of the kit: half time, double time, percussion only, a drum solo, or resting.</param>
/// <param name="Texture">How its parts come in: steady, building, thinning out, or one part alone.</param>
/// <param name="Parts">The parts that play in it.</param>
/// <param name="Soloists">The parts that solo in it, none where none does.</param>
/// <param name="Chords">Its chords, in Roman numerals and by name (<see cref="ChordDescription" />), none where it has none.</param>
public sealed record SectionDescription(
    int Place,
    string Name,
    int Bars,
    string Energy,
    string? Key,
    string? TimeSignature,
    double? Bpm,
    string? Feel,
    string? Drums,
    string Texture,
    ImmutableArray<string> Parts,
    ImmutableArray<string> Soloists,
    ImmutableArray<ChordDescription> Chords
);

/// <param name="Numeral">The chord in Roman numerals in the section's key, such as "ii7" or "♭VII".</param>
/// <param name="Name">The chord by name, such as "Em7", or none where the chords' part does not play it.</param>
public sealed record ChordDescription(string Numeral, string? Name);

/// <param name="Lowest">Its lowest note, such as "A3".</param>
/// <param name="Highest">Its highest note.</param>
/// <param name="NotesPerBar">How many notes it plays a bar where it plays.</param>
/// <param name="Improvisation">How freely it varies its tune where a section comes back, in a word: little, some or much.</param>
public sealed record MelodyDescription(string Lowest, string Highest, double NotesPerBar, string Improvisation);
