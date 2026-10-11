using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Rendering;

/// <summary>The orchestra kit as a timpanist plays it: its timpani on the chord's root or fifth.</summary>
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
}
