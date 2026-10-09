using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>An edit a mutating groove makes to its drums' bars (<see cref="Mutations" />).</summary>
public enum MutationKind
{
    /// <summary>A hit moved a 16th earlier or later.</summary>
    Shift,

    /// <summary>A hit left out.</summary>
    Drop,

    /// <summary>A hit struck again on an empty 16th of its drum.</summary>
    Double,

    /// <summary>A hit ratcheted, struck again a 32nd apart, two to four times.</summary>
    Ratchet,

    /// <summary>Two 8ths of the bar swapped, every drum's hits in them, a break chopped and resequenced.</summary>
    Swap,

    /// <summary>The kick and the snare traded within a beat.</summary>
    Trade
}

/// <summary>An edit at a place in the bar, in beats from its start, and what it moves by: a 16th either way, the times a ratchet strikes, or the slice swapped with.</summary>
public sealed record Mutation(MutationKind Kind, int Track, double At, double By);

/// <summary>
///     A groove that mutates, as a programmed break evolves: every bar of a section's appearance after its first takes an
///     edit or two now and then, by a vocabulary as data (<see cref="MutationKind" />), and keeps every edit made before it,
///     so that the groove strays further from its own as the section goes on, each bar its own pattern with the edits laid
///     on it; at a phrase's start it snaps back to its own now and then, the likelier the plainer. The downbeat's kick and
///     the backbeat's snare kept but at the wild end. Each bar's chance by the groove facet, none at the plain end and the
///     middle, a genre's, an IDM or a drum and bass track's, and leaned by the section's energy. A stage of the song after
///     its figures and before its fills, which play over it (<see cref="SongStages" />), from a sequence of its own an
///     appearance.
/// </summary>
internal static class Mutations
{
    /// <summary>The chance a bar adds an edit, by the groove facet: a wild song's alone, as often as a genre has it.</summary>
    public static ByConvention Chance { get; } = new(0, 0, 0.4);

    /// <summary>The edits, and how likely each is.</summary>
    public static ImmutableArray<Weighted<MutationKind>> Kinds { get; } =
    [
        new(1, MutationKind.Shift), new(1, MutationKind.Drop), new(0.7, MutationKind.Double), new(0.7, MutationKind.Ratchet),
        new(0.5, MutationKind.Swap), new(0.3, MutationKind.Trade)
    ];

    /// <summary>The chance a phrase's start snaps the groove back to its own, by the groove facet: always at the plain end, seldom at the wild.</summary>
    public static ByConvention SnapBack { get; } = new(1, 0.5, 0.15);

    /// <summary>The chance the downbeat's kick and the backbeat's snare are kept, by the groove facet: but at the wild end.</summary>
    public static ByConvention KeepsAnchors { get; } = new(1, 1, 0);

    /// <summary>The chance a bar that edits makes a second edit.</summary>
    public const double SecondEdit = 0.3;

    private const double Sixteenth = 0.25;

