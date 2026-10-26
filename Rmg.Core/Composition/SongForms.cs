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

    /// <summary>A section of its own material that opens the song, played once, or a riff section coming back between the others and closing the song (<see cref="SongForms.RiffSectionChance" />).</summary>
    Intro,

    /// <summary>A section of its own material that closes the song, played once.</summary>
    Outro,

    /// <summary>A short section after the chorus on a hook of its own, the full band's, a pop song's bonus hook.</summary>
    PostChorus
}

/// <summary>
///     A form songs are written in, its roles in order, and whether it is a cycle the song's length repeats, as a jazz
///     tune's AABA is played through again and again, each pass its sections once more.
/// </summary>
public sealed record WrittenForm(ImmutableArray<SectionRole> Roles, bool Repeats = false)
{
    /// <summary>
    ///     The form's roles as a song of a length plays them: a cycle as many times as comes nearest the length, at least
    ///     once, by how long an appearance of each role plays; once where no length is given, as any other form.
    /// </summary>
    internal ImmutableArray<SectionRole> Played(SongLength? length)
    {
        if (!Repeats || length is null)
            return Roles;
        var pass = Roles.Sum(length.AppearanceSeconds);
        var passes = Math.Max(1, (int)Math.Round(length.Target / pass));
        return [..Enumerable.Repeat(Roles, passes).SelectMany(x => x)];
    }
}

