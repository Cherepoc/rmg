using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     How a part plays its notes where it is (<see cref="CompositionStateKinds.Articulation" />): its articulations, and the
///     finest step it tremolo-picks its notes at, in beats, the tempo's hand coarsening it, 0 where it picks none.
/// </summary>
public readonly record struct ArticulationChoice(PartArticulation Articulates, double TremoloStep);

/// <summary>The articulations a part plays its notes with where it is (<see cref="ArticulationChoice" />).</summary>
[Flags]
public enum PartArticulation
{
    None = 0,

    /// <summary>A piano's held chords rolled upward.</summary>
    Rolls = 1,

    /// <summary>A 16th chain's notes between the 8ths muted, dead notes.</summary>
    DeadNotes = 2,

    /// <summary>The chords struck short, stabs.</summary>
    Stabs = 4,

    /// <summary>A line's steps slurred.</summary>
    Slurs = 8,

    /// <summary>Broken chords let ring.</summary>
    LetsRing = 16,

    /// <summary>Whole chords cycled at a sound chip's frame rate.</summary>
    Frames = 32,

    /// <summary>A line's notes held to its next, no rest between them in a phrase (<see cref="Articulations.Held" />).</summary>
    Holds = 64,

    /// <summary>A line's last bar before a louder section picked faster and faster into it (<see cref="Articulations.Tremolo" />).</summary>
    Builds = 128
}

