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
        song.Song.TrackDefinitions.TryGetValue(song.Track(TrackRole.Riff), out var riff) && InstrumentTraits.Has(((PitchInstrumentTrack)riff).InstrumentCode, InstrumentTrait.Driven);

    private static CorpusSong[] Songs(string genre) => TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre))));

    /// <summary>Whether a note of the track sounds in the span.</summary>
    private static bool Sounds(CorpusSong song, int track, double start, double end) =>
        song.Song.Notes!.GetValueOrDefault(track)?.Any(x => x.Position < end - 1e-9 && x.Position + x.Value.Duration > start + 1e-6) ?? false;

    /// <summary>
    ///     The bass plays every phrase the riff plays, but in the opening, a section it sits out, a section the twin leads,
    ///     whose lead line the bass plays its own under, and a phrase its texture leaves it out of, as the riff opening a
    ///     section alone.
    /// </summary>
    [Test]
    public async Task UnderADrivenRiff_TheBass_PlaysEveryPhraseItPlays_ButInTheOpening_AndInASectionItSitsOut()
    {
        var phrases = 0;
        foreach (var (genre, song) in Genres.SelectMany(x => Songs(x).Select(song => (x, song))).Where(x => IsDriven(x.song)))
        {
            var events = song.Song.TrackEventStateTimelineMap.TrackTimelineMap;
            if (!events.ContainsKey(song.Track(TrackRole.Bass)))
                continue;
            bool Plays(int track, double start, double end) => events[track].EventTimeline.Any(x => x.Position >= start - 1e-9 && x.Position < end - 1e-9);
            var twinLed = song.Trace.Where(x => x.Point == TracePoints.TwinLead && (bool)x.Value!).Select(x => x.Section).ToHashSet();
            foreach (var span in song.Map.Sections.Skip(1).Where(x => Plays(song.Track(TrackRole.Bass), x.Start, x.End) && !twinLed.Contains(x.SectionId)))
            {
                var silent = song.Texture(span).PhraseSilent;
                foreach (var ((_, start, end), phrase) in span.Phrases.Select((x, p) => (x, p)))
                {
                    if (!Plays(song.Track(TrackRole.Riff), start, end) || phrase < silent.Length && silent[phrase].Contains(TrackRole.Bass))
                        continue;
                    phrases++;
                    await Assert.That(Plays(song.Track(TrackRole.Bass), start, end)).IsTrue().Because($"{genre} seed {song.Seed}, phrase at {start}");
                }
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
                    if (Sounds(song, song.Track(TrackRole.Riff), t, t + bar))
                    {
                        bars++;
                        if (!Sounds(song, song.Track(TrackRole.Bass), t, t + bar))
                            without["intro"] = without.GetValueOrDefault("intro") + 1;
                    }

                foreach (var span in song.Map.Sections)
                {
                    var whole = !Sounds(song, song.Track(TrackRole.Bass), span.Start, span.End);
                    for (var t = span.Start; t < span.End - 1e-9; t += span.Meter.BarDuration)
                    {
                        if (!Sounds(song, song.Track(TrackRole.Riff), t, t + span.Meter.BarDuration))
                            continue;
                        bars++;
                        if (Sounds(song, song.Track(TrackRole.Bass), t, t + span.Meter.BarDuration))
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
