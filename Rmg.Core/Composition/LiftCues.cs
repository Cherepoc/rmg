using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     The drums announcing a bigger section, as a drummer does: into a section louder by more than a little, the hi-hat
///     opening on the last beat before it and the snare striking its head over the last bar, where it played its
///     cross-stick, each as data, a drum, its sound and how long before the line; where the fill takes the beat, the fill
///     is the cue. Leaned by nothing but the lift itself, as the lift is.
/// </summary>
internal static class LiftCues
{
    /// <summary>How much more energy, as the lift measures it, a section has before the drums announce it.</summary>
    public const double Threshold = 0.15;

    /// <summary>The cues: a drum, the sound it plays, and whether over the whole last bar before the line or its last pulse alone, 4/4's last beat, 6/8's last dotted quarter.</summary>
    public static ImmutableArray<(PercussionInstrumentDefinition Drum, int Sound, bool WholeBar)> Cues { get; } =
    [
        (DrumDefinitions.HiHat, 46, false),
        (DrumDefinitions.AcousticSnare, 38, true),
        (DrumDefinitions.ElectricSnare, 40, true)
    ];

    /// <summary>The song with the drums' cues played into every section louder enough than the one before.</summary>
    public static TrackEventStateTimelineMap<StateMap> Apply(TrackEventStateTimelineMap<StateMap> song, IEnumerable<FillLine> lines, SongMap map)
    {
        foreach (var line in lines.Where(x => x.Ending != x.Next && (x.Next.Energy - x.Ending.Energy) * x.Ending.Rhythm.Coupling > Threshold))
        {
            StateTrace.RecordSection(TracePoints.LiftCue, FillGenerator.DrumsTrace, line.Next.SectionId, $"into {line.Position}", line.Position);
            song = song.MapTrackEvents(Cues.Select(x => (Track: DrumGroups.GetTrackNumber(x.Drum), x.Drum, x.Sound, x.WholeBar)).Where(x => song.TrackTimelineMap.ContainsKey(x.Track)).ToDictionary(
                x => x.Track,
                x => (Func<EventTimeline<StateMap>, EventTimeline<StateMap>>)(notes =>
                {
                    // the bar before the line, a short one's own, and its last pulse
                    var bar = line.Position - map.BarBefore(line.Position);
                    var from = x.WholeBar ? bar : bar + map.MeterAt(bar).Pulses[^1];
                    var index = x.Drum.GetArticulationIndex(x.Sound);
                    return EventTimeline.Create(notes.Duration, notes.Select(n => n.IsWithin(from, line.Position)
                        ? n.Value.With(StateKinds.ArticulationIndex, index).ToTimelineItem(n.Position)
                        : n));
                })
            ));
        }

        return song;
    }
}
