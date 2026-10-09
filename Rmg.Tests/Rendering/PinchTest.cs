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
        var (pinches, length, squeals) = (0, 0.0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre)))))
        foreach (var (track, notes) in song.Song.Notes!)
        {
            var phrases = new HashSet<(int, int)>();
            foreach (var note in notes.Where(x => x.Value.State.GetStateValue(CompositionStateKinds.Pinch) == 1))
            {
                pinches++;
                var because = $"seed {song.Seed}, track {track} at {note.Position}";
                var seconds = Meter.SecondsPerBeat(song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetEffectiveStateMapAt(note.Position).GetStateValue(StateKinds.Tempo));
                await Assert.That(note.Value.Pitches.Length).IsEqualTo(1).Because(because);
                await Assert.That(note.Value.Duration * seconds).IsGreaterThanOrEqualTo(Expression.PinchShortest - 1e-9).Because(because);
                length += note.Value.Duration * seconds;
                squeals += note.Value.State.GetStateValue(CompositionStateKinds.Program) == 0 ? 1 : 0;
                await Assert.That(notes.Count(x => Math.Abs(x.Position - note.Position) < 1 - 1e-9) / 2.0).IsLessThanOrEqualTo(Expression.PinchBusiest).Because(because);
                await Assert.That(note.Value.State.GetStateValue(CompositionStateKinds.Vibrato)).IsGreaterThan(0).Because(because);
                await Assert.That(song.Song.TrackDefinitions[track].Role).IsNotEqualTo(TrackRole.Chords).Because(because);
                var span = song.Map.Sections.Last(x => x.Start <= note.Position + 1e-9);
                await Assert.That(phrases.Add((song.Map.Sections.IndexOf(span), (int)Math.Floor((note.Position - span.Start) / span.Meter.PhraseDuration + 1e-9))))
                    .IsTrue().Because($"{because}: one a phrase");
            }
        }

        Console.WriteLine($"{genre}: {pinches} pinches in 64 songs, {length / Math.Max(1, pinches):F2} s long on the average, {squeals} squealing");
        // some, against a pinch never made: a tremolo-picked genre holds few notes to pinch
        await Assert.That(pinches).IsGreaterThan(0);
    }

    /// <summary>
    ///     A riff's dive (<see cref="BendKind.Dive" />): its last held note before another section alone, in the section's last bar, falling an octave,
    ///     held into the section, but where the band stops before it; the next appearance's being the louder, by the energies
    ///     the song steps its appearances by, which the trace does not keep, not asserted.
    /// </summary>
    [Test]
    public async Task ADive_IsTheRiffsLastHeldNoteBeforeALouderSection()
    {
        var (dives, songs, beats, reaching) = (0, 0, 0.0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("nuMetal")))))
        {
            var any = false;
            foreach (var (track, notes) in song.Song.Notes!)
            foreach (var (note, i) in notes.Select((x, i) => (x, i)).Where(x => x.x.Value.State.GetStateValue(CompositionStateKinds.Bend) == (int)BendKind.Dive))
            {
                dives++;
                any = true;
                beats += note.Value.Duration;
                var because = $"seed {song.Seed}, track {track} at {note.Position}";
                await Assert.That(song.Song.TrackDefinitions[track].Role).IsEqualTo(TrackRole.Riff).Because(because);
                await Assert.That(note.Value.Duration).IsGreaterThanOrEqualTo(1).Because(because);
                await Assert.That(note.Value.State.GetStateValue(CompositionStateKinds.BendDepth)).IsEqualTo(-Expression.DiveDepth).Because(because);
                var at = song.Map.Sections.IndexOf(song.Map.Sections.Last(x => x.Start <= note.Position + 1e-9));
                await Assert.That(at + 1).IsLessThan(song.Map.Sections.Length).Because(because);
                await Assert.That(i + 1 >= notes.Count || notes[i + 1].Position >= song.Map.Sections[at].End - 1e-9).IsTrue().Because(because);
                await Assert.That(note.Position).IsGreaterThanOrEqualTo(song.Map.Sections[at].End - song.Map.Sections[at].Meter.BarDuration - 1e-9).Because(because);
                // held into the section it dives into, but where the band stops before it
                reaching += note.Position + note.Value.Duration >= song.Map.Sections[at].End - 1e-9 ? 1 : 0;
            }

            songs += any ? 1 : 0;
        }

        Console.WriteLine($"{dives} dives in {songs} of 64 Nu metal songs, {beats / Math.Max(1, dives):F2} beats long on the average, {reaching} held to the section");
        await Assert.That(dives).IsGreaterThan(3);
        await Assert.That(reaching).IsGreaterThanOrEqualTo(dives * 3 / 4);
    }
}
