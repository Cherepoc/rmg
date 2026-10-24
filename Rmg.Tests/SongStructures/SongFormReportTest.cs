using System.Collections.Immutable;
using Rmg.Core.Composition;

namespace Rmg.Tests.SongStructures;

/// <summary>
///     The songs' forms: how many take one of the forms songs are written in, how long their sections play, their
///     energy by role, and how often the chorus is the song's loudest section.
/// </summary>
public sealed class SongFormReportTest
{
    /// <summary>
    ///     A song with a post-chorus plays it after every chorus, shorter than the chorus, as its role leans it, and on a hook
    ///     as a chorus is.
    /// </summary>
    [Test]
    public async Task APostChorus_FollowsEveryChorus_ShortAndOnAHook()
    {
        var genre = Rmg.Tests.Genres.GenrePresets.With("pop", GenreChoices.Form, ("vcqvcqbcq", 1), ("vpcvpcbc", 0), ("vcvcbc", 0), ("vpcvpcbcvpc", 0), ("cvcvbc", 0), ("vpcqvpcqbcq", 0));
        var (posts, hooks) = (0, 0);
        var (chorusBars, postBars) = (new List<double>(), new List<double>());
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre))))
        {
            var structure = (SongStructure)song.Trace.Single(x => x.Point == TracePoints.SongForm).Value!;
            var ids = structure.SectionIds;
            if (!ids.Any(x => structure.Roles[x] == SectionRole.PostChorus))
                continue;
            for (var i = 0; i < ids.Length; i++)
                if (structure.Roles[ids[i]] == SectionRole.Chorus)
                    await Assert.That(i + 1 < ids.Length && structure.Roles[ids[i + 1]] == SectionRole.PostChorus).IsTrue().Because($"seed {song.Seed}");
            var spans = song.Map.Sections;
            chorusBars.AddRange(spans.Where(x => structure.Roles[x.SectionId] == SectionRole.Chorus).Select(x => x.Duration / x.Meter.BarDuration));
            var post = spans.Where(x => structure.Roles[x.SectionId] == SectionRole.PostChorus).ToArray();
            postBars.AddRange(post.Select(x => x.Duration / x.Meter.BarDuration));
            posts++;
            var postId = post[0].SectionId;
            hooks += song.Trace.Any(x => x.Point == TracePoints.MelodyHook && x.Section == postId && (bool)x.Value!) ? 1 : 0;
        }

        Console.WriteLine($"{posts} songs with a post-chorus, {hooks} of them on a hook; a chorus {chorusBars.Average():0.0} bars, a post-chorus {postBars.Average():0.0}");
        // (measured: 61 songs of 64 with a post-chorus, 49 on a hook, a post-chorus 6.7 to 7.6 bars against a chorus 14.0 to 14.6, at offsets 0 and a million)
        await Assert.That(postBars.Average()).IsLessThan(chorusBars.Average());
        await Assert.That(posts).IsGreaterThan(32);
        await Assert.That(hooks).IsGreaterThan(posts / 3);
    }

    /// <summary>
    ///     Hip-hop's hook half its verse, its chorus's length halved (`roleLength.chorus` 25), and some of its songs three
    ///     verses long (`vcvcvc`), against the same songs with the songs' own lengths.
    /// </summary>
    [Test]
    public async Task AHipHopHook_IsShorterThanItsVerse_AndSomeSongsHaveThreeVerses()
    {
        (double Verse, double Chorus, int ThreeVerses) Measure(Rmg.Core.Composition.Genre genre)
        {
            var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre)));
            double Bars(SectionRole role) => songs.SelectMany(song =>
            {
                var structure = (SongStructure)song.Trace.Single(x => x.Point == TracePoints.SongForm).Value!;
                return song.Map.Sections.Where(x => structure.Roles[x.SectionId] == role).Select(x => x.Duration / x.Meter.BarDuration);
            }).DefaultIfEmpty(0).Average();
            var three = songs.Count(song => ((SongStructure)song.Trace.Single(x => x.Point == TracePoints.SongForm).Value!) is var structure
                                            && structure.SectionIds.Count(x => structure.Roles[x] == SectionRole.Verse) >= 3);
            return (Bars(SectionRole.Verse), Bars(SectionRole.Chorus), three);
        }

        var hipHop = Measure(Rmg.Tests.Genres.GenrePresets.Genre("hipHop"));
        var own = Measure(Rmg.Tests.Genres.GenrePresets.Without("hipHop", GenreChoices.RoleLength.Name));
        Console.WriteLine($"Hip-hop: a verse {hipHop.Verse:0.0} bars, a chorus {hipHop.Chorus:0.0}, {hipHop.ThreeVerses} of 64 songs three verses; with the songs' own lengths a verse {own.Verse:0.0}, a chorus {own.Chorus:0.0}");
        // (measured: a chorus 9.7 to 10.2 bars against a verse 15.8 to 15.9, 15 to 16 of 64 songs three verses, at offsets 0 and a million)
        await Assert.That(hipHop.Chorus).IsLessThan(hipHop.Verse * 0.8);
        await Assert.That(hipHop.Chorus).IsLessThan(own.Chorus);
        await Assert.That(hipHop.ThreeVerses).IsGreaterThan(5);
    }

    [Test]
    public async Task ASongOfAForm_PlaysASectionForEveryRole_NoneFollowingItself()
    {
        var formed = 0;
        foreach (var song in TestCorpus.Range(32))
        {
            var structure = (SongStructure)song.Trace.Single(x => x.Point == TracePoints.SongForm).Value!;
            // an intro and an outro of their own around the form
            var roles = structure.SectionIds.Select(x => structure.Roles[x]).Where(x => x is not (SectionRole.Intro or SectionRole.Outro)).ToArray();
            await Assert.That(structure.SectionIds.Zip(structure.SectionIds.Skip(1)).All(x => x.First != x.Second)).IsTrue();
            if (roles.All(x => x == SectionRole.Free))
                continue;

            formed++;
            await Assert.That(structure.Roles.Values.Distinct().Count()).IsEqualTo(structure.Roles.Count);
            await Assert.That(SongForms.Forms.Any(x => x.Value.Played(null).SequenceEqual(roles))).IsTrue();
        }

        await Assert.That(formed).IsGreaterThan(0);
    }

    [Test]
    public async Task AnIntroAndAnOutroOfTheirOwn_PlayOnce_FirstAndLast_NeverInAPlainSong()
    {
        var (intros, outros) = (0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 256), seed => TestCorpus.Get(seed, new SongOverrides(Facets: TestCorpus.FacetsOf((Facet.Form, 0.5))))))
        {
            var structure = (SongStructure)song.Trace.Single(x => x.Point == TracePoints.SongForm).Value!;
            var roles = structure.SectionIds.Select(x => structure.Roles[x]).ToArray();
            foreach (var (role, place) in new[] { (SectionRole.Intro, 0), (SectionRole.Outro, roles.Length - 1) })
            {
                var count = roles.Count(x => x == role);
                await Assert.That(count).IsLessThanOrEqualTo(1);
                if (count == 1)
                    await Assert.That(roles[place]).IsEqualTo(role).Because($"seed {song.Seed}");
            }

            intros += roles.Contains(SectionRole.Intro) ? 1 : 0;
            outros += roles.Contains(SectionRole.Outro) ? 1 : 0;
        }

        // bounded widely against the mechanism breaking, the targets about 0.3 and 0.15 (measured: intros 29% to 32%, outros 14% to 16%, at offsets 0 and a million)
        Console.WriteLine($"intros {intros / 256.0:P0}, outros {outros / 256.0:P0}");
        await Assert.That(intros / 256.0).IsBetween(0.1, 0.5);
        await Assert.That(outros / 256.0).IsBetween(0.02, 0.35);
        foreach (var seed in Enumerable.Range(0, 16))
        {
            var structure = (SongStructure)TestCorpus.Get(seed, new SongOverrides(Base: 0)).Trace.Single(x => x.Point == TracePoints.SongForm).Value!;
            await Assert.That(structure.Roles.Values.Any(x => x is SectionRole.Intro or SectionRole.Outro)).IsFalse();
        }
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var songs = TestCorpus.Range(256).ToArray();
        var energies = new Dictionary<SectionRole, List<double>>();
        var plays = new Dictionary<SectionRole, List<int>>();
        var (formed, chorusLoudest, withChorus, bridgesAway, bridges) = (0, 0, 0, 0, 0);
        var bars = new List<double>();
        foreach (var song in songs)
        {
            var structure = (SongStructure)song.Trace.Single(x => x.Point == TracePoints.SongForm).Value!;
            var energy = song.Trace.Where(x => x.Point == TracePoints.SectionEnergy).ToDictionary(x => x.Section, x => ((SectionEnergyTrace)x.Value!).Energy);
            var lengths = song.Trace.Where(x => x.Point == TracePoints.SectionLength).ToDictionary(x => x.Section, x => (int)x.Value!);
            bars.Add(song.Map.Sections.Sum(x => x.BarCount));
            if (structure.Roles.Values.Any(x => x != SectionRole.Free))
                formed++;
            foreach (var (id, role) in structure.Roles)
            {
                (energies.TryGetValue(role, out var e) ? e : energies[role] = []).Add(energy[id]);
                (plays.TryGetValue(role, out var p) ? p : plays[role] = []).Add(lengths[id]);
            }

            if (structure.Roles.FirstOrDefault(x => x.Value == SectionRole.Chorus) is { Value: SectionRole.Chorus } chorus)
            {
                withChorus++;
                chorusLoudest += energy[chorus.Key] >= energy.Values.Max() - 1e-9 ? 1 : 0;
            }
        }

        Console.WriteLine($"{formed} of {songs.Length} songs take a form; the chorus the loudest section in {chorusLoudest} of {withChorus}");
        foreach (var (role, e) in energies.OrderBy(x => x.Key))
            Console.WriteLine($"{role}: {e.Count} sections, energy {e.Average():F2}, plays once {plays[role].Count(x => x == 1) / (double)plays[role].Count:P0}, " +
                              $"twice {plays[role].Count(x => x == 2) / (double)plays[role].Count:P0}, four times {plays[role].Count(x => x == 4) / (double)plays[role].Count:P0}");
        var keyChanges = songs.Select(x => (ImmutableArray<KeyChange>)x.Trace.Single(t => t.Point == TracePoints.KeyChange).Value!).ToArray();
        Console.WriteLine($"{keyChanges.Count(x => x.Length > 0)} songs change key, {keyChanges.Sum(x => x.Length)} times, {keyChanges.SelectMany(x => x).Count(x => x.Semitones == 2)} up a whole step");
        var sorted = bars.Order().ToArray();
        Console.WriteLine($"the sections' bars a song: median {sorted[sorted.Length / 2]}, 10% {sorted[sorted.Length / 10]}, 90% {sorted[sorted.Length * 9 / 10]}");
        await Task.CompletedTask;
    }
}
