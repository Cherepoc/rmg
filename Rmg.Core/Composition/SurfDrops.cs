using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     A surf drop, a stage of the song after its notes are placed (<see cref="SongStages" />): a guitar's last bar before a
///     louder section played as a tremolo-picked, palm-muted slide down the neck onto the section's first note, swelling.
/// </summary>
internal static class SurfDrops
{
    /// <summary>The chance a lift into a louder section takes a surf drop, by the next section's form facet: a genre's alone, Surf rock's.</summary>
    public static ByConvention Chance { get; } = new(0, 0, 0);

    /// <summary>How far above its landing a drop starts at most, in semitones, an octave down the neck; a bar too short for it a semitone a stroke.</summary>
    public const int Fall = 12;

    /// <summary>How much softer a drop starts than the note before it and how much louder it ends, as a velocity sum, a swell into the section.</summary>
    public const double FromSofter = 0.2, ToLouder = 0.1;

    /// <summary>What a drop keeps out of: stop time, a drum solo, a solo, a build into a drop and a cut.</summary>
    internal const Hold KeepsOutOf = Hold.Stop | Hold.DrumSolo | Hold.Solo | Hold.Build | Hold.Cut;

    /// <summary>
    ///     Whether each appearance after the first is dropped into, should a lift into it be louder, from a sequence of its own
    ///     by its appearance, drawn for every one, by its section's form facet, a genre's (<see cref="SongDecisions.SurfDrops" />).
    /// </summary>
    internal static ImmutableArray<bool> Drops(SongDecisions song) =>
    [
        ..song.Form.Map.Sections.Select((x, i) => i > 0 && song.AppearanceStream(SongStream.SurfDrops, i)
            .TestProbability(Chance.At(song.SectionOf(x.SectionId).Facets[Facet.Form], song.Genre.Chance(GenreChoices.SurfDrop, "lifts"))))
    ];

    /// <summary>
    ///     The song with a surf drop into every louder section that draws one, from a sequence of its own by the section's
    ///     appearance, drawn for every one: the riff's guitar, or the melody's where no riff plays a guitar, its notes in the
    ///     bar before taken out and its 16ths there sliding down by semitones from an octave above the next section's first
    ///     note, or as far as a bar too short for it reaches a semitone a 16th, to a semitone above it.
    /// </summary>
    internal static SongDraft Apply(SongDecisions song, SongDraft draft)
    {
        var notes = SongStages.NotesOf(draft);
        var holds = draft.Holds(song);
        var sections = song.Form.Map.Sections;
        // a guitar, which a drop is played on
        bool Guitar(int track) => song.Tracks.Definitions[track] is PitchInstrumentTrack { InstrumentCode: var program } && InstrumentTraits.IsGuitar(program);
        var guitar = new[] { TrackRole.Riff, TrackRole.Melody }.SelectMany(role => song.Playing.Where(x => x.Value == role).Select(x => x.Key))
            .Where(x => notes.TryGetValue(x, out var played) && played.Count > 0 && Guitar(x)).Select(x => (int?)x).FirstOrDefault();
        var result = notes.ToBuilder();
        for (var i = 1; i < sections.Length; i++)
        {
            if (!song.SurfDrops[i] || !song.LiftsInto(i - 1) || guitar is not { } track)
                continue;
            var (from, to) = sections[i - 1].Bar(sections[i - 1].BarCount - 1);
            var timeline = result[track];
            // onto the section's first note, where the guitar plays one on its first beat, and never in a stretch it keeps out of
            if (timeline.FirstOrDefault(x => x.Position >= to - 1e-9) is not { Value: not null } landing || landing.Position > to + 1e-6 || holds.Meets(KeepsOutOf, from, to))
                continue;
            var before = timeline.LastOrDefault(x => x.Position < from - 1e-9) is { Value: not null } last ? last : landing;
            var strokes = (int)Math.Round((to - from) * 4);
            var fall = Math.Min(Fall, strokes);
            var slide = Enumerable.Range(0, strokes).Select(k =>
            {
                var share = strokes == 1 ? 1 : k / (double)(strokes - 1);
                var pitch = landing.Value.Pitches.Max() + fall - (int)Math.Round((fall - 1) * share);
                // the note before's instrument and sends, never its ornament or its slur, which every stroke would play
                var state = before.Value.State.Except([CompositionStateKinds.Ornament, CompositionStateKinds.OrnamentStep, CompositionStateKinds.Legato]).With(CompositionStateKinds.PalmMute, 1);
                return new RealizedNote([pitch], 0, 0.25, state).WithVelocity(before.Value.Velocity - FromSofter + (FromSofter + ToLouder) * share).ToTimelineItem(from + k * 0.25);
            });
            result[track] = EventTimeline.Create(timeline.Duration, timeline.Cleared(from, to).Concat(slide).OrderBy(x => x.Position));
            StateTrace.RecordSong(TracePoints.SurfDrop, $"{from}-{to}", (from, to));
        }

        return draft with { Notes = result.ToImmutable() };
    }
}
