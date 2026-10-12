using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>What the drums do in a song: keep time, as a band's do, or mark its form, as a calm piece's percussion does.</summary>
public enum DrumJob
{
    Groove,
    Marks
}

/// <summary>
///     The drums marking the form in place of keeping time: one or two of the song's drums, struck now and then where the
///     form moves, a section's start, a phrase's, a chord's change and the song's ending, the likelier the stronger the
///     place and the louder the section, and rolling now and then: into a section on the beat before it, or swelling
///     through a section's start or the ending, a roll quickening and growing to its peak there and slowing and fading
///     after it, as a calm piece's cymbal or timpani does (<see cref="Roll" />); nothing between, so that they never keep
///     time. A genre's, which the songs never play (<see cref="GenreChoices.DrumJob" />), an edit of
///     the drums' notes after the song is put together, in the sections where the drums play, from a sequence of its own.
/// </summary>
internal static class DrumMarks
{
    /// <summary>The drums' jobs, and how likely: the songs' always a groove; marks a genre's alone.</summary>
    public static ImmutableArray<(DrumJob Job, ByConvention Weight)> Jobs { get; } =
    [
        (DrumJob.Groove, new ByConvention(1, 1, 1)),
        (DrumJob.Marks, new ByConvention(0, 0, 0))
    ];

    /// <summary>A job's name, as a genre names it.</summary>
    public static string NameOf(DrumJob job) => job == DrumJob.Groove ? "groove" : "marks";

    /// <summary>How many drums mark the form, and how likely: one, or two, a stronger and a lighter one.</summary>
    public static ImmutableArray<Weighted<int>> Counts { get; } = [new(1, 1), new(1, 2)];

    /// <summary>
    ///     The chance a place is marked, by its strength, a section's start, a phrase's and a chord's change, and how loud the
    ///     mark is against its drum's typical note, the middle of its notes by loudness, so that a mark colours the parts rather than
    ///     strikes over them; a section's start the likeliest and the loudest, as the ending's is.
    /// </summary>
    public static ImmutableArray<(double Chance, double Loudness)> Places { get; } = [(0.8, 1), (0.35, 0.85), (0.05, 0.7)];

    /// <summary>
    ///     The kits a song's drums mark its form on, whose drums colour rather than keep time, the orchestra's timpani and
    ///     the brushes, never a band's; the orchestra's first, played where a genre's pool names neither.
    /// </summary>
    public static ImmutableArray<int> Kits { get; } = [48, 40];

    /// <summary>The chance a section is rolled into on the beat before it.</summary>
    public const double RollChance = 0.15;

    /// <summary>How many notes a beat of a roll plays, and how likely: 16ths and 32nds.</summary>
    public static ImmutableArray<Weighted<int>> RollRates { get; } = [new(1, 4), new(1, 8)];

    /// <summary>How loud a roll's first and last notes play against its peak, the drum's typical note, and how fast, against its rate there.</summary>
    public const double RollFrom = 0.3;

    /// <summary>The chance a section's start or the ending, where it is marked, swells instead of being struck once.</summary>
    public const double SwellChance = 0.25;

    /// <summary>How long a swell grows to its peak, and how long it fades after it, in beats.</summary>
    public const double SwellBeats = 2;

    /// <summary>How far a genre's value moves the chances at its most and its least, as odds: four times, or a quarter (<see cref="GenreChoices.Marks" />).</summary>
    public const double GenreOdds = 4;

    /// <summary>The song's drums' job, a genre's, from a sequence of its own.</summary>
    public static DrumJob Draw(IGenerationContext context, double unconventionality, Genre genre) =>
        context.Pick(ByConvention.Weigh(Jobs, unconventionality, genre.Pool<DrumJob>(GenreChoices.DrumJob, NameOf)));

