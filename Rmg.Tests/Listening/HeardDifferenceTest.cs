using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Tests.Listening;

/// <summary>The listening measure (<see cref="HeardDifference" />), which chooses the listening questions' songs and where to listen.</summary>
public sealed class HeardDifferenceTest
{
    // the song with a part's tracks taken out, its notes all gone
    private static RenderedSong Without(RenderedSong song, TrackRole role) =>
        new(song.Duration, song.Meters, song.TempoTimeline, song.FadeTimeline, [..song.Tracks.Where(x => x.Role != role)]);

    [Test]
    public async Task ASong_DiffersFromItselfNowhere()
    {
        var song = TestCorpus.Get(3).Rendered;
        var heard = new HeardDifference(song, song);

        await Assert.That(heard.Between(0, song.Seconds)).IsEqualTo(0);
        await Assert.That(heard.FirstDifference(0, song.Seconds)).IsNull();
    }

    /// <summary>
    ///     A part taken out differs by its notes alone, all its part's, from where it first plays, the parts' shares adding up
    ///     to the whole.
    /// </summary>
    [Test]
    public async Task APartTakenOut_DiffersByItsNotes_FromItsFirst()
    {
        var song = TestCorpus.Get(3).Rendered;
        var bass = song.Tracks.Where(x => x.Role == TrackRole.Bass).SelectMany(x => x.NoteTimeline).ToArray();
        await Assert.That(bass).IsNotEmpty();
        var heard = new HeardDifference(song, Without(song, TrackRole.Bass));

        var parts = heard.Parts(0, song.Seconds);
        await Assert.That(parts.Keys.All(x => x.StartsWith("Bass"))).IsTrue();
        await Assert.That(parts.Values.Sum()).IsEqualTo(heard.Between(0, song.Seconds)).Within(1e-9);
        await Assert.That(heard.Between(0, song.Seconds)).IsGreaterThan(0).And.IsLessThan(1);
        await Assert.That(heard.FirstDifference(0, song.Seconds)!.Value).IsEqualTo(bass.Min(x => song.SecondsAt(x.Position + x.Value.Shift))).Within(0.01);
    }
}
