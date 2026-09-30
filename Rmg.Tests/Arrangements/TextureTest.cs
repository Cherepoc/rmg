using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Tests.Arrangements;

public sealed class TextureTest
{
    private static readonly TrackRole[] Band = [TrackRole.Melody, TrackRole.Chords, TrackRole.Bass, TrackRole.Pad, TrackRole.Drum];

    [Test]
    public async Task ThePlainestSections_PlaySteady_AndTheWildest_EveryTextureAlike()
    {
        var context = new GenerationContext(1);
        var plain = Enumerable.Range(0, 2000).Select(_ => Texture.Draw(context, 0, 2, Band).Kind).ToArray();
        var wild = Enumerable.Range(0, 4000).Select(_ => Texture.Draw(context, 1, 2, Band).Kind).ToArray();

        await Assert.That(plain.All(x => x == TextureKind.Steady)).IsTrue();
        foreach (var kind in Enum.GetValues<TextureKind>())
            await Assert.That(wild.Count(x => x == kind) / (double)wild.Length).IsEqualTo(0.25).Within(0.03);
    }

    [Test]
    public async Task ABuild_BringsItsPitchedPartsIn_PhraseByPhrase_TheDrumsThroughout_AndAThinTakesThemOut()
    {
        var context = new GenerationContext(2);
        var draws = Enumerable.Range(0, 400).Select(_ => Texture.Draw(context, 1, 4, Band)).ToArray();
        foreach (var (kind, resting, silent) in draws.Where(x => x.Kind is TextureKind.Build or TextureKind.Thin))
        {
            var counts = silent.Select(x => x.Count).ToArray();
            var order = kind == TextureKind.Build ? counts : [..counts.Reverse()];

            await Assert.That(resting).IsEmpty();
            await Assert.That(order.Zip(order.Skip(1)).All(x => x.First >= x.Second)).IsTrue();
            await Assert.That(order[^1]).IsEqualTo(0);
            await Assert.That(order[0]).IsLessThan(4);
            await Assert.That(silent.All(x => !x.Contains(TrackRole.Drum))).IsTrue();
        }

        foreach (var (_, resting, _) in draws.Where(x => x.Kind == TextureKind.Alone))
            await Assert.That(resting.Count).IsEqualTo(Band.Length - 1);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var songs = TestCorpus.Range(256);
        var rows = songs.SelectMany(song => song.Map.Sections.Select(span => (Form: song.Trace.Where(x => x.Point == TracePoints.SectionUnconventionality && x.Section == span.SectionId).Select(x => ((Unconventionality)x.Value!)[Facet.Form]).First(), Texture: song.Texture(span), Resting: song.Resting(span), Song: song))).ToArray();
        Console.WriteLine("textures by the section's form facet, in fifths:");
        foreach (var band in rows.GroupBy(x => Math.Clamp((int)(x.Form * 5), 0, 4)).OrderBy(x => x.Key))
            Console.WriteLine($"  {band.Key}/5, {band.Count()} appearances: " + string.Join(", ", Enum.GetValues<TextureKind>().Select(k => $"{k} {band.Count(x => x.Texture.Kind == k) / (double)band.Count():P0}")));
        var alone = rows.Where(x => x.Texture.Kind == TextureKind.Alone).Select(x => x.Song.Song.TrackDefinitions.Values.Select(d => d.Role).Distinct().Single(r => !x.Resting.Contains(r) && r != TrackRole.Drum || r == TrackRole.Drum && !x.Resting.Contains(r))).GroupBy(x => x);
        Console.WriteLine($"alone: {string.Join(", ", alone.Select(x => $"{x.Key} {x.Count()}"))}");
        await Task.CompletedTask;
    }
}
