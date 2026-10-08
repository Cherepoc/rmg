using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     The chord progression of a section: the root of every chord of its 4-bar pattern, in scale steps from the
///     section's home. The first chord is the home chord, the last the cadence, which resolves to the first when the
///     pattern plays again, the one before it prepares the cadence, and the others move away; four chords, a chord a
///     bar, are home, away, preparation and cadence, two are home and cadence. Each chord's root is drawn by how
///     strongly the previous root leads to it, falling a fifth the most, and by how well it suits its place. Roots stay
///     within 3 steps of home, so the progression keeps to one register.
/// </summary>
internal static class Progressions
{
    // the chord roots and the rules below are in steps of a 7-note scale, which every scale of Scales is
    private const int StepCount = 7;

    /// <summary>
    ///     The chance a progression of four chords or more holds its home over its first two, a slower start, I for two bars
    ///     before the rest move, as often in a plain song as a wild one, a little more in the wilder.
    /// </summary>
    public static ByConvention HeldHome { get; } = new(0.1, 0.15, 0.25);

    /// <summary>How much a root that does not suit its bar weighs against one that does.</summary>
    private const double OutOfRoleWeight = 0.05;

    /// <summary>
    ///     How strongly a root leads to the next, by how many steps up the scale the next is: falling a fifth (3 steps
    ///     up, the same as a fifth down) the most, then falling a third and moving a step, and the same root rarely.
    /// </summary>
    private static readonly ImmutableArray<double> MotionWeights =
    [
        0.1, // the same root
        0.5, // a step up
        0.35, // a third up
        1, // a fourth up, a fifth down
        0.3, // a fifth up, a fourth down
        0.6, // a third down
        0.4 // a step down
    ];

    /// <summary>
    ///     The motions as a genre names them (<see cref="GenreChoices.RootMotions" />), by how many steps up the scale the
    ///     next root is, as <see cref="MotionWeights" /> has them, and how a page shows them.
    /// </summary>
    public static ImmutableArray<(string Name, string Label)> Motions { get; } =
    [
        ("same", "The same root"),
        ("stepUp", "Up a step"),
        ("thirdUp", "Up a third"),
        ("fifthDown", "Down a fifth, as ii–V or V–I"),
        ("fourthDown", "Down a fourth, as IV–I or ♭VII–IV"),
        ("thirdDown", "Down a third"),
        ("stepDown", "Down a step")
    ];

    /// <summary>How far a genre's lean of a motion moves it, at its most or its least, as odds: four times as likely, or a quarter.</summary>
    public const double MotionOdds = 4;

    /// <summary>
    ///     The degrees a genre leans a root to (<see cref="GenreChoices.RootDegrees" />), by its steps above the section's
    ///     home, as its mode makes them, and how a page shows them: rock's ♭VII and ♭VI, which the motions alone reach by
    ///     chance. A degree leans as a motion does, by <see cref="MotionOdds" />.
    /// </summary>
    public static ImmutableArray<(string Name, string Label)> Degrees { get; } =
    [
        ("home", "Home, I or i"),
        ("second", "The second, ii, ii° or Phrygian's ♭II"),
        ("third", "The third, iii or ♭III"),
        ("fourth", "The fourth, IV or iv"),
        ("fifth", "The fifth, V or v"),
        ("sixth", "The sixth, vi or ♭VI"),
        ("seventh", "The seventh, vii° or ♭VII")
    ];

    /// <summary>
    ///     How a phrase of a longer progression starts and ends by its place, by the root's step above home, the others as
    ///     a root out of its place weighs (<see cref="OutOfPlaceWeight" />): a middle phrase from IV, now and then from home;
    ///     the last from V, now and then from IV, but the second of two from home (<see cref="AnswerStarts" />); and every
    ///     phrase but the last back home, the first of two on V now and then, a half close (<see cref="QuestionEnds" />), so
    ///     that three phrases make the blues' I / IV–I / V–I and two a song's 8-bar, a question and its answer.
    /// </summary>
    private static readonly ImmutableDictionary<int, double> MiddlePhraseStarts = ImmutableDictionary<int, double>.Empty.Add(3, 1).Add(0, 0.15);

    private static readonly ImmutableDictionary<int, double> LastPhraseStarts = ImmutableDictionary<int, double>.Empty.Add(4, 1).Add(3, 0.15);

