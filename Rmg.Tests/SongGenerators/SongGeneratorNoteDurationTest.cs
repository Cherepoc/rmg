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
        // the final chord of an ending that rings out is held for as long as the ending, up to two bars
        var maxDuration = Enumerable.Range(0, 20)
            .Select(seed => Render.RenderSong(SongGenerator.GenerateSong(seed)))
            .SelectMany(song => song.Tracks
                .Where(x => !x.IsPercussionInstrument)
                .SelectMany(x => x.NoteTimeline)
                .Where(x => x.Position + x.Value.Duration < song.Duration - 1e-9)
            )
            .Max(x => x.Value.Duration);

        await Assert.That(maxDuration).IsLessThanOrEqualTo(BarDuration);
    }
}
