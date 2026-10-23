using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>How far each part plays off the beat, a stage of the song after it is put together (<see cref="SongStages" />).</summary>
internal static class Pocket
{
    /// <summary>
    ///     How far each part plays off the beat, in seconds at its middle, late above 0, as a band's pocket, alike at every
    ///     tempo: the backbeat a little behind, the time on it, the bass a touch ahead, a lead laid back; a rule a role,
    ///     never a jitter.
    /// </summary>
    public static double OffsetOf(TrackRole role, DrumRole? drum) => (role, drum) switch
    {
        (TrackRole.Drum, DrumRole.Backbeat) => 0.01,
        (TrackRole.Drum, DrumRole.Time) => -0.0025,
        (TrackRole.Drum, _) => 0,
        (TrackRole.Bass, _) => -0.005,
        (TrackRole.Melody or TrackRole.Solo, _) => 0.0075,
        (TrackRole.CounterMelody, _) => 0.005,
        _ => 0
    };

    /// <summary>How much of its offsets a song plays by its feel facet: none in the plainest, which keep to the click, more in the wildest.</summary>
    public static ByConvention Amount { get; } = new(0, 1, 1.5);

    /// <summary>
    ///     How far a genre's pocket moves the offsets at its most and its least, as a factor: four times, or a quarter; the
    ///     late ones by it, laid back, the early ones, the bass's and the time's, only where it tightens them, as a laid-back
    ///     band lays its backbeat back and never rushes its bass for it.
    /// </summary>
    public const double GenreReach = 4;

    /// <summary>
    ///     Every note played off the beat as its part's role has it (<see cref="OffsetOf" />), as much as the song's feel
    ///     facet plays it and a genre's pocket moves it, laid back or tight; but stop time and its ensemble hits, the band
    ///     striking together, and a bass on the kick's figure in the kick's place.
    /// </summary>
    internal static SongDraft Apply(SongDecisions song, SongDraft draft)
    {
        var amount = Amount.At(song.Facets[Facet.Feel]);
        if (amount == 0)
            return draft;
        var reach = Math.Pow(GenreReach, song.Genre.Shift(GenreChoices.Pocket, "laidBack"));
        double Played(double offset) => amount * offset * (offset > 0 ? reach : Math.Min(reach, 1));
        var notes = SongStages.NotesOf(draft);
        var tempo = draft.Timeline.CommonStateTimelineMap.GetStateTimeline(StateKinds.Tempo);
        var result = notes.ToBuilder();
        // but stop time's hits and the ensemble hits, the band striking together
        var holds = draft.Holds(song);
        foreach (var (track, timeline) in notes)
        {
            // a drum by its role in the song's groove, whatever a section makes of it
            var definition = song.Tracks.Definitions[track];
            var offset = Played(OffsetOf(definition.Role, definition.Role == TrackRole.Drum ? DrumRoles.Of(definition.StateMap) : null));
            // but a bass playing the kick's figure, note for note with it, in the kick's place
            var onKick = Played(OffsetOf(TrackRole.Drum, DrumRole.Ground));
            bool WithKick(double position) => definition.Role == TrackRole.Bass && song.Form.Map.PlayingAt(position) is { Span: var span }
                                              && draft.Figures.GetValueOrDefault(span.SectionId)?.GetValueOrDefault(FigurePlayer.Bass) is { } figure
                                              && Figures.Of(FigurePlayer.Bass, figure)?.Follows == FigurePlayer.Ground;
            // a drum in a fill or a drum solo, a run round the kit, all on the kick's time, so that its strokes keep together
            bool Runs(double position) => definition.Role == TrackRole.Drum && holds.At(Hold.Fill | Hold.DrumSolo, position, track);
            // in beats at the tempo where the note plays
            double Beats(double position) => holds.At(Hold.Stop, position) ? 0 : (WithKick(position) || Runs(position) ? onKick : offset) * Meter.BeatsPerSecond(tempo.GetEffectiveValueAt(position));
            result[track] = EventTimeline.Create(timeline.Duration, timeline.Select(note => Beats(note.Position) == 0
                ? note
                : (note.Value with { State = note.Value.State.With(CompositionStateKinds.TimingOffset, Beats(note.Position)) }).ToTimelineItem(note.Position)));
        }

        return draft with { Notes = result.ToImmutable() };
    }
}
