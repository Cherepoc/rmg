using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     Stop time and ensemble hits, a stage of the song after it is put together (<see cref="SongStages" />): the band
///     striking together and resting between, under the lines, on a stopped bar's first beat, or on the kick's notes in a
///     section's last bar before a louder one.
/// </summary>
internal static class StopTime
{
    /// <summary>What stop time keeps out of: a calling verse, its call and its answer, and a drop's build roll.</summary>
    internal const Hold KeepsOutOf = Hold.Call | Hold.Answer | Hold.Build;

    /// <summary>
    ///     The chance a section of a song that stops plays its appearances' first phrase in stop time, by its form facet: never in the plainest
    ///     songs, now and then past them; a genre's where it names it, the blues' and rock's.
    /// </summary>
    public static ByConvention Chance { get; } = new(0, 0.06, 0.06);

    /// <summary>
    ///     The chance a song stops at all, its sections then drawing stop time and ensemble hits, by its form facet: never in
    ///     the plainest songs, and as often in the wildest as in a middling one, as a band stops in one place or two as a
    ///     feature rather than in section after section (<see cref="GenreChoices.Stops" />).
    /// </summary>
    public static ByConvention SongChance { get; } = new(0, 0.5, 0.5);

    /// <summary>
    ///     The chance a stop-time section's later appearance stops again, its first always stopping: no more apt in a wild song
    ///     than a plain one, a stop being a feature in one place or two; a genre's where it names it, every verse of the blues'.
    /// </summary>
    public static ByConvention ReturnChance { get; } = new(0.3, 0.3, 0.3);

    /// <summary>Whether the song stops at all, from a sequence of its own (<see cref="SongChance" />).</summary>
    internal static bool Stops(SongDecisions song) =>
        song.Stream(SongStream.Stops).TestProbability(SongChance.At(song.Facets[Facet.Form], song.Genre.Chance(GenreChoices.Stops, "songs")));

    /// <summary>How a section's energy leans the chance, through its odds: a quiet section the likelier, a verse more than a chorus.</summary>
    public const double EnergyLean = -1;

    /// <summary>
    ///     The chance a section strikes its last bar before a louder one in ensemble hits, by its form facet: never in the
    ///     plainest songs, now and then past them, as stop time; a genre's where it names it, a funk or a horn band's.
    /// </summary>
    public static ByConvention HitsChance { get; } = new(0, 0.06, 0.06);

    /// <summary>How long a stop-time hit is held at most, in beats.</summary>
    public const double Hit = 1;

    /// <summary>
    ///     How much of a stopped bar's rest, after its hit, the lines must sound through to carry it alone; less, and the
    ///     drum keeping time plays on through the bar.
    /// </summary>
    public const double CarriedShare = 0.5;

    /// <summary>
    ///     Where the song plays in stop time: a stop-time section's first appearance's first phrase, and a later one's by its
    ///     chance, from a sequence of its own for each, but a solo's; its start, end and bar.
    /// </summary>
    internal static (double Start, double End, double BarDuration)[] Phrases(SongDecisions song)
    {
        // in a song that stops at all, every section drawn all the same
        var stops = Stops(song);
        var stopping = song.SectionIds.Distinct().ToDictionary(id => id, id =>
        {
            var section = song.SectionOf(id);
            var chance = section.EnergyTilt
                .Chance(Chance.At(section.Facets[Facet.Form], song.Genre.Chance(GenreChoices.StopTime, "sections")), EnergyLean);
            return song.Stream(SongStream.StopTime, id).TestProbability(chance) && stops;
        });
        // but the first section where an intro brings the band in part by part over it, whose parts not in yet would leave
        // its hits to none
        var intro = song.Form.Map.Intro;
        var built = intro.Kind == IntroKind.Entries && !intro.Window.IsBefore ? song.Form.Map.Sections[0] : null;
        // a later appearance by its chance, drawn for every one
        var sections = song.Form.Map.Sections;
        bool[] again = [..sections.Select((x, i) => song.AppearanceStream(SongStream.StopReturns, i)
            .TestProbability(ReturnChance.At(song.SectionOf(x.SectionId).Facets[Facet.Form], song.Genre.Chance(GenreChoices.StopTime, "returns"))))];
        bool Stopped(SectionSpan span, int index) => stopping[span.SectionId] && (again[index] || song.Form.Map.TimesBefore(index) == 0);
        // and but in a solo, whose soloists play over the band as loud as it was measured, as a push leaves a solo be, and an
        // appearance whose texture leaves parts out of its first phrase, building or opening on one part alone, as the
        // section an intro builds is left
        bool Thinned(int index) => song.Sections[index].PhraseSilent is [{ Count: > 0 }, ..];
        return
        [
            ..sections.Where((x, i) => Stopped(x, i) && x != built && !song.Solos.ContainsKey(i) && !Thinned(i))
                .Select(x => (x.Start, Math.Min(x.End, x.Start + x.Meter.PhraseDuration), x.Meter.BarDuration))
        ];
    }

