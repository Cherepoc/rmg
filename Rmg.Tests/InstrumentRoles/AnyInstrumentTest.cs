using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Tests.InstrumentRoles;

/// <summary>Any General MIDI instrument in any role, a genre's (<see cref="InstrumentRole.Everything" />); the songs' own lists as they were.</summary>
public sealed class AnyInstrumentTest
{
    [Test]
    public async Task AGenre_PlaysAnInstrumentItsPartDoesNotList_AChoirOnTheBass()
    {
        // the choir, which no bass lists
        await Assert.That(Rmg.Core.Composition.InstrumentRoles.Bass.Instruments.Any(x => x.Program == 52)).IsFalse();
        var genre = new Genre(ImmutableDictionary<string, ImmutableDictionary<string, ByConvention>>.Empty
            .Add(GenreChoices.Instruments[TrackRole.Bass].Name, ImmutableDictionary<string, ByConvention>.Empty.Add("choirAahs", new ByConvention(1, 1, 1))));
        var basses = TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre)))
            .SelectMany(x => x.Song.TrackDefinitions.Values.OfType<PitchInstrumentTrack>().Where(t => t.Role == TrackRole.Bass)).ToArray();
        await Assert.That(basses).IsNotEmpty();
        await Assert.That(basses.All(x => x.InstrumentCode == 52)).IsTrue();
    }

    [Test]
    public async Task TheSongs_StartEveryPartOnAnInstrumentItsRoleLists()
    {
        foreach (var song in TestCorpus.Range(64))
        foreach (var track in song.Song.TrackDefinitions.Values.OfType<PitchInstrumentTrack>().Where(x => InstrumentsOverTime.RoleOf(x.Role) is not null))
            await Assert.That(InstrumentsOverTime.RoleOf(track.Role)!.Instruments.Any(x => x.Program == track.InstrumentCode)).IsTrue().Because($"seed {song.Seed}, {track.Role} {track.InstrumentCode}");
    }
}
