using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     The form of a song around its sections: how it starts and how it ends. It sees the sections before they are put
///     one after another, so it can put bars before and after them, and it tells the fills where the lines are that
///     they mark.
/// </summary>
internal sealed class SongFormGenerator
{
    private readonly IGenerationContext _context;

    public SongFormGenerator(IGenerationContext context)
    {
        _context = context;
    }

    /// <param name="sectionIds">The sections in the song's order.</param>
    /// <param name="sections">Every section in the song's order, as generated.</param>
    public SongForm Generate(IReadOnlyList<int> sectionIds, IReadOnlyList<GeneratedSection> sections)
    {
        ImmutableArray<FillSection> fillSections =
        [
            ..sectionIds.Zip(sections, (id, section) => new FillSection(id, section.Timeline.Duration, section.Rhythm, section.DrumTuplet))
        ];
        return new SongForm(
            [..sections.Select(x => x.Timeline)],
            FillGenerator.GetSectionLines(fillSections),
            0,
            new TimelineEdits(_context)
        );
    }
}

/// <summary>A song's form: its blocks to put one after another, the lines the fills mark, and the changes it makes.</summary>
/// <param name="Blocks">The intro, if it has bars of its own, the sections, and the ending, if it has bars of its own.</param>
/// <param name="Origin">Where the first section starts, after the intro's bars.</param>
/// <param name="Edits">What the form changes once the blocks are put one after another, such as the bars an intro leaves out.</param>
internal sealed record SongForm(
    ImmutableArray<TrackEventStateTimelineMap<StateMap>> Blocks,
    ImmutableArray<FillLine> Lines,
    double Origin,
    TimelineEdits Edits
);
