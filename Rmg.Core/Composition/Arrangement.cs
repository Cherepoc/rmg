using System.Collections.Immutable;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     Which of the band's parts a section leaves out: now and then the drums, for a breakdown, the bass or the chords,
///     or the melody, for a section of the band alone, and more often than not the pad, which a section adds to lift
///     it, each the likelier the less energy the section has, and the melody
///     all but never where the section has a verse's, a pre-chorus's or a chorus's role, which a tune carries. A section
///     keeps its harmony: where both the bass and the chords would rest, the chords play; and it never falls silent:
///     where every part the song has would rest, the one least likely to plays.
/// </summary>
internal static class Arrangement
{
    /// <summary>The parts that may rest, and the chance each does, at a section of middling energy.</summary>
    public static ImmutableArray<(TrackRole Part, double Chance)> Rests { get; } =
    [
        (TrackRole.Drum, 0.08),
        (TrackRole.Bass, 0.05),
        (TrackRole.Chords, 0.05),
        (TrackRole.Melody, 0.1),
        (TrackRole.Pad, 0.6),
        (TrackRole.CounterMelody, 0.75),
        (TrackRole.Riff, 0.4),
        (TrackRole.Rhythm, 0.3)
    ];

    /// <summary>How far a role that a tune carries keeps its melody, as the odds against it resting.</summary>
    public const double TuneOdds = 8;

    /// <summary>The chance a verse leaves its melody out for a voice, a genre's that the songs never play (<see cref="GenreChoices.Voice" />).</summary>
    public static ByConvention VoiceChance { get; } = new(0, 0, 0);

    /// <summary>How far a genre leans a part's rests at its most and its least (<see cref="GenreChoices.Rests" />), as the odds of its chance.</summary>
    public const double GenreRestOdds = 6;

    /// <summary>The parts the section leaves out, the song's absent ones among them.</summary>
    /// <param name="energy">How the section's energy leans the draws: the more, the fewer parts rest.</param>
    /// <param name="absent">The parts the song leaves out (<see cref="SongParts" />), which rest whatever the draws.</param>
    /// <param name="genre">The genre whose parts rest more or less, a dub's drums and bass dropping out, where it names them.</param>
    /// <param name="form">The form facet of the section's unconventionality, which a verse left to a voice leans by.</param>
    public static ImmutableHashSet<TrackRole> DrawRests(IGenerationContext context, Tilt energy, SectionRole role, ImmutableHashSet<TrackRole> absent, Genre genre, double form)
    {
        Tilt Genre(TrackRole part) => Tilt.Of(GenreRestOdds, genre.Shift(GenreChoices.Rests, GenreChoices.Name(part.ToString())));
        // a tune's roles keeping their melody, and an intro and an outro, mostly the band's, leaving it out the oftener
        var tune = Tilt.Of(TuneOdds, role switch
        {
            SectionRole.Verse or SectionRole.PreChorus or SectionRole.Chorus or SectionRole.PostChorus => -1,
            SectionRole.Intro or SectionRole.Outro => 1,
            _ => 0
        });
        var resting = Rests
            .Where(x => context.TestProbability(Genre(x.Part).Chance((x.Part == TrackRole.Melody ? tune : Tilt.None).Chance(energy.Chance(x.Chance, -1), 1), 1)))
            .Select(x => x.Part)
            .Union(absent)
            .ToHashSet();

        // a verse left to a voice now and then, its melody out, as a rap's verse is over its beat, a genre's; drawn last,
        // so that the draws above stay as they were
        if (context.TestProbability(VoiceChance.At(form, genre.Chance(GenreChoices.Voice, "verses"))) && role == SectionRole.Verse)
            resting.Add(TrackRole.Melody);

        var kept = KeepingHarmony([..resting], absent).ToHashSet();
        var parts = Rests.Where(x => !absent.Contains(x.Part)).ToArray();
        if (parts.Length > 0 && parts.All(x => kept.Contains(x.Part)))
            kept.Remove(parts.MinBy(x => x.Chance).Part);
        return kept.ToImmutableHashSet();
    }

    /// <summary>
    ///     The parts resting with the section keeping its harmony, where the song has the chords: the chords playing where
    ///     the bass rests too, or where nothing would play but the bass, which a quiet section's rhythm may leave without a
    ///     note.
    /// </summary>
    public static ImmutableHashSet<TrackRole> KeepingHarmony(ImmutableHashSet<TrackRole> resting, ImmutableHashSet<TrackRole> absent)
    {
        var others = Rests.Any(x => x.Part is not (TrackRole.Bass or TrackRole.Chords) && !resting.Contains(x.Part));
        return resting.Contains(TrackRole.Chords) && (resting.Contains(TrackRole.Bass) || !others) && !absent.Contains(TrackRole.Chords)
            ? resting.Remove(TrackRole.Chords)
            : resting;
    }
}
