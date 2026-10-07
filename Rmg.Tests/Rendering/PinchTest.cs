using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Rendering;

/// <summary>A driven guitar's pinch harmonic (<see cref="Expression.SongPinchChance" />): a held single note's highlight, rare, always wavering.</summary>
public sealed class PinchTest
{
    [Test]
    [Arguments("metal")]
    [Arguments("nuMetal")]
    [Arguments("blackMetal")]
    public async Task APinch_IsAHeldSingleNotesHighlight_NeverAChordOrInARun_OneAPartAPhrase_AlwaysWavering(string genre)
    {
        var pinches = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre)))))
        foreach (var (track, notes) in song.Song.Notes!)
        {
            var phrases = new HashSet<(int, int)>();
            foreach (var note in notes.Where(x => x.Value.State.GetStateValue(CompositionStateKinds.Pinch) == 1))
            {
                pinches++;
                var because = $"seed {song.Seed}, track {track} at {note.Position}";
                var seconds = 60 / (song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetEffectiveStateMapAt(note.Position).GetStateValue(StateKinds.Tempo) * Meter.BaseTempo);
                await Assert.That(note.Value.Pitches.Length).IsEqualTo(1).Because(because);
                await Assert.That(note.Value.Duration * seconds).IsGreaterThanOrEqualTo(Expression.PinchShortest - 1e-9).Because(because);
                await Assert.That(notes.Count(x => Math.Abs(x.Position - note.Position) < 1 - 1e-9) / 2.0).IsLessThanOrEqualTo(Expression.PinchBusiest).Because(because);
                await Assert.That(note.Value.State.GetStateValue(CompositionStateKinds.Vibrato)).IsGreaterThan(0).Because(because);
                await Assert.That(song.Song.TrackDefinitions[track].Role).IsNotEqualTo(TrackRole.Chords).Because(because);
                var span = song.Map.Sections.Last(x => x.Start <= note.Position + 1e-9);
                await Assert.That(phrases.Add((song.Map.Sections.IndexOf(span), (int)Math.Floor((note.Position - span.Start) / (Meter.PhraseBarCount * span.Meter.BarDuration) + 1e-9))))
                    .IsTrue().Because($"{because}: one a phrase");
            }
        }

        await Assert.That(pinches).IsGreaterThan(5);
    }
}
