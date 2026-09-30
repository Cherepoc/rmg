using Rmg.Core.Composition;
using Rmg.Core.Rendering;

namespace Rmg.Tests.SongGenerators;

/// <summary>
///     A pitched note is held towards the next note of its track, but never through the silence of the bars in which
///     the track does not play, which would ring on under the chords that follow.
/// </summary>
public sealed class SongGeneratorNoteDurationTest
{
    private const double BarDuration = 4;

    [Test]
    public async Task PitchedNotes_LastNoLongerThanABar_ButTheFinalChord()
    {
        // the final chord of an ending that rings out is held for as long as the ending, up to two bars; a pad holds
        // its chords as long as they last, which it is left out for
        var maxDuration = Enumerable.Range(0, 20)
            .Select(seed => TestCorpus.Get(seed))
            .Select(song => (Pad: ((Rmg.Core.Songs.PitchInstrumentTrack)song.Song.TrackDefinitions[Rmg.Core.Composition.SongTracks.PadTrack]).InstrumentCode, song.Rendered))
            .SelectMany(song => song.Rendered.Tracks
                .Where(x => !x.IsPercussionInstrument && x.PitchInstrumentCode != song.Pad)
                .SelectMany(x => x.NoteTimeline)
                .Where(x => x.Position + x.Value.Duration < song.Rendered.Duration - 1e-9)
            )
            .Max(x => x.Value.Duration);

        await Assert.That(maxDuration).IsLessThanOrEqualTo(BarDuration);
    }
}
