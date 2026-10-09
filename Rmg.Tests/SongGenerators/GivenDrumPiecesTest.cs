using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.SongGenerators;

/// <summary>A song's drums given as a set of pieces, in place of those it draws, changing nothing but the drums.</summary>
public sealed class GivenDrumPiecesTest
{
    /// <summary>The drums a song plays, by their names.</summary>
    private static ImmutableHashSet<string> Drums(CorpusSong song) =>
    [
        ..song.Song.TrackDefinitions.Keys.Where(x => song.IsDrum(x)).Select(x => DrumPieces.NameOf(DrumGroups.GetDrum(x)))
    ];

    /// <summary>A pitched track's notes from <paramref name="from" /> on, as text, which two songs alike in everything but their drums share.</summary>
    private static string[] Notes(CorpusSong song, int track, double from) =>
    [
        ..song.Song.Notes!.GetValueOrDefault(track)?.Where(n => n.Position >= from - 1e-9)
            .Select(n => $"{n.Position:F6} {string.Join(",", n.Value.Pitches)} {n.Value.Velocity:F6} {n.Value.Duration:F6}") ?? []
    ];

    /// <summary>
    ///     Where the song's second section starts, after an intro's entries, which bring the parts in one by one, the drums'
    ///     roles among them, and the first section they land in.
    /// </summary>
    private static double AfterEntries(CorpusSong song) => song.Map.Sections[1].Start;

    // the parts that join the drums' fills at the lines (LineFills) and the soloists, who trade with the drums, which
    // follow the drums the song plays, as the others do not; and any other part where it solos
    private static readonly TrackRole[] FollowTheDrums = [TrackRole.Bass, TrackRole.Melody, TrackRole.CounterMelody, TrackRole.Solo];

    private static bool FollowsTheDrums(TrackRole role, params CorpusSong[] songs) =>
        FollowTheDrums.Contains(role) || songs.Any(x => x.Solos.Values.Any(s => s.Soloists.Contains(role)));

    /// <summary>Whether two songs have the same intro, its parts entering in the same order, and sections, in the same places.</summary>
    private static bool SameForm(SongMap first, SongMap second) =>
        first.Intro.Kind == second.Intro.Kind && Math.Abs(first.Intro.Duration - second.Intro.Duration) < 1e-9
        && (first.Intro.Entries.IsDefault ? [] : first.Intro.Entries).Select(x => (x.Part, x.Entry)).SequenceEqual((second.Intro.Entries.IsDefault ? [] : second.Intro.Entries).Select(x => (x.Part, x.Entry)))
        && first.Sections.Select(x => (x.SectionId, x.Start, x.Duration)).SequenceEqual(second.Sections.Select(x => (x.SectionId, x.Start, x.Duration)));

    private static byte[] Write(CorpusSong song)
    {
        using var stream = new MemoryStream();
        song.Rendered.Write(stream, null);
        return stream.ToArray();
    }

    [Test]
    [Arguments(0.5)]
    [Arguments(1.0)]
    public async Task TheKickAndPercussionGiven_AreHeard_HoweverWildTheGroove(double groove)
    {
        foreach (var seed in Enumerable.Range(0, 16))
        {
            var song = TestCorpus.Get(seed, new SongOverrides(DrumPieces: ["kick", "bongo", "conga"], Facets: ImmutableDictionary<Facet, double>.Empty.Add(Facet.Groove, groove)));
            // the pieces given, not the drums: a song that draws none has none to hear
            if (!song.PlaysDrums)
                continue;
            var silent = song.Song.TrackDefinitions.Keys.Where(x => song.IsDrum(x))
                .Where(x => !song.Song.Notes!.TryGetValue(x, out var notes) || notes.Count == 0)
                .Select(x => DrumPieces.NameOf(DrumGroups.GetDrum(x)));
            await Assert.That(silent).IsEmpty().Because($"seed {seed}");
        }
    }

    [Test]
    public async Task TheDrumsDrawn_MakeTheSetupDrawn()
    {
        foreach (var song in TestCorpus.Range(128))
            await Assert.That(DrumPieces.SetupOf(Drums(song))).IsEqualTo((DrumSetup)song.Trace.Single(x => x.Point == TracePoints.DrumSetup).Value!).Because($"seed {song.Seed}");
    }

    [Test]
    public async Task TheDrumsDrawn_GivenBack_LeaveTheSongAsItsSeedMadeIt()
    {
        foreach (var seed in Enumerable.Range(0, 8))
        {
            var drawn = TestCorpus.Get(seed);
            var given = TestCorpus.Get(seed, new SongOverrides(DrumPieces: Drums(drawn)));

            await Assert.That(Write(given)).IsEquivalentTo(Write(drawn), CollectionOrdering.Matching).Because($"seed {seed}");
        }
    }

    [Test]
    [Arguments("kick", "acousticSnare", "hiHat", "conga", "cowbell", "whistle")]
    [Arguments("claves")]
    [Arguments("electricSnare", "acousticSnare", "ride", "tom", "cymbal")]
    [Arguments("bongo", "tambourine", "guiro", "triangle")]
    public async Task OtherDrumsGiven_AreTheSongs_AndChangeNoOtherPart_ButWhereItFollowsTheDrumsOrEntersWithThem(params string[] pieces)
    {
        var (notes, changed, compared) = (0, 0, 0);
        foreach (var seed in Enumerable.Range(0, 32))
        {
            var drawn = TestCorpus.Get(seed);
            var given = TestCorpus.Get(seed, new SongOverrides(DrumPieces: [..pieces]));

            await Assert.That(Drums(given).SetEquals(pieces)).IsTrue().Because($"seed {seed}");
            // where the drums change the form, the parts move with it: where they enter, as an intro bringing the parts in
            // one by one does, its order taking the drums' roles, a drum solo, and stop time, which the drums keeping time
            // carry through a bar
            if (!SameForm(drawn.Map, given.Map) || drawn.Trace.Concat(given.Trace).Any(x => x.Point == TracePoints.DrumSolo || x.Point == TracePoints.StopTime && (bool)x.Value!))
                continue;
            compared++;
            foreach (var (track, definition) in drawn.Song.TrackDefinitions.Where(x => !drawn.IsDrum(x.Key)))
            {
                var from = Math.Max(AfterEntries(drawn), AfterEntries(given));
                var (before, after) = (Notes(drawn, track, from), Notes(given, track, from));
                if (!FollowsTheDrums(definition.Role, drawn, given))
                    await Assert.That(after).IsEquivalentTo(before, CollectionOrdering.Matching).Because($"seed {seed}, the {definition.Role}");
                else
                    (notes, changed) = (notes + before.Length, changed + before.Except(after).Count());
            }
        }

        // a few in a hundred, from 1% to 5% over these songs and drums
        await Assert.That(changed / (double)notes).IsLessThan(0.06);
        // (14 to 26 of the 32 over these songs and drums, the rest bringing their parts in by the drums or soloing on them)
        await Assert.That(compared).IsGreaterThanOrEqualTo(12);
    }

    [Test]
    public async Task NoDrums_OrADrumUnknown_IsRefused()
    {
        await Assert.That(() => SongGenerator.GenerateSong(0, new SongOverrides(DrumPieces: []), CancellationToken.None)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => SongGenerator.GenerateSong(0, new SongOverrides(DrumPieces: ["gong"]), CancellationToken.None)).Throws<ArgumentOutOfRangeException>();
    }
}