internal static partial class Articulations
{
    /// <summary>
    ///     Every part's articulations chosen as its track's state along the song, which <see cref="Apply" /> plays once the
    ///     notes are realized (<see cref="CompositionStateKinds.Articulation" />), and the song's stroke across a guitar's
    ///     strings (<see cref="CompositionStateKinds.Stroke" />): a piano's held chords rolled, a bass's, a
    ///     riff's or a rhythm part's 16th chains muted between the 8ths, the chords struck short, a line's steps slurred, but
    ///     the bass's, broken chords let ring, and in a calm section always (SectionForm), and whole chords cycled at the
    ///     frame rate, each a genre's or the songs' own by the sound facet, every part from a sequence of its own, and a note
    ///     doubling another's line as its source plays it, a twin's as its riff's; and a riff's, a rhythm part's or a lead's notes tremolo-picked in an appearance
    ///     that picks them, a genre's, at a step of its speed, both leaning by the appearance's energy, each appearance and
    ///     part from a sequence of its own, never in a calm section.
    /// </summary>
    internal static SongDraft Choose(SongDecisions song, SongDraft draft)
    {
        var duration = draft.Timeline.Duration;
        var stroke = StrokeFrom + (StrokeTo - StrokeFrom) * song.Stream(SongStream.Strum).GenerateDouble();
        Play[] plays = [..song.Form.Map.Plays];
        bool Calm(SectionSpan span) => song.SectionOf(span.SectionId).Form.Kind == SectionFormKind.Calm;
        bool Draws(SongStream stream, long key, ByConvention chance, string option) =>
            song.Stream(stream, key).TestProbability(chance.At(song.Facets[Facet.Sound], song.Genre.Chance(GenreChoices.Articulation, option)));
        // the steps the song's tremolo picks at, a measured picker's 8th and 16th or a fast one's 16th, sextuplet and 32nd
        var fast = song.Stream(SongStream.Tremolo).Pick(ByConvention.Weigh(Speeds, song.Facets[Facet.Sound], song.Genre.Pool<TremoloSpeed>(GenreChoices.TremoloSpeed))) == TremoloSpeed.Fast;
        ImmutableArray<double> steps = fast ? [..TremoloSteps.Skip(MeasuredSteps - 1)] : [..TremoloSteps.Take(MeasuredSteps)];
        // and whether a picked line is carried through its rests, from a sequence of its own
        var through = song.Stream(SongStream.TremoloThrough).TestProbability(TremoloThroughChance.At(song.Facets[Facet.Sound], song.Genre.Chance(GenreChoices.Articulation, "tremoloThrough")));
        // and whether a part builds into a louder appearance, its last bar before it picked faster and faster, from a sequence of
        // its own and the part's, by the section's sound facet, a genre's; never out of a calm section or into the ending
        bool Builds(int index, int track)
        {
            var span = song.Form.Map.Sections[index];
            var builds = song.AppearanceStream(SongStream.TremoloBuild, index, track)
                .TestProbability(TremoloBuildChance.At(song.SectionOf(span.SectionId).Facets[Facet.Sound], song.Genre.Chance(GenreChoices.Articulation, "tremoloBuild")));
            return builds && index + 1 < song.Form.Map.Sections.Length && song.LiftsInto(index) && !Calm(span);
        }
        // an appearance's step, none where it picks none: the likelier and the finer the more energy it has, as far as its
        // section's rhythm follows it, from a sequence of its own and the part's, never in a calm section, whose guitars ring
        // their broken chords
        double Step(int index, int track)
        {
            var section = song.SectionOf(song.Form.Map.Sections[index].SectionId);
            var context = song.AppearanceStream(SongStream.Tremolo, index, track);
            var tilt = SectionEnergy.Tilt(song.Energies[index], section.Rhythm.Coupling);
            var picks = context.TestProbability(tilt.Chance(TremoloChance.At(section.Facets[Facet.Sound], song.Genre.Chance(GenreChoices.Articulation, "tremolo")), TremoloLean));
            var step = context.Pick(tilt.Weigh(steps.Select(x => new Weighted<double>(1, x)), x => TremoloLean * (steps.Length == 1 ? 0 : 2.0 * steps.IndexOf(x) / (steps.Length - 1) - 1)));
            return picks && section.Form.Kind != SectionFormKind.Calm ? step : 0;
        }

        var tracks = draft.Timeline.TrackTimelineMap.ToBuilder();
        // every part's choice in each stretch it plays as a section, which a doubled note of its line plays too
        var choices = new Dictionary<TrackRole, Func<double, ArticulationChoice>>();
        foreach (var (track, definition) in song.Tracks.Definitions)
        {
            if (definition is not PitchInstrumentTrack { Role: var role })
                continue;
            var articulates = PartArticulation.None;
            if (Draws(SongStream.Roll, track, RollChance, "roll"))
                articulates |= PartArticulation.Rolls;
            // its 16th chains' notes between the 8ths muted; a solo's its soloist's own (Soloist.Ghosts)
            if (role is TrackRole.Bass or TrackRole.Riff or TrackRole.Rhythm && Draws(SongStream.DeadNotes, track, DeadNoteChance, "deadNotes"))
                articulates |= PartArticulation.DeadNotes;
            // the chords struck short, the chords' and the rhythm part's, a skank's chop
            if (role.PlaysChords() && Draws(SongStream.Stabs, track, StabChance, "stabs"))
                articulates |= PartArticulation.Stabs;
            if (role.PlaysLine() && role != TrackRole.Bass && Draws(SongStream.Legato, track, LegatoChance, "legato"))
                articulates |= PartArticulation.Slurs;
            var rings = role.PlaysChords() && Draws(SongStream.LetRing, track, LetRingChance, "letRing");
            // the chords cycled, its own sequence drawn for every part that holds chords
            if (role.PlaysChords() && Draws(SongStream.FrameArpeggio, track, FrameChance, "frames"))
                articulates |= PartArticulation.Frames;
            var picking = role is TrackRole.Riff or TrackRole.Rhythm or TrackRole.Melody
                ? Enumerable.Range(0, song.Form.Map.Sections.Length).ToDictionary(index => index, index => Step(index, track))
                : [];
            var building = role is TrackRole.Riff or TrackRole.Rhythm or TrackRole.Melody
                ? Enumerable.Range(0, song.Form.Map.Sections.Length).ToDictionary(index => index, index => Builds(index, track))
                : [];
            if (tracks.ContainsKey(track))
                foreach (var (index, step) in picking.Where(x => x.Value > 0))
                    StateTrace.RecordSection(TracePoints.Tremolo, track, song.Form.Map.Sections[index].SectionId, $"{step:F3}", (index, step));
            // the chords' broken chords let ring in a calm section whatever the part draws, as the part's own, which a note's
            // own choice is over
            var ringing = articulates | PartArticulation.LetsRing;
            // and a line it picks held through its rests, where the song carries its tremolo through them; but none in an
            // ending's final chord, which the band strikes once
            ArticulationChoice In(Play? play)
            {
                var step = play is { Index: var index, IsEnding: var ending } && !(ending && FormLayers.HasFinalChord(song.Form.Map.Ending.Kind)) ? picking.GetValueOrDefault(index) : 0;
                var own = rings || play is { Span: var played } && role.PlaysChords() && Calm(played) ? ringing : articulates;
                return new ArticulationChoice(through && step > 0 ? own | PartArticulation.Holds : own, step);
            }
            // a section's own stretch's last bar where the part builds out of it, held through and picked from an 8th to the
            // finest of the song's steps
            (double From, ArticulationChoice Choice)? Built(Play play) =>
                !play.IsEnding && building.GetValueOrDefault(play.Index) && Math.Abs(play.Span.Start - song.Form.Map.Sections[play.Index].Start) < 1e-9
                    ? (play.Span.LastBar, new ArticulationChoice(In(play).Articulates | PartArticulation.Holds | PartArticulation.Builds, steps[^1]))
                    : null;
            ArticulationChoice At(double position) => song.Form.Map.PlayingAt(position) is { } play
                ? Built(play) is var (from, built) && position >= from - 1e-9 ? built : In(play)
                : In(null);
            if (tracks.ContainsKey(track))
                foreach (var (index, _) in building.Where(x => x.Value))
                    StateTrace.RecordSection(TracePoints.TremoloBuild, track, song.Form.Map.Sections[index].SectionId, $"{index}", index);
            choices.TryAdd(role, At);
            var state = StateTimelineMap.Create(duration, [Along(duration, plays, In, Built)]);
            // on the part's track, which every stage before the notes has made where the part plays, a soloist's from the
            // melody's state among them (Solos), so that its notes take the part's own; a part with no notes plays none
            if (!state.IsDefault && tracks.TryGetValue(track, out var existing))
                tracks[track] = existing.MergeStateTimelineMap(state);
        }

        // and a note doubling another part's line as its source plays it there, a note's own choice over its part's: a twin as
        // its riff, a harmony as the melody, the bass with the riff, each as its instrument can (Apply)
        foreach (var (track, map) in tracks.ToArray())
            if (map.EventTimeline.Any(x => x.Value.GetStateValue(CompositionStateKinds.Doubled) > 0))
                tracks[track] = map.WithEvents(map.EventTimeline.MapValues((position, note) =>
                    note.GetStateValue(CompositionStateKinds.Doubled) is var source and > 0 && choices.TryGetValue((TrackRole)(source - 1), out var of)
                        ? note.With(CompositionStateKinds.Articulation, new LayerValue<ArticulationChoice>(StateDepths.Note, of(position)))
                        : note));

        return draft with
        {
            Timeline = TrackEventStateTimelineMap.Create(duration, tracks.ToImmutable(), draft.Timeline.CommonStateTimelineMap)
                .MergeStateMap(StateMap.FromStates([CompositionStateKinds.Stroke.CreateState(stroke)]))
        };
    }

