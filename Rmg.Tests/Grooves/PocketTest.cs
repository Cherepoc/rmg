using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Tests.Grooves;

public sealed class PocketTest
{
    /// <summary>A part's offset off the beat in seconds, its first note's, at the tempo where it plays.</summary>
    private static double? Seconds(CorpusSong song, int track) =>
        song.Song.Notes!.GetValueOrDefault(track)?.FirstOrDefault(x => x.Value.State.GetStateValue(CompositionStateKinds.TimingOffset) != 0) is { Value: not null } note
            ? note.Value.State.GetStateValue(CompositionStateKinds.TimingOffset)
              * Meter.SecondsPerBeat(song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetEffectiveStateMapAt(note.Position).GetStateValue(StateKinds.Tempo))
            : null;

    [Test]
    public async Task APartsPocket_IsAsLongInSeconds_AtAnyTempo()
    {
        var compared = 0;
        foreach (var seed in Enumerable.Range(0, 16))
        {
            var (slow, fast) = (TestCorpus.Get(seed, new SongOverrides(Tempo: 0.6)), TestCorpus.Get(seed, new SongOverrides(Tempo: 1.4)));
            foreach (var track in new[] { TrackRole.Melody, TrackRole.Bass }.Select(slow.Track))
                if (Seconds(slow, track) is { } a && Seconds(fast, track) is { } b)
                {
                    await Assert.That(a).IsEqualTo(b).Within(1e-9).Because($"seed {seed}, track {track}");
                    compared++;
                }
        }

        await Assert.That(compared).IsGreaterThan(0);
    }

    /// <summary>The plainest feel keeps to the click, no note off its place.</summary>
    [Test]
    public async Task ThePlainestFeel_PlaysNoNoteOffItsPlace()
    {
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Facets: TestCorpus.FacetsOf((Facet.Feel, 0))))))
            await Assert.That(song.Song.Notes!.Values.SelectMany(x => x).All(x => x.Value.State.GetStateValue(CompositionStateKinds.TimingOffset) == 0)).IsTrue().Because($"seed {song.Seed}");
    }

    /// <summary>A drum off its place the way its role in the groove has it: the backbeat behind, the time ahead or on it.</summary>
    [Test]
    public async Task ADrumsPocket_IsItsRolesWay()
    {
        var offset = 0;
        foreach (var song in TestCorpus.Range(16))
        foreach (var (track, notes) in song.Song.Notes!.Where(x => song.IsDrum(x.Key)))
        {
            var role = DrumRoles.Of(song.Song.TrackDefinitions[track].StateMap);
            var shifts = notes.Select(x => x.Value.State.GetStateValue(CompositionStateKinds.TimingOffset)).ToArray();
            offset += shifts.Count(x => x != 0);
            await Assert.That(role switch
            {
                DrumRole.Backbeat => shifts.All(x => x >= 0),
                DrumRole.Time => shifts.All(x => x <= 0),
                _ => true
            }).IsTrue().Because($"seed {song.Seed}, {role}");
        }

        await Assert.That(offset).IsGreaterThan(0);
    }
}
