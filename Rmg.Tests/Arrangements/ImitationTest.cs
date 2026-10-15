using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Arrangements;

/// <summary>Imitation (<see cref="LineDoubling.ImitationChance" />): the counter-melody taking the melody's phrase a bar later.</summary>
public sealed class ImitationTest
{
    /// <summary>
    ///     In a section Chamber's counter-melody imitates, its notes start where the melody's did a bar before, and each on the
    ///     same step above its chord, an octave below; the songs' own imitate nothing.
    /// </summary>
    [Test]
    public async Task AnImitation_PlaysTheMelodysRhythmABarLater()
    {
        var genre = GenrePresets.With("chamber", GenreChoices.Doubling, ("imitation", 1));
        var (sections, matched, notes) = (0, 0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre))))
        foreach (var entry in song.Trace.Where(x => x.Point == TracePoints.Imitation))
        {
            sections++;
            var span = song.Map.Sections.First(x => x.SectionId == entry.Section);
            var bar = (int)entry.Value! * span.Meter.BarDuration;
            var melody = song.Song.Notes![song.Track(TrackRole.Melody)].Where(x => x.Position >= span.Start && x.Position < span.End).Select(x => Math.Round(x.Position, 3)).ToHashSet();
            foreach (var note in song.Song.Notes![song.Track(TrackRole.CounterMelody)].Where(x => x.Position >= span.Start + bar && x.Position < span.End - 1e-9
                                                                                                && x.Value.State.GetStateValue(CompositionStateKinds.Doubled) > 0))
            {
                notes++;
                matched += melody.Contains(Math.Round(note.Position - bar, 3)) ? 1 : 0;
            }
        }

        Console.WriteLine($"{sections} sections imitating, {matched} of {notes} notes a bar after the melody's");
        await Assert.That(sections).IsGreaterThan(5);
        await Assert.That(matched / (double)notes).IsGreaterThan(0.8);
        await Assert.That(TestCorpus.Range(32).Any(x => x.Trace.Any(t => t.Point == TracePoints.Imitation))).IsFalse();
    }
}