    /// <summary>The second of two phrases from home, as a period's answer starts as its question did, now and then from IV.</summary>
    private static readonly ImmutableDictionary<int, double> AnswerStarts = ImmutableDictionary<int, double>.Empty.Add(0, 1).Add(3, 0.15);

    /// <summary>
    ///     How much a root out of its phrase's place weighs, by the progression's strictness: none at the plainest, which
    ///     keeps a longer pattern's phrases as they go, as much as a root out of its bar's role at a middling song's
    ///     strictness, and more the wilder, where the strictness then lets every root weigh alike.
    /// </summary>
    private static double OutOfPlaceWeight(double strictness) => OutOfRoleWeight * (1 - strictness) / (1 - Strictness.Tuned);

    private static readonly ImmutableDictionary<int, double> PhraseEnds = ImmutableDictionary<int, double>.Empty.Add(0, 1);

    /// <summary>A question's end, the first of two phrases: home, or V now and then, a half close.</summary>
    private static readonly ImmutableDictionary<int, double> QuestionEnds = PhraseEnds.Add(4, 0.25);

    /// <summary>The roots that prepare a cadence: ii and IV.</summary>
    private static readonly ImmutableHashSet<int> PreCadenceSteps = [1, 3];

    /// <summary>
    ///     The homes a section may have, as a genre names them, and how likely each is: the tonic itself most often, the
    ///     relative key (the third degree of a minor scale, the sixth of a major one) sometimes, and IV or V now and then.
    /// </summary>
    public static ImmutableArray<(string Name, string Label, double Weight)> Homes { get; } =
    [
        ("tonic", "The tonic", 0.6),
        ("relative", "The relative key, a minor scale's third, a major one's sixth", 0.2),
        ("fourth", "The fourth", 0.1),
        ("fifth", "The fifth", 0.1)
    ];

    /// <summary>
    ///     The step of a section's home above the song's tonic, from -3 to 3, one of <see cref="Homes" />, as likely as the
    ///     genre weighs it where it names them, a black metal band's on its minor tonic and never its relative major.
    /// </summary>
    /// <param name="role">What the section does in the song's form: a bridge leans away from the tonic, to another key.</param>
    /// <param name="progression">The section's progression facet, which a genre's weights are taken at.</param>
    public static int GenerateHome(IGenerationContext context, Scale scale, SectionRole role, Genre genre, double progression)
    {
        var relativeStep = GetQuality(scale.Offsets, 0) == TriadQuality.Minor ? 2 : 5;
        int[] steps = [0, Normalize(relativeStep), Normalize(3), Normalize(4)];
        var pool = genre.Pool<string>(GenreChoices.SectionHome, x => x);
        ImmutableArray<Weighted<int>> homes =
        [
            ..Homes.Select((x, i) => new Weighted<int>(pool is null ? x.Weight : pool(x.Name)?.At(progression) ?? 0, steps[i])).Where(x => x.Weight > 0)
        ];
        var away = Tilt.Of(BridgeAwayOdds, role == SectionRole.Bridge ? 1 : 0);
        return context.Pick(away.Weigh(homes, home => home == 0 ? 0 : 1));
    }

    /// <summary>How far a bridge leans its home away from the tonic, as the odds of any other home over the tonic.</summary>
    public const double BridgeAwayOdds = 4;

    /// <summary>
    ///     How closely a section's progression keeps to its rules by its progression facet: to all of them at the plain
    ///     end, to none at the wild, where every root is as likely, and at the middle as closely as a middling song did.
    /// </summary>
    public static ByConvention Strictness { get; } = new(1, 0.85, 0);

    /// <summary>
    ///     The roots of the chords, in steps above the section's home, from -3 to 3.
    /// </summary>
    /// <param name="count">How many chords the pattern has, at least two.</param>
    /// <param name="home">The step of the section's home above the song's tonic, which sets the chords around it.</param>
    /// <param name="strictness">
    ///     How closely the progression keeps to its rules, from 1, which follows them, to 0, which picks every root
    ///     as likely as any other.
    /// </param>
    /// <param name="phrases">
    ///     How many phrases the progression is, its chords shared among them alike: one, as a four-bar pattern is, its first
    ///     chord home and its last the cadence; more, each starting and ending by its place, as a longer pattern's are.
    /// </param>
    /// <param name="motionLean">
    ///     How a genre leans each motion, by its place in <see cref="Motions" />, from -1 to 1, 0 as the songs have it,
    ///     beyond the strictness, so that a genre's motions are heard in its wildest songs too.
    /// </param>
    /// <param name="degreeLean">
    ///     How a genre leans each root, by its steps above home, its place in <see cref="Degrees" />, as the motions are
    ///     leaned.
    /// </param>
    public static ImmutableArray<int> Generate(IGenerationContext context, Scale scale, int home, double strictness, int count, int phrases, Func<int, double> motionLean,
        Func<int, double> degreeLean) => Generate(context, scale, home, strictness, count, phrases, motionLean, degreeLean, _ => 0);

