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
    public async Task PitchedNotes_LastNoLongerThanABar()
    {
        var maxDuration = Enumerable.Range(0, 20)
            .Select(seed => Render.RenderSong(SongGenerator.GenerateSong(seed)))
            .SelectMany(x => x.Tracks)
            .Where(x => !x.IsPercussionInstrument)
            .SelectMany(x => x.NoteTimeline)
            .Max(x => x.Value.Duration);

        await Assert.That(maxDuration).IsLessThanOrEqualTo(BarDuration);
    }
}
