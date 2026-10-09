using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     A section playing at a tempo of its own, by the feel facet of the song's unconventionality: never in the plainest
///     song, which keeps its tempo; now and then at the middle, a chorus or a bridge the likelier, a little slower or
///     faster most often; and every section but the first at the wild end, by any step. Drawn by appearance, a section
///     mostly keeps the tempo it played at, the wilder the less, and now and then, the wilder the oftener, takes the
///     tempo before it by a metric ratio, as a triplet's pulse becomes the beat. The tempo
///     changes where the section starts, eased into over the bar before half the time, but where it changes by a metric
///     ratio, which a pulse taken over is heard in; and the ending slows from the last section's.
/// </summary>
internal static class SectionTempo
{
    /// <summary>The chance a section, but the song's first, plays at a tempo of its own, before its contrast leans it.</summary>
    public static ByConvention Chance { get; } = new(0, 0.04, 1);

    /// <summary>
    ///     The tempos a section plays at, as multiples of the song's: a little slower or faster most often, and as far as
    ///     two thirds or half again as likely as any other at the wild end.
    /// </summary>
    public static ImmutableArray<(double Factor, ByConvention Weight)> Factors { get; } =
    [
        (0.9, new ByConvention(0.25, 0.25, 1)),
        (1 / 0.9, new ByConvention(0.25, 0.25, 1)),
        (0.8, new ByConvention(0.12, 0.12, 1)),
        (1.25, new ByConvention(0.12, 0.12, 1)),
        (0.75, new ByConvention(0.06, 0.06, 1)),
        (4 / 3.0, new ByConvention(0.06, 0.06, 1)),
        (2 / 3.0, new ByConvention(0.02, 0.02, 1)),
        (1.5, new ByConvention(0.02, 0.02, 1))
    ];

    /// <summary>The chance a section that played before keeps the tempo it played at, by the feel facet: a chorus at its tempo.</summary>
    public static ByConvention KeepChance { get; } = new(1, 0.9, 0.5);

    /// <summary>
    ///     The chance a section's tempo is the one before it by a metric ratio, a pulse of the one becoming the beat of the
    ///     other, by the feel facet: never at the plain end.
    /// </summary>
    public static ByConvention ModulationChance { get; } = new(0, 0.05, 0.5);

    /// <summary>
    ///     Every appearance's tempo as a multiple of the song's, by its place, each drawing from a sequence of its own: the
    ///     first section's the song's; a section that played before at the tempo it played at, mostly; else now and then the
    ///     tempo before it by a metric ratio, within the factors' range; else its own, or the song's.
    /// </summary>
    /// <param name="appearances">An appearance's draws, by its place.</param>
    /// <param name="unconventionality">The feel facet of the song's unconventionality.</param>
    /// <param name="played">The song's sections as it plays them, by their ids.</param>
    /// <param name="roles">What each section does in the song's form, by its id.</param>
    /// <param name="genre">The genre whose chances of a section's own tempo and of one by a metric ratio an appearance draws (<see cref="GenreChoices.SectionChanges" />).</param>
    public static ImmutableArray<double> Generate(
        Func<int, IGenerationContext> appearances,
        double unconventionality,
        IReadOnlyList<int> played,
        IReadOnlyDictionary<int, SectionRole> roles,
        Genre genre
    )
    {
        var tempos = new List<double>();
        var last = new Dictionary<int, double>();
        var (lowest, highest) = (Factors.Min(x => x.Factor), Factors.Max(x => x.Factor));
        for (var i = 0; i < played.Count; i++)
        {
            var id = played[i];
            var drawing = appearances(i);
            var hasOwn = drawing.TestProbability(SectionContrast.Of(roles[id]).Chance(Chance.At(unconventionality, genre.Chance(GenreChoices.SectionChanges, "tempo")), 1));
            var factor = drawing.Pick(ByConvention.Weigh(Factors, unconventionality));
            var keeps = drawing.TestProbability(KeepChance.At(unconventionality));
            var modulates = drawing.TestProbability(ModulationChance.At(unconventionality, genre.Chance(GenreChoices.SectionChanges, "modulation")));
            var ratio = MetricRatios[drawing.GenerateInt(0, MetricRatios.Length)];
            var modulated = i > 0 ? tempos[i - 1] * ratio : 1;
            // the first section sets the song's tempo
            var tempo = id == played[0] ? 1
                : keeps && last.TryGetValue(id, out var before) ? before
                : modulates && modulated >= lowest - 1e-9 && modulated <= highest + 1e-9 ? modulated
                : hasOwn ? factor : 1;
            tempos.Add(tempo);
            last[id] = tempo;
        }

        return [..tempos];
    }

    /// <summary>
    ///     The ratios of a tempo to the one before that a pulse of the one becomes the beat of the other at, as a triplet's
    ///     quarter does at 3:2: taken at once, never eased into, so that the pulse is heard taken over.
    /// </summary>
    public static ImmutableArray<double> MetricRatios { get; } = [1.5, 4 / 3.0, 2 / 3.0, 0.75];

    /// <summary>The chance a change of tempo is eased into over the bar before, beat by beat, rather than taken at once.</summary>
    public const double RampChance = 0.5;


    /// <summary>
    ///     The tempo, as a multiple of the song's, from every section's start on, eased into over the bar before now and
    ///     then, beat by beat, evenly in its ratio, but for a metric one; none for a song that keeps its tempo.
    /// </summary>
    /// <param name="ramps">A change's draws, by the place among the song's sections of the section it changes to.</param>
    public static StateTimelineMap ToStateTimelineMap(ImmutableArray<double> tempos, SongMap map, double duration, Func<int, IGenerationContext> ramps)
    {
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (tempos.All(x => x == 1))
            return StateTimelineMap.Create(duration);

        var items = new List<TimelineItem<double>>();
        for (var i = 0; i < map.Sections.Length; i++)
        {
            var (section, next) = (map.Sections[i], tempos[i]);
            if (i > 0)
            {
                var before = tempos[i - 1];
                var eases = ramps(i).TestProbability(RampChance);
                var ratio = next / before;
                if (eases && Math.Abs(ratio - 1) > 1e-9 && !MetricRatios.Any(x => Math.Abs(x - ratio) < 1e-9))
                {
                    // over the bar before the section, a short one's where the section before cuts it
                    var bar = map.MeterAt(section.Start - 1e-6).BarDuration;
                    var beats = (int)Math.Ceiling(bar - 1e-9);
                    for (var k = 0; k < beats; k++)
                        items.Add((before * Math.Pow(ratio, (k + 1) / (double)(beats + 1))).ToTimelineItem(section.Start - bar + k));
                }
            }

            items.Add(next.ToTimelineItem(section.Start));
        }

        IStateTimeline tempo = StateTimeline.Create(duration, StateKinds.Tempo, items).WithLayer("Section tempo");
        return new[] { tempo }.ToStateTimelineMap(duration);
    }
}
