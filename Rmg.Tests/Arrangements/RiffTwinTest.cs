using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Tests.Arrangements;

public sealed class RiffTwinTest
{
    private static ImmutableHashSet<TrackRole> Absent(CorpusSong song) =>
        (ImmutableHashSet<TrackRole>)song.Trace.Single(x => x.Point == TracePoints.SongParts).Value!;

    [Test]
    public async Task ATwin_PlaysTheRiffsNotes_AThirdAboveOrASixthBelow_AllTheWayToTheOtherSide()
    {
        var twins = 0;
        foreach (var song in TestCorpus.Range(64))
        {
            var absent = Absent(song);
            var riffPan = ((PitchInstrumentTrack)song.Song.TrackDefinitions[SongTracks.RiffTrack]).Pan;
            var twinPan = ((PitchInstrumentTrack)song.Song.TrackDefinitions[SongTracks.RiffTwinTrack]).Pan;
            var twin = song.Song.Notes!.GetValueOrDefault(SongTracks.RiffTwinTrack)?.ToArray() ?? [];
            if (absent.Contains(TrackRole.RiffTwin))
            {
                await Assert.That(twin).IsEmpty();
                continue;
            }

            twins++;
            await Assert.That(absent).DoesNotContain(TrackRole.Riff);
            await Assert.That(Math.Abs(riffPan)).IsEqualTo(1);
            await Assert.That(twinPan).IsEqualTo(-riffPan);
            // every note where the riff plays one, but where the riff solos, which the twin keeps the riff under: its scale
            // step a third above or a sixth below the riff's, the same in a section
            var riff = song.Song.Notes![SongTracks.RiffTrack].ToDictionary(x => Math.Round(x.Position, 6), x => x.Value.State);
            foreach (var span in song.Map.Sections)
            {
                var apart = twin.Where(x => x.Position >= span.Start && x.Position < span.End && riff.TryGetValue(Math.Round(x.Position, 6), out var r) && r.GetStateValue(CompositionStateKinds.LineSolo) == 0)
                    .Select(x => x.Value.State.GetStateValue(StateKinds.ScaleStep) - riff[Math.Round(x.Position, 6)].GetStateValue(StateKinds.ScaleStep))
                    .Distinct()
                    .ToArray();
                await Assert.That(apart.Length <= 1 && apart.All(x => x is 2 or -5)).IsTrue().Because($"seed {song.Seed} at {span.Start}");
            }
        }

        await Assert.That(twins).IsGreaterThan(3);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var songs = TestCorpus.Range(256);
        var riffs = songs.Count(x => !Absent(x).Contains(TrackRole.Riff));
        var twins = songs.Where(x => !Absent(x).Contains(TrackRole.RiffTwin)).ToArray();
        var notes = twins.Sum(x => x.Song.Notes![SongTracks.RiffTwinTrack].Count);
        var riffNotes = twins.Sum(x => x.Song.Notes![SongTracks.RiffTrack].Count);
        Console.WriteLine($"  {riffs} of {songs.Length} songs have a riff, {twins.Length} of them a twin, playing {notes} notes against the riff's {riffNotes}");
        await Task.CompletedTask;
    }
}