    internal static SongDraft Apply(SongDecisions song, SongDraft draft)
    {
        var timelines = draft.Timeline.TrackTimelineMap;
        var drums = song.Playing.Where(x => x.Value == TrackRole.Drum && timelines.ContainsKey(x.Key)).Select(x => x.Key).Order().ToArray();
        if (drums.Length == 0)
            return draft;
        var kicks = DrumGroups.Kick.Drums.Select(DrumGroups.GetTrackNumber).ToHashSet();
        var snares = DrumGroups.Snare.Drums.Select(DrumGroups.GetTrackNumber).ToHashSet();
        var played = drums.ToDictionary(x => x, x => timelines[x].EventTimeline.ToList());
        var any = false;
        var claves = DrumGroups.GetTrackNumber(DrumDefinitions.Claves);
        foreach (var (span, index) in song.Form.Map.Sections.Select((x, i) => (x, i)))
        {
            var section = song.SectionOf(span.SectionId);
            // the drums playing their own rhythm alone: not a figure's, a cycle's run across the bar lines, one bound to a lead or a lead
            var figures = draft.Figures.GetValueOrDefault(span.SectionId, ImmutableDictionary<FigurePlayer, string>.Empty);
            int[] own = draft.Polymeters.Contains(span.SectionId) ? [] :
            [
                ..drums.Where(x => !section.Bindings.ContainsKey(x) && !section.Bindings.ContainsValue(x) && !(figures.ContainsKey(FigurePlayer.Ground) && kicks.Contains(x))
                                   && !(figures.ContainsKey(FigurePlayer.Backbeat) && snares.Contains(x)) && !(figures.ContainsKey(FigurePlayer.Claves) && x == claves))
            ];
            var groove = section.Facets[Facet.Groove];
            var context = song.AppearanceStream(SongStream.Mutations, index);
            var chance = SectionEnergy.Tilt(section.Energy, section.Rhythm.Coupling).Chance(Chance.At(groove, song.Genre.Chance(GenreChoices.Mutation, "bars")), 1);
            var anchored = context.TestProbability(KeepsAnchors.At(groove));
            var barLength = span.Meter.BarDuration;
            var bars = (int)Math.Round(span.Duration / barLength);
            var edits = new List<Mutation>();
            var made = 0;
            for (var bar = 1; bar < bars; bar++)
            {
                if (bar % Meter.PhraseBarCount == 0 && context.TestProbability(SnapBack.At(groove)))
                    edits.Clear();
                var start = span.Start + bar * barLength;
                var count = context.TestProbability(chance) ? context.TestProbability(SecondEdit) ? 2 : 1 : 0;
                for (var edit = 0; edit < count; edit++)
                    if (Draw(context, played, own, start, span.Meter, kicks, snares, anchored) is { } mutation)
                        edits.Add(mutation);
                made += count;
                if (edits.Count > 0)
                {
                    foreach (var mutation in edits)
                        Lay(mutation, played, own, start, barLength, kicks, snares);
                    any = true;
                }
            }

            StateTrace.RecordSection(TracePoints.Mutations, FillGenerator.DrumsTrace, span.SectionId, $"{made} edits", (index, made));
        }

        if (!any)
            return draft;
        return draft with
        {
            Timeline = draft.Timeline.MapTrackEvents(drums.ToDictionary(x => x,
                x => (Func<EventTimeline<StateMap>, EventTimeline<StateMap>>)(notes => EventTimeline.Create(notes.Duration, played[x].OrderBy(n => n.Position)))))
        };
    }

    /// <summary>The hits of a drum in a bar, in beats from its start.</summary>
    private static IEnumerable<double> HitsIn(List<TimelineItem<StateMap>> notes, double start, double length) =>
        notes.Where(x => x.Position >= start - 1e-9 && x.Position < start + length - 1e-9).Select(x => x.Position - start);

    /// <summary>Whether a hit is an anchor: the downbeat's kick, a snare on a later group of the meter, its backbeat in any meter.</summary>
    private static bool IsAnchor(int track, double at, Meter meter, IReadOnlySet<int> kicks, IReadOnlySet<int> snares) =>
        kicks.Contains(track) && at < 1e-9 || snares.Contains(track) && at > 1e-9 && meter.Levels[1].Any(x => Math.Abs(x.Start / 4.0 - at) < 1e-9);

    /// <summary>An edit drawn on the bar as it is, none where it finds nothing to edit; every draw made whatever it finds.</summary>
    private static Mutation? Draw(IGenerationContext context, Dictionary<int, List<TimelineItem<StateMap>>> played, int[] drums, double start, Meter meter,
        IReadOnlySet<int> kicks, IReadOnlySet<int> snares, bool anchored)
    {
        var length = meter.BarDuration;
        var kind = context.Pick(Kinds);
        var track = drums.Length == 0 ? -1 : drums[context.GenerateInt(0, drums.Length)];
        var pick = context.GenerateDouble();
        var by = context.TestProbability(0.5) ? 1 : -1;
        var slices = (int)Math.Floor(length / (2 * Sixteenth) + 1e-9);
        var (from, to) = (context.GenerateInt(0, slices), context.GenerateInt(0, slices));
        var times = 2 + context.GenerateInt(0, 3);
        if (track < 0)
            return null;
        var hits = HitsIn(played[track], start, length).Where(x => !anchored || !IsAnchor(track, x, meter, kicks, snares)).Distinct().ToArray();
        // a stretch of the bar holding an anchor, which a swap or a trade moves whole
        bool Holds(double from, double width) => anchored && drums.Any(drum =>
            HitsIn(played[drum], start, length).Any(x => x >= from - 1e-9 && x < from + width - 1e-9 && IsAnchor(drum, x, meter, kicks, snares)));
        var beat = Math.Floor(pick * Math.Floor(length + 1e-9));
        return kind switch
        {
            MutationKind.Swap when from != to && !Holds(from * 2 * Sixteenth, 2 * Sixteenth) && !Holds(to * 2 * Sixteenth, 2 * Sixteenth) =>
                new Mutation(kind, track, from * 2 * Sixteenth, to * 2 * Sixteenth),
            MutationKind.Trade when !Holds(beat, 1) => new Mutation(kind, track, beat, 0),
            MutationKind.Swap or MutationKind.Trade => null,
            _ when hits.Length == 0 => null,
            MutationKind.Shift => new Mutation(kind, track, hits[(int)(pick * hits.Length)], by * Sixteenth),
            MutationKind.Ratchet => new Mutation(kind, track, hits[(int)(pick * hits.Length)], times),
            MutationKind.Double => new Mutation(kind, track, hits[(int)(pick * hits.Length)], by * 2 * Sixteenth),
            _ => new Mutation(kind, track, hits[(int)(pick * hits.Length)], 0)
        };
    }

