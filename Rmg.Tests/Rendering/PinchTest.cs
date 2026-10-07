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

    /// <summary>A riff's dive (<see cref="BendKind.Dive" />): its last held note before a louder section alone, falling an octave.</summary>
    [Test]
    public async Task ADive_IsTheRiffsLastHeldNoteBeforeALouderSection()
    {
        var (dives, songs) = (0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("nuMetal")))))
        {
            var energy = song.Trace.Where(x => x.Point == TracePoints.SectionEnergy).GroupBy(x => x.Section).ToDictionary(x => x.Key, x => ((SectionEnergyTrace)x.First().Value!).Energy);
            var any = false;
            foreach (var (track, notes) in song.Song.Notes!)
            foreach (var (note, i) in notes.Select((x, i) => (x, i)).Where(x => x.x.Value.State.GetStateValue(CompositionStateKinds.Bend) == (int)BendKind.Dive))
            {
                dives++;
                any = true;
                var because = $"seed {song.Seed}, track {track} at {note.Position}";
                await Assert.That(song.Song.TrackDefinitions[track].Role).IsEqualTo(TrackRole.Riff).Because(because);
                await Assert.That(note.Value.Duration).IsGreaterThanOrEqualTo(1).Because(because);
                await Assert.That(note.Value.State.GetStateValue(CompositionStateKinds.BendDepth)).IsEqualTo(-Expression.DiveDepth).Because(because);
                var at = song.Map.Sections.IndexOf(song.Map.Sections.Last(x => x.Start <= note.Position + 1e-9));
                await Assert.That(at + 1).IsLessThan(song.Map.Sections.Length).Because(because);
                await Assert.That(i + 1 >= notes.Count || notes[i + 1].Position >= song.Map.Sections[at].End - 1e-9).IsTrue().Because(because);
            }

            songs += any ? 1 : 0;
        }

        Console.WriteLine($"{dives} dives in {songs} of 64 Nu metal songs");
        await Assert.That(dives).IsGreaterThan(3);
    }
}
