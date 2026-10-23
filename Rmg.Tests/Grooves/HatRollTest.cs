using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Grooves;

/// <summary>Hi-hat rolls: a bar's last beat on the time drum in 32nds or 16th triplets, growing louder, a genre's.</summary>
public sealed class HatRollTest
{
    /// <summary>
    ///     The rolls the stage adds, the same songs made with it and without, so that a hat's own tuplets are not counted:
    ///     Trap's, louder as they go, far more than the songs' own.
    /// </summary>
    [Test]
    public async Task ATrapHat_RollsInEvenFastNotes_GrowingLouder_FarOftenerThanTheSongs()
    {
        static (int Rolls, int Louder) Rolls(Song song)
        {
            var (rolls, louder) = (0, 0);
            foreach (var (track, definition) in song.TrackDefinitions.Where(x => x.Value is PercussionInstrumentTrack d && DrumRoles.Of(d.StateMap) == DrumRole.Time))
            {
                if (!song.Notes!.TryGetValue(track, out var notes))
                    continue;
                var on = notes.ToArray();
                // four notes or more, each a 32nd or a 16th triplet after the last
                for (var i = 0; i + 3 < on.Length; i++)
                {
                    var gap = on[i + 1].Position - on[i].Position;
                    if (gap > 1 / 6.0 + 1e-6 || Enumerable.Range(i + 1, 2).Any(j => Math.Abs(on[j + 1].Position - on[j].Position - gap) > 1e-6))
                        continue;
                    rolls++;
                    // louder as heard, its first a share of its fourth no greater than a roll's start
                    louder += Rmg.Core.Rendering.Render.ToMidiVelocity(on[i].Value.Velocity) <= Rmg.Core.Rendering.Render.ToMidiVelocity(on[i + 3].Value.Velocity) * (HatRolls.From + (1 - HatRolls.From) * 3 / 4.0) + 1e-6 ? 1 : 0;
                    i += 3;
                }
            }

            return (rolls, louder);
        }

        static (int Rolls, int Louder) Added(int seed, Genre? genre)
        {
            var overrides = new SongOverrides(Genre: genre);
            var (with, without) = (Rolls(SongGenerator.GenerateSong(TestCorpus.SongSeed(seed), overrides, CancellationToken.None)),
                Rolls(SongGenerator.GenerateSong(TestCorpus.SongSeed(seed), overrides, ImmutableHashSet.Create("Hat rolls"), CancellationToken.None)));
            return (with.Rolls - without.Rolls, with.Louder - without.Louder);
        }

        var trap = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => Added(seed, GenrePresets.Genre("trap")));
        var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => Added(seed, null));
        Console.WriteLine($"rolls added: Trap {trap.Sum(x => x.Rolls)}, {trap.Sum(x => x.Louder)} louder; the songs {songs.Sum(x => x.Rolls)}");

        // seen: Trap's 64 songs add about 2120 rolls, nearly all louder, the songs' own none
        await Assert.That(trap.Sum(x => x.Rolls)).IsGreaterThan(1000);
        await Assert.That(trap.Sum(x => x.Louder)).IsGreaterThan(trap.Sum(x => x.Rolls) * 3 / 4);
        // the songs' own none, as their chance is none
        await Assert.That(songs.Sum(x => x.Rolls)).IsEqualTo(0);
    }

    /// <summary>A roll on the hi-hat alone: a ride's, a tambourine's or a shaker's time plays as it would without the rolls.</summary>
    [Test]
    public async Task ARoll_IsTheHiHats_NeverAnotherTimekeepers()
    {
        var trap = GenrePresets.With("trap", GenreChoices.HatRolls, ("bars", 1));
        var hiHat = DrumGroups.GetTrackNumber(DrumDefinitions.HiHat);
        var (others, rolled) = (0, 0);
        foreach (var seed in Enumerable.Range(0, 32))
        {
            var with = SongGenerator.GenerateSong(TestCorpus.SongSeed(seed), new SongOverrides(Genre: trap), CancellationToken.None);
            var without = SongGenerator.GenerateSong(TestCorpus.SongSeed(seed), new SongOverrides(Genre: trap), ImmutableHashSet.Create("Hat rolls"), CancellationToken.None);
            foreach (var (track, notes) in with.Notes!.Where(x => with.TrackDefinitions[x.Key] is PercussionInstrumentTrack d && DrumRoles.Of(d.StateMap) == DrumRole.Time))
            {
                var count = without.Notes!.TryGetValue(track, out var own) ? own.Count : 0;
                if (track == hiHat)
                    rolled += notes.Count - count;
                else
                {
                    others++;
                    await Assert.That(notes.Count).IsEqualTo(count).Because($"seed {seed}, track {track}");
                }
            }
        }

        Console.WriteLine($"notes the hi-hat's rolls add: {rolled}; other timekeepers checked: {others}");
        await Assert.That(rolled).IsGreaterThan(100);
    }

    /// <summary>
    ///     A roll's notes as heard in a swung song, Hip-hop's, even between its swung ends, a drill rather than a limp: the
    ///     render spaces them from the roll's start to its end as swung, never note by note.
    /// </summary>
    [Test]
    public async Task ARoll_InASwungSong_PlaysEvenlyAsHeard()
    {
        var hipHop = GenrePresets.With("hipHop", GenreChoices.HatRolls, ("bars", 1));
        var (rolls, swung) = (0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: hipHop))))
        foreach (var (track, realized) in song.Song.Notes!.Where(x => song.IsDrum(x.Key)))
        {
            foreach (var roll in realized.Select(x => (From: x.Value.State.GetStateValue(CompositionStateKinds.EvenFrom), To: x.Value.State.GetStateValue(CompositionStateKinds.EvenTo), Sound: x.Value.Pitches[0]))
                         .Where(x => x.To > x.From).Distinct())
            {
                // its sound as heard among the drums', between its swung ends
                var (from, to) = (song.Swing.Apply(roll.From), song.Swing.Apply(roll.To));
                var notes = song.Drums.Where(x => x.Value.Offset == roll.Sound && x.Position >= from - 1e-6 && x.Position < to - 1e-6).Select(x => x.Position).Distinct().Order().ToArray();
                if (notes.Length < 3)
                    continue;
                rolls++;
                swung += Math.Abs(song.Swing.Apply(roll.From + 0.25) - song.Swing.Apply(roll.From) - 0.25) > 1e-6 ? 1 : 0;
                var gaps = notes.Zip(notes.Skip(1), (a, b) => b - a).ToArray();
                await Assert.That(gaps.Max() - gaps.Min()).IsLessThan(1e-6).Because($"seed {song.Seed}, track {track}, the roll from {roll.From}");
            }
        }

        Console.WriteLine($"{rolls} rolls, {swung} of them swung");
        await Assert.That(swung).IsGreaterThan(10);
    }

    /// <summary>A drill, IDM's: its rolls in 64ths where its genre names them (`hatRollRate`), four notes a 64th apart, none without the stage.</summary>
    [Test]
    public async Task AnIdmHat_DrillsIn64ths_WhereItsGenreNamesThem()
    {
        var idm = GenrePresets.With("idm", GenreChoices.HatRolls, ("bars", 1));
        idm = idm with { Choices = idm.Choices.SetItem(GenreChoices.HatRollRate.Name, GenrePresets.With("idm", GenreChoices.HatRollRate, ("sixtyFourths", 1)).Choices[GenreChoices.HatRollRate.Name]) };
        static int Drills(Song song) => song.TrackDefinitions.Where(x => x.Value is PercussionInstrumentTrack d && DrumRoles.Of(d.StateMap) == DrumRole.Time)
            .Sum(x => song.Notes!.TryGetValue(x.Key, out var notes) ? notes.Zip(notes.Skip(1)).Count(p => Math.Abs(p.Second.Position - p.First.Position - 1 / 16.0) < 1e-6) : 0);
        var (with, without) = (0, 0);
        foreach (var seed in Enumerable.Range(0, 16))
        {
            with += Drills(SongGenerator.GenerateSong(TestCorpus.SongSeed(seed), new SongOverrides(Genre: idm), CancellationToken.None));
            without += Drills(SongGenerator.GenerateSong(TestCorpus.SongSeed(seed), new SongOverrides(Genre: idm), ImmutableHashSet.Create("Hat rolls"), CancellationToken.None));
        }

        Console.WriteLine($"64ths in a row: {with} with the rolls, {without} without");
        await Assert.That(with).IsGreaterThan(without + 100);
    }

    /// <summary>A roll rolls into a pulse of its bar, whatever the meter: 4/4's beats, 6/8's dotted quarters, a short bar's own.</summary>
    [Test]
    public async Task ARoll_EndsOnAPulseOfItsBar()
    {
        var (rolls, other) = (0, 0);
        foreach (var genre in new[] { "trap", "idm" })
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre)))))
        // a song with no drum keeping time rolling none
        foreach (var (_, to, _) in song.Trace.SingleOrDefault(x => x.Point == TracePoints.HatRolls)?.Value as System.Collections.Immutable.ImmutableArray<(double, double, int)>? ?? [])
        {
            rolls++;
            var place = song.Map.PlaceOf(to);
            other += place.Meter.PulseSixteenths != 4 ? 1 : 0;
            await Assert.That(place.OnPulse).IsTrue().Because($"seed {song.Seed}, a roll into {to}");
        }

        Console.WriteLine($"{rolls} rolls, {other} in a meter of other pulses");
        await Assert.That(rolls).IsGreaterThan(20);
    }
}
