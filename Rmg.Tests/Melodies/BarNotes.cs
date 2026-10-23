using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Tests.Melodies;

/// <summary>A part's notes bar by bar, as a section's bars are on the map, and a later bar against its first's.</summary>
internal static class BarNotes
{
    /// <summary>A section's bar's notes, each its place in the bar and its pitch, the short last bar at its own length; none past the section's bars.</summary>
    public static (double Offset, int Pitch)[] Of(IReadOnlyCollection<TimelineItem<RealizedNote>> notes, SectionSpan span, int k)
    {
        if (span.Bars.Skip(k).Take(1).ToArray() is not [var bar])
            return [];
        return [..notes.Where(x => x.Position >= bar.Start - 1e-9 && x.Position < bar.End - 1e-9).Select(x => (Math.Round(x.Position - bar.Start, 3), x.Value.Pitches[0]))];
    }

    /// <summary>A later bar against its first's: whether it starts its notes where the first does, and how many of its notes play the first's at the same place, of how many.</summary>
    public static (bool Rhythm, int Kept, int Notes) Compare((double Offset, int Pitch)[] first, (double Offset, int Pitch)[] later) =>
        (first.Select(n => n.Offset).SequenceEqual(later.Select(n => n.Offset)), later.Count(first.Contains), later.Length);
}
