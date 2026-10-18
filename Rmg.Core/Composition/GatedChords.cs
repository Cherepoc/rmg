using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     A trance gate's chords (<see cref="StrongEffect.Gate" />): the gated part holding its chords through every section it
///     plays in, each changing on a beat, so that the gate chops one held sound in time rather than the part's own rhythm.
/// </summary>
internal static class GatedChords
{
    private const double Epsilon = 1e-6;

    /// <summary>How loud a held chord struck again at a bar line plays, of its first stroke's velocity.</summary>
    public const double RestrikeVelocity = 0.8;

    /// <summary>
    ///     A part's notes as held chords: for every chord of the song, from the first beat of the bar the part plays at or
    ///     after, every note it plays over the chord as one, the notes it sounds longest there struck as the first, held until
    ///     the next chord, the section's end or the band's next stop, whichever comes first; a note pushed ahead of a chord,
    ///     as it reaches the chord's beat, the chord's, and a chord the same as the one before it part of it. Stop time's
    ///     notes as they are, its hits, an arpeggio's, a broken chord being a rhythm of its own, which the gate chops as it
    ///     plays, as trance gates a pluck, and a solo's, a line of its own. A chord on an instrument that dies away as it rings, a guitar's or a piano's, is
    ///     struck again, softer, on every bar line it holds across, so that the gate has a sound to chop.
    /// </summary>
    /// <param name="changes">Where the chords change, in order.</param>
    /// <param name="cuts">Where the band stops, as at a drop, which a held chord ends by.</param>
    /// <param name="stopTime">The spans played in stop time, whose notes stay as they are and which a held chord ends by.</param>
    /// <param name="decays">Whether a note's instrument dies away as it rings (<see cref="InstrumentTrait.Decays" />).</param>
    public static EventTimeline<RealizedNote> Hold(
        EventTimeline<RealizedNote> timeline,
        SongMap map,
        IReadOnlyList<double> changes,
        IReadOnlyList<double> cuts,
        IReadOnlyList<(double Start, double End)> stopTime,
        Func<RealizedNote, bool> decays
    )
    {
        bool InStopTime(double position) => stopTime.Any(x => position >= x.Start - Epsilon && position < x.End - Epsilon);
        var stops = cuts.Concat(stopTime.Select(x => x.Start)).Order().ToArray();
        double NextStop(double position) => stops.FirstOrDefault(x => x > position + Epsilon, double.PositiveInfinity);

        // every section's; an intro's and an ending's notes, which play no gate, as they are, and a broken chord's and a
        // solo's, which hold no chord
        bool HoldsNoChord(RealizedNote note) => note.State.GetStateValue(CompositionStateKinds.Arpeggio) != 0 || note.State.GetStateValue(CompositionStateKinds.LineSolo) != 0;
        var held = timeline.Where(x => InStopTime(x.Position) || HoldsNoChord(x.Value) || x.Position < map.Sections[0].Start - Epsilon || x.Position >= map.Sections[^1].End - Epsilon).ToList();
        foreach (var section in map.Sections)
        {
            var bar = section.Meter.BarDuration;
            // the next beat of the bar at or after a position
            double Beat(double position)
            {
                var start = section.Bar(section.BarOf(position)).Start;
                return section.Meter.Pulses.Select(x => start + x).Append(start + bar).First(x => x >= position - Epsilon);
            }

            // the section's chords, each from its beat, and the notes played over each
            var starts = changes.Where(x => x > section.Start + Epsilon && x < section.End - Epsilon).Select(Beat).Prepend(section.Start).Distinct().ToArray();
            var chords = new List<(double Start, double End, RealizedNote Note)>();
            // over a chord, the notes before a stop apart from those after it
            foreach (var group in timeline
                         .Where(x => x.Position >= section.Start - Epsilon && x.Position < section.End - Epsilon && !InStopTime(x.Position) && !HoldsNoChord(x.Value))
                         .Select(x => (Beat: Beat(x.Position), x.Value, Stop: NextStop(x.Position)))
                         .Where(x => x.Beat < Math.Min(section.End, x.Stop) - Epsilon)
                         .GroupBy(x => (Chord: starts.Last(s => s <= x.Beat + Epsilon), x.Stop)))
            {
                var start = group.First().Beat;
                // the notes it sounds longest over it, their chord with them, a stab pushed ahead of it or a passing note left
                // out, struck as its first note is
                var longest = group.GroupBy(x => string.Join(",", x.Value.Pitches)).MaxBy(x => x.Sum(y => y.Value.Duration))!.First().Value;
                var end = Math.Min(Math.Min(starts.FirstOrDefault(s => s > group.Key.Chord + Epsilon, section.End), section.End), group.Key.Stop);
                if (chords.Count > 0 && chords[^1].Note.Pitches.SequenceEqual(longest.Pitches) && Math.Abs(chords[^1].End - start) < Epsilon)
                    chords[^1] = chords[^1] with { End = end };
                else
                    chords.Add((start, end, longest.WithVelocity(group.First().Value.Velocity)));
            }

            foreach (var (start, end, note) in chords)
            {
                // struck again on every bar line it holds across, where it dies away
                var strokes = decays(note)
                    ? Enumerable.Range(1, (int)Math.Ceiling((end - section.Start) / bar)).Select(x => section.Start + x * bar).Where(x => x > start + Epsilon && x < end - Epsilon).Prepend(start).ToArray()
                    : [start];
                held.AddRange(strokes.Select((x, i) => (note with { Duration = (i + 1 < strokes.Length ? strokes[i + 1] : end) - x })
                    .WithVelocity(i == 0 ? note.Velocity : Render.ScaleLoudness(note.Velocity, RestrikeVelocity)).ToTimelineItem(x)));
            }
        }

        return EventTimeline.Create(timeline.Duration, held.OrderBy(x => x.Position));
    }
}