    /// <summary>An edit laid on a bar, as far as the bar has what it edits.</summary>
    private static void Lay(Mutation mutation, Dictionary<int, List<TimelineItem<StateMap>>> played, int[] drums, double start, double length,
        IReadOnlySet<int> kicks, IReadOnlySet<int> snares)
    {
        bool In(double position, double from, double width) => position >= start + from - 1e-9 && position < start + from + width - 1e-9;
        var notes = played[mutation.Track];
        var at = notes.FindIndex(x => Math.Abs(x.Position - (start + mutation.At)) < 1e-9);
        switch (mutation.Kind)
        {
            case MutationKind.Drop when at >= 0:
                notes.RemoveAt(at);
                break;
            case MutationKind.Shift when at >= 0:
            {
                var moved = start + mutation.At + mutation.By;
                if (moved >= start - 1e-9 && moved < start + length - 1e-9 && notes.All(x => Math.Abs(x.Position - moved) > 1e-9))
                    notes[at] = notes[at].Value.ToTimelineItem(moved);
                break;
            }
            case MutationKind.Double when at >= 0:
            {
                var again = start + mutation.At + mutation.By;
                if (again >= start - 1e-9 && again < start + length - 1e-9 && notes.All(x => Math.Abs(x.Position - again) > 1e-9))
                    notes.Add(notes[at].Value.ToTimelineItem(again));
                break;
            }
            case MutationKind.Ratchet when at >= 0:
                for (var hit = 1; hit < (int)mutation.By; hit++)
                {
                    var again = start + mutation.At + hit * Sixteenth / 2;
                    if (again < start + length - 1e-9 && notes.All(x => Math.Abs(x.Position - again) > 1e-9))
                        notes.Add(notes[at].Value.ToTimelineItem(again));
                }

                break;
            case MutationKind.Swap:
                foreach (var drum in drums)
                {
                    var hits = played[drum];
                    var (a, b) = (mutation.At, mutation.By);
                    played[drum] = hits.Select(x => In(x.Position, a, 2 * Sixteenth) ? x.Value.ToTimelineItem(x.Position - a + b)
                        : In(x.Position, b, 2 * Sixteenth) ? x.Value.ToTimelineItem(x.Position - b + a) : x).ToList();
                }

                break;
            case MutationKind.Trade:
            {
                var kick = drums.FirstOrDefault(kicks.Contains, -1);
                var snare = drums.FirstOrDefault(snares.Contains, -1);
                if (kick < 0 || snare < 0)
                    break;
                var (kickHits, snareHits) = (played[kick].Where(x => In(x.Position, mutation.At, 1)).ToArray(), played[snare].Where(x => In(x.Position, mutation.At, 1)).ToArray());
                // each struck where the other was, as the other's note was
                var kickNote = kickHits.FirstOrDefault().Value ?? played[kick].FirstOrDefault().Value;
                var snareNote = snareHits.FirstOrDefault().Value ?? played[snare].FirstOrDefault().Value;
                if (kickNote is null || snareNote is null)
                    break;
                played[kick] = [..played[kick].Except(kickHits), ..snareHits.Select(x => kickNote.ToTimelineItem(x.Position))];
                played[snare] = [..played[snare].Except(snareHits), ..kickHits.Select(x => snareNote.ToTimelineItem(x.Position))];
                break;
            }
        }
    }
}
