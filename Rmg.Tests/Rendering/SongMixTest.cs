using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Tests.Rendering;

public sealed class SongMixTest
{
    private static SongMix Parts(TrackRole role, PartMix part) => SongMix.None with { Parts = ImmutableDictionary<TrackRole, PartMix>.Empty.Add(role, part) };

    private static SongMix Group(string name, DrumGroupMix group) => SongMix.None with { DrumGroups = ImmutableDictionary<string, DrumGroupMix>.Empty.Add(name, group) };

    private static CorpusSong WithDrums() => TestCorpus.Range(8).First(x => x.PlaysDrums);

    [Test]
    public async Task APart_PlaysItsInstrument_AtItsVolumeAndPan_UnderTheSongs_OrIsNotWritten()
    {
        var song = TestCorpus.Get(1).Song;
        var mixed = Render.RenderSong(song, Parts(TrackRole.Bass, new PartMix(73, 0.5, -1, true)) with { Volume = 0.8 });
        var bass = mixed.Tracks.Single(x => x.Role == TrackRole.Bass);

        await Assert.That((bass.PitchInstrumentCode, bass.Volume, bass.Pan)).IsEqualTo((73, 0.4, -1.0));
        await Assert.That(mixed.Tracks.Single(x => x.Role == TrackRole.Melody).Volume).IsEqualTo(0.8);
        await Assert.That(Render.RenderSong(song, Parts(TrackRole.Bass, new PartMix(null, 1, null, false))).Tracks.Any(x => x.Role == TrackRole.Bass)).IsFalse();
    }

    [Test]
    public async Task ADrumGroup_PlaysAtItsVolume_OrIsNotWritten()
    {
        var song = WithDrums().Song;
        double[] Kicks(RenderedSong rendered) =>
        [
            ..rendered.Tracks.Single(x => x.IsPercussionInstrument).NoteTimeline
                .Where(x => DrumDefinitions.Kick.Sounds.Any(s => s.Code == x.Value.Offset)).Select(x => x.Value.Velocity)
        ];
        var asMade = Kicks(Render.RenderSong(song));

        await Assert.That(asMade).IsNotEmpty();
        await Assert.That(Kicks(Render.RenderSong(song, Group(DrumGroups.Kick.Name, new DrumGroupMix(0.5, true))))).IsEquivalentTo(asMade.Select(x => x * 0.5));
        await Assert.That(Kicks(Render.RenderSong(song, Group(DrumGroups.Kick.Name, new DrumGroupMix(1, false))))).IsEmpty();
    }

    [Test]
    public async Task APartTheSongLeavesOut_TakesNoChannel()
    {
        var song = TestCorpus.Get(1, new SongOverrides(Parts: ImmutableDictionary<TrackRole, bool>.Empty.Add(TrackRole.Pad, false)));

        await Assert.That(song.Rendered.Tracks.Any(x => x.Role == TrackRole.Pad)).IsFalse();
    }
}