    /// <summary>The song with its drums marking its form where its genre has them do so.</summary>
    internal static SongDraft Apply(SongDecisions song, SongDraft draft)
    {
        // an empty marking traced where the drums keep the groove or the song has none, so that the trace is there on every path
        if (song.DrumJob == DrumJob.Groove)
            return Unmarked();

        var context = song.Stream(SongStream.DrumMarks);

        var notes = SongStages.NotesOf(draft);
        var drums = notes.Where(x => song.Tracks.Definitions[x.Key] is PercussionInstrumentTrack && x.Value.Count > 0).Select(x => x.Key).ToArray();
        if (drums.Length == 0)
            return Unmarked();
        var count = Math.Min(context.Pick(Counts), drums.Length);
        var marking = drums.OrderBy(_ => context.GenerateDouble()).Take(count).ToArray();
        // every drum's typical note, the middle of its notes by loudness, which its marks play as
        var typical = marking.ToDictionary(x => x, x => notes[x].OrderBy(n => n.Value.Velocity).ElementAt(notes[x].Count / 2).Value);

        var map = song.Form.Map;
        var changes = draft.Timeline.CommonStateTimelineMap.GetStateTimeline(StateKinds.ChordChange).Select(x => x.Position).ToArray();
        // where the drums play in a section, which they mark only there
        bool Plays(SectionSpan span) => drums.Any(x => notes[x].Any(n => n.IsWithin(span.Start, span.End)));
        var often = Tilt.Of(GenreOdds, song.Genre.Shift(GenreChoices.Marks, "often"));
        var swells = Tilt.Of(GenreOdds, song.Genre.Shift(GenreChoices.Marks, "swells"));
        var marks = marking.ToDictionary(x => x, _ => new List<TimelineItem<RealizedNote>>());
        foreach (var (span, index) in map.Sections.Select((x, i) => (x, i)))
        {
            var section = song.SectionOf(span.SectionId);
            var energy = SectionEnergy.Tilt(section.Energy, section.Rhythm.Coupling);
            var plays = Plays(span);
            // the section's places, each at its strongest: its start, its phrases' and its chords' changes
            var phrase = span.Meter.PhraseDuration;
            var places = new SortedDictionary<double, int> { [span.Start] = 0 };
            for (var at = span.Start + phrase; at < span.End - 1e-9; at += phrase)
                places.TryAdd(at, 1);
            foreach (var at in changes.Where(x => x > span.Start + 1e-9 && x < span.End - 1e-9))
                places.TryAdd(at, 2);
            if (index == map.Sections.Length - 1 && map.Ending.Duration > 0)
                places.TryAdd(map.Ending.Start, 0);

            // a section's start and the ending swelling, from a sequence of the section's own, so that the strikes' draws stay
            var swelling = song.AppearanceStream(SongStream.DrumMarks, index);
            // and its strikes and its roll from sequences of its own, so that an earlier section's places leave them
            var (striking, rolling) = (song.AppearanceStream(SongStream.DrumMarks, index, 1), song.AppearanceStream(SongStream.DrumMarks, index, 2));
            // the drum swelling into the section's start, which no roll on it doubles
            int? swelled = null;
            foreach (var (at, strength) in places)
            {
                // drawn for every place, so that the draws are as many
                var (chance, loudness) = Places[strength];
                var (hits, drum) = (striking.TestProbability(often.Chance(energy.Chance(chance, 1), 1)), marking[striking.GenerateInt(0, marking.Length)]);
                var (swell, swellRate) = (swelling.TestProbability(swells.Chance(SwellChance, 1)), swelling.Pick(RollRates));
                if (hits && plays && strength == 0 && swell && at >= SwellBeats)
                {
                    marks[drum].AddRange(Roll(typical[drum], at, SwellBeats, SwellBeats, swellRate));
                    swelled = Math.Abs(at - span.Start) < 1e-9 ? drum : swelled;
                }
                else if (hits && plays)
                    marks[drum].Add((typical[drum] with { Velocity = Render.ScaleLoudness(typical[drum].Velocity, loudness) }).ToTimelineItem(at));
            }

            // a roll into the section on the beat before it, where the drums play the section, but on a drum swelling into it
            var (rolls, rate, roller) = (rolling.TestProbability(often.Chance(energy.Chance(RollChance, 1), 1)), rolling.Pick(RollRates), marking[rolling.GenerateInt(0, marking.Length)]);
            if (rolls && plays && span.Start >= 1 && swelled != roller)
                marks[roller].AddRange(Roll(typical[roller], span.Start, 1, 0, rate));
        }

        var result = notes.ToBuilder();
        foreach (var track in drums)
            result[track] = EventTimeline.Create<RealizedNote>(notes[track].Duration,
                marks.TryGetValue(track, out var own) ? own.GroupBy(x => x.Position).Select(x => x.First()).OrderBy(x => x.Position) : []);
        StateTrace.RecordSong(TracePoints.DrumMarks, string.Join(", ", marking), marking.ToImmutableArray());
        return draft with { Notes = result.ToImmutable() };

        SongDraft Unmarked()
        {
            StateTrace.RecordSong(TracePoints.DrumMarks, "", ImmutableArray<int>.Empty);
            return draft;
        }
    }

    /// <summary>
    ///     A roll peaking at a place: growing from <see cref="RollFrom" /> of its drum's note to the whole of it over the
    ///     beats before, and fading as far over the beats after, its notes the quicker the louder, at its rate at the peak;
    ///     a roll into a section all growth, a swell both.
    /// </summary>
    internal static IEnumerable<TimelineItem<RealizedNote>> Roll(RealizedNote note, double peak, double before, double after, int rate)
    {
        double Loudness(double at) => at <= peak
            ? RollFrom + (1 - RollFrom) * (before > 0 ? (at - (peak - before)) / before : 1)
            : 1 - (1 - RollFrom) * (at - peak) / after;
        // a roll with nothing after its peak ending before it, on the section's first strike
        for (var at = peak - before; at < peak + after + 1e-9 && (after > 0 || at < peak - 1e-9); at += 1 / (rate * Loudness(at)))
            yield return (note with { Velocity = Render.ScaleLoudness(note.Velocity, Loudness(at)), Duration = Math.Min(note.Duration, 1.0 / rate) }).ToTimelineItem(at);
    }
}
