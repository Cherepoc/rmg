using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>How far each part plays off the beat, a stage of the song after it is put together (<see cref="SongStages" />).</summary>
internal static class Pocket
{
    /// <summary>
    ///     How far each part plays off the beat, in beats at its middle, late above 0, as a band's pocket: the backbeat a
    ///     little behind, the time on it, the bass a touch ahead, a lead laid back; a rule a role, never a jitter.
    /// </summary>
    public static double OffsetOf(TrackRole role, DrumRole? drum) => (role, drum) switch
    {
        (TrackRole.Drum, DrumRole.Backbeat) => 0.02,
        (TrackRole.Drum, DrumRole.Time) => -0.005,
        (TrackRole.Drum, _) => 0,
        (TrackRole.Bass, _) => -0.01,
        (TrackRole.Melody or TrackRole.Solo, _) => 0.015,
        (TrackRole.CounterMelody, _) => 0.01,
        _ => 0
    };

    /// <summary>How much of its offsets a song plays by its feel facet: none in the plainest, which keep to the click, more in the wildest.</summary>
    public static ByConvention Amount { get; } = new(0, 1, 1.5);

    /// <summary>How far a genre's pocket moves the offsets at its most and its least, as a factor: four times, or a quarter.</summary>
    public const double GenreReach = 4;

    /// <summary>
    ///     Every note played off the beat as its part's role has it (<see cref="OffsetOf" />), as much as the song's feel
    ///     facet plays it and a genre's pocket moves it, laid back or tight.
    /// </summary>
    internal static SongDraft Apply(SongDecisions song, SongDraft draft)
    {
        var amount = Amount.At(song.Facets[Facet.Feel]) * Math.Pow(GenreReach, song.Genre.Shift(GenreChoices.Pocket, "laidBack"));
        if (amount == 0)
            return draft;
        var notes = SongStages.NotesOf(draft);
        var result = notes.ToBuilder();
        foreach (var (track, timeline) in notes)
        {
            // a drum by its role in the song's groove, whatever a section makes of it
            var definition = song.Tracks.Definitions[track];
            var offset = amount * OffsetOf(definition.Role, definition.Role == TrackRole.Drum ? DrumRoles.Of(definition.StateMap) : null);
            result[track] = EventTimeline.Create(timeline.Duration, timeline.Select(note => offset == 0
                ? note
                : (note.Value with { State = note.Value.State.With(CompositionStateKinds.TimingOffset, offset) }).ToTimelineItem(note.Position)));
        }

        return draft with { Notes = result.ToImmutable() };
    }
}
