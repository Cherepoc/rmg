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
    internal const Hold KeepsOutOf = Hold.Fill | Hold.Build | Hold.Stop | Hold.DrumSolo | Hold.Cut;

    /// <summary>The chance a bar's time drum rolls its last beat, a genre's that the songs never play (<see cref="GenreChoices.HatRolls" />).</summary>
    public static ByConvention Chance { get; } = new(0, 0, 0);

    /// <summary>
    ///     How fast a roll's notes come, a beat's worth of them, and how likely: 32nds, and 16th triplets the trap's other;
    ///     64ths, a drill, a genre's alone (<see cref="GenreChoices.HatRollRate" />).
    /// </summary>
    public static ImmutableArray<(int Rate, ByConvention Weight)> Rates { get; } = [(8, new(1, 1, 1)), (6, new(1, 1, 1)), (16, new(0, 0, 0))];

    /// <summary>A rate as a genre names it.</summary>
    public static string NameOf(int rate) => rate switch
    {
        8 => "thirtySeconds",
        6 => "tripletSixteenths",
        16 => "sixtyFourths",
        _ => throw new ArgumentOutOfRangeException(nameof(rate), rate, null)
    };

    /// <summary>How much likelier a roll ends with its bar than on another of its beats: three times, the bar's end most at home.</summary>
    public const double BarEndOdds = 3;

    /// <summary>How long a roll lasts, in beats, up to where it ends, and how likely: a beat, or the half of it.</summary>
    public static ImmutableArray<Weighted<double>> Lengths { get; } = [new(1, 1), new(1, 0.5)];

    /// <summary>How loud a roll's first note plays against its last, which is the hat's own.</summary>
    public const double From = 0.6;

    /// <summary>
    ///     The rolls the song plays, each bar's decision, a genre's, from a sequence of its own for each appearance, drawn for
    ///     every bar so that the draws are as many, before a fill or stop time keeps one out (<see cref="SongDecisions.HatRolls" />).
    /// </summary>
    internal static ImmutableArray<(double From, double To, int Rate)> Planned(SongDecisions song)
    {
        var rolls = new List<(double From, double To, int Rate)>();
        // the sections and an intro of entries' window, which plays as the first, its own sequence the first's
        foreach (var (index, span, _) in song.Form.Map.Plays.Where(x => !x.IsEnding))
        {
            var context = song.AppearanceStream(SongStream.HatRolls, index);
            var groove = song.SectionOf(span.SectionId).Facets[Facet.Groove];
            var chance = Chance.At(groove, song.Genre.Chance(GenreChoices.HatRolls, "bars"));
            var rates = ByConvention.Weigh(Rates, groove, song.Genre.Pool<int>(GenreChoices.HatRollRate, NameOf));
            // every bar, a short last one's too, the bar before the next section where a roll is most at home
            foreach (var (_, start, end) in span.Bars)
            {
                // the pulses it may end on, a short bar's own, its end the likeliest: 4/4's beats, 6/8's dotted quarters
                double[] pulses = [..song.Form.Map.MeterAt(start).Pulses.Where(x => x > 1e-9 && start + x < end - 1e-9).Select(x => start + x), end];
                ImmutableArray<Weighted<double>> ends = [..pulses.Select(x => new Weighted<double>(x == end ? BarEndOdds : 1, x))];
                // drawn for every bar, so that the draws are as many
                var (rolls1, rate, length, into) = (context.TestProbability(chance), context.Pick(rates), context.Pick(Lengths), context.Pick(ends));
                if (rolls1)
                    rolls.Add((into - length, into, rate));
            }
        }

        return [..rolls];
    }

    /// <summary>
    ///     Hi-hat rolls, the trap's: now and then a beat of a bar, or its half, on the drums keeping time played as a roll of
    ///     32nds or 16th triplets growing louder, into one of the bar's beats, its end the likeliest, into the snare or the
    ///     next bar, each note the drum's own note there, where it plays and no fill does. A
    ///     bar's decision, a genre's, from a sequence of its own for each appearance. The drum keeping time is the song's
    ///     hi-hat where it keeps time, as the roles are drawn for the song, a wild section's redrawn roles aside; a ride's
    ///     or a shaker's time never rolls.
    /// </summary>
    internal static SongDraft Apply(SongDecisions song, SongDraft draft)
    {
        var notes = SongStages.NotesOf(draft);
        var times = song.Tracks.DrumsOf(DrumRole.Time).Where(x => notes.ContainsKey(x) && x == DrumGroups.GetTrackNumber(DrumDefinitions.HiHat)).ToArray();
        if (times.Length == 0)
            return draft;

        // but where a fill plays, from its first note to its line, and in stop time
        var holds = draft.Holds(song);
        var rolls = song.HatRolls.Where(r => !times.Any(track => holds.Meets(KeepsOutOf, r.From, r.To, track))).ToArray();
        StateTrace.RecordSong(TracePoints.HatRolls, string.Join(", ", rolls.Select(x => $"{x.From}-{x.To}")), rolls.ToImmutableArray());

        var result = notes.ToBuilder();
        foreach (var track in times)
        {
            var timeline = notes[track];
            var rolled = timeline.ToList();
            foreach (var (from, to, rate) in rolls)
            {
                // the drum's note where the roll starts, which every note of it plays, where it plays there, on its stroke,
                // never an accent's or a cue's open hat
                var own = timeline.FirstOrDefault(x => x.IsWithin(from, to));
                if (own.Value is null)
                    continue;
                own = (own.Value with { Pitches = [DrumGroups.GetDrum(track).Sounds[0].Code] }).ToTimelineItem(own.Position);
                rolled.RemoveAll(x => x.IsWithin(from, to));
                var count = (int)Math.Round((to - from) * rate);
                // even between its ends as swung, a drill rather than a limp
                rolled.AddRange(Enumerable.Range(0, count).Select(k => (own.Value with { Duration = Math.Min(own.Value.Duration, 1.0 / rate), State = own.Value.State.With(CompositionStateKinds.EvenFrom, from).With(CompositionStateKinds.EvenTo, to) })
                    .WithVelocity(Render.ScaleLoudness(own.Value.Velocity, From + (1 - From) * k / Math.Max(1, count - 1))).ToTimelineItem(from + k / (double)rate)));
            }

            result[track] = EventTimeline.Create(timeline.Duration, rolled.OrderBy(x => x.Position));
        }

        return draft with { Notes = result.ToImmutable() };
    }
}
