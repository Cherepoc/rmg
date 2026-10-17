using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>What a section does in its song's form.</summary>
public enum SectionRole
{
    /// <summary>A section of a song whose form is its own, with no role of a convention's.</summary>
    Free,
    Verse,
    PreChorus,
    Chorus,
    Bridge,

    /// <summary>A section of its own material that opens the song, played once.</summary>
    Intro,

    /// <summary>A section of its own material that closes the song, played once.</summary>
    Outro,

    /// <summary>A short section after the chorus on a hook of its own, the full band's, a pop song's bonus hook.</summary>
    PostChorus
}

/// <summary>A song's sections in its order, and the role each plays.</summary>
public sealed record SongStructure(ImmutableArray<int> SectionIds, ImmutableDictionary<int, SectionRole> Roles);

internal static class SectionContrast
{
    /// <summary>How far a section that contrasts with the verse leans to a key or a scale of its own, as odds.</summary>
    public const double Odds = 2;

    /// <summary>
    ///     How a section leans to a key or a scale of its own: the chorus and the bridge, which contrast with the verse,
    ///     to it, and the others not at all.
    /// </summary>
    public static Tilt Of(SectionRole role)
    {
        return Tilt.Of(Odds, role is SectionRole.Chorus or SectionRole.Bridge ? 1 : 0);
    }
}

/// <summary>
///     A song's form: most often one of the forms songs are written in, its roles in their order, a verse and a
///     chorus coming back, a pre-chorus before the chorus and a bridge before the last chorus, a section for every
///     role; and otherwise a form of its own (<see cref="SongStructureGenerator" />), whose sections have no role, the
///     likelier the less conventional the song's form.
/// </summary>
internal static class SongForms
{
    /// <summary>The chance a song plays its last section once more, as a last chorus is played twice, by the form facet: never in a plain song.</summary>
    public static ByConvention LastRepeatChance { get; } = new(0, 0.25, 0.25);

    /// <summary>
    ///     The chance a last chorus played twice plays its first pass as a breakdown, built up from the voice alone, the drums
    ///     next, the full band by its end, and the second in full, a pop or a ballad's drop chorus: a genre's that the songs
    ///     never play (<see cref="GenreChoices.LastRepeat" />).
    /// </summary>
    public static ByConvention BreakdownChorusChance { get; } = new(0, 0, 0);

    /// <summary>
    ///     The chance a song opens on an intro of its own material, a section played once, by the form facet: never in a
    ///     plain song, which plays one of the forms as it is.
    /// </summary>
    public static ByConvention IntroChance { get; } = new(0, 0.3, 0.3);

    /// <summary>The chance a song closes on an outro of its own, by the form facet: never in a plain song, and no apter in a wild one than a middling one.</summary>
    public static ByConvention OutroChance { get; } = new(0, 0.15, 0.15);

    /// <summary>The chance a song takes one of the forms, at a form facet of middling conventionality.</summary>
    public const double FormChance = 0.7;

    /// <summary>The forms, as their roles in order, and how often each is drawn.</summary>
    public static ImmutableArray<Weighted<ImmutableArray<SectionRole>>> Forms { get; } =
    [
        new(0.35, Parse("VCVCBC")),
        new(0.25, Parse("VPCVPCBC")),
        new(0.15, Parse("VCVCBVC")),
        new(0.1, Parse("VCVC")),
        new(0.1, Parse("CVCVBC")),
        new(0.05, Parse("VPCVPC")),
        // and two longer, seldom drawn but where a song is to play long
        new(0.02, Parse("VCVCBCVC")),
        new(0.02, Parse("VPCVPCBCVPC")),
        // and AABA, once and twice, the A a verse and the B a bridge, which songs never take but a genre may, as a jazz
        // standard of eight-bar sections is
        new(0, Parse("VVBV")),
        new(0, Parse("VVBVVVBV")),
        // and ABA and a rondo, ABACA, a classical piece's, the A a verse, the B a bridge and the C a chorus, an episode of
        // its own, which songs never take but a genre may
        new(0, Parse("VBV")),
        new(0, Parse("VBVCV")),
        // and strophic, verse after verse, a ballad's or a blues', four or six, which songs never take but a genre may
        new(0, Parse("VVVV")),
        new(0, Parse("VVVVVV")),
        // and with a post-chorus after every chorus, a pop song's bonus hook, which songs never take but a genre may
        new(0, Parse("VCQVCQBCQ")),
        // and three verses with their hooks, a rap song's, which songs never take but a genre may
        new(0, Parse("VCVCVC")),
        new(0, Parse("VPCQVPCQBCQ"))
    ];

