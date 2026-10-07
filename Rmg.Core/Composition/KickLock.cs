using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     The kick with the riff: in a section that locks them, the kick striking where the riff's notes start, the bass's
///     where the song has no riff, in the bars it plays, as a groove or thrash band's kick follows the guitar's chugs, a
///     riff chugging in 16ths a double kick. A stage of the song before its fills, which play over it
///     (<see cref="SongStages" />); a section's decision from a sequence of its own, a genre's, whose songs alone lock,
///     the louder the section the likelier.
/// </summary>
internal static class KickLock
{
    /// <summary>The chance a section locks the kick to the riff: never in the songs, a genre's (<see cref="GenreChoices.KickLock" />).</summary>
    public static ByConvention Chance { get; } = new(0, 0, 0);

    /// <summary>How a section's energy leans the chance, through its odds: a loud section the likelier.</summary>
    public const double EnergyLean = 1;

    internal static SongDraft Apply(SongDecisions song, SongDraft draft)
    {
        var locks = song.SectionIds.Distinct().ToDictionary(id => id, id =>
        {
            var section = song.SectionOf(id);
            var chance = SectionEnergy.Tilt(section.Energy, section.Rhythm.Coupling)
                .Chance(Chance.At(section.Facets[Facet.Feel], song.Genre.Chance(GenreChoices.KickLock, "sections")), EnergyLean);
            var locked = song.Stream(SongStream.KickLock, id).TestProbability(chance);
            StateTrace.Record(TracePoints.KickLock, SectionGenerator.SectionTrace, id, 0, StateMap.Default, 0, $"{locked}", locked);
            return locked;
        });
        // a kick the song pins too, its figure the genre's in every other section
        if (!locks.Values.Any(x => x))
            return draft;

        var timelines = draft.Timeline.TrackTimelineMap;
        int? TrackOf(TrackRole role) => song.Playing.Where(x => x.Value == role).Select(x => (int?)x.Key).FirstOrDefault(x => timelines.ContainsKey(x!.Value));
        if ((TrackOf(TrackRole.Riff) ?? TrackOf(TrackRole.Bass)) is not { } leader)
            return draft;
        var leading = timelines[leader].EventTimeline;
        var kicks = DrumGroups.Kick.Drums.Select(DrumGroups.GetTrackNumber).Where(timelines.ContainsKey).ToArray();
        var spans = song.Form.Map.Sections.Where(x => locks[x.SectionId]).ToArray();

        return draft with
        {
            Timeline = draft.Timeline.MapTrackEvents(kicks.ToDictionary(x => x, _ => (Func<EventTimeline<StateMap>, EventTimeline<StateMap>>)(notes =>
            {
                var played = notes.ToList();
                foreach (var span in spans)
                for (var bar = span.Start; bar < span.End - 1e-9; bar += span.Meter.BarDuration)
                {
                    var end = bar + span.Meter.BarDuration;
                    bool In(double position) => position >= bar - 1e-9 && position < end - 1e-9;
                    var own = played.Where(x => In(x.Position)).ToArray();
                    var onsets = leading.Where(x => In(x.Position)).Select(x => x.Position).Distinct().ToArray();
                    // in a bar both play, the kick's own strokes as they were, each hit taking the one at or before it
                    if (own.Length == 0 || onsets.Length == 0)
                        continue;
                    played.RemoveAll(x => In(x.Position));
                    played.AddRange(onsets.Select(at => own.Where(x => x.Position <= at + 1e-9).DefaultIfEmpty(own[0]).Last().Value.ToTimelineItem(at)));
                }

                return EventTimeline.Create(notes.Duration, played.OrderBy(x => x.Position));
            })))
        };
    }
}
