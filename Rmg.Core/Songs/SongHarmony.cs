using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;

namespace Rmg.Core.Songs;

/// <summary>
///     A song's harmony as it plays: at every change of chord, the key and the mode it is in there, such as "A Dorian", and
///     the chord, such as "Dm7", each named by the part that plays it, its root as the part has it whatever note it puts
///     lowest; a span no pitched part plays in, as a drums' intro, no chord (<see cref="NoChord" />), as a lead sheet marks it.
/// </summary>
public static class SongHarmony
{
    /// <summary>
    ///     The parts whose notes name a chord, in the order they are asked: the chords, the pad and the rhythm part, which
    ///     play it, and where none does, the other pitched parts, whose notes' state holds it too, a line's its own shape
    ///     of it, as a bass's power chord for a sus4: the melody, the bass, the riff, as one that opens a song alone, and
    ///     the rest.
    /// </summary>
    private static readonly TrackRole[] ChordParts =
    [
        TrackRole.Chords, TrackRole.Pad, TrackRole.Rhythm, TrackRole.Melody, TrackRole.Bass, TrackRole.Riff, TrackRole.CounterMelody, TrackRole.Solo,
        TrackRole.RiffTwin
    ];

    /// <summary>The chord of a span no pitched part plays in.</summary>
    public const string NoChord = "N.C.";

    /// <param name="Position">Where it starts, in beats.</param>
    /// <param name="Key">The key and the mode there, such as "A Dorian".</param>
    /// <param name="Chord">The chord, such as "Dm7".</param>
    public sealed record Change(double Position, string Key, string Chord);

    /// <summary>
    ///     The song's chords and keys where they change, in order, a change that names what the one before did left out;
    ///     none for a song of no sections.
    /// </summary>
    /// <param name="notes">Its notes realized, by their tracks, as the render plays them.</param>
    public static ImmutableArray<Change> Of(Song song, IReadOnlyDictionary<int, EventTimeline<RealizedNote>> notes)
    {
        if (song.Map is not { Sections.IsEmpty: false } map)
            return [];
        // every part's notes in order, and where each starts, to find a chord's by halving
        var parts = ChordParts.SelectMany(role => song.TrackDefinitions.Where(x => x.Value.Role == role && notes.ContainsKey(x.Key)).Select(x => notes[x.Key].ToArray()))
            .Select(x => (Notes: x, Starts: x.Select(n => n.Position).ToArray())).ToArray();
        // every change of chord, and the song's start, where an intro plays before its first section's first change
        var common = song.TrackEventStateTimelineMap.CommonStateTimelineMap;
        var changes = common.GetStateTimeline(StateKinds.ChordChange).Select(x => x.Position).Prepend(0).Distinct().Order().ToArray();
        // the sections' span, which an intro before it and an ending after it play in the key and the scale of
        var sections = (Start: map.Origin, End: map.Sections[^1].End - 1e-6);
        var result = ImmutableArray.CreateBuilder<Change>();
        for (var i = 0; i < changes.Length; i++)
        {
            var (start, end) = (changes[i], i + 1 < changes.Length ? changes[i + 1] : double.MaxValue);
            // the first note of a part that plays the chord, of the first part that does, a broken chord's one note too, its
            // state the chord's: one struck in the chord's span, or one held into it
            var note = parts.Select(x => NoteAt(x.Notes, x.Starts, start, end)).FirstOrDefault(x => x is not null);
            var name = NoChord;
            if (note is not null)
            {
                var (chord, _, classes) = Realizer.GetChordNotes(note.State);
                var root = chord.Root.Mod(12);
                name = ChordNames.KeyNames[root] + ChordNames.Of(classes.Select(x => (x - root).Mod(12)).ToHashSet()).Suffix;
            }

            // the key and the mode the song is in there, as every part shares them, a key change's and a section's own scale
            // among them, not a line's own scale or a chord's
            var shared = common.GetEffectiveStateMapAt(Math.Clamp(start, sections.Start, sections.End));
            var offsets = shared.GetStateValue(StateKinds.ScaleOffsets);
            var mode = Scales.All.FirstOrDefault(x => x.Offsets.SequenceEqual(offsets))?.Name ?? throw new InvalidOperationException($"No scale of the offsets {string.Join(", ", offsets)}.");
            var key = $"{ChordNames.KeyNames[shared.GetStateValue(StateKinds.KeyOffset).Mod(12)]} {mode}";
            if (result.Count == 0 || result[^1].Key != key || result[^1].Chord != name)
                result.Add(new Change(start, key, name));
        }

        return result.ToImmutable();
    }

    /// <summary>A part's first note struck in a span, or the one before it where it is held into the span; none for none.</summary>
    private static RealizedNote? NoteAt(TimelineItem<RealizedNote>[] notes, double[] starts, double start, double end)
    {
        var found = Array.BinarySearch(starts, start - 1e-9);
        var index = found >= 0 ? found : ~found;
        if (index < notes.Length && notes[index].Position < end - 1e-9)
            return notes[index].Value;
        return index > 0 && notes[index - 1].Position + notes[index - 1].Value.Duration > start + 1e-9 ? notes[index - 1].Value : null;
    }
}