    /// <summary>
    ///     A part's choice along the song, its track's: in each stretch it plays as a section (<see cref="SongMap.Plays" />)
    ///     the section's, and elsewhere, in an intro of its own, the part's own; a change only where it changes.
    /// </summary>
    /// <param name="choice">The part's choice in a stretch it plays as a section, by the stretch, or none, elsewhere.</param>
    /// <param name="last">Where a stretch's last stretch plays a choice of its own, and the choice, a build's; none where it plays its own.</param>
    private static StateTimeline<LayerValue<ArticulationChoice>> Along(double duration, IReadOnlyList<Play> plays, Func<Play?, ArticulationChoice> choice,
        Func<Play, (double From, ArticulationChoice Choice)?> last)
    {
        // the part's own from the start, and again after a stretch no other follows, past the ending
        var at = new SortedDictionary<double, ArticulationChoice> { [0] = choice(null) };
        foreach (var play in plays.Where(x => plays.All(y => Math.Abs(y.Span.Start - x.Span.End) > 1e-9)))
            at[play.Span.End] = choice(null);
        foreach (var play in plays)
            at[play.Span.Start] = choice(play);
        foreach (var play in plays)
            if (last(play) is var (from, built))
                at[from] = built;
        var items = new List<TimelineItem<LayerValue<ArticulationChoice>>>();
        foreach (var (position, state) in at.Where(x => x.Key < duration - 1e-9))
            if (items.Count == 0 || items[^1].Value.Value != state)
                items.Add(new LayerValue<ArticulationChoice>(StateDepths.Track, state).ToTimelineItem(position));
        return StateTimeline.Create(duration, CompositionStateKinds.Articulation, items);
    }
}
