using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>A chromatic line inside a held chord, a stage of the song after it is put together (<see cref="SongStages" />).</summary>
internal static class InnerLines
{
    /// <summary>The chance a section moves a chromatic line inside its held chords, by its chords facet: never in the plainest songs.</summary>
    public static ByConvention Chance { get; } = new(0, 0.15, 0.3);

    /// <summary>How long a chord is held, in bars, before a line moves inside it.</summary>
    public const int Bars = 2;

    /// <summary>
    ///     A chromatic line inside a held chord, the line cliché: in a section that takes it, over a chord two bars long or
    ///     longer, the chord's root voice falling a semitone each quarter of the chord, on the meter's group nearest it, the root, its major seventh, its
    ///     seventh and its sixth, as the chords Cmaj7, C7 and C6 over C, each quarter a change of chord, the rest of the chord
    ///     held; decided as the song's state before the lines are placed, so that the melody placed over it fits it as it fits
    ///     any chord, the chord's scale altered to hold the fallen note where it has it not (a C7's B♭ in C major). A
    ///     section's decision, from a sequence of its own; a chord a key change moves or leads with, or moved by semitones,
    ///     kept, as is one of a sixth or a seventh of its own, and a line stopped where the scale cannot take its next note.
    /// </summary>
    internal static SongDraft Apply(SongDecisions song, SongDraft draft)
    {
        var lining = song.SectionIds.Distinct().ToDictionary(id => id, id =>
        {
            var lines = song.Stream(SongStream.InnerLines, id).TestProbability(Chance.At(song.SectionOf(id).Facets[Facet.Chords], song.Genre.Chance(GenreChoices.HarmonyDevices, "innerLine")));
            StateTrace.RecordSection(TracePoints.InnerLine, SectionGenerator.SectionTrace, id, $"{lines}", lines);
            return lines;
        });
        var common = draft.Timeline.CommonStateTimelineMap;
        var changes = common.GetStateTimeline(StateKinds.ChordChange).Select(x => x.Position).ToArray();
        var kept = draft.Chains.SelectMany(x => x.Chords.Append(x.Change)).ToHashSet();
        // a chord the chords play as a triad or a power chord, its root among its notes and none from its sixth up but the
        // root again, which the line passes through: the shape every note of the chords in it has, as every part has it then
        var chordsTrack = song.Track(TrackRole.Chords);
        var chordsMap = draft.Timeline.TrackTimelineMap.GetValueOrDefault(chordsTrack.Key);
        var chords = chordsMap?.EventTimeline;
        bool Lines((double Start, double End) chord) =>
            chords?.Where(x => x.Position >= chord.Start - 1e-9 && x.Position < chord.End - 1e-9).Select(x => x.Value).ToArray() is { Length: > 0 } notes
            && notes.All(note => note.GetStateValue(StateKinds.ChordNotePitchOffsets) is { IsEmpty: false } heights
                                 && heights.Any(IsRoot) && heights.All(x => IsRoot(x) || x * Realizer.OctaveNoteCount < Highest));
        static bool IsRoot(double height) => Math.Abs(height - Math.Round(height)) < 1e-9;
        // every chord held long enough in a section that takes the line, but in a solo, and in a hook intro, the chorus's
        // first phrase, as the chorus takes it: where it starts and ends
        var map = song.Form.Map;
        List<(double Start, double End, Meter Meter)> spans = [..map.Sections.Where((x, i) => lining[x.SectionId] && !song.Solos.ContainsKey(i)).Select(x => (x.Start, x.End, x.Meter))];
        // a chord's bars counted from its section's start, a hook intro's from the song's
        if (map.Intro.Kind == IntroKind.Hook && map.Sections.Select((x, i) => (Span: x, Index: i)).First(x => song.Structure.Roles[x.Span.SectionId] == SectionRole.Chorus) is var (chorus, chorusIndex)
            && lining[chorus.SectionId] && !song.Solos.ContainsKey(chorusIndex))
            spans.Insert(0, (0, map.Origin, chorus.Meter));
        var held = spans
            .SelectMany(span => changes.Where(x => x >= span.Start - 1e-9 && x < span.End - 1e-9)
                .Select(x => (Start: x, End: Math.Min(changes.FirstOrDefault(c => c > x + 1e-9, span.End), span.End), span.Meter, Bars: span.Start))
                .Where(x => x.End - x.Start >= Bars * x.Meter.BarDuration - 1e-9))
            .Where(x => !kept.Contains(x.Start) && common.GetEffectiveStateMapAt(x.Start).GetStateValue(StateKinds.ChordShift) == 0 && Lines((x.Start, x.End)))
            .ToArray();
        var (falls, marks, scales) = (new List<TimelineItem<int>>(), new List<TimelineItem<int>>(), new List<TimelineItem<LayerValue<StepAlterations>>>());
        foreach (var (start, end, meter, bars) in held)
        {
            var before = start;
            for (var quarter = 1; quarter < 4; quarter++)
            {
                // on the meter's nearest group, never on the step before it
                var at = bars + meter.NearestGroupStart(start + quarter * (end - start) / 4 - bars);
                if (at <= before + 1e-9 || at >= end - 1e-9)
                    break;
                before = at;
                // the chord as the chords' note there sounds it, in its own scale where it has one, the section's otherwise
                var played = chords?.LastOrDefault(x => x.Position <= at + 1e-9).Value;
                var state = played is null ? common.GetEffectiveStateMapAt(at) : Realizer.StateAt(played, at, chordsMap!, chordsTrack.Value, common);
                var (offsets, root, chord) = Realizer.GetChord(state);
                // the fallen note a step or two under the root, the step nearest it raised or lowered onto it where the scale
                // has it not, the chord's own alterations kept
                var target = chord.Root - quarter;
                var step = new[] { -1, -2 }.MinBy(x => Math.Abs(chord.GetPitch(x) - target));
                var alterations = state.GetStateValue(StateKinds.ScaleAlterations) is { IsSet: true } own ? own.Value : StepAlterations.None;
                var index = (root + step).Mod(offsets.Length);
                var by = target - chord.GetPitch(step);
                var moved = by == 0 ? alterations
                    : Math.Abs(by) == 1 && alterations.By(index) == 0
                        ? new StepAlterations(alterations.Raised | (by > 0 ? 1 << index : 0), alterations.Lowered | (by < 0 ? 1 << index : 0))
                        : (StepAlterations?)null;
                var unaltered = state.GetStateValue(StateKinds.ScaleOffsets) is { IsEmpty: false } scale ? scale : offsets;
                if (moved is not { } line || Realizer.TryAlterScaleSteps(unaltered, line) is null)
                    break;
                // and where the chords' note there sounds it, the fallen note in place of the root
                if (played is not null
                    && Realizer.GetChordNotes(state.With(CompositionStateKinds.InnerLine, quarter)
                        .With(StateKinds.ScaleAlterations, new LayerValue<StepAlterations>(StateDepths.Note, line))) is var (sounding, _, classes)
                    && (!classes.Contains((sounding.Root - quarter).Mod(Realizer.OctaveNoteCount)) || classes.Contains(sounding.Root.Mod(Realizer.OctaveNoteCount))))
                    break;
                falls.Add(quarter.ToTimelineItem(at));
                marks.Add((Change * quarter).ToTimelineItem(at));
                scales.Add(new LayerValue<StepAlterations>(StateDepths.Note, line).ToTimelineItem(at));
            }

            falls.Add(0.ToTimelineItem(end));
            marks.Add(0.ToTimelineItem(end));
            scales.Add(LayerValue<StepAlterations>.Unset.ToTimelineItem(end));
        }

        if (falls.All(x => x.Value == 0))
            return draft;
        static IEnumerable<TimelineItem<T>> Once<T>(IEnumerable<TimelineItem<T>> items) => items.GroupBy(x => x.Position).Select(x => x.Last()).OrderBy(x => x.Position);
        return draft with
        {
            Timeline = draft.Timeline.MergeStateTimelineMap(StateTimelineMap.Create(common.Duration,
            [
                StateTimeline.Create(common.Duration, CompositionStateKinds.InnerLine, Once(falls)).WithLayer("Inner line"),
                StateTimeline.Create(common.Duration, StateKinds.ChordChange, Once(marks)).WithLayer("Inner line"),
                StateTimeline.Create(common.Duration, StateKinds.ScaleAlterations, Once(scales)).WithLayer("Inner line")
            ]))
        };
    }

    /// <summary>How high above its root a chord's notes may reach, in semitones, for a line to fall through it: under its sixth.</summary>
    private const double Highest = 8.5;

    /// <summary>How far a quarter of an inner line moves the chord changes' count, so that each is a change of its own, unlike the chords' around it.</summary>
    private const int Change = 1000;
}