/// <summary>A song's sections in its order, the role each plays, and how many sections a pass of its form plays where the form is a cycle, 0 where not.</summary>
public sealed record SongStructure(ImmutableArray<int> SectionIds, ImmutableDictionary<int, SectionRole> Roles, int Pass = 0);

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

    /// <summary>
    ///     The chance a song in one of the forms plays a riff section: its intro coming back after a chorus that a verse
    ///     follows and closing the song, as a riff-driven band plays its riff between its verses and to end on, a genre's
    ///     that the songs never play (<see cref="GenreChoices.RiffSection" />).
    /// </summary>
    public static ByConvention RiffSectionChance { get; } = new(0, 0, 0);

    /// <summary>
    ///     The chance a verse of a song that opens on an intro plays over the intro's riff, its chords, riff and bass the
    ///     intro's and its melody over them, as a riff-driven band sings its verse over its main riff: a genre's that the songs
    ///     never play (<see cref="GenreChoices.RiffSection" />, <see cref="SectionGenerator.Loop" />).
    /// </summary>
    public static ByConvention VerseOverRiffChance { get; } = new(0, 0, 0);

    /// <summary>The chance a song takes one of the forms, at a form facet of middling conventionality.</summary>
    public const double FormChance = 0.7;

    /// <summary>The forms, as their roles in order, and how often each is drawn.</summary>
    public static ImmutableArray<Weighted<WrittenForm>> Forms { get; } =
    [
        new(0.35, new WrittenForm(Parse("VCVCBC"))),
        new(0.25, new WrittenForm(Parse("VPCVPCBC"))),
        new(0.15, new WrittenForm(Parse("VCVCBVC"))),
        new(0.1, new WrittenForm(Parse("VCVC"))),
        new(0.1, new WrittenForm(Parse("CVCVBC"))),
        new(0.05, new WrittenForm(Parse("VPCVPC"))),
        // and two longer, seldom drawn but where a song is to play long
        new(0.02, new WrittenForm(Parse("VCVCBCVC"))),
        new(0.02, new WrittenForm(Parse("VPCVPCBCVPC"))),
        // and AABA, the A a verse and the B a bridge, which songs never take but a genre may, as a jazz standard of eight-bar
        // sections is
        new(0, new WrittenForm(Parse("VVBV"))),
        // and ABA and a rondo, ABACA, a classical piece's, the A a verse, the B a bridge and the C a chorus, an episode of
        // its own, which songs never take but a genre may
        new(0, new WrittenForm(Parse("VBV"))),
        new(0, new WrittenForm(Parse("VBVCV"))),
        // and with a post-chorus after every chorus, a pop song's bonus hook, which songs never take but a genre may
        new(0, new WrittenForm(Parse("VCQVCQBCQ"))),
        // and three verses with their hooks, a rap song's, which songs never take but a genre may
        new(0, new WrittenForm(Parse("VCVCVC"))),
        new(0, new WrittenForm(Parse("VPCQVPCQBCQ"))),
        // and AABA as a cycle, played through as many times as the song's length comes nearest, a jazz tune's head and its
        // choruses over the same form, which songs never take but a genre may
        new(0, new WrittenForm(Parse("VVBV"), Repeats: true)),
        // and strophic, verse after verse as many as the length comes nearest, a ballad's or a blues' choruses, which songs
        // never take but a genre may
        new(0, new WrittenForm(Parse("V"), Repeats: true))
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
    /// <param name="hasRiff">Whether the song has a riff, which a riff section plays.</param>
    public static SongStructure Generate(IGenerationContext context, IGenerationContext freeContext, IGenerationContext ends, double unconventionality, SongLength? length, Genre genre, bool hasRiff)
    {
        var formChance = FormsChance.At(unconventionality, genre.Chance(GenreChoices.FormChances, "forms"));
        // the forms as the genre weighs them where it names them, its plainest at the plain end and its wildest at the wild;
        // a length given weighs those that may be drawn
        var written = genre.Pool<WrittenForm>(GenreChoices.Form, NameOf) is { } pool
            ? ByConvention.Weigh(Forms.Select(x => (x.Value, new ByConvention(x.Weight, x.Weight, x.Weight))), unconventionality, pool)
            : Forms;
        // each as the length plays it, a cycle as many times as comes nearest it, with its pass
        ImmutableArray<Weighted<(ImmutableArray<SectionRole> Roles, int Pass)>> forms =
            [..written.Select(x => new Weighted<(ImmutableArray<SectionRole>, int)>(x.Weight, (x.Value.Played(length), x.Value.Repeats ? x.Value.Roles.Length : 0)))];
        var isConventional = context.TestProbability(length is null ? formChance : length.FormChance(formChance, forms.Where(x => x.Weight > 0).Select(x => (IEnumerable<SectionRole>)x.Value.Roles)));
        var (form, pass) = context.Pick(length is null ? forms : [..forms.Select(x => x with { Weight = x.Weight * length.Closeness(x.Value.Roles.Sum(length.AppearanceSeconds)) })]);
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
            structure = new SongStructure([..form.Select(x => ids[x])], ids.ToImmutableDictionary(x => x.Value, x => x.Key), pass);
        }

        // and an intro and an outro of their own now and then, sections of their own after the others
        var (intro, outro) = (ends.TestProbability(IntroChance.At(unconventionality)), ends.TestProbability(OutroChance.At(unconventionality)));
        var next = structure.SectionIds.Max() + 1;
        var (introId, outroId) = (next, next + 1);
        // or a riff section, a genre's, the intro coming back after a chorus a verse follows and as the outro, in a song with a
        // riff to come back
        if (ends.TestProbability(RiffSectionChance.At(unconventionality, genre.Chance(GenreChoices.RiffSection, "songs"))) && isConventional && hasRiff)
        {
            var ids = structure.SectionIds;
            ImmutableArray<int> returning =
            [
                introId,
                ..ids.SelectMany((id, i) => structure.Roles[id] == SectionRole.Chorus && i + 1 < ids.Length && structure.Roles[ids[i + 1]] == SectionRole.Verse ? [id, introId] : new[] { id }),
                introId
            ];
            return new SongStructure(returning, structure.Roles.Add(introId, SectionRole.Intro));
        }
        return new SongStructure(
            [..(intro ? [introId] : Array.Empty<int>()), ..structure.SectionIds, ..(outro ? [outroId] : Array.Empty<int>())],
            structure.Roles.AddRange(new[] { (intro, introId, SectionRole.Intro), (outro, outroId, SectionRole.Outro) }.Where(x => x.Item1).Select(x => new KeyValuePair<int, SectionRole>(x.Item2, x.Item3))),
            structure.Pass
        );
    }

    /// <summary>
    ///     The chance a song of a cycle of three passes or more plays its last pass as its head, the tune sung, none of its
    ///     sections soloed, as a jazz band plays the head out after the solos, by its form facet: a convention, every time at
    ///     the plain end, now and then not in the wildest songs.
    /// </summary>
    public static ByConvention HeadOutChance { get; } = new(1, 0.9, 0.5);

    /// <summary>The chance a song takes one of the forms, by its form facet: the plainest always, the wildest never.</summary>
    public static ByConvention FormsChance { get; } = RhythmicUnconventionality.Ends(FormChance, -1);

    /// <summary>A form's name, as a genre gives it: its roles' letters, "vcvcbc", and a cycle's a "+" after them, "vvbv+".</summary>
    public static string NameOf(WrittenForm form) => LettersOf(form.Roles) + (form.Repeats ? "+" : "");

    private static string LettersOf(ImmutableArray<SectionRole> form) => string.Concat(form.Select(x => x switch
    {
        SectionRole.Verse => 'v',
        SectionRole.PreChorus => 'p',
        SectionRole.Chorus => 'c',
        SectionRole.Bridge => 'b',
        SectionRole.PostChorus => 'q',
        _ => throw new ArgumentOutOfRangeException(nameof(form), x, "A form is of verses, pre-choruses, choruses, post-choruses and bridges.")
    }));

    /// <summary>A form as a page shows it where room is short: its roles' letters, "VCVCBC".</summary>
    public static string ShortOf(WrittenForm form) => NameOf(form).ToUpperInvariant();

    /// <summary>A form as a page shows it: its roles in words, "Verse, chorus, verse, chorus, bridge, chorus", a cycle's "played through again".</summary>
    public static string LabelOf(WrittenForm form)
    {
        var words = string.Join(", ", form.Roles.Select(x => x switch { SectionRole.PreChorus => "pre-chorus", SectionRole.PostChorus => "post-chorus", _ => x.ToString().ToLowerInvariant() }));
        return char.ToUpperInvariant(words[0]) + words[1..] + (form.Repeats ? ", played through again" : "");
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
