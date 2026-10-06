using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Arrangements;

/// <summary>The bass under a riff on a driven guitar, which a band's bass plays with.</summary>
public sealed class DrivenRiffBassTest
{
    private static readonly string[] Genres = ["rock", "metal", "djent", "nuMetal", "punk"];

    private static bool IsDriven(CorpusSong song) =>
        song.Song.TrackDefinitions.TryGetValue(SongTracks.RiffTrack, out var riff) && InstrumentTraits.Has(((PitchInstrumentTrack)riff).InstrumentCode, InstrumentTrait.Driven);

    private static CorpusSong[] Songs(string genre) => TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre))));

    /// <summary>Whether a note of the track sounds in the span.</summary>
    private static bool Sounds(CorpusSong song, int track, double start, double end) =>
        song.Song.Notes!.GetValueOrDefault(track)?.Any(x => x.Position < end - 1e-9 && x.Position + x.Value.Duration > start + 1e-6) ?? false;

    private static ImmutableDictionary<int, Polymeter.Plan> Plans(CorpusSong song) =>
        (ImmutableDictionary<int, Polymeter.Plan>)song.Trace.Single(x => x.Point == TracePoints.Polymeter).Value!;

    [Test]
    public async Task APartPlayingAPolymeter_PlaysInEverySectionItPlaysOtherwise()
    {
        var sections = 0;
        foreach (var genre in Genres)
            foreach (var song in Songs(genre))
            {
                var plans = Plans(song);
                var spans = song.Map.Sections.Where(x => plans.TryGetValue(x.SectionId, out var plan) && plan.Parts.Contains(TrackRole.Bass)).ToArray();
                if (spans.Length == 0)
                    continue;

                // the song with its sections in their bars, where the bass plays what it plays without the cycle
                var unrolled = SongGenerator.GenerateSong((ulong)song.Seed, ProgressionSettings.Default, new SongOverrides(Genre: GenrePresets.Genre(genre)),
                    new HashSet<string> { "Polymeter" }, (_, _) => { }, CancellationToken.None);
                foreach (var span in spans.Where(x => unrolled.Notes![SongTracks.BassTrack].Any(n => n.Position >= x.Start && n.Position < x.End)))
                {
                    sections++;
                    await Assert.That(Sounds(song, SongTracks.BassTrack, span.Start, span.End)).IsTrue().Because($"{genre} seed {song.Seed}, section at {span.Start}");
                }
            }

        await Assert.That(sections).IsGreaterThan(10);
    }

    [Test]
    public async Task UnderADrivenRiff_TheBass_PlaysEveryPhraseItPlays_ButInTheOpening_AndInASectionItSitsOut()
    {
        var phrases = 0;
        foreach (var song in Genres.SelectMany(Songs).Where(IsDriven))
        {
            var events = song.Song.TrackEventStateTimelineMap.TrackTimelineMap;
            if (!events.ContainsKey(SongTracks.BassTrack))
                continue;
            bool Plays(int track, double start, double end) => events[track].EventTimeline.Any(x => x.Position >= start - 1e-9 && x.Position < end - 1e-9);
            foreach (var span in song.Map.Sections.Skip(1).Where(x => Plays(SongTracks.BassTrack, x.Start, x.End)))
                for (var start = span.Start; start < span.End - 1e-9; start += span.Meter.PatternDuration)
                {
                    var end = Math.Min(start + span.Meter.PatternDuration, span.End);
                    if (!Plays(SongTracks.RiffTrack, start, end))
                        continue;
                    phrases++;
                    await Assert.That(Plays(SongTracks.BassTrack, start, end)).IsTrue().Because($"seed {song.Seed}, phrase at {start}");
                }
        }

        await Assert.That(phrases).IsGreaterThan(100);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var genre in Genres)
        {
            var (bars, without) = (0, new Dictionary<string, int>());
            foreach (var song in Songs(genre).Where(IsDriven))
            {
                for (var (t, bar) = (0.0, song.Map.Meter.BarDuration); t < song.Origin - 1e-9; t += bar)
                    if (Sounds(song, SongTracks.RiffTrack, t, t + bar))
                    {
                        bars++;
                        if (!Sounds(song, SongTracks.BassTrack, t, t + bar))
                            without["intro"] = without.GetValueOrDefault("intro") + 1;
                    }

                foreach (var span in song.Map.Sections)
                {
                    var whole = !Sounds(song, SongTracks.BassTrack, span.Start, span.End);
                    for (var t = span.Start; t < span.End - 1e-9; t += span.Meter.BarDuration)
                    {
                        if (!Sounds(song, SongTracks.RiffTrack, t, t + span.Meter.BarDuration))
                            continue;
                        bars++;
                        if (Sounds(song, SongTracks.BassTrack, t, t + span.Meter.BarDuration))
                            continue;
                        var cause = whole ? "whole section" : span.Start < song.Origin + 1e-9 ? "opening" : "within a section";
                        without[cause] = without.GetValueOrDefault(cause) + 1;
                    }
                }
            }

            Console.WriteLine($"{genre}: {bars} bars of a driven riff, without the bass {without.Values.Sum() / (double)bars:P1}: " +
                              string.Join(", ", without.OrderByDescending(x => x.Value).Select(x => $"{x.Key} {x.Value / (double)bars:P1}")));
        }

        await Task.CompletedTask;
    }
}