    /// <summary>
    ///     Where the band strikes its hits under a drum solo over them, its whole section, a hit every bar or every second
    ///     (<see cref="SoloPlan.HitEvery" />), the drums soloing between, as stop time is played but the solo its carrier.
    /// </summary>
    internal static (double Start, double End, double BarDuration)[] SoloHits(SongDecisions song) => song.StopTimeSkipped ? [] :
    [
        ..song.Solos.Where(x => x.Value is { IsDrumSolo: true, HitEvery: > 0 }).Select(x => (Span: song.Form.Map.Sections[x.Key], x.Value.HitEvery))
            .Select(x => (x.Span.Start, x.Span.End, x.Span.Meter.BarDuration * x.HitEvery))
    ];

    /// <summary>
    ///     Where the band strikes ensemble hits: a section's last bar before a louder section, its start, end and bar, but
    ///     where a drop silences or filters it, stop time plays it, a solo plays or the song ends; a section's decision, from
    ///     a sequence of its own, drawn for every section.
    /// </summary>
    internal static (double Start, double End, double BarDuration)[] HitBars(SongDecisions song)
    {
        // in a song that stops at all, every section drawn all the same
        var stops = Stops(song);
        var hits = song.SectionIds.Distinct().ToDictionary(id => id, id => song.Stream(SongStream.EnsembleHits, id)
            .TestProbability(HitsChance.At(song.SectionOf(id).Facets[Facet.Form], song.Genre.Chance(GenreChoices.EnsembleHits, "sections"))) && stops);
        var sections = song.Form.Map.Sections;
        return
        [
            ..sections.Select((x, i) => (Span: x, Index: i))
                .Where(x => x.Index + 1 < sections.Length && hits[x.Span.SectionId] && !song.Solos.ContainsKey(x.Index)
                            && song.LiftsInto(x.Index)
                            && !song.Form.Drops.Any(d => Math.Abs(d - sections[x.Index + 1].Start) < 1e-9))
                .Select(x => (Bar: x.Span.Bar(x.Span.BarCount - 1), x.Span.Meter.BarDuration))
                .Where(x => !song.StopTime.Any(s => s.Start < x.Bar.End - 1e-9 && s.End > x.Bar.Start + 1e-9))
                .Select(x => (x.Bar.Start, x.Bar.End, x.BarDuration))
        ];
    }

