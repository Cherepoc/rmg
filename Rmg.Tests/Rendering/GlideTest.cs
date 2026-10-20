using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Rendering;

/// <summary>A gliding bass: a genre's, sliding into its changes of pitch from the note before, as an 808 does.</summary>
public sealed class GlideTest
{
    /// <summary>The share of a part's changes of pitch it slides into, over the songs given, the bass's where none is named.</summary>
    private static double Slid(IEnumerable<CorpusSong> songs, TrackRole role = TrackRole.Bass)
    {
        var changes = songs.SelectMany(song => song.Song.Notes!.GetValueOrDefault(song.Song.Track(role))?.ToArray() is { } bass
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

    /// <summary>A part a genre has glide, the bass or the lead, slides into its changes oftener than the same songs' part not gliding.</summary>
    [Test]
    [Arguments(TrackRole.Bass, "bass")]
    [Arguments(TrackRole.Melody, "lead")]
    public async Task AGlidingPart_SlidesIntoItsChanges_OftenerThanTheSameSongsNotGliding(TrackRole role, string option)
    {
        double Of(double glide) => Slid(TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.With("hipHop", GenreChoices.Glide, (option, glide))))), role);
        var (gliding, not) = (Of(1), Of(0));
        Console.WriteLine($"the {role}'s changes slid into, over 64 Hip-hop songs: gliding {gliding:P1}, not {not:P1}");
        await Assert.That(gliding).IsGreaterThan(not);
    }

    /// <summary>A gliding bass slides into more of its changes at a genre's share of them at its most than at its least, the same songs.</summary>
    [Test]
    public async Task AGlidingBass_SlidesIntoMoreOfItsChanges_AtAGreaterShare()
    {
        var gliding = GenrePresets.With("hipHop", GenreChoices.Glide, ("bass", 1));
        double Of(double share) => Slid(TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: gliding with
        {
            Choices = gliding.Choices.SetItem(GenreChoices.GlideRate.Name, System.Collections.Immutable.ImmutableDictionary<string, Rmg.Core.Probabilities.ByConvention>.Empty.Add("share", new(share, share, share)))
        }))));
        var (most, least) = (Of(1), Of(0));
        Console.WriteLine($"a gliding bass's changes slid into, over 64 Hip-hop songs: at the most {most:P1}, at the least {least:P1}");
        await Assert.That(most).IsGreaterThan(least);
    }

    /// <summary>
    ///     A gliding bass slides into a leap oftener than into a step, as the slide leans to it (<see cref="Expression.GlideOdds" />);
    ///     a leap's share of the changes it may slide into, within a pitch bend's reach, against a step's (seen 22% against 20%
    ///     and 24% against 12% at two offsets, over 128 songs, as Hip-hop's loops play one bass line through a song's sections).
    /// </summary>
    [Test]
    public async Task AGlidingBass_SlidesIntoALeap_OftenerThanIntoAStep()
    {
        var genre = GenrePresets.With("hipHop", GenreChoices.Glide, ("bass", 1));
        var (leaps, steps, ends, inner) = (new List<bool>(), new List<bool>(), new List<bool>(), new List<bool>());
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 128), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre))))
        {
            if (song.Song.Notes!.GetValueOrDefault(song.Song.Track(TrackRole.Bass))?.ToArray() is not { } bass)
                continue;
            foreach (var ((before, note), i) in bass.Zip(bass.Skip(1)).Select((x, i) => (x, i)).Where(x => x.x.First.Position + x.x.First.Value.Duration >= x.x.Second.Position - Expression.GlideGap && x.x.Second.Value.Duration >= Expression.GlideShortest))
            {
                var interval = Math.Abs(before.Value.Pitches[0] - note.Value.Pitches[0]);
                if (interval is 0 or > 12)
                    continue;
                var slid = note.Value.State.GetStateValue(CompositionStateKinds.Bend) == (int)BendKind.Slide;
                (interval >= Expression.GlideLeap ? leaps : steps).Add(slid);
                // a step ending its phrase, the note after a beat or more after it or starting a phrase, against one within it
                var after = i + 2 < bass.Length ? bass[i + 2] : default;
                if (interval < Expression.GlideLeap)
                    (after.Value is null || after.Position >= note.Position + note.Value.Duration + 1 - 1e-9 || after.Value.State.GetStateValue(CompositionStateKinds.LinePhraseStart) > 0 ? ends : inner).Add(slid);
            }
        }

        var (leapShare, stepShare) = (leaps.Average(x => x ? 1.0 : 0), steps.Average(x => x ? 1.0 : 0));
        var (endShare, innerShare) = (ends.Average(x => x ? 1.0 : 0), inner.Average(x => x ? 1.0 : 0));
        Console.WriteLine($"a gliding bass slides into {leapShare:P0} of {leaps.Count} leaps and {stepShare:P0} of {steps.Count} steps, {endShare:P0} of {ends.Count} steps ending a phrase and {innerShare:P0} of {inner.Count} within one");
        await Assert.That(leapShare).IsGreaterThan(stepShare);
        await Assert.That(endShare).IsGreaterThan(innerShare);
    }
}
