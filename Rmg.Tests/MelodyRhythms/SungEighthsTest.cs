using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.MelodyRhythms;

/// <summary>A melody sung in 8ths, no note of its own shorter (<see cref="MelodyLayers.SungEighths" />).</summary>
public sealed class SungEighthsTest
{
    /// <summary>The plainest song sings every section in 8ths.</summary>
    [Test]
    public async Task ThePlainestSong_SingsEverySectionInEighths()
    {
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Base: 0))))
        foreach (var entry in song.Trace.Where(x => x.Point == TracePoints.SungEighths))
            await Assert.That((bool)entry.Value!).IsTrue().Because($"seed {song.Seed}, section {entry.Section}");
    }

    /// <summary>
    ///     In a section sung in 8ths, the melody's own notes, not an ornament's nor a solo's, start an 8th or more apart,
    ///     all but a few a walk or a pickup adds (measured over 64: 97.1% against 78.8% in the sections not sung so).
    /// </summary>
    [Test]
    public async Task ASectionSungInEighths_PlaysItsNotesAnEighthApart()
    {
        var (sung, unsung) = (new List<bool>(), new List<bool>());
        foreach (var song in TestCorpus.Range(64))
        {
            var sections = song.Trace.Where(x => x.Point == TracePoints.SungEighths).ToDictionary(x => x.Section, x => (bool)x.Value!);
            var melody = song.Song.Notes!.GetValueOrDefault(song.Song.Track(TrackRole.Melody))?
                .Where(x => x.Value.State.GetStateValue(CompositionStateKinds.Ornament).IsDefaultOrEmpty && x.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 0).ToArray() ?? [];
            foreach (var play in song.Map.Plays.Where(x => sections.ContainsKey(x.Span.SectionId)))
            {
                var notes = melody.Where(x => x.IsWithin(play.Span.Start, play.Span.End)).ToArray();
                (sections[play.Span.SectionId] ? sung : unsung).AddRange(notes.Zip(notes.Skip(1), (a, b) => b.Position - a.Position >= 0.5 - 1e-6));
            }
        }

        var (sungShare, unsungShare) = (sung.Average(x => x ? 1.0 : 0), unsung.Average(x => x ? 1.0 : 0));
        Console.WriteLine($"notes an 8th or more apart: {sungShare:P1} of {sung.Count} sung in 8ths, {unsungShare:P1} of {unsung.Count} not");
        await Assert.That(sungShare).IsGreaterThan(0.9);
        await Assert.That(sungShare).IsGreaterThan(unsungShare);
    }
}
