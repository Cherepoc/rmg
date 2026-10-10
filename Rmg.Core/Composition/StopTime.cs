using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>Stop time, a stage of the song after it is put together (<see cref="SongStages" />).</summary>
internal static class StopTime
{
    /// <summary>
    ///     The chance a section plays its appearances' first phrase in stop time, by its form facet: never in the plainest
    ///     songs, now and then past them; a genre's where it names it, the blues' and rock's.
    /// </summary>
    public static ByConvention Chance { get; } = new(0, 0.03, 0.03);

    /// <summary>How a section's energy leans the chance, through its odds: a quiet section the likelier, a verse more than a chorus.</summary>
    public const double EnergyLean = -1;

    /// <summary>How long a stop-time hit is held at most, in beats.</summary>
    public const double Hit = 1;

    /// <summary>
    ///     How much of a stopped bar's rest, after its hit, the lines must sound through to carry it alone; less, and the
    ///     drum keeping time plays on through the bar.
    /// </summary>
    public const double CarriedShare = 0.5;

    /// <summary>Where the song plays in stop time: every stop-time section's appearances' first phrase, but a solo's, its start, end and bar.</summary>
    internal static (double Start, double End, double BarDuration)[] Phrases(SongDecisions song)
    {
        var stops = song.SectionIds.Distinct().ToDictionary(id => id, id =>
        {
            var section = song.SectionOf(id);
            var chance = SectionEnergy.Tilt(section.Energy, section.Rhythm.Coupling)
                .Chance(Chance.At(section.Facets[Facet.Form], song.Genre.Chance(GenreChoices.StopTime, "sections")), EnergyLean);
            return song.Stream(SongStream.StopTime, id).TestProbability(chance);
        });
        // but the first section where an intro brings the band in part by part over it, whose parts not in yet would leave
        // its hits to none
        var intro = song.Form.Map.Intro;
        var built = intro.Kind == IntroKind.Entries && !intro.Window.IsBefore ? song.Form.Map.Sections[0] : null;
        // and but in a solo, whose soloists play over the band as loud as it was measured, as a push leaves a solo be
        return
        [
            ..song.Form.Map.Sections.Where((x, i) => stops[x.SectionId] && x != built && !song.Solos.ContainsKey(i))
                .Select(x => (x.Start, Math.Min(x.End, x.Start + x.Meter.PhraseDuration), x.Meter.BarDuration))
        ];
    }

