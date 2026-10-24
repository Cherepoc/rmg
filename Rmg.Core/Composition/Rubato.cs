using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     The tempo at a phrase's edges, a genre's that the songs never play: the part alone at the song's opening pushing a
///     little through each of its phrases and broadening at their ends, the band coming in in tempo, and a section's last
///     bar broadening into the next, a breath before it, back in tempo where it starts: into a section heard before, a
///     return, at the plain end, and into any at the wild. One tempo edit, the ritardando's steps over a phrase's last bar
///     (<see cref="FormLayers.Ritardando" />), a layer of the song's tempo.
/// </summary>
internal static class Rubato
{
    /// <summary>The chance the part alone at the opening pushes and broadens its phrases.</summary>
    public static ByConvention OpeningChance { get; } = new(0, 0, 0);

    /// <summary>The chance a section's last bar broadens into a section heard before.</summary>
    public static ByConvention ReturnChance { get; } = new(0, 0, 0);

    /// <summary>The chance a section's last bar broadens into one not heard before.</summary>
    public static ByConvention ChangeChance { get; } = new(0, 0, 0);

    /// <summary>How the tempo pushes over a phrase's first half, in even steps, before it broadens over its last bar.</summary>
    public static ImmutableArray<double> Push { get; } = [1.02, 1.04, 1.03];

    /// <summary>The song's tempo at its phrases' edges as a layer of it, none where it plays none.</summary>
    /// <param name="opening">The song's own sequence, which the opening draws from.</param>
    /// <param name="appearance">An appearance's sequence, by its place, which its breath into it draws from.</param>
    /// <param name="facet">An appearance's form facet, by its place.</param>
    public static StateTimelineMap Apply(SongMap map, IGenerationContext opening, Func<int, IGenerationContext> appearance, Func<int, double> facet, Genre genre)
    {
        var items = new List<TimelineItem<double>>();
        // the part alone at the opening, from its start until the next part comes in, its phrases pushed and broadened
        var entries = map.Intro.Kind == IntroKind.Entries ? map.Intro.Entries : [];
        var pushes = opening.TestProbability(OpeningChance.At(facet(0), genre.Chance(GenreChoices.Rubato, "openings")));
        if (pushes && entries.Length > 1 && entries[1].Entry > 1e-9 && !map.Sections.IsEmpty)
        {
            var start = map.Intro.Window.IsBefore ? 0 : map.Origin;
            var (meter, alone) = (map.Sections[0].Meter, entries[1].Entry);
            var phrase = Math.Min(meter.PhraseDuration, alone);
            for (var from = start; from < start + alone - 1e-9; from += phrase)
                Phrase(from, Math.Min(from + phrase, start + alone), meter.BarDuration);
        }

        // a section's last bar broadening into the next, a return's or any's, each appearance from a sequence of its own
        var heard = new HashSet<int>();
        for (var i = 0; i < map.Sections.Length; i++)
        {
            var span = map.Sections[i];
            var returns = !heard.Add(span.SectionId);
            if (i == 0)
                continue;
            var breathes = appearance(i).TestProbability((returns ? ReturnChance : ChangeChance).At(facet(i), genre.Chance(GenreChoices.Rubato, returns ? "returns" : "changes")));
            if (breathes)
                Broaden(span.Start, map.BarBefore(span.Start));
        }

        StateTrace.RecordSong(TracePoints.Rubato, $"{items.Count} steps", items.Count > 0);
        if (items.Count == 0)
            return StateTimelineMap.Create(map.Duration);
        static IEnumerable<TimelineItem<T>> Once<T>(IEnumerable<TimelineItem<T>> steps) => steps.GroupBy(x => x.Position).Select(x => x.Last()).OrderBy(x => x.Position);
        return new[] { StateTimeline.Create(map.Duration, StateKinds.Tempo, Once(items)).WithLayer("Rubato") }.ToStateTimelineMap(map.Duration);

        // the ritardando's steps over the bar before a line, and the tempo again at it
        void Broaden(double line, double bar)
        {
            var steps = FormLayers.Ritardando;
            items.AddRange(steps.Select((x, k) => x.ToTimelineItem(line - bar + k * bar / steps.Length)));
            items.Add(1.0.ToTimelineItem(line));
        }

        // a phrase pushed over its first half and broadened over its last bar, a phrase of a bar broadened alone
        void Phrase(double from, double to, double bar)
        {
            var pushed = (to - from) / 2;
            if (to - from > bar + 1e-9)
                items.AddRange(Push.Select((x, k) => x.ToTimelineItem(from + k * pushed / Push.Length)));
            Broaden(to, Math.Min(bar, to - from));
        }
    }
}
