using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>Hi-hat rolls, a stage of the song after it is put together (<see cref="SongStages" />).</summary>
internal static class HatRolls
{
    /// <summary>What a hat roll keeps out of: a fill, stop time, a drum solo and the hats stopped by the form.</summary>
    private const Hold KeepsOutOf = Hold.Fill | Hold.Stop | Hold.DrumSolo | Hold.Cut;

    /// <summary>The chance a bar's time drum rolls its last beat, a genre's that the songs never play (<see cref="GenreChoices.HatRolls" />).</summary>
    public static ByConvention Chance { get; } = new(0, 0, 0);

    /// <summary>How fast a roll's notes come, a beat's worth of them, and how likely: 32nds, and 16th triplets the trap's other.</summary>
    public static ImmutableArray<Weighted<int>> Rates { get; } = [new(1, 8), new(1, 6)];

    /// <summary>How much likelier a roll ends with its bar than on another of its beats: three times, the bar's end most at home.</summary>
    public const double BarEndOdds = 3;

    /// <summary>How long a roll lasts, in beats, up to where it ends, and how likely: a beat, or the half of it.</summary>
    public static ImmutableArray<Weighted<double>> Lengths { get; } = [new(1, 1), new(1, 0.5)];

    /// <summary>How loud a roll's first note plays against its last, which is the hat's own.</summary>
    public const double From = 0.6;

    /// <summary>
    ///     Hi-hat rolls, the trap's: now and then a beat of a bar, or its half, on the drums keeping time played as a roll of
    ///     32nds or 16th triplets growing louder, into one of the bar's beats, its end the likeliest, into the snare or the
    ///     next bar, each note the drum's own note there, where it plays and no fill does. A
    ///     bar's decision, a genre's, from a sequence of its own for each appearance. The drums keeping time are the song's,
    ///     as their roles are drawn for it, a wild section's redrawn roles aside.
    /// </summary>
    internal static SongDraft Apply(SongDecisions song, SongDraft draft)
    {
        var notes = SongStages.NotesOf(draft);
        var times = notes.Keys.Where(x => song.Tracks.Definitions[x] is PercussionInstrumentTrack definition && DrumRoles.Of(definition.StateMap) == DrumRole.Time).ToArray();
        if (times.Length == 0)
            return draft;

        var rolls = new List<(double From, double To, int Rate)>();
        foreach (var (span, index) in song.Form.Map.Sections.Select((x, i) => (x, i)))
        {
            var context = song.AppearanceStream(SongStream.HatRolls, index);
            var chance = Chance.At(song.SectionOf(span.SectionId).Facets[Facet.Groove], song.Genre.Chance(GenreChoices.HatRolls, "bars"));
            // every bar, a short last one's too, the bar before the next section where a roll is most at home
            foreach (var (_, start, end) in span.Bars)
            {
                // the beats it may end on, a short bar's own, its end the likeliest
                var beats = (int)Math.Max(1, Math.Round(end - start));
                ImmutableArray<Weighted<double>> ends = [..Enumerable.Range(1, beats).Select(k => new Weighted<double>(k == beats ? BarEndOdds : 1, k == beats ? end : start + k))];
                // drawn for every bar, so that the draws are as many
                var (rolls1, rate, length, into) = (context.TestProbability(chance), context.Pick(Rates), context.Pick(Lengths), context.Pick(ends));
                if (rolls1)
                    rolls.Add((into - length, into, rate));
            }
        }

        // but where a fill plays, from its first note to its line, and in stop time, after the draws so that they are as many
        var holds = draft.Holds(song);
        rolls.RemoveAll(r => times.Any(track => holds.Meets(KeepsOutOf, r.From, r.To, track)));

        var result = notes.ToBuilder();
        foreach (var track in times)
        {
            var timeline = notes[track];
            var rolled = timeline.ToList();
            foreach (var (from, to, rate) in rolls)
            {
                // the drum's note where the roll starts, which every note of it plays, where it plays there
                var own = timeline.FirstOrDefault(x => x.IsWithin(from, to));
                if (own.Value is null)
                    continue;
                rolled.RemoveAll(x => x.IsWithin(from, to));
                var count = (int)Math.Round((to - from) * rate);
                rolled.AddRange(Enumerable.Range(0, count).Select(k => (own.Value with
                {
                    Velocity = Render.ScaleLoudness(own.Value.Velocity, From + (1 - From) * k / Math.Max(1, count - 1)),
                    Duration = Math.Min(own.Value.Duration, 1.0 / rate)
                }).ToTimelineItem(from + k / (double)rate)));
            }

            result[track] = EventTimeline.Create(timeline.Duration, rolled.OrderBy(x => x.Position));
        }

        return draft with { Notes = result.ToImmutable() };
    }
}