    /// <summary>
    ///     Stop time: a section's appearances' first phrase, now and then, its band hitting the bar's first beat and resting
    ///     the rest of the bar, a hit held a beat at most, under the lines, the melody's, the counter-melody's and a solo's,
    ///     which go on. A section's decision, from a sequence of its own. A stopped bar is always carried by a part that
    ///     goes on through it: the lines, where they sound through half its rest at least; else the drums keeping time,
    ///     which play on through it; a phrase with a bar neither carries is not stopped, the band playing it as it was. The
    ///     drums play the phrase's last half bar as they have it, a fill there as heavy as a section's leading the band back.
    /// </summary>
    internal static SongDraft Apply(SongDecisions song, SongDraft draft)
    {
        var phrases = song.StopTime;
        // whether a section is stopped wherever it plays, as it is played: a phrase drawn that nothing carries is not
        void Record(Func<(double Start, double End, double BarDuration), bool> played)
        {
            foreach (var id in song.SectionIds.Distinct())
            {
                var drawn = phrases.Where(p => song.Form.Map.Sections.Any(x => x.SectionId == id && Math.Abs(p.Start - x.Start) < 1e-9)).ToArray();
                var stops = drawn.Length > 0 && drawn.All(played);
                StateTrace.RecordSection(TracePoints.StopTime, SectionGenerator.SectionTrace, id, $"{stops}", stops);
            }
        }

        if (phrases.Length == 0)
        {
            Record(_ => false);
            return draft;
        }

        var notes = SongStages.NotesOf(draft);
        var lines = notes.Where(x => song.Tracks.Definitions[x.Key].Role is TrackRole.Melody or TrackRole.CounterMelody or TrackRole.Solo).SelectMany(x => x.Value).ToArray();
        var times = notes.Keys.Where(x => song.Tracks.Definitions[x] is PercussionInstrumentTrack definition && DrumRoles.Of(definition.StateMap) == DrumRole.Time).ToHashSet();
        // every stopped bar's carrier: the lines, or the drums keeping time; a phrase with a bar neither carries played as it
        // was, stop time being the phrase's device
        var carriers = new Dictionary<double, Carrier>();
        foreach (var (start, end, bar) in phrases)
        {
            var bars = new List<(double At, Carrier Carrier)>();
            for (var at = start; at < end - 1e-9; at += bar)
            {
                var (from, to) = (at + Hit, Math.Min(end, at + bar));
                bars.Add((at, Covered(lines.Where(x => x.Position < to - 1e-9 && x.Position + x.Value.Duration > from + 1e-9)
                                   .Select(x => (Math.Max(from, x.Position), Math.Min(to, x.Position + x.Value.Duration)))) >= CarriedShare * (to - from) - 1e-9
                    ? Carrier.Lines
                    : times.Any(t => notes[t].Any(x => x.Position > at + 1e-6 && x.Position < to - 1e-9)) ? Carrier.Time : Carrier.None));
            }

            var carried = bars.All(x => x.Carrier != Carrier.None);
            foreach (var (at, carrier) in bars)
                carriers[Math.Round(at, 6)] = carried ? carrier : Carrier.None;
        }

        Record(p => carriers[Math.Round(p.Start, 6)] != Carrier.None);

        // where a note falls in stop time, a bar a part plays through left out: off the bar's first beat, or on it, a hit
        (bool In, bool OnTheBar) Place(double position, int track)
        {
            foreach (var (start, end, bar) in phrases)
                if (position >= start - 1e-9 && position < end - 1e-9)
                {
                    var at = start + Math.Floor((position - start + 1e-6) / bar) * bar;
                    var carrier = carriers[Math.Round(at, 6)];
                    if (carrier == Carrier.None || carrier == Carrier.Time && times.Contains(track))
                        return (false, false);
                    return (true, (position - start + 1e-6) % bar < 2e-6);
                }

            return (false, false);
        }

        // the drums' fills out of it, from a run's first note to its line, which go on as a drummer fills into the next
        // section, a drum never heard otherwise landing there, and the bass walking with them in a phrase's last half bar
        var fills = draft.FillSpans
            // and the last half bar of every phrase, the drums leading the band back in, with the fill its line draws there
            .Concat(phrases.Select(x => (From: x.End - x.BarDuration / 2, To: x.End))).ToArray();
        bool InAFill(double position) => fills.Any(x => position >= x.From - 1e-9 && position < x.To - 1e-9);

        var result = notes.ToBuilder();
        foreach (var (track, timeline) in notes)
        {
            var role = song.Tracks.Definitions[track].Role;
            if (role is TrackRole.Melody or TrackRole.CounterMelody or TrackRole.Solo)
                continue;
            bool Fills(double position) => role is TrackRole.Drum or TrackRole.Bass && InAFill(position);
            result[track] = EventTimeline.Create(timeline.Duration, timeline
                .Where(note => Place(note.Position, track) is not (true, false) || Fills(note.Position))
                .Select(note => Place(note.Position, track).In && !Fills(note.Position)
                    ? (note.Value with { Duration = Math.Min(note.Value.Duration, Hit) }).ToTimelineItem(note.Position)
                    : note));
        }

        return draft with { Notes = result.ToImmutable() };
    }

    /// <summary>What goes on through a stopped bar.</summary>
    private enum Carrier
    {
        /// <summary>Nothing, so the phrase is not stopped.</summary>
        None,

        /// <summary>The lines, sounding through half its rest at least.</summary>
        Lines,

        /// <summary>The drums keeping time, playing on through it.</summary>
        Time
    }

    /// <summary>How long a set of spans covers, their overlaps once.</summary>
    private static double Covered(IEnumerable<(double From, double To)> spans)
    {
        var (total, reached) = (0.0, double.NegativeInfinity);
        foreach (var (from, to) in spans.OrderBy(x => x.Item1))
        {
            total += Math.Max(0, to - Math.Max(from, reached));
            reached = Math.Max(reached, to);
        }

        return total;
    }
}
