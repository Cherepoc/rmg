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
            var (index, _, bars) = ((int, TrackRole, int))entry.Value!;
            var span = song.Map.Sections[index];
            var bar = bars * span.Meter.BarDuration;
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

    /// <summary>
    ///     An imitation's notes rub against the melody playing on, a second or a seventh from its note, seldom: the melody's
    ///     passing notes heard a bar later over another chord bend to its chord.
    /// </summary>
    [Test]
    public async Task AnImitation_SeldomRubsAgainstTheMelody()
    {
        var genre = GenrePresets.With("chamber", GenreChoices.Doubling, ("imitation", 1));
        var (notes, rubbing) = (0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre))))
        {
            var imitated = song.Trace.Where(x => x.Point == TracePoints.Imitation).Select(x => x.Section).ToHashSet();
            if (imitated.Count == 0)
                continue;
            var melody = song.Song.Notes![song.Track(TrackRole.Melody)].ToArray();
            foreach (var note in song.Song.Notes![song.Track(TrackRole.CounterMelody)].Where(x => x.Value.State.GetStateValue(CompositionStateKinds.Doubled) > 0
                                                                                                && song.Map.SectionAt(x.Position) is { } span && imitated.Contains(span.SectionId)))
            {
                var at = Array.FindLastIndex(melody, x => x.Position <= note.Position + 1e-9);
                if (at < 0 || melody[at].Position + melody[at].Value.Duration <= note.Position + 1e-9)
                    continue;
                notes++;
                rubbing += ((melody[at].Value.Pitches[0] - note.Value.Pitches[0]) % 12 + 12) % 12 is 1 or 2 or 10 or 11 ? 1 : 0;
            }
        }

        // against the mechanism breaking (measured: 1.6% to 2.3% of the notes at offsets 0 and a million, 16% to 18% before they bent)
        Console.WriteLine($"{rubbing} of {notes} imitated notes a second or a seventh from the melody's");
        await Assert.That(notes).IsGreaterThan(500);
        await Assert.That(rubbing / (double)notes).IsLessThan(0.08);
    }
}
