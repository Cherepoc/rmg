using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Grooves;

/// <summary>Hi-hat rolls: a bar's last beat on the time drum in 32nds or 16th triplets, growing louder, a genre's.</summary>
public sealed class HatRollTest
{
    [Test]
    public async Task AHipHopHat_RollsSomeBarsLastBeat_InEvenFastNotes_GrowingLouder_TheSongsNever()
    {
        static (int Rolls, int Louder) Rolls(CorpusSong song)
        {
            var (rolls, louder) = (0, 0);
            foreach (var (track, definition) in song.Song.TrackDefinitions.Where(x => x.Value is PercussionInstrumentTrack d && DrumRoles.Of(d.StateMap) == DrumRole.Time))
            {
                if (!song.Song.Notes!.TryGetValue(track, out var notes))
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

        var hipHop = TestCorpus.InParallel(Enumerable.Range(0, 16), seed => Rolls(TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("hipHop")))));
        var songs = TestCorpus.InParallel(Enumerable.Range(0, 16), seed => Rolls(TestCorpus.Get(seed)));
        Console.WriteLine($"rolls: Hip-hop {hipHop.Sum(x => x.Rolls)}, the songs {songs.Sum(x => x.Rolls)}");

        await Assert.That(hipHop.Sum(x => x.Rolls)).IsGreaterThan(20);
        // most of them rolls, louder as they go, a few the hat's own tuplets
        await Assert.That(hipHop.Sum(x => x.Louder)).IsGreaterThan(hipHop.Sum(x => x.Rolls) * 3 / 4);
        await Assert.That(songs.Sum(x => x.Rolls)).IsLessThan(hipHop.Sum(x => x.Rolls) / 5);
    }
}
