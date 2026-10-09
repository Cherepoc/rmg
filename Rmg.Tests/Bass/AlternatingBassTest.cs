using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Bass;

/// <summary>A bass alternating its chord's root and fifth on its strong beats, a country bass's.</summary>
public sealed class AlternatingBassTest
{
    private static Genre With(string preset, double chance)
    {
        var genre = GenrePresets.Genre(preset);
        return genre with
        {
            Choices = genre.Choices.SetItem(GenreChoices.AlternatingBass.Name, ImmutableDictionary<string, ByConvention>.Empty.Add("sections", new ByConvention(chance, chance, chance)))
        };
    }

    /// <summary>
    ///     The share of the bass's strong notes, but the first of each chord, that sound their chord's fifth, in the
    ///     sections that alternate and in the others.
    /// </summary>
    private static (int Fifths, int Strong) Measure(CorpusSong song, bool alternating)
    {
        var sections = song.Trace.Where(t => t.Point == TracePoints.AlternatingBass && (bool)t.Value! == alternating).Select(t => t.Section).ToHashSet();
        var bass = song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key].Role == TrackRole.Bass).SelectMany(x => x.Value).ToArray();
        var (fifths, strong) = (0, 0);
        TimelineItem<RealizedNote>? first = null;
        foreach (var note in bass.Where(x => x.Value.State.GetStateValue(CompositionStateKinds.BeatRank) == 0))
        {
            var chordStart = first is { } f && f.Value.State.GetStateValue(StateKinds.ChordChange) == note.Value.State.GetStateValue(StateKinds.ChordChange)
                                             && song.Map.SectionAt(f.Position) == song.Map.SectionAt(note.Position);
            if (!chordStart)
            {
                first = note;
                continue;
            }

            if (!sections.Contains(song.Map.SectionAt(note.Position)?.SectionId ?? -1) || note.Value.Pitches.Length != 1)
                continue;
            strong++;
            var fifth = Rmg.Core.Composition.Realizer.GetChord(note.Value.State).Chord.GetPitch(4);
            fifths += ((note.Value.Pitches[0] - fifth) % 12 + 12) % 12 == 0 ? 1 : 0;
        }

        return (fifths, strong);
    }

    /// <summary>In a section that alternates, the bass sounds its chord's fifth on far more of its strong notes than in one that does not.</summary>
    [Test]
    public async Task AnAlternatingBass_SoundsTheFifth_OnItsStrongNotesAfterTheRoot()
    {
        (int Fifths, int Strong) Sum(double chance, bool alternating) => TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: With("country", chance))))
            .Select(x => Measure(x, alternating)).Aggregate((0, 0), (a, b) => (a.Item1 + b.Fifths, a.Item2 + b.Strong));
        var (fifths, strong) = Sum(1, true);
        var (plainFifths, plainStrong) = Sum(0, false);
        Console.WriteLine($"the fifth on {fifths} of {strong} strong notes after a chord's first, against {plainFifths} of {plainStrong} where none alternates");
        await Assert.That(strong).IsGreaterThan(50);
        await Assert.That(plainStrong).IsGreaterThan(50);
        await Assert.That(fifths).IsGreaterThan(strong / 3);
        await Assert.That(fifths / (double)strong).IsGreaterThan(2 * plainFifths / (double)plainStrong);
    }

    /// <summary>How often the presets' bass sounds the fifth on a strong note after a chord's first, with the alternating bass and without.</summary>
    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var name in new[] { "country", "folk" })
        {
            var songs = TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(name))));
            string Share(bool alternating)
            {
                var (fifths, strong) = songs.Select(x => Measure(x, alternating)).Aggregate((0, 0), (a, b) => (a.Item1 + b.Fifths, a.Item2 + b.Strong));
                return $"{fifths / (double)Math.Max(1, strong):P0} of {strong}";
            }

            var share = songs.SelectMany(x => x.Trace.Where(t => t.Point == TracePoints.AlternatingBass)).Average(t => (bool)t.Value! ? 1.0 : 0);
            Console.WriteLine($"{name}: sections alternating {share:P0}; the fifth on the strong notes after a chord's first there {Share(true)}, elsewhere {Share(false)}");
        }

        await Task.CompletedTask;
    }
}
