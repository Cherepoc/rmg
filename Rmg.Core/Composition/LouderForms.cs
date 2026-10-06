using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     A part played in its instrument's louder form in the song's louder appearances, as a guitarist kicks in the
///     distortion for the chorus after a clean verse: the electric guitars' louder form a driven one, drawn for the part
///     once, played where an appearance's energy is over the song's mean and the note keeps the part's own instrument. A
///     part's decision, by the sound facet: never in the plainest songs, now and then in a middling one; a genre's where it
///     names it (<see cref="GenreChoices.LouderForms" />). A stage of the song after it is put together (<see cref="SongStages" />).
/// </summary>
internal static class LouderForms
{
    /// <summary>The chance a part with a louder form plays it in the louder appearances.</summary>
    public static ByConvention Chance { get; } = new(0, 0.1, 0.25);

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
        var result = notes.ToBuilder();
        foreach (var (track, definition) in song.Tracks.Definitions.Where(x => x.Value is PitchInstrumentTrack && notes.ContainsKey(x.Key)))
        {
            var part = (PitchInstrumentTrack)definition;
            var context = song.Stream(SongStream.LouderForms, track);
            // drawn for every part, so that the draws are as many
            var plays = context.TestProbability(Chance.At(song.Facets[Facet.Sound], song.Genre.Chance(GenreChoices.LouderForms, "guitars")));
            var form = context.GenerateDouble();
            if (!plays || !Forms.TryGetValue(part.InstrumentCode, out var forms) || louder.Length == 0)
                continue;
            var program = forms[(int)(form * forms.Length)];
            StateTrace.Record(TracePoints.LouderForm, track, 0, 0, StateMap.Default, 0, $"{program}", program);
            result[track] = EventTimeline.Create(notes[track].Duration, notes[track].Select(x =>
                x.Value.State.GetStateValue(CompositionStateKinds.Program) == 0 && louder.Any(s => x.Position >= s.Start - 1e-9 && x.Position < s.End - 1e-9)
                    ? (x.Value with { State = x.Value.State.With(CompositionStateKinds.Program, program + 1) }).ToTimelineItem(x.Position)
                    : x));
        }

        return draft with { Notes = result.ToImmutable() };
    }
}
