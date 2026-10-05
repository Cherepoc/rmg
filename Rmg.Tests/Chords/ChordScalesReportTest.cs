using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Chords;

/// <summary>What the chords' own scales do: how many chords play one, and whether the melody clashes with the chords more.</summary>
public sealed class ChordScalesReportTest
{
    /// <summary>
    ///     The melody's notes on a beat a semitone from a note the chords hold then, of all its notes on a beat, as the
    ///     songs play them: a clash a chord's own scale must not make commoner.
    /// </summary>
    internal static (int Clashes, int Notes) Clashes(CorpusSong song)
    {
        var tracks = song.Rendered.Tracks.Where(x => !x.IsPercussionInstrument).ToArray();
        var chords = tracks.Where(x => x.Role == TrackRole.Chords).SelectMany(x => x.NoteTimeline).ToArray();
        int clashes = 0, notes = 0;
        foreach (var note in tracks.Where(x => x.Role == TrackRole.Melody).SelectMany(x => x.NoteTimeline))
        {
            if (Math.Abs(note.Position - Math.Round(note.Position)) > 1e-6)
                continue;
            var held = chords.Where(x => x.Position <= note.Position + 1e-6 && x.Position + x.Value.Duration > note.Position + 1e-6).ToArray();
            if (held.Length == 0)
                continue;
            notes++;
            if (held.Any(x => ((x.Value.Offset - note.Value.Offset) % 12 + 12) % 12 is 1 or 11))
                clashes++;
        }

        return (clashes, notes);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var measured = TestCorpus.Measure(256, Clashes);
        var clashes = measured.Sum(x => x.Clashes);
        var notes = measured.Sum(x => x.Notes);
        Console.WriteLine($"melody notes on a beat a semitone from the chords: {clashes} of {notes}, {100.0 * clashes / notes:F1}%");
        // the chords by the scale they play in, by fifths of their section's progression facet
        var chords = TestCorpus.Measure(256, song => song.Trace.Where(x => x.Point == TracePoints.ChordScales)
            .Select(x => (Fifth: Math.Min(4, (int)(((Unconventionality)song.Trace.First(y => y.Point == TracePoints.SectionUnconventionality && y.Section == x.Section).Value!)[Facet.Progression] * 5)),
                Scales: (System.Collections.Immutable.ImmutableArray<ChordScale>)x.Value!))
            .ToArray());
        foreach (var fifth in Enumerable.Range(0, 5))
        {
            var kinds = chords.SelectMany(x => x).Where(x => x.Fifth == fifth).SelectMany(x => x.Scales).ToArray();
            Console.WriteLine($"fifth {fifth}: {kinds.Length} chords, " + string.Join(", ",
                Enum.GetValues<ChordScaleKind>().Select(k => $"{k} {100.0 * kinds.Count(x => x.Kind == k) / Math.Max(1, kinds.Length):F1}%")));
        }
        await Assert.That(notes).IsGreaterThan(0);
    }
}
