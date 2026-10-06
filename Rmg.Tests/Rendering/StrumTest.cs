using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Rendering;

/// <summary>A guitar's strum: its chords' strings struck one after another, down on the beat and up off it, in a stroke's time; and a piano's roll, upward.</summary>
public sealed class StrumTest
{
    [Test]
    public async Task AGuitarsChord_IsStruckString_ByString_DownOnTheBeat_UpOffIt()
    {
        int strummed = 0, down = 0, rolled = 0;
        var strokes = new List<double>();
        foreach (var song in TestCorpus.Range(32))
        foreach (var (track, notes) in song.Song.Notes!)
        {
            if (song.Song.TrackDefinitions[track] is not PitchInstrumentTrack part)
                continue;
            var rendered = song.Rendered.Tracks.Where(x => !x.IsPercussionInstrument && !x.IsDouble && x.Role == part.Role).SelectMany(x => x.NoteTimeline).ToArray();
            int Program(RealizedNote note) => note.State.GetStateValue(CompositionStateKinds.Program) is var stated and > 0 ? stated - 1 : part.InstrumentCode;
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
                // down on a beat's first half, up on its second
                await Assert.That(strum > 0).IsEqualTo(note.Position % 1 < 0.5 - 1e-9);
                // its strings in order from the stroke's first, the last a whole stroke after it
                var strings = rendered.Where(x => Math.Abs(x.Position - Swing(song.Song).Apply(note.Position)) < 1e-6 && note.Value.Pitches.Contains(x.Value.Offset))
                    .OrderBy(x => x.Value.Shift).Select(x => x.Value).ToArray();
                if (strings.Select(x => x.Offset).Distinct().Count() != note.Value.Pitches.Distinct().Count())
                    continue;
                var pitches = strings.Select(x => x.Offset).ToArray();
                await Assert.That(pitches).IsEquivalentTo(strum > 0 ? pitches.Order() : pitches.OrderDescending(), TUnit.Assertions.Enums.CollectionOrdering.Matching);
                strokes.Add(strings[^1].Shift - strings[0].Shift);
            }
        }

        await Assert.That(strummed).IsGreaterThan(500);
        await Assert.That(rolled).IsGreaterThan(20);
        await Assert.That(down / (double)strummed).IsBetween(0.3, 0.95);
        await Assert.That(strokes.All(x => x is > 0 and < 0.2)).IsTrue();
    }

    private static Swing Swing(Song song) => Rmg.Core.Rendering.Render.GetSwing(song);
}
