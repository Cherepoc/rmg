using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Rendering;

/// <summary>
///     A guitar's strum: its chords' strings struck one after another, down on the beat and up off it, in a stroke's time,
///     a full chord's up stroke missing its lowest string; and a piano's roll, upward.
/// </summary>
public sealed class StrumTest
{
    [Test]
    public async Task AGuitarsChord_IsStruckString_ByString_DownOnTheBeat_UpOffIt()
    {
        int strummed = 0, down = 0, rolled = 0, thinned = 0;
        var strokes = new List<double>();
        foreach (var song in TestCorpus.Range(32))
        foreach (var (track, notes) in song.Song.Notes!)
        {
            if (song.Song.TrackDefinitions[track] is not PitchInstrumentTrack part)
                continue;
            var rendered = song.Rendered.Tracks.Where(x => !x.IsPercussionInstrument && !x.IsDouble && x.Role == part.Role).SelectMany(x => x.NoteTimeline).ToArray();
            int Program(RealizedNote note) => InstrumentsOverTime.ProgramOf(note.State, part.InstrumentCode);
            // the strumming grid, the shortest step between the part's chords, a 16th at the finest
            var chords = notes.Where(x => x.Value.Pitches.Length > 1).Select(x => x.Position).ToArray();
            var grid = Math.Max(0.25, chords.Zip(chords.Skip(1), (a, b) => b - a).Where(x => x > 1e-6).DefaultIfEmpty(0.5).Min());
            (double Position, double Strum)? previous = null;
            foreach (var note in notes.Where(x => x.Value.State.GetStateValue(CompositionStateKinds.Strum) != 0))
            {
                // a piano's roll, upward, on a held chord
                if (Program(note.Value) is >= 0 and <= 7)
                {
                    rolled++;
                    await Assert.That(note.Value.State.GetStateValue(CompositionStateKinds.Strum)).IsGreaterThan(0);
                    await Assert.That(note.Value.Duration).IsGreaterThanOrEqualTo(1 - 1e-9);
                    continue;
                }

                var strum = note.Value.State.GetStateValue(CompositionStateKinds.Strum);
                strummed++;
                down += strum > 0 ? 1 : 0;
                // down on the bar's first beat, counted from the bar, and down and up in turn on the strumming grid within the bar:
                // a chord a step after another the other way
                var beat = song.Map.BeatInBar(note.Position);
                if (beat < 1e-6)
                    await Assert.That(strum).IsGreaterThan(0).Because($"seed {song.Seed}, {part.Role} at {note.Position}");
                else if (previous is { } last && Math.Abs(note.Position - last.Position - grid) < 1e-6 && last.Position >= note.Position - beat - 1e-6)
                    await Assert.That(strum > 0).IsNotEqualTo(last.Strum > 0).Because($"seed {song.Seed}, {part.Role} at {note.Position} after {last.Position}, grid {grid}");
                previous = (note.Position, strum);
                // its strings in order from the stroke's first, the last a whole stroke after it
                var strings = rendered.Where(x => Math.Abs(x.Position - Swing(song.Song).Apply(note.Position)) < 1e-6 && note.Value.Pitches.Contains(x.Value.Offset))
                    .OrderBy(x => x.Value.Shift).Select(x => x.Value).ToArray();
                // a full chord's up stroke missing its lowest string
                var struck = strum < 0 && note.Value.Pitches.Length >= Rmg.Core.Rendering.Render.UpStrokeFullest ? note.Value.Pitches.Order().Skip(1).ToArray() : note.Value.Pitches.ToArray();
                if (strum < 0 && note.Value.Pitches.Length >= Rmg.Core.Rendering.Render.UpStrokeFullest)
                {
                    thinned++;
                    await Assert.That(strings.Any(x => x.Offset == note.Value.Pitches.Min() && note.Value.Pitches.Count(p => p == x.Offset) == 1)).IsFalse();
                }
                if (strings.Select(x => x.Offset).Distinct().Count() != struck.Distinct().Count())
                    continue;
                var pitches = strings.Select(x => x.Offset).ToArray();
                await Assert.That(pitches).IsEquivalentTo(strum > 0 ? pitches.Order() : pitches.OrderDescending(), CollectionOrdering.Matching);
                strokes.Add(strings[^1].Shift - strings[0].Shift);
            }
        }

        await Assert.That(strummed).IsGreaterThan(500);
        await Assert.That(rolled).IsGreaterThan(8);
        await Assert.That(thinned).IsGreaterThan(20);
        await Assert.That(down / (double)strummed).IsBetween(0.3, 0.95);
        // most strokes' strings found and checked, the order and the time of each
        await Assert.That(strokes.Count).IsGreaterThan(strummed / 2);
        await Assert.That(strokes.All(x => x is > 0 and < 0.2)).IsTrue();
    }

    private static Swing Swing(Song song) => Rmg.Core.Rendering.Render.GetSwing(song);
}
