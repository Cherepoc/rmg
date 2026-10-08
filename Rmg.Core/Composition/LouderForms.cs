using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     A part played in its instrument's louder form in the song's louder appearances, as a guitarist kicks in the
///     distortion for the chorus after a clean verse, or in its quieter form in the quieter ones, a driven guitar acoustic
///     between the storms: the electric guitars' louder form a driven one, drawn for the part
///     once, played where an appearance's energy is over the song's mean and the note keeps the part's own instrument, a broken
///     chord's ringing clean as a metal guitar's arpeggio over the riff. A
///     part's decision, by the sound facet: never in the plainest songs, now and then in a middling one; a genre's where it
///     names it (<see cref="GenreChoices.LouderForms" />). A stage of the song after it is put together (<see cref="SongStages" />).
/// </summary>
internal static class LouderForms
{
    /// <summary>The chance a part with a louder form plays it in the louder appearances.</summary>
    public static ByConvention Chance { get; } = new(0, 0.1, 0.25);

    /// <summary>
    ///     The chance a part with a quieter form plays it in the quieter appearances, a driven guitar clean or acoustic
    ///     between the storms, a genre's that the songs never play.
    /// </summary>
    public static ByConvention QuieterChance { get; } = new(0, 0, 0);

    /// <summary>The instruments with a quieter form, by their programs, and their quieter forms: the driven guitars, an acoustic or a clean one.</summary>
    public static ImmutableDictionary<int, ImmutableArray<int>> QuieterForms { get; } = new Dictionary<int, ImmutableArray<int>>
    {
        [29] = [25, 27],
        [30] = [25, 27]
    }.ToImmutableDictionary();

    /// <summary>The instruments with a louder form, by their programs, and their louder forms: the clean, jazz and muted guitars, driven.</summary>
    public static ImmutableDictionary<int, ImmutableArray<int>> Forms { get; } = new Dictionary<int, ImmutableArray<int>>
    {
        [26] = [29, 30],
        [27] = [29, 30],
        [28] = [29, 30]
    }.ToImmutableDictionary();

    internal static SongDraft Apply(SongDecisions song, SongDraft draft)
    {
        var notes = SongStages.NotesOf(draft);
        var mean = song.Energies.Average();
        var louder = song.Form.Map.Sections.Where((_, i) => song.Energies[i] > mean + 1e-9).ToArray();
        var quieter = song.Form.Map.Sections.Where((_, i) => song.Energies[i] < mean - 1e-9).ToArray();
        var result = notes.ToBuilder();
        foreach (var (track, definition) in song.Tracks.Definitions.Where(x => x.Value is PitchInstrumentTrack && notes.ContainsKey(x.Key)))
        {
            var part = (PitchInstrumentTrack)definition;
            // drawn for every part, so that the draws are as many, the quieter form from a sequence of its own
            // the riff's twin as its riff plays, from the riff's sequences
            var keyed = definition.Role == TrackRole.RiffTwin ? song.Track(TrackRole.Riff).Key : track;
            var context = song.Stream(SongStream.LouderForms, keyed);
            var (plays, form) = (context.TestProbability(Chance.At(song.Facets[Facet.Sound], song.Genre.Chance(GenreChoices.LouderForms, "guitars"))), context.GenerateDouble());
            var quieterContext = song.Stream(SongStream.QuieterForms, keyed);
            var (playsQuieter, quieterForm) = (quieterContext.TestProbability(QuieterChance.At(song.Facets[Facet.Sound], song.Genre.Chance(GenreChoices.LouderForms, "quieter"))),
                quieterContext.GenerateDouble());
            foreach (var (spans, program, trace) in new[]
                     {
                         (louder, plays && Forms.TryGetValue(part.InstrumentCode, out var forms) ? forms[(int)(form * forms.Length)] : (int?)null, TracePoints.LouderForm),
                         (quieter, playsQuieter && QuieterForms.TryGetValue(part.InstrumentCode, out var quieterForms) ? quieterForms[(int)(quieterForm * quieterForms.Length)] : null,
                             TracePoints.QuieterForm)
                     })
            {
                if (program is not { } played || spans.Length == 0)
                    continue;
                StateTrace.Record(trace, track, 0, 0, StateMap.Default, 0, $"{played}", played);
                result[track] = EventTimeline.Create(result[track].Duration, result[track].Select(x =>
                    x.Value.State.GetStateValue(CompositionStateKinds.Program) == 0 && x.Value.State.GetStateValue(CompositionStateKinds.Arpeggio) == 0
                    && spans.Any(s => x.Position >= s.Start - 1e-9 && x.Position < s.End - 1e-9)
                        ? (x.Value with { State = x.Value.State.With(CompositionStateKinds.Program, played + 1) }).ToTimelineItem(x.Position)
                        : x));
            }
        }

        return draft with { Notes = result.ToImmutable() };
    }
}
