using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Tests.Rendering;

/// <summary>A synth's filter moving (<see cref="StrongEffect.Wobble" />, <see cref="StrongEffect.Squelch" />, <see cref="StrongEffect.Opening" />).</summary>
public sealed class FilterTest
{
    /// <summary>A genre whose songs play the one strong effect given, and no part's tone nor the mix filtered, which would move the brightness of the others.</summary>
    internal static Genre Playing(StrongEffect effect) => new(ImmutableDictionary<string, ImmutableDictionary<string, ByConvention>>.Empty
        .Add(GenreChoices.StrongEffect.Name, ImmutableDictionary<string, ByConvention>.Empty.Add(GenreChoices.Name(effect.ToString()), new ByConvention(1, 1, 1)))
        .Add(GenreChoices.Effects.Name, ImmutableDictionary<string, ByConvention>.Empty.Add("tone", new ByConvention(0, 0, 0)))
        .Add(GenreChoices.MixFilter.Name, ImmutableDictionary<string, ByConvention>.Empty.Add("opening", new ByConvention(0, 0, 0))));

    private static CorpusSong[] Songs(StrongEffect effect) => TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Playing(effect))));

    [Test]
    [Arguments(StrongEffect.Wobble)]
    [Arguments(StrongEffect.Squelch)]
    [Arguments(StrongEffect.Opening)]
    public async Task AFilter_MovesOnePartOnASynth_ClosingBelowItsSound_AndNoOther(StrongEffect effect)
    {
        var (filtered, closing) = (0, 0);
        foreach (var song in Songs(effect))
        {
            var moved = song.Rendered.Tracks.Where(x => x.Controllers.Any(c => c.Controller == ExpressionRender.BrightnessController)).ToArray();
            await Assert.That(moved.Length).IsLessThanOrEqualTo(1);
            if (moved.Length == 0)
                continue;
            filtered++;
            var track = moved[0];
            await Assert.That(InstrumentTraits.Has(track.PitchInstrumentCode, InstrumentTrait.Filters)).IsTrue();
            var values = track.Controllers.Where(c => c.Controller == ExpressionRender.BrightnessController).Select(c => c.Value).ToArray();
            await Assert.That(values.All(x => x is >= 40 and <= 64)).IsTrue();
            // closed where it plays, but where an opening's part comes in after it has opened
            closing += values.Min() < 52 ? 1 : 0;
        }

        await Assert.That(filtered).IsGreaterThan(8);
        await Assert.That(closing).IsGreaterThan(filtered / 2);
    }

    [Test]
    public async Task ASquelch_OpensOnEveryNote_AndAWobble_ClosesAndOpensWithinACycle()
    {
        foreach (var effect in new[] { StrongEffect.Squelch, StrongEffect.Wobble })
        foreach (var song in Songs(effect))
        foreach (var track in song.Rendered.Tracks.Where(x => x.Controllers.Any(c => c.Controller == ExpressionRender.BrightnessController)))
        {
            var curve = track.Controllers.Where(c => c.Controller == ExpressionRender.BrightnessController).ToArray();
            // its notes in the sections, before the ending, which plays no effect
            var notes = track.NoteTimeline.Where(x => x.Position < song.Swing.Apply(song.Map.Sections[^1].End) - 1e-9).ToArray();
            if (effect == StrongEffect.Squelch)
                // open at its start, which an ornament's quick notes play ahead of
                foreach (var note in notes.Where(x => x.Value.Duration >= 0.5))
                    await Assert.That(curve.Any(c => c.Value == 64 && c.Position <= note.Position + 1e-6 && c.Position > note.Position - 0.5)).IsTrue()
                        .Because($"seed {song.Seed} {track.Role} at {note.Position} for {note.Value.Duration}: {string.Join(" ", curve.Where(c => Math.Abs(c.Position - note.Position) < 1).Select(c => $"{c.Position:F3}:{c.Value}"))}");
            else
                // a cycle of a quarter at the longest: its curve both closed and open within any beat a note holds
                foreach (var note in notes.Where(x => x.Value.Duration >= 1))
                {
                    var within = curve.Where(c => c.Position >= note.Position - 1e-9 && c.Position < note.Position + 1).Select(c => c.Value).ToArray();
                    await Assert.That(within.Max() - within.Min()).IsGreaterThan(6);
                }
        }
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var genre in new[] { "techno", "trance", "electronic", "dnb", "synthwave" })
        {
            var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Genres.GenrePresets.Genre(genre))));
            var kinds = songs.Select(x => x.Song.Notes!.Values.SelectMany(n => n).Select(n => (StrongEffect)n.Value.State.GetStateValue(CompositionStateKinds.Filter)).FirstOrDefault(k => k != StrongEffect.None)).ToArray();
            Console.WriteLine($"{genre}: filtered {kinds.Count(x => x != StrongEffect.None) / (double)songs.Length:P0}: " +
                              string.Join(", ", kinds.Where(x => x != StrongEffect.None).GroupBy(x => x).Select(x => $"{x.Key} {x.Count() / (double)songs.Length:P0}")));
        }

        await Task.CompletedTask;
    }
}
