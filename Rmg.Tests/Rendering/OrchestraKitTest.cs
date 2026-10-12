using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Rendering;

/// <summary>The orchestra kit as a timpanist plays it: its timpani on the chord's root or fifth, its hi-hats the kit's own, wherever the kit that plays is the orchestra's.</summary>
public sealed class OrchestraKitTest
{
    [Test]
    public async Task TheOrchestraKitsTimpani_PlayTheChordsRootOrFifth()
    {
        var tuned = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("orchestral"))))
                     .Where(x => x.Song.Draws?.Kit == 48))
        {
            var common = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap;
            // every drum note's chord, by where it is heard
            var chords = song.Song.Notes!.Where(x => song.IsDrum(x.Key)).SelectMany(x => x.Value)
                .GroupBy(x => Math.Round(song.Swing.Apply(x.Position), 6))
                .ToDictionary(x => x.Key, x => Realizer.GetChord(common.GetEffectiveStateMapAt(x.First().Position)).Chord);
            foreach (var note in song.Rendered.Tracks.Where(x => x.IsPercussionInstrument).SelectMany(x => x.NoteTimeline))
            {
                var pitch = note.Value.Offset;
                if (pitch is < 41 or > 53 || !chords.TryGetValue(Math.Round(note.Position, 6), out var chord))
                    continue;
                tuned++;
                await Assert.That(new[] { chord.Root, chord.GetPitch(4) }.Select(x => x.Mod(12))).Contains(pitch.Mod(12)).Because($"seed {song.Seed} at {note.Position}");
            }
        }

        await Assert.That(tuned).IsGreaterThan(0);
    }

    /// <summary>A song given the orchestra kit in its mix plays its timpani tuned and its hi-hats as the kit's, whatever kit it drew.</summary>
    [Test]
    public async Task AKitGivenInTheMix_IsPlayedAsItIs()
    {
        var (hats, tuned) = (0, 0);
        var orchestra = new Rmg.Core.Rendering.SongMix(1, System.Collections.Immutable.ImmutableDictionary<Rmg.Core.Songs.TrackRole, Rmg.Core.Rendering.PartMix>.Empty
            .Add(Rmg.Core.Songs.TrackRole.Drum, new Rmg.Core.Rendering.PartMix(48, 1, null)), System.Collections.Immutable.ImmutableDictionary<string, Rmg.Core.Rendering.DrumGroupMix>.Empty);
        foreach (var song in TestCorpus.Range(16).Where(x => x.Song.Draws?.Kit != 48))
        {
            var common = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap;
            var chords = song.Song.Notes!.Where(x => song.IsDrum(x.Key)).SelectMany(x => x.Value)
                .GroupBy(x => Math.Round(song.Swing.Apply(x.Position), 6))
                .ToDictionary(x => x.Key, x => Realizer.GetChord(common.GetEffectiveStateMapAt(x.First().Position)).Chord);
            var rendered = Rmg.Core.Rendering.Render.RenderSong(song.Song, orchestra).Tracks.Where(x => x.IsPercussionInstrument).SelectMany(x => x.NoteTimeline).ToArray();
            foreach (var note in rendered)
            {
                var pitch = note.Value.Offset;
                // the hi-hats on the kit's own, every note in the timpani's range a timpani tuned
                hats += pitch is >= 27 and <= 29 ? 1 : 0;
                if (pitch is >= 41 and <= 53 && chords.TryGetValue(Math.Round(note.Position, 6), out var chord))
                {
                    tuned++;
                    await Assert.That(new[] { chord.Root, chord.GetPitch(4) }.Select(x => x.Mod(12))).Contains(pitch.Mod(12)).Because($"seed {song.Seed} at {note.Position}");
                }
            }
        }

        await Assert.That(tuned).IsGreaterThan(0);
        await Assert.That(hats).IsGreaterThan(0);
    }
}
