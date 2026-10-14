using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Rendering;

/// <summary>A gliding bass: a genre's, sliding into its changes of pitch from the note before, as an 808 does.</summary>
public sealed class GlideTest
{
    /// <summary>The share of a bass's changes of pitch it slides into, over the songs given.</summary>
    private static double Slid(IEnumerable<CorpusSong> songs)
    {
        var changes = songs.SelectMany(song => song.Song.Notes!.GetValueOrDefault(song.Song.Track(TrackRole.Bass))?.ToArray() is { } bass
            ? bass.Skip(1).Where((x, i) => x.Value.Pitches[0] != bass[i].Value.Pitches[0])
            : []).ToArray();
        return changes.Count(x => x.Value.State.GetStateValue(CompositionStateKinds.Bend) == (int)BendKind.Slide) / (double)changes.Length;
    }

    /// <summary>
    ///     Hip-hop's songs slide into their bass's changes oftener with the genre's glide than the same songs with the songs'
    ///     own, a few of them gliding (measured over 128: 3.4% against 0.9% at offset 0, 2.2% against 1.5% at a million).
    /// </summary>
    [Test]
    public async Task AHipHopBass_SlidesIntoItsChanges_OftenerWithItsGlide()
    {
        var genre = GenrePresets.Genre("hipHop");
        var own = genre with { Choices = genre.Choices.Remove(GenreChoices.Glide.Name) };
        var hipHop = Slid(TestCorpus.InParallel(Enumerable.Range(0, 128), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre))));
        var songs = Slid(TestCorpus.InParallel(Enumerable.Range(0, 128), seed => TestCorpus.Get(seed, new SongOverrides(Genre: own))));
        Console.WriteLine($"changes slid into, over 128 Hip-hop songs: with its glide {hipHop:P1}, with the songs' own {songs:P1}");

        await Assert.That(hipHop).IsGreaterThan(songs);
    }

    /// <summary>
    ///     A gliding bass slides into a leap oftener than into a step, as the slide leans to it (<see cref="Expression.GlideOdds" />);
    ///     a leap's share of the changes it may slide into, within a pitch bend's reach, against a step's.
    /// </summary>
    [Test]
    public async Task AGlidingBass_SlidesIntoALeap_OftenerThanIntoAStep()
    {
        var genre = GenrePresets.With("hipHop", GenreChoices.Glide, ("bass", 1));
        var (leaps, steps) = (new List<bool>(), new List<bool>());
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre))))
        {
            if (song.Song.Notes!.GetValueOrDefault(song.Song.Track(TrackRole.Bass))?.ToArray() is not { } bass)
                continue;
            foreach (var (before, note) in bass.Zip(bass.Skip(1)).Where(x => x.First.Position + x.First.Value.Duration >= x.Second.Position - Expression.GlideGap && x.Second.Value.Duration >= Expression.GlideShortest))
            {
                var interval = Math.Abs(before.Value.Pitches[0] - note.Value.Pitches[0]);
                if (interval is 0 or > 12)
                    continue;
                (interval >= Expression.GlideLeap ? leaps : steps).Add(note.Value.State.GetStateValue(CompositionStateKinds.Bend) == (int)BendKind.Slide);
            }
        }

        var (leapShare, stepShare) = (leaps.Average(x => x ? 1.0 : 0), steps.Average(x => x ? 1.0 : 0));
        Console.WriteLine($"a gliding bass slides into {leapShare:P0} of {leaps.Count} leaps and {stepShare:P0} of {steps.Count} steps");
        await Assert.That(leapShare).IsGreaterThan(stepShare);
    }
}
