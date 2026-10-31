using Rmg.Core.Composition;
using Rmg.Core.Rendering;

namespace Rmg.Tests.SongGenerators;

/// <summary>
///     A pitched note is held towards the next note of its track, but never through the silence of the bars in which
///     the track does not play, which would ring on under the chords that follow.
/// </summary>
public sealed class SongGeneratorNoteDurationTest
{
    [Test]
    public async Task PitchedNotes_LastNoLongerThanABar_ButTheFinalChord()
    {
        // the final chord of an ending that rings out is held for as long as the ending, up to two bars, and a note
        // sounding where a stopped ending's band falls silent is held up to the silence; a pad holds its chords as long as
        // they last, which it is left out for, as is a gated part, which holds its chords from change to change for the gate
        // to chop (GatedChords), and a silent start's first note is held from the song's start
        // in the bars of the notes' sections
        var maxBars = Enumerable.Range(0, 16)
            .Select(seed => TestCorpus.Get(seed))
            .Select(song => (song.Rendered, song.Map,
                // the parts the song gates
                Gated: song.Song.Notes!.Where(x => x.Value.Any(n => n.Value.State.GetStateValue(Rmg.Core.Composition.CompositionStateKinds.Gate) != 0))
                    .Select(x => song.Song.TrackDefinitions[x.Key].Role).ToHashSet(),
                Silence: song.Map.Ending.Kind == Rmg.Core.Composition.EndingKind.Stop ? song.Map.Ending.Start - song.Map.Ending.Stop : double.NaN,
                // a silent start, its holder's note alone at the song's start
                SilentStart: song.Rendered.Tracks.Where(x => !x.IsPercussionInstrument).SelectMany(x => x.NoteTimeline).Count(x => x.Position <= 1e-9) == 1))
            .SelectMany(song => song.Rendered.Tracks
                .Where(x => !x.IsPercussionInstrument && x.Role != TrackRole.Pad && !song.Gated.Contains(x.Role))
                .SelectMany(x => x.NoteTimeline)
                // and the note a silent start holds from the song's start into where it was, as long as the silence it fills
                // (SilentStart)
                .Where(x => !song.SilentStart || x.Position > 1e-9)
                .Where(x => x.Position + x.Value.Duration < song.Rendered.Duration - 1e-9 && (double.IsNaN(song.Silence) || Math.Abs(x.Position + x.Value.Duration - song.Silence) > 1e-6))
                .Select(x => x.Value.Duration / song.Map.MeterAt(x.Position).BarDuration)
            )
            .Max();

        await Assert.That(maxBars).IsLessThanOrEqualTo(1 + 1e-9);
    }
}
