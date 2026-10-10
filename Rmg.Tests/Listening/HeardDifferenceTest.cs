using Rmg.Core.Events;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Tests.Listening;

/// <summary>The listening measure (<see cref="HeardDifference" />), which chooses the listening questions' songs and where to listen.</summary>
public sealed class HeardDifferenceTest
{
    // the song with a part's tracks taken out, its notes all gone
    private static RenderedSong Without(RenderedSong song, TrackRole role) =>
        new(song.Duration, song.Meters, song.TempoTimeline, song.FadeTimeline, [..song.Tracks.Where(x => x.Role != role)]);

    // a song of eight beats, a note on each, every note moved by the beats given, as a swing or a pocket moves it
    private static RenderedSong Moved(double by) =>
        new(8, [(0, Rmg.Core.Composition.Meter.FourFour)], Rmg.Core.Events.StateKinds.Tempo.CreateDefaultTimeline(0), Rmg.Core.Events.StateKinds.Fade.CreateDefaultTimeline(0),
        [
            new RenderedTrack(false, TrackRole.Melody, 0,
                Rmg.Core.Events.EventTimeline.Create(8, Enumerable.Range(0, 8).Select(x => new RenderedNote(60, 0.8, 0.5, by).ToTimelineItem(x))), 0)
        ]);

    /// <summary>A note moved too little to be heard as moved is the same note; one moved as far as a heard swing moves it is not.</summary>
    [Test]
    public async Task ANoteMovedLessThanIsHeard_IsTheSameNote()
    {
        // at the base tempo a beat is half a second: 0.03 beats 15 ms, 0.2 beats 100 ms
        await Assert.That(new HeardDifference(Moved(0), Moved(0.03)).Between(0, 4)).IsEqualTo(0);
        await Assert.That(new HeardDifference(Moved(0), Moved(0.2)).Between(0, 4)).IsGreaterThan(0.9);
    }

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
