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
        // they last, which it is left out for
        // in the song's bars
        var maxBars = Enumerable.Range(0, 16)
            .Select(seed => TestCorpus.Get(seed))
            .Select(song => (Pad: ((Rmg.Core.Songs.PitchInstrumentTrack)song.Song.TrackDefinitions[Rmg.Core.Composition.SongTracks.PadTrack]).InstrumentCode, song.Rendered, song.Map.Meter,
                Silence: song.Map.Ending.Kind == Rmg.Core.Composition.EndingKind.Stop ? song.Map.Ending.Start - song.Map.Ending.Stop : double.NaN))
            .SelectMany(song => song.Rendered.Tracks
                .Where(x => !x.IsPercussionInstrument && x.PitchInstrumentCode != song.Pad)
                .SelectMany(x => x.NoteTimeline)
                .Where(x => x.Position + x.Value.Duration < song.Rendered.Duration - 1e-9 && (double.IsNaN(song.Silence) || Math.Abs(x.Position + x.Value.Duration - song.Silence) > 1e-6))
                .Select(x => x.Value.Duration / song.Meter.BarDuration)
            )
            .Max();

        await Assert.That(maxBars).IsLessThanOrEqualTo(1 + 1e-9);
    }
}