    /// <param name="cadenceLean">
    ///     How a genre leans the chord that leads home, by its steps above home, its place in <see cref="Degrees" />: a black
    ///     metal band's ♭II or ♭VI rather than V, as the degrees are leaned.
    /// </param>
    public static ImmutableArray<int> Generate(IGenerationContext context, Scale scale, int home, double strictness, int count, int phrases, Func<int, double> motionLean,
        Func<int, double> degreeLean, Func<int, double> cadenceLean)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(strictness);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(strictness, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 2);
        if (phrases < 1 || count % phrases != 0 || count / phrases < 2)
            throw new ArgumentOutOfRangeException(nameof(phrases), phrases, $"{count} chords do not share alike among {phrases} phrases of two or more.");
        var perPhrase = count / phrases;

        var cadenceWeights = GetCadenceWeights(scale.Offsets, home);
        var tilt = Tilt.Of(MotionOdds, 1);
        var roots = new int[count];
        for (var chord = 1; chord < count; chord++)
        {
            var previous = roots[chord - 1];
            var isCadence = chord == count - 1;
            var isPreparation = chord == count - 2;
            var weights = new Weighted<int>[StepCount];
            for (var step = 0; step < StepCount; step++)
            {
                // a longer progression's phrases, past the first, start by their place, and all but the last end home
                var phrase = chord / perPhrase;
                var placed = chord % perPhrase == 0 ? phrase == phrases - 1 ? phrases == 2 ? AnswerStarts : LastPhraseStarts : MiddlePhraseStarts
                    : chord % perPhrase == perPhrase - 1 && phrase < phrases - 1 ? phrases == 2 ? QuestionEnds : PhraseEnds
                    : null;
                // a phrase's place before the cadence's preparation, which a phrase of two chords starts on: the blues' V at
                // bar 9 in chords of two bars
                var roleWeight = isCadence ? cadenceWeights[step] > 0 ? cadenceWeights[step] : OutOfRoleWeight
                    : placed is not null ? placed.GetValueOrDefault(step, OutOfPlaceWeight(strictness))
                    : isPreparation ? PreCadenceSteps.Contains(step) ? 1 : OutOfRoleWeight
                    : 1;
                var motion = Mod(step - previous);
                weights[step] = new Weighted<int>(tilt.Weigh(tilt.Weigh(tilt.Weigh(Math.Pow(MotionWeights[motion] * roleWeight, strictness), motionLean(motion)), degreeLean(step)),
                    isCadence ? cadenceLean(step) : 0), step);
            }

            // a root out of its phrase's place weighs nothing at the plainest
            roots[chord] = Normalize(context.Pick([..weights.Where(x => x.Weight > 0)]));
        }