    /// <param name="context">The sequence the form is drawn from.</param>
    /// <param name="freeContext">The sequence a form of the song's own is drawn from (<see cref="SongStructureGenerator" />).</param>
    /// <param name="unconventionality">
    ///     The form facet of the song's unconventionality: the plainest song always in one of the forms, the wildest always
    ///     in one of its own.
    /// </param>
    /// <param name="ends">The sequence the intro and the outro are drawn from.</param>
    /// <param name="length">The song's length given, which the forms lean to by how near theirs come; none for none.</param>
    /// <param name="genre">The genre, whose chance of one of the forms and whose pool of them are the song's where it names them.</param>
    public static SongStructure Generate(IGenerationContext context, IGenerationContext freeContext, IGenerationContext ends, double unconventionality, SongLength? length, Genre genre)
    {
        var formChance = FormsChance.At(unconventionality, genre.Chance(GenreChoices.FormChances, "forms"));
        // the forms as the genre weighs them where it names them, its plainest at the plain end and its wildest at the wild;
        // a length given weighs those that may be drawn
        var forms = genre.Pool<ImmutableArray<SectionRole>>(GenreChoices.Form, NameOf) is { } pool
            ? ByConvention.Weigh(Forms.Select(x => (x.Value, new ByConvention(x.Weight, x.Weight, x.Weight))), unconventionality, pool)
            : Forms;
        var isConventional = context.TestProbability(length is null ? formChance : length.FormChance(formChance, forms.Where(x => x.Weight > 0).Select(x => (IEnumerable<SectionRole>)x.Value)));
        var form = context.Pick(length is null ? forms : [..forms.Select(x => x with { Weight = x.Weight * length.Closeness(x.Value.Sum(length.AppearanceSeconds)) })]);
        SongStructure structure;
        if (!isConventional)
        {
            var free = SongStructureGenerator.Generate(freeContext, length, unconventionality).SelectMany(part => part.SectionIds).ToImmutableArray();
            structure = new SongStructure(free, free.Distinct().ToImmutableDictionary(x => x, _ => SectionRole.Free));
        }
        else
        {
            // a section for every role, numbered in the order the roles first play
            var ids = form.Distinct().Select((role, id) => (role, id)).ToDictionary(x => x.role, x => x.id);
            structure = new SongStructure([..form.Select(x => ids[x])], ids.ToImmutableDictionary(x => x.Value, x => x.Key));
        }

        // and an intro and an outro of their own now and then, sections of their own after the others
        var (intro, outro) = (ends.TestProbability(IntroChance.At(unconventionality)), ends.TestProbability(OutroChance.At(unconventionality)));
        var next = structure.SectionIds.Max() + 1;
        var (introId, outroId) = (next, next + 1);
        return new SongStructure(
            [..(intro ? [introId] : Array.Empty<int>()), ..structure.SectionIds, ..(outro ? [outroId] : Array.Empty<int>())],
            structure.Roles.AddRange(new[] { (intro, introId, SectionRole.Intro), (outro, outroId, SectionRole.Outro) }.Where(x => x.Item1).Select(x => new KeyValuePair<int, SectionRole>(x.Item2, x.Item3)))
        );
    }

    /// <summary>The chance a song takes one of the forms, by its form facet: the plainest always, the wildest never.</summary>
    public static ByConvention FormsChance { get; } = RhythmicUnconventionality.Ends(FormChance, -1);

    /// <summary>A form's name, as a genre gives it: its roles' letters, "vcvcbc".</summary>
    public static string NameOf(ImmutableArray<SectionRole> form) => string.Concat(form.Select(x => x switch
    {
        SectionRole.Verse => 'v',
        SectionRole.PreChorus => 'p',
        SectionRole.Chorus => 'c',
        SectionRole.Bridge => 'b',
        SectionRole.PostChorus => 'q',
        _ => throw new ArgumentOutOfRangeException(nameof(form), x, "A form is of verses, pre-choruses, choruses, post-choruses and bridges.")
    }));

    /// <summary>A form as a page shows it where room is short: its roles' letters, "VCVCBC".</summary>
    public static string ShortOf(ImmutableArray<SectionRole> form) => NameOf(form).ToUpperInvariant();

    /// <summary>A form as a page shows it: its roles in words, "Verse, chorus, verse, chorus, bridge, chorus".</summary>
    public static string LabelOf(ImmutableArray<SectionRole> form)
    {
        var words = string.Join(", ", form.Select(x => x switch { SectionRole.PreChorus => "pre-chorus", SectionRole.PostChorus => "post-chorus", _ => x.ToString().ToLowerInvariant() }));
        return char.ToUpperInvariant(words[0]) + words[1..];
    }

    private static ImmutableArray<SectionRole> Parse(string roles)
    {
        return
        [
            ..roles.Select(x => x switch
            {
                'V' => SectionRole.Verse,
                'P' => SectionRole.PreChorus,
                'C' => SectionRole.Chorus,
                'B' => SectionRole.Bridge,
                'Q' => SectionRole.PostChorus,
                _ => throw new ArgumentException($"No role is written '{x}'.", nameof(roles))
            })
        ];
    }
}
