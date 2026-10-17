using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;

namespace Rmg.Tests.SongGenerators;

public sealed class GivenTempoKeyMeterTest
{
    private static StateMap Start(CorpusSong song) => song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetEffectiveStateMapAt(0);

    private static byte[] Write(CorpusSong song)
    {
        using var stream = new MemoryStream();
        song.Rendered.Write(stream, null);
        return stream.ToArray();
    }

    [Test]
    public async Task AGivenTempoKeyAndMeter_AreTheSongs()
    {
        // a tempo the song never draws, as given
        var song = TestCorpus.Get(3, new SongOverrides(Tempo: 0.5, Key: 7, MeterOption: 2));

        await Assert.That(Start(song).GetStateValue(StateKinds.Tempo)).IsEqualTo(0.5);
        await Assert.That(Start(song).GetStateValue(StateKinds.KeyOffset)).IsEqualTo(7);
        await Assert.That(Meter.OptionOf(song.Map.Meter)).IsEqualTo(2);
    }

    [Test]
    public async Task AGivenScaleAndSwing_AreTheSongs_TheRestAsItsSeedMadeIt()
    {
        var swung = 0;
        foreach (var seed in Enumerable.Range(0, 8))
        {
            var drawn = TestCorpus.Get(seed);
            var scale = Core.Composition.Scales.All.First(x => x != drawn.Song.Draws!.Scale);
            var given = TestCorpus.Get(seed, new SongOverrides(Scale: scale, Swing: 0.5));

            var (_, songScale) = ((HarmonicUnconventionality, Scale))given.Trace.Single(x => x.Point == TracePoints.SongHarmony).Value!;
            await Assert.That(songScale).IsEqualTo(scale);
            // swung half a triplet's swing, or straight where the meter pairs no notes to swing
            var amount = given.Song.Draws!.SwingAmount;
            await Assert.That(amount is 0 || Math.Abs(amount - 0.5) < 1e-9).IsTrue();
            swung += amount > 0 ? 1 : 0;
            // and the rest drawn as it was: the same form, tempo and instruments
            await Assert.That(given.Map.Sections.Select(x => (x.SectionId, x.Duration))).IsEquivalentTo(drawn.Map.Sections.Select(x => (x.SectionId, x.Duration)), CollectionOrdering.Matching);
            await Assert.That(Start(given).GetStateValue(StateKinds.Tempo)).IsEqualTo(Start(drawn).GetStateValue(StateKinds.Tempo));
            await Assert.That(given.Rendered.Tracks.Select(x => x.PitchInstrumentCode)).IsEquivalentTo(drawn.Rendered.Tracks.Select(x => x.PitchInstrumentCode), CollectionOrdering.Matching);
        }

        await Assert.That(swung).IsGreaterThan(4);
    }

    [Test]
    public async Task ASwingOf0_IsStraight()
    {
        foreach (var seed in Enumerable.Range(0, 8))
            await Assert.That(TestCorpus.Get(seed, new SongOverrides(Swing: 0)).Song.Draws!.Swing).IsEqualTo(Swing.None);
    }

    [Test]
    public async Task GivenAsDrawn_LeavesTheSongAsItsSeedMadeIt()
    {
        foreach (var seed in Enumerable.Range(0, 4))
        {
            var drawn = TestCorpus.Get(seed);
            var given = TestCorpus.Get(seed, new SongOverrides(
                Tempo: Start(drawn).GetStateValue(StateKinds.Tempo),
                Key: Start(drawn).GetStateValue(StateKinds.KeyOffset),
                MeterOption: Meter.OptionOf(drawn.Map.Meter),
                Scale: drawn.Song.Draws!.Scale,
                Swing: drawn.Song.Draws.SwingAmount
            ));

            await Assert.That(Write(given)).IsEquivalentTo(Write(drawn), CollectionOrdering.Matching);
        }
    }

    /// <summary>A mood given as the song drew it leaves the song as its seed made it, and one given at its ends is the song's as given.</summary>
    [Test]
    public async Task AGivenMood_IsTheSongs_AndGivenAsDrawn_LeavesTheSongAsItsSeedMadeIt()
    {
        foreach (var seed in Enumerable.Range(0, 4))
        {
            var drawn = TestCorpus.Get(seed);
            var mood = (Mood)drawn.Trace.Single(x => x.Point == TracePoints.Mood).Value!;
            var given = TestCorpus.Get(seed, new SongOverrides(Darkness: mood.Darkness, Intensity: mood.Intensity));
            await Assert.That(Write(given)).IsEquivalentTo(Write(drawn), CollectionOrdering.Matching);

            var ends = TestCorpus.Get(seed, new SongOverrides(Darkness: -1, Intensity: 1));
            await Assert.That((Mood)ends.Trace.Single(x => x.Point == TracePoints.Mood).Value!).IsEqualTo(new Mood(-1, 1));
        }
    }

    /// <summary>A drawn mood lies within its genre's span, the songs' own where it has none.</summary>
    [Test]
    public async Task ADrawnMood_LiesWithinItsGenresSpan()
    {
        foreach (var (id, genre) in new[] { ("songs", (Genre?)null), ("darkJazz", Genres.GenrePresets.Genre("darkJazz")), ("trance", Genres.GenrePresets.Genre("trance")) })
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre))))
        {
            var mood = (Mood)song.Trace.Single(x => x.Point == TracePoints.Mood).Value!;
            var at = ((Unconventionality)song.Trace.Single(x => x.Point == TracePoints.SongUnconventionality).Value!).Base;
            var (darkness, intensity) = genre is null
                ? (Mood.SongsSpan, Mood.SongsSpan)
                : (genre.Range(GenreChoices.Darkness, at) ?? Mood.SongsSpan, genre.Range(GenreChoices.Intensity, at) ?? Mood.SongsSpan);
            await Assert.That(mood.Darkness).IsBetween(Mood.OfAmount(darkness.From) - 1e-9, Mood.OfAmount(darkness.To) + 1e-9).Because($"{id} seed {song.Seed}");
            await Assert.That(mood.Intensity).IsBetween(Mood.OfAmount(intensity.From) - 1e-9, Mood.OfAmount(intensity.To) + 1e-9).Because($"{id} seed {song.Seed}");
        }
    }

    [Test]
    public async Task TheTempos_RunFrom90ToAbout175BeatsAMinute()
    {
        await Assert.That(SongGenerator.TempoOptions[0] * Meter.BaseTempo).IsEqualTo(90);
        await Assert.That(SongGenerator.TempoOptions[^1] * Meter.BaseTempo).IsEqualTo(174.5).Within(0.1);
        await Assert.That(SongGenerator.TempoOptions.Length).IsLessThanOrEqualTo(64);
    }
}