        return [..roots];
    }

    /// <summary>
    ///     How well each root, in steps above the home, ends a phrase that returns home. They follow from the chords
    ///     the scale builds around the home, so the cadence suits the mode: a major chord on the fifth (V), or a
    ///     minor one less; a major chord a whole step below home (♭VII, as in mixolydian or natural minor); a major
    ///     chord a half step above it (♭II, phrygian) or a whole step above it (II, lydian); the leading-tone chord a
    ///     half step below it a little; IV, more when it is major over a minor home (dorian); and VI now and then, which
    ///     does not resolve.
    /// </summary>
    public static ImmutableArray<double> GetCadenceWeights(ImmutableArray<int> scaleOffsets, int home)
    {
        if (scaleOffsets.Length != StepCount)
            throw new ArgumentException($"The scale must have {StepCount} notes.", nameof(scaleOffsets));

        var weights = new double[StepCount];
        var homeQuality = GetQuality(scaleOffsets, home);
        for (var step = 1; step < StepCount; step++)
        {
            var quality = GetQuality(scaleOffsets, home + step);
            var interval = GetInterval(scaleOffsets, home, home + step);
            weights[step] = step switch
            {
                4 => quality switch { TriadQuality.Major => 1, TriadQuality.Minor => 0.5, _ => 0.2 },
                6 when interval == 10 => quality == TriadQuality.Major ? 1 : 0.3,
                6 when interval == 11 => 0.2,
                1 when quality == TriadQuality.Major => interval == 1 ? 1 : 0.7,
                3 => quality == TriadQuality.Major && homeQuality == TriadQuality.Minor ? 0.8 : 0.3,
                5 => 0.1,
                _ => 0
            };
        }

        return [..weights];
    }

    /// <summary>
    ///     The step of the scale, above the song's tonic, to raise a semitone on the cadence bar so that a minor chord
    ///     on the fifth turns major and leads home as strongly as in harmonic minor; none when the cadence is another
    ///     chord, when the fifth's chord is major already, or when raising its third would not make it major, as in
    ///     phrygian. It is the step a whole step below the home, as in natural minor, dorian and mixolydian.
    /// </summary>
    /// <param name="cadenceRoot">The cadence bar's root, in steps above the home.</param>
    /// <summary>
    ///     The chance a cadence raises the seventh where the scale allows it, by the progression facet: every time at the
    ///     plain end and the middle, as a minor key leads home, and never at the wild end, whose chord on the fifth stays
    ///     modal; a genre's where it names it, a modal band's never (<see cref="GenreChoices.CadenceRaise" />).
    /// </summary>
    public static ByConvention CadenceRaise { get; } = new(1, 1, 0);

    /// <summary>
    ///     The chance a section turns its pattern around (<see cref="TurnAround" />), by the progression facet: never at the
    ///     plain end, which is never reharmonised, and as often at the wild end as in the middle.
    /// </summary>
    public static ByConvention Turnaround { get; } = new(0, 0.1, 0.1);

    /// <summary>
    ///     The progression with its pattern's last bar leading back to its start, the home: a fifth above it, V, and where the
    ///     bar has two chords or more, the one before a fifth above that, ii, as a ii–V does; the chords before kept.
    /// </summary>
    /// <param name="inLastBar">How many of its chords start in the pattern's last bar.</param>
    public static ImmutableArray<int> TurnAround(ImmutableArray<int> progression, int inLastBar)
    {
        var turned = progression.ToBuilder();
        turned[^1] = Normalize(4);
        // the first chord stays home
        if (inLastBar >= 2 && progression.Length > 2)
            turned[^2] = Normalize(1);
        return turned.ToImmutable();
    }

    public static int? GetCadenceRaisedStep(ImmutableArray<int> scaleOffsets, int home, int cadenceRoot)
    {
        if (Mod(cadenceRoot) != 4 || GetQuality(scaleOffsets, home + 4) != TriadQuality.Minor)
            return null;

        var seventh = Mod(home + 6);
        if (GetInterval(scaleOffsets, seventh, home) != 2)
            return null;

        var raised = scaleOffsets.ToArray();
        raised[seventh]++;
        return GetQuality([..raised], home + 4) == TriadQuality.Major ? seventh : null;
    }

    private enum TriadQuality
    {
        Major,
        Minor,
        Other
    }

    /// <summary>The quality of the triad the scale builds on a step: its third and fifth above the root.</summary>
    private static TriadQuality GetQuality(ImmutableArray<int> scaleOffsets, int step)
    {
        var third = GetInterval(scaleOffsets, step, step + 2);
        var fifth = GetInterval(scaleOffsets, step, step + 4);
        return (third, fifth) switch
        {
            (4, 7) => TriadQuality.Major,
            (3, 7) => TriadQuality.Minor,
            _ => TriadQuality.Other
        };
    }

    /// <summary>The semitones from one step of the scale up to another, within an octave.</summary>
    private static int GetInterval(ImmutableArray<int> scaleOffsets, int from, int to)
    {
        return (scaleOffsets[Mod(to)] - scaleOffsets[Mod(from)]).Mod(12);
    }

    private static int Mod(int step)
    {
        return step.Mod(StepCount);
    }

    /// <summary>The step, moved by octaves to within 3 steps of 0.</summary>
    private static int Normalize(int step)
    {
        return Mod(step + 3) - 3;
    }
}
