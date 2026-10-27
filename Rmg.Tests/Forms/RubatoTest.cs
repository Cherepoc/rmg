using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Forms;

/// <summary>The tempo at a phrase's edges (<see cref="Rubato" />): the opening part alone pushing and broadening its phrases, a section's last bar broadening into the next.</summary>
public sealed class RubatoTest
{
    /// <summary>
    ///     A genre's breath slows a section's last bar under the tempo it starts in and is back at the next section's start,
    ///     the song's tempo there as its sections set it; and the songs' own bend none.
    /// </summary>
    [Test]
    public async Task ABreath_BroadensASectionsLastBar_AndIsBackInTempoAtTheNext()
    {
        var breathing = GenrePresets.With("filmPiano", GenreChoices.Rubato, ("returns", 1), ("changes", 1));
        var (songs, lines) = (0, 0);
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: breathing))))
        {
            var tempo = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetStateTimeline(StateKinds.Tempo);
            songs += (bool)song.Trace.Single(x => x.Point == TracePoints.Rubato).Value! ? 1 : 0;
            foreach (var span in song.Map.Sections.Skip(1))
            {
                var bar = song.Map.BarBefore(span.Start);
                var (before, last, at) = (tempo.GetEffectiveValueAt(span.Start - bar - 1e-6), tempo.GetEffectiveValueAt(span.Start - 1e-6), tempo.GetEffectiveValueAt(span.Start));
                // a bar's last step under the bar's first, where no section's own ramp or tempo moves it
                if (Math.Abs(before - at) > 1e-6)
                    continue;
                lines++;
                await Assert.That(last).IsLessThan(before).Because($"seed {song.Seed} at {span.Start}");
            }
        }

        Console.WriteLine($"{songs} of 32 songs bending, {lines} breaths into a section in its own tempo");
        await Assert.That(lines).IsGreaterThan(20);
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed)))
            await Assert.That((bool)song.Trace.Single(x => x.Point == TracePoints.Rubato).Value!).IsFalse().Because($"seed {song.Seed}");
    }
}
