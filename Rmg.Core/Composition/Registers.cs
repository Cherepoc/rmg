using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     Where the pitched parts sit: every part's octaves, as drawn, kept within its instrument's range
///     (<see cref="InstrumentRanges" />), an octave counted where half of it or more is the instrument's, and the melody
///     over the chords, its singable middle a fourth or more above theirs, the one further from where it sits best
///     moved by an octave where its instrument has one, the other otherwise, as an arranger keeps the tune on top.
/// </summary>
public static class Registers
{
    /// <summary>The octave of a part's range note 0 sits in, as the octave offsets count from (<see cref="Realizer" />).</summary>
    private const int ZeroOctave = 5;

    /// <summary>How far the melody's middle sits above the chords' at least, in semitones: a fourth.</summary>
    public const int MelodyOverChords = 5;

    /// <summary>How many times the melody and the chords are moved apart by an octave at most.</summary>
    private const int Moves = 2;

    /// <summary>Where the melody's middle sits best, C5, and the chords', D4, which the one further from its own moves from first.</summary>
    public const int MelodyCentre = 72, ChordsCentre = 62;

    /// <summary>The octaves, as offsets, an instrument plays: those of which half or more is in its range.</summary>
    public static (int Low, int High) OctavesOf(int program)
    {
        var (low, high) = InstrumentRanges.Of(program);
        // the lowest whose top six notes are in its range, and the highest whose bottom six are
        return ((int)Math.Ceiling((low - 6) / 12.0) - ZeroOctave, (int)Math.Floor((high - 5) / 12.0) - ZeroOctave);
    }

    /// <summary>
    ///     A part's octaves, as drawn, moved within its instrument's, as many as it has room for; both moved an octave where a
    ///     genre's register for the part is at its lowest quarter or its highest (<see cref="GenreChoices.Register" />), as
    ///     a guitar tuned down plays, or a bass up high.
    /// </summary>
    public static PitchInstrumentTrack WithinInstrument(PitchInstrumentTrack part, Genre genre)
    {
        var moved = OctavesOf(genre, part.Role);
        var (low, high) = OctavesOf(part.InstrumentCode) is var (l, h) ? (l + moved, h + moved) : default;
        var width = part.MaxOctaveOffset - part.MinOctaveOffset;
        var min = Math.Clamp(part.MinOctaveOffset + moved, low, high);
        var max = Math.Clamp(min + width, min, high);
        return Moved(part, Math.Max(low, max - width), max);
    }

    /// <summary>How many octaves a genre moves a part, the riff's twin as the riff: -1, 0 or 1, by its register's shift.</summary>
    public static int OctavesOf(Genre genre, TrackRole role) =>
        GenreChoices.Name(role.Source().ToString()) is var name && GenreChoices.Register.Options.Any(x => x.Name == name)
            ? genre.Shift(GenreChoices.Register, name) switch { <= -0.5 => -1, >= 0.5 => 1, _ => 0 }
            : 0;

    /// <summary>
    ///     The melody and the chords placed apart, the melody's singable middle a fourth or more above the chords' middle, each
    ///     moved by an octave within its instrument, as far as a genre's register moved it (<see cref="WithinInstrument" />),
    ///     the one further from where it sits best first; as they were where they are apart or cannot be.
    /// </summary>
    public static (PitchInstrumentTrack Melody, PitchInstrumentTrack Chords) Apart(PitchInstrumentTrack melody, PitchInstrumentTrack chords, Genre genre)
    {
        var highest = OctavesOf(melody.InstrumentCode).High + OctavesOf(genre, melody.Role);
        var lowest = OctavesOf(chords.InstrumentCode).Low + OctavesOf(genre, chords.Role);
        for (var move = 0; move < Moves && MiddleOfMelody(melody) < MiddleOf(chords) + MelodyOverChords; move++)
        {
            var (up, down) = (melody.MaxOctaveOffset < highest, chords.MinOctaveOffset > lowest);
            // the one further below or above where it sits best, as far as its instrument lets it
            if (up && (!down || MelodyCentre - MiddleOfMelody(melody) >= MiddleOf(chords) - ChordsCentre))
                melody = Moved(melody, melody.MinOctaveOffset + 1, melody.MaxOctaveOffset + 1);
            else if (down)
                chords = Moved(chords, chords.MinOctaveOffset - 1, chords.MaxOctaveOffset - 1);
            else
                break;
        }

        return (melody, chords);
    }

    private static double MiddleOf(PitchInstrumentTrack part) => Realizer.GetRange(part) is var (low, high) ? (low + high) / 2.0 : 0;

    private static double MiddleOfMelody(PitchInstrumentTrack melody) =>
        Realizer.GetRange(melody) is var (low, high) && Line.GetSingableRange(low, high, MelodyLayers.Line.RangeWidth) is var (from, to) ? (from + to) / 2.0 : 0;

    private static PitchInstrumentTrack Moved(PitchInstrumentTrack part, int min, int max) => min == part.MinOctaveOffset && max == part.MaxOctaveOffset
        ? part
        : new PitchInstrumentTrack(part.StateMap, part.InstrumentCode, min, max, part.Role, part.Pan);
}