    /// <summary>
    ///     Stop time: a section's appearances' first phrase, now and then, its band hitting the bar's first beat and resting
    ///     the rest of the bar, a hit held a beat at most, under the lines, the melody's, the counter-melody's and a solo's,
    ///     which go on. A section's decision, from a sequence of its own. A stopped bar is always carried by a part that
    ///     goes on through it: the lines, where they sound through half its rest at least; else the drums keeping time,
    ///     which play on through it; a phrase with a bar neither carries is not stopped, the band playing it as it was. The
    ///     drums play the phrase's last half bar as they have it, a fill there as heavy as a section's leading the band back.
    ///     Ensemble hits alike, a bar of them its band striking the kick's notes there together, each part its note sounding
    ///     at each, the drums whatever falls on one; a bar of more than half its 8ths' hits, which leave no rests, not struck.
    /// </summary>
    internal static SongDraft Apply(SongDecisions song, SongDraft draft)
    {
        StateTrace.RecordSong(TracePoints.Stops, $"{Stops(song)}", Stops(song));
        // but in a calling verse, whose answer stop time would cut to a hit a bar
        var holds = draft.Holds(song);
        // and no hits in a section running a polymeter, whose cycle runs to its end
        var hitBars = song.EnsembleHits.Where(x => !holds.Meets(KeepsOutOf | Hold.Polymeter, x.Start, x.End)).ToImmutableArray();
        var drawn = song.StopTime.Where(x => !holds.Meets(KeepsOutOf, x.Start, x.End)).Concat(hitBars).ToImmutableArray();
        // and a drum solo's hits, the solo carrying them, which are not the song's stop time
        var soloHits = SoloHits(song);
        var phrases = drawn.Concat(soloHits).ToImmutableArray();
        // where a section plays in stop time, as it is played: the starts of its appearances' phrases stopped, a phrase drawn
        // that nothing carries not
        void Record(Func<(double Start, double End, double BarDuration), bool> played)
        {
            foreach (var id in song.SectionIds.Distinct())
            {
                ImmutableArray<double> stops = [..drawn.Where(p => song.Form.Map.Sections.Any(x => x.SectionId == id && Math.Abs(p.Start - x.Start) < 1e-9) && played(p)).Select(p => p.Start)];
                StateTrace.RecordSection(TracePoints.StopTime, SectionGenerator.SectionTrace, id, string.Join(", ", stops), stops);
                ImmutableArray<double> struck = [..hitBars.Where(p => song.Form.Map.Sections.Any(x => x.SectionId == id && x.IsWithin(p.Start)) && played(p)).Select(p => p.Start)];
                StateTrace.RecordSection(TracePoints.EnsembleHits, SectionGenerator.SectionTrace, id, string.Join(", ", struck), struck);
            }
        }

        if (phrases.Length == 0)
        {
            Record(_ => false);
            return draft;
        }

        var notes = SongStages.NotesOf(draft);
        // every bar's hits, its first beat's in stop time, the kick's notes in a bar of ensemble hits
        var kicks = DrumGroups.Kick.Tracks.Where(notes.ContainsKey).ToArray();
        var hitStarts = hitBars.Select(x => Math.Round(x.Start, 6)).ToHashSet();
        // the bar's hits up to its end, a short bar's (ShortBars) before the next section's first beat
        double[] HitsOf(double at, double end) => hitStarts.Contains(Math.Round(at, 6))
            ? [..kicks.SelectMany(k => notes[k]).Where(x => x.Position >= at - 1e-9 && x.Position < end - 1e-9).Select(x => x.Position).Distinct().Order()]
            : [at];
        var lines = notes.Where(x => song.Tracks.Definitions[x.Key].Role.Sings()).SelectMany(x => x.Value).ToArray();
        var times = song.Tracks.DrumsOf(DrumRole.Time).Where(notes.ContainsKey).ToHashSet();
        // every stopped bar's carrier: the lines, or the drums keeping time; a phrase with a bar neither carries played as it
        // was, stop time being the phrase's device
        var carriers = new Dictionary<double, Carrier>();
        foreach (var (start, end, bar) in soloHits)
            for (var at = start; at < end - 1e-9; at += bar)
                carriers[Math.Round(at, 6)] = Carrier.Solo;
        foreach (var (start, end, bar) in drawn)
        {
            var bars = new List<(double At, Carrier Carrier)>();
            for (var at = start; at < end - 1e-9; at += bar)
            {
                var (from, to) = (at + Hit, Math.Min(end, at + bar));
                // a bar of hits that leave no rests between them, or of none, not struck
                var hits = HitsOf(at, to);
                if (hits.Length == 0 || hits.Length > EighthsOf(to - at) / 2)
                {
                    bars.Add((at, Carrier.None));
                    continue;
                }

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

        // where a note falls in a struck bar, a bar a part plays through left out: its bar's start and hits, and whether it
        // falls on one of them
        (bool In, double At, double[] Hits, bool OnAHit) Place(double position, int track)
        {
            foreach (var (start, end, bar) in phrases)
                if (position >= start - 1e-9 && position < end - 1e-9)
                {
                    var at = start + Math.Floor((position - start + 1e-6) / bar) * bar;
                    var carrier = carriers[Math.Round(at, 6)];
                    if (carrier == Carrier.None || carrier == Carrier.Time && times.Contains(track) || carrier == Carrier.Solo && song.Tracks.Definitions[track].Role == TrackRole.Drum)
                        return (false, 0, [], false);
                    var hits = HitsOf(at, Math.Min(end, at + bar));
                    return (true, at, hits, hits.Any(x => Math.Abs(x - position) < 2e-6));
                }

            return (false, 0, [], false);
        }

        // the drums' fills out of it, from a fill's start to its line, which go on as a drummer fills into the next
        // section, a drum never heard otherwise landing there, and the bass walking with them in a phrase's last half bar
        var fills = draft.FillSpans
            // and the last half bar of every phrase, the drums leading the band back in, with the fill its line draws there
            .Concat(phrases.Select(x => (From: x.End - song.Form.Map.BarBefore(x.End) / 2, To: x.End))).ToArray();
        bool InAFill(double position) => fills.Any(x => position >= x.From - 1e-9 && position < x.To - 1e-9);

        // where the chords change, which a hit strikes the chord of
        var chordChanges = draft.Timeline.CommonStateTimelineMap.GetStateTimeline(StateKinds.ChordChange).Select(x => x.Position).ToArray();
        // the song's loudness at a place, the lift's swell into the next section among it, which a hit struck there carries
        double Loudness(double at) => draft.Timeline.CommonStateTimelineMap.GetEffectiveStateMapAt(at).GetStateValue(StateKinds.Velocity);
        var result = notes.ToBuilder();
        foreach (var (track, timeline) in notes)
        {
            var role = song.Tracks.Definitions[track].Role;
            if (role.Sings())
                continue;
            bool Fills(double position) => role is TrackRole.Drum or TrackRole.Bass && InAFill(position);
            // a pitched part's every hit in a bar of ensemble hits, the note it sounds there, or its last before, struck, of the
            // chord there, never one the chord changed since, as loud as the song is there
            var struck = role == TrackRole.Drum ? [] : hitBars.SelectMany(b => HitsOf(b.Start, b.End).Where(at => Place(at, track).In && !Fills(at))
                .Select(at => (At: at, Note: timeline.Where(x => x.Position <= at + 1e-6 && x.Position >= chordChanges.LastOrDefault(c => c <= at + 1e-6) - 1e-9
                                                                && (x.Position >= b.Start - 1e-9 || x.Position + x.Value.Duration > b.Start + 1e-6)).Select(x => (TimelineItem<RealizedNote>?)x).LastOrDefault()))
                .Where(x => x.Note is not null)
                .Select(x => (x.Note!.Value.Value with { Duration = Math.Min(x.Note.Value.Value.Duration, Hit) })
                    .WithVelocity(x.Note.Value.Value.Velocity + Loudness(x.At) - Loudness(x.Note.Value.Position)).ToTimelineItem(x.At))).ToArray();
            // a hit bar's own notes given up for those struck, the drums' but on a hit, and a note held into it ending at its start
            bool IsHitBar(double at) => hitStarts.Contains(Math.Round(at, 6));
            // a note held into a struck phrase, stop time's or a bar of hits, ending at its first hit
            double? HeldInto(TimelineItem<RealizedNote> note) => role == TrackRole.Drum ? null : phrases
                .Where(b => note.Position < b.Start - 1e-9 && note.Position + note.Value.Duration > b.Start + 1e-6 && Place(b.Start, track).In && !Fills(b.Start))
                .Select(b => (double?)b.Start).FirstOrDefault();
            result[track] = EventTimeline.Create(timeline.Duration, timeline
                .Where(note => Place(note.Position, track) is not { In: true } place || place.OnAHit && (role == TrackRole.Drum || !IsHitBar(place.At)) || Fills(note.Position))
                .Select(note => Place(note.Position, track).In && !Fills(note.Position)
                    ? (note.Value with { Duration = Math.Min(note.Value.Duration, Hit) }).ToTimelineItem(note.Position)
                    : HeldInto(note) is { } cut ? (note.Value with { Duration = cut - note.Position }).ToTimelineItem(note.Position) : note)
                .Concat(struck)
                .OrderBy(x => x.Position));
        }

        return draft with { Notes = result.ToImmutable(), Stopped = [..phrases.Where(p => carriers[Math.Round(p.Start, 6)] != Carrier.None)] };
    }

    /// <summary>How many 8ths a bar lasts.</summary>
    private static int EighthsOf(double bar) => (int)Math.Round(bar * 2);

    /// <summary>What goes on through a stopped bar.</summary>
    private enum Carrier
    {
        /// <summary>Nothing, so the phrase is not stopped.</summary>
        None,

        /// <summary>The lines, sounding through half its rest at least.</summary>
        Lines,

        /// <summary>The drums keeping time, playing on through it.</summary>
        Time,

        /// <summary>A drum solo, its drums every one playing on through it.</summary>
        Solo
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
