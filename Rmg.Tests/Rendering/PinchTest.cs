using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Rendering;

/// <summary>A driven guitar's pinch harmonic (<see cref="Expression.SongPinchChance" />): a held single note's highlight, rare, wavering where it holds long enough.</summary>
public sealed class PinchTest
{
    [Test]
    [Arguments("metal")]
    [Arguments("nuMetal")]
    [Arguments("thrash")]
    public async Task APinch_IsAHeldSingleNotesHighlight_NeverAChordOrInARun_OneAPartAPhrase_WaveringWhereItHolds(string genre)
    {
        var (pinches, shaken, length) = (0, 0, 0.0);
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre)))))
        foreach (var (track, notes) in song.Song.Notes!)
        {
            var phrases = new HashSet<(double, int)>();
            foreach (var note in notes.Where(x => x.Value.State.GetStateValue(CompositionStateKinds.Pinch) == 1))
            {
                pinches++;
                var because = $"seed {song.Seed}, track {track} at {note.Position}";
                var seconds = Meter.SecondsPerBeat(song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetEffectiveStateMapAt(note.Position).GetStateValue(StateKinds.Tempo));
                await Assert.That(note.Value.Pitches.Length).IsEqualTo(1).Because(because);
                await Assert.That(note.Value.Duration * seconds).IsGreaterThanOrEqualTo(Expression.PinchShortest - 1e-9).Because(because);
                length += note.Value.Duration * seconds;
                // on General MIDI's guitar harmonics
                await Assert.That(note.Value.State.GetStateValue(CompositionStateKinds.Program)).IsEqualTo(Expression.GuitarHarmonics + 1).Because(because);
                await Assert.That(notes.Count(x => Math.Abs(x.Position - note.Position) < 1 - 1e-9) / 2.0).IsLessThanOrEqualTo(Expression.PinchBusiest).Because(because);
                // shaken wherever it holds long enough after its bend
                var holds = Expression.HoldsVibrato((BendKind)note.Value.State.GetStateValue(CompositionStateKinds.Bend), note.Value.State.GetStateValue(CompositionStateKinds.BendRise) / 1000.0,
                    note.Value.Duration * seconds, 0.5 * seconds, Expression.PinchRate, true);
                await Assert.That(note.Value.State.GetStateValue(CompositionStateKinds.Vibrato) > 0).IsEqualTo(holds).Because(because);
                shaken += holds ? 1 : 0;
                await Assert.That(song.Song.TrackDefinitions[track].Role).IsNotEqualTo(TrackRole.Chords).Because(because);
                // its phrase where it plays, a hook intro's quote its own
                var span = song.Map.PlayingAt(note.Position)!.Value.Span;
                await Assert.That(phrases.Add((span.Start, span.PhraseOf(note.Position))))
                    .IsTrue().Because($"{because}: one a phrase");
            }
        }

        Console.WriteLine($"{genre}: {pinches} pinches in 64 songs, {length / Math.Max(1, pinches):F2} s long on the average, {shaken / (double)Math.Max(1, pinches):P0} shaken");
        // some, against a pinch never made: a tremolo-picked genre holds few notes to pinch
        await Assert.That(pinches).IsGreaterThan(0);
    }

    /// <summary>
    ///     A riff's dive (<see cref="BendKind.Dive" />): its last held note before another section alone, in the section's last bar, falling an octave,
    ///     held into the section, but where the band stops before it; the next appearance the louder, by the energies the song
    ///     steps its appearances by, as the octaves' trace keeps them.
    /// </summary>
    [Test]
    public async Task ADive_IsTheRiffsLastHeldNoteBeforeALouderSection()
    {
        var (dives, songs, beats, reaching, louder) = (0, 0, 0.0, 0, 0);
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 128), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("nuMetal")))))
        {
            var any = false;
            var energies = song.Trace.Where(x => x.Point == TracePoints.Octave).Select(x => ((int Index, TrackRole, double Energy, bool))x.Value!)
                .GroupBy(x => x.Index).ToDictionary(x => x.Key, x => x.First().Energy);
            foreach (var (track, notes) in song.Song.Notes!)
            foreach (var (note, i) in notes.Select((x, i) => (x, i)).Where(x => x.x.Value.State.GetStateValue(CompositionStateKinds.Bend) == (int)BendKind.Dive))
            {
                dives++;
                any = true;
                beats += note.Value.Duration;
                var because = $"seed {song.Seed}, track {track} at {note.Position}";
                // the riff's, or its twin's, which dives with it
                await Assert.That(song.Song.TrackDefinitions[track].Role.Source()).IsEqualTo(TrackRole.Riff).Because(because);
                await Assert.That(note.Value.Duration).IsGreaterThanOrEqualTo(1).Because(because);
                await Assert.That(note.Value.State.GetStateValue(CompositionStateKinds.BendDepth)).IsEqualTo(-Expression.DiveDepth).Because(because);
                var at = song.Map.Sections.IndexOf(song.Map.Sections.Last(x => x.Start <= note.Position + 1e-9));
                await Assert.That(at + 1).IsLessThan(song.Map.Sections.Length).Because(because);
                await Assert.That(i + 1 >= notes.Count || notes[i + 1].Position >= song.Map.Sections[at].End - 1e-9).IsTrue().Because(because);
                await Assert.That(note.Position).IsGreaterThanOrEqualTo(song.Map.Sections[at].LastBar - 1e-9).Because(because);
                if (energies.TryGetValue(at, out var before) && energies.TryGetValue(at + 1, out var after))
                {
                    louder++;
                    await Assert.That(after).IsGreaterThan(before).Because(because);
                }
                // held into the section it dives into, but where the band stops before it
                reaching += note.Position + note.Value.Duration >= song.Map.Sections[at].End - 1e-9 ? 1 : 0;
            }

            songs += any ? 1 : 0;
        }

        Console.WriteLine($"{dives} dives in {songs} of 128 Nu metal songs, {beats / Math.Max(1, dives):F2} beats long on the average, {reaching} held to the section");
        // a rare device's count, 6 to 17 seen at the corpora's offsets
        await Assert.That(dives).IsGreaterThan(3);
        await Assert.That(reaching).IsGreaterThanOrEqualTo(dives * 3 / 4);
        await Assert.That(louder).IsGreaterThan(0);
    }
}
