using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Rendering;

/// <summary>A driven guitar's part played twice, all the way to either side (<see cref="DoubleTracking" />).</summary>
public sealed class DoubleTrackingTest
{
    private static readonly string[] Genres = ["metal", "djent", "rock"];

    private static CorpusSong[] Songs() =>
        TestCorpus.InParallel(Enumerable.Range(0, 16 * Genres.Length), i => TestCorpus.Get(i / Genres.Length, new SongOverrides(Genre: GenrePresets.Genre(Genres[i % Genres.Length]))));

    [Test]
    public async Task ADrivenGuitarsPart_PlaysTwice_HardLeftAndRight_TheSecondTakeOnTheOtherDrivenGuitar_ALittleLate()
    {
        var doubled = 0;
        foreach (var song in Songs())
        {
            var tempo = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetStateTimeline(StateKinds.Tempo);
            foreach (var part in song.Song.TrackDefinitions.Values.OfType<PitchInstrumentTrack>())
            {
                var takes = song.Rendered.Tracks.Where(x => !x.IsPercussionInstrument && x.Role == part.Role).ToArray();
                if (!DoubleTracking.Doubles(part.Role, part.InstrumentCode))
                {
                    await Assert.That(takes.Any(x => x.IsDouble)).IsFalse();
                    continue;
                }

                if (takes.Length == 0)
                    continue;
                doubled++;
                var (first, second) = (takes.Single(x => !x.IsDouble), takes.Single(x => x.IsDouble));
                await Assert.That(Math.Abs(first.Pan)).IsEqualTo(1);
                await Assert.That(second.Pan).IsEqualTo(-first.Pan);
                await Assert.That(second.PitchInstrumentCode).IsEqualTo(DoubleTracking.SecondTake(first.PitchInstrumentCode));
                await Assert.That(second.PitchInstrumentCode).IsNotEqualTo(first.PitchInstrumentCode);
                await Assert.That((first.Volume, second.Volume)).IsEqualTo((DoubleTracking.Level, DoubleTracking.Level));
                await Assert.That(second.NoteTimeline.Select(x => (x.Position, x.Value.Offset))).IsEquivalentTo(first.NoteTimeline.Select(x => (x.Position, x.Value.Offset)), CollectionOrdering.Matching);
                foreach (var (a, b) in first.NoteTimeline.Zip(second.NoteTimeline))
                    await Assert.That(b.Value.Shift - a.Value.Shift).IsEqualTo(DoubleTracking.Delay * Meter.BeatsPerSecond(tempo.GetEffectiveValueAt(a.Position))).Within(1e-9);
            }

            // every part on a channel of its own, the drums' kept
            await Assert.That(song.Rendered.Tracks.Count(x => !x.IsPercussionInstrument)).IsLessThanOrEqualTo(15);
            song.Rendered.Write(Stream.Null, null);
        }

        await Assert.That(doubled).IsGreaterThan(40);
    }

    [Test]
    public async Task ADoubledPartsMix_PlaysBothTakes_TheSecondOnTheOtherSideOfThePanGiven_OnTheOtherDrivenGuitar()
    {
        var song = Songs().First(x => x.Rendered.Tracks.Any(t => t.IsDouble && t.Role == TrackRole.Riff)).Song;
        var mixed = Render.RenderSong(song, SongMix.None with { Parts = ImmutableDictionary<TrackRole, PartMix>.Empty.Add(TrackRole.Riff, new PartMix(30, 0.5, 0.4, true)) });
        var takes = mixed.Tracks.Where(x => x.Role == TrackRole.Riff).OrderBy(x => x.IsDouble).ToArray();

        await Assert.That(takes.Select(x => (x.PitchInstrumentCode, x.Pan))).IsEquivalentTo([(30, 0.4), (29, -0.4)], CollectionOrdering.Matching);
        await Assert.That(takes.Select(x => x.Volume)).IsEquivalentTo([0.5 * DoubleTracking.Level, 0.5 * DoubleTracking.Level], CollectionOrdering.Matching);
        await Assert.That(mixed.GetDoubleChannels()[TrackRole.Riff]).IsNotEqualTo(mixed.GetPartChannels()[TrackRole.Riff]);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var genre in new[] { "", "rock", "metal", "djent", "nuMetal", "punk", "blues" })
        {
            var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => genre == "" ? TestCorpus.Get(seed) : TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre))));
            var parts = songs.Select(x => x.Rendered.Tracks.Where(t => t.IsDouble).Select(t => t.Role).ToArray()).ToArray();
            Console.WriteLine($"{(genre == "" ? "drawn" : genre)}: songs doubling a part {parts.Count(x => x.Length > 0) / (double)songs.Length:P0}, parts doubled a song {parts.Average(x => x.Length):F2}: " +
                              string.Join(", ", parts.SelectMany(x => x).GroupBy(x => x).OrderByDescending(x => x.Count()).Select(x => $"{x.Key} {x.Count() / (double)songs.Length:P0}")));
        }

        await Task.CompletedTask;
    }
}
