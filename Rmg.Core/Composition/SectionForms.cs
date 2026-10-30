using System.Collections.Immutable;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>What a section's guitars play as, the song's own instruments or a form of their own.</summary>
public enum SectionFormKind
{
    /// <summary>As the song draws them.</summary>
    Own,

    /// <summary>
    ///     A quieter section turned calm, a heavy band's acoustic passage between the storms: its driven guitars acoustic or
    ///     clean together, one breaking the chords slowly and letting them ring, every other guitar resting, no figure
    ///     played and the drums in half time or resting.
    /// </summary>
    Calm,

    /// <summary>
    ///     A louder section turned heavy, as a band kicks in the distortion for the chorus: its clean guitars driven, its broken
    ///     chords struck, in each appearance louder than the song, so that a section heard clean comes back heavy.
    /// </summary>
    Heavy
}

/// <summary>
///     A section's form, decided with the section, before its parts play, so that they play as the form has them: calm in a
///     quieter section of a song with a driven guitar, heavy in a louder appearance of one with a clean guitar, the guitars
///     always together. By the sound facet: never in the plainest songs; a genre's (<see cref="GenreChoices.LouderForms" />).
/// </summary>
/// <param name="Program">The program the section's guitars play: an acoustic or clean one in a calm section, a driven one in a heavy one.</param>
/// <param name="Guitars">The parts the form turns, by their tracks: the driven guitars for a calm section, the clean ones for a heavy one.</param>
/// <param name="Breaker">The calm section's part that breaks its chords, the chords' or the rhythm part's that a guitar plays; none but in a calm section.</param>
/// <param name="Leaves">The parts the section leaves out wherever it plays: a calm section's guitars but the one breaking its chords.</param>
/// <param name="Plays">The part a calm section keeps playing whatever its appearance draws, its guitar breaking the chords.</param>
internal sealed record SectionForm(SectionFormKind Kind, int Program, ImmutableHashSet<int> Guitars, int? Breaker, ImmutableHashSet<TrackRole> Leaves, ImmutableHashSet<TrackRole> Plays)
{
    public static SectionForm Own { get; } = new(SectionFormKind.Own, 0, [], null, [], []);

    /// <summary>The chance a louder section's clean guitars turn driven, by the sound facet.</summary>
    public static ByConvention HeavyChance { get; } = new(0, 0.1, 0.25);

    /// <summary>The chance a quieter section of a song with a driven guitar turns calm, a genre's that the songs never play.</summary>
    public static ByConvention CalmChance { get; } = new(0, 0, 0);

    /// <summary>The programs a calm section's driven guitars play, an acoustic or a clean one.</summary>
    public static ImmutableArray<int> CalmPrograms { get; } = [25, 27];

    /// <summary>The programs a heavy section's clean, jazz or muted guitars play, the driven guitars.</summary>
    public static ImmutableArray<int> HeavyPrograms { get; } = [..Enumerable.Range(0, 128).Where(x => InstrumentTraits.Has(x, InstrumentTrait.Driven))];

    /// <summary>The guitars a heavy section turns: the clean, the jazz and the muted guitar.</summary>
    private static readonly ImmutableHashSet<int> Clean = [26, 27, 28];

    /// <summary>The chance a calm section's drums rest, the guitar alone with the bass and the lines, rather than playing on in half time.</summary>
    public const double CalmDrumsRest = 0.5;

    /// <summary>The shortest note a calm section's chords are broken in, in beats: an 8th, slow enough to ring.</summary>
    public const double CalmShortest = 0.5;

    /// <summary>The parts a guitar may play: the chords, the rhythm part, the riff and its twin.</summary>
    private static readonly ImmutableArray<TrackRole> GuitarParts = [TrackRole.Chords, TrackRole.Rhythm, TrackRole.Riff, TrackRole.RiffTwin];

    /// <summary>
    ///     A section's form, from a sequence of its own, every draw made whatever the section is: calm where its energy is
    ///     under the song's, its own where its guitars have no such form or it breaks down, and else heavy where it draws so,
    ///     which its appearances louder than the song play.
    /// </summary>
    /// <param name="energy">The section's energy against the song's, below 0 the quieter.</param>
    /// <param name="parts">The pitched parts the song plays, by their tracks, with their roles and programs.</param>
    public static SectionForm Draw(IGenerationContext context, double energy, double sound, Genre genre, IReadOnlyDictionary<int, (TrackRole Role, int Program)> parts, bool breakdown)
    {
        var calms = context.TestProbability(CalmChance.At(sound, genre.Chance(GenreChoices.LouderForms, "quieter")));
        var heavies = context.TestProbability(HeavyChance.At(sound, genre.Chance(GenreChoices.LouderForms, "guitars")));
        var programDraw = context.GenerateDouble();
        var drumsRest = context.TestProbability(CalmDrumsRest);
        var guitars = parts.Where(x => GuitarParts.Contains(x.Value.Role)).ToArray();
        var driven = guitars.Where(x => InstrumentTraits.Has(x.Value.Program, InstrumentTrait.Driven)).ToArray();
        var clean = guitars.Where(x => Clean.Contains(x.Value.Program)).ToArray();
        // the guitar breaking the chords, the chords' or the rhythm part's, clean or driven, and every other guitar resting, so
        // that nothing plays a riff's tremolo or chugs under the broken chords; none calm where no guitar plays the chords
        // the guitars, which a calm section rests but the one breaking its chords
        var strung = guitars.Where(x => InstrumentTraits.IsGuitar(x.Value.Program)).ToArray();
        var breaker = strung.Where(x => x.Value.Role is TrackRole.Chords or TrackRole.Rhythm).OrderBy(x => x.Value.Role != TrackRole.Chords).Select(x => (int?)x.Key).FirstOrDefault();
        if (!breakdown && energy < -1e-9 && calms && driven.Length > 0 && breaker is { } broken)
            return new SectionForm(SectionFormKind.Calm, CalmPrograms[(int)(programDraw * CalmPrograms.Length)], [..driven.Select(x => x.Key)], broken,
                [..strung.Where(x => x.Key != broken).Select(x => x.Value.Role), ..drumsRest ? [TrackRole.Drum] : Array.Empty<TrackRole>()], [parts[broken].Role]);

        return heavies && clean.Length > 0
            ? new SectionForm(SectionFormKind.Heavy, HeavyPrograms[(int)(programDraw * HeavyPrograms.Length)], [..clean.Select(x => x.Key)], null, [], [])
            : Own;
    }
}
