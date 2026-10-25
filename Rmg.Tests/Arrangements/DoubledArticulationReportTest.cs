using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Arrangements;

/// <summary>A doubled line's notes played as their source plays them (<see cref="Articulations.Choose" />).</summary>
public sealed class DoubledArticulationReportTest
{
    /// <summary>
    ///     The corpus's doubled notes by the part playing them, the twin, the counter-melody and the bass: how many, and how many
    ///     slurred, muted and tremolo-picked (measured over 64 songs on 25 October 2026, before a doubled note played as its
    ///     source: the twin's 403 slurred 132, the counter-melody's 929 slurred 142 by its own draw, the bass's 4752 none;
    ///     after, the counter-melody's slurred 98 with the melody, the twin's and the bass's as they were).
    /// </summary>
    [Test]
    [Explicit]
    public async Task Report()
    {
        var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed)).ToArray();
        foreach (var role in new[] { TrackRole.RiffTwin, TrackRole.CounterMelody, TrackRole.Bass })
        {
            var notes = songs.Where(x => x.Song.TrackDefinitions.Values.Any(t => t.Role == role))
                .SelectMany(x => x.Song.Notes!.GetValueOrDefault(x.Track(role))?.ToArray() ?? [])
                .Where(x => x.Value.State.GetStateValue(CompositionStateKinds.Doubled) > 0).ToArray();
            Console.WriteLine($"{role}: {notes.Length} doubled, {notes.Count(x => x.Value.State.GetStateValue(CompositionStateKinds.Legato) > 0)} slurred, " +
                              $"{notes.Count(x => x.Value.State.GetStateValue(CompositionStateKinds.PalmMute) > 0)} muted, {notes.Count(x => x.Value.State.GetStateValue(CompositionStateKinds.Picked) > 0)} picked");
        }

        await Task.CompletedTask;
    }
}
