using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>The snare's flams and drags, a stage of the song after its notes are made (<see cref="SongStages" />).</summary>
internal static class DrumOrnaments
{
    /// <summary>What a flam or a drag keeps out of: stop time, a drum solo, whose strokes are the drummer's own, a drop's build roll and the snare stopped by the form.</summary>
    internal const Hold KeepsOutOf = Hold.Stop | Hold.DrumSolo | Hold.Build | Hold.Cut;

    /// <summary>The chance a song's snare flams its beats in a fill, by its fills facet: never at the plain end, every song at the wild.</summary>
    public static ByConvention FlamChance { get; } = new(0, 0.25, 1);

    /// <summary>The chance a song's snare drags into its backbeat, by its groove facet: never at the plain end, every song at the wild.</summary>
    public static ByConvention DragChance { get; } = new(0, 0.1, 1);

    /// <summary>The share of a fill's snare notes on an 8th a flamming song flams, and of its backbeats a dragging one drags.</summary>
    public const double FlamShare = 0.5, DragShare = 0.15;

    /// <summary>How many grace strokes a flam and a drag play before their note.</summary>
    public const int FlamStrokes = 1, DragStrokes = 2;

    /// <summary>How many grace strokes a drum plays a second: a flam's grace about 35 ms before its note.</summary>
    public const double Rate = 28;

    /// <summary>Whether the song flams and drags, each by a chance of its own from one sequence, a genre's (<see cref="SongDecisions.Rudiments" />).</summary>
    internal static (bool Flams, bool Drags) Rudiments(SongDecisions song)
    {
        var context = song.Stream(SongStream.DrumOrnaments);
        var flams = context.TestProbability(FlamChance.At(song.Facets[Facet.Fills], song.Genre.Chance(GenreChoices.Rudiments, "flams")));
        return (flams, context.TestProbability(DragChance.At(song.Facets[Facet.Groove], song.Genre.Chance(GenreChoices.Rudiments, "drags"))));
    }

    /// <summary>
    ///     The snare's ornaments, a rudiment's: a flam, a soft stroke just before a note, in a fill, a rock or a
    ///     marching drummer's accent, on its 8ths; and a drag, two, before its backbeat elsewhere, a jazz or a funk drummer's. Each the
    ///     song's by a chance of its own, a flam's by its fills facet and a drag's by its groove's, a genre's where it names
    ///     one; a share of the notes each may go on, drawn note by note, on the snare's head alone in a section, never in stop time. The
    ///     note marked with its strokes (<see cref="CompositionStateKinds.Ornament" />), which the rendering plays before it,
    ///     on the same sound.
    /// </summary>
    internal static SongDraft Apply(SongDecisions song, SongDraft draft)
    {
        var (flams, drags) = song.Rudiments;
        StateTrace.RecordSong(TracePoints.DrumOrnaments, $"flams {flams}, drags {drags}", (flams, drags));
        if (!flams && !drags)
            return draft;

        var notes = SongStages.NotesOf(draft);
        var snares = notes.Keys.Where(DrumGroups.SnareHeads.Contains).ToArray();
        var tempo = draft.Timeline.CommonStateTimelineMap.GetStateTimeline(StateKinds.Tempo);
        var (holds, map) = (draft.Holds(song), song.Form.Map);
        var result = notes.ToBuilder();
        foreach (var track in snares)
        {
            var head = DrumGroups.GetDrum(track).Sounds[0].Code;
            result[track] = EventTimeline.Create(notes[track].Duration, notes[track].Select(note =>
            {
                var position = note.Position;
                // the snare's head where the song plays as a section, an intro of entries' window and the ending among them, out
                // of stop time and of a drum solo, whose strokes are the drummer's own
                if (note.Value.Pitches is not [var code] || code != head || map.PlayingAt(position) is not { Span: var section } || holds.At(KeepsOutOf, position, track))
                    return note;
                // in a fill on an 8th, its accents, and elsewhere on its bar's backbeat
                var inFill = holds.At(Hold.Fill, position);
                var beat = map.BeatInBar(position);
                if (inFill ? Math.Abs(beat * 2 - Math.Round(beat * 2)) > 1e-6 : !map.MeterAt(position).BackbeatsIn(song.SectionOf(section.SectionId).TimeFeel).Any(x => Math.Abs(x - beat) < 1e-6))
                    return note;
                var (plays, share, strokes) = inFill ? (flams, FlamShare, FlamStrokes) : (drags, DragShare, DragStrokes);
                // drawn by the note's place, so that a song made again plays it alike
                if (!plays || song.Stream(SongStream.DrumOrnaments, track, map.PlaceKey(position)).GenerateDouble() >= share)
                    return note;
                var step = Meter.BeatsPerSecond(tempo.GetEffectiveValueAt(position)) / Rate;
                // its strokes in its section, none before a section's first note, and clear of the drum's own notes, which a
                // tuplet or a busy groove may strike just before it
                if (position - strokes * step < section.Start - 1e-9 || notes[track].Any(x => x.Position > position - strokes * step - 1e-9 && x.Position < position - 1e-9))
                    return note;
                return (note.Value with
                {
                    State = note.Value.State.With(CompositionStateKinds.Ornament, [..Enumerable.Repeat(0, strokes)]).With(CompositionStateKinds.OrnamentStep, step)
                }).ToTimelineItem(position);
            }));
        }

        return draft with { Notes = result.ToImmutable() };
    }
}
