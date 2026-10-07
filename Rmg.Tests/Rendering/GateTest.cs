using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Rendering;

/// <summary>A trance gate (<see cref="StrongEffect.Gate" />): a held chord chopped in 16ths.</summary>
public sealed class GateTest
{
    private static readonly Genre Gating = new(ImmutableDictionary<string, ImmutableDictionary<string, ByConvention>>.Empty
        .Add(GenreChoices.StrongEffect.Name, ImmutableDictionary<string, ByConvention>.Empty.Add(GenreChoices.Name(nameof(StrongEffect.Gate)), new ByConvention(1, 1, 1))));

    [Test]
    public async Task AGatedPart_HoldsChords_AndSoundsAndFallsSilentOnlyOnTheGrid()
    {
        var gated = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Gating))))
        {
            var chopped = song.Rendered.Tracks.Where(x => x.Expression.Any(e => e.Value == 0)).ToArray();
            await Assert.That(chopped.Length).IsLessThanOrEqualTo(1);
            if (chopped.Length == 0)
                continue;
            gated++;
            await Assert.That(chopped[0].Role is TrackRole.Pad or TrackRole.Chords or TrackRole.Rhythm).IsTrue();
            // silent from 0.6 of a 16th, or from its start where it lets none through
            foreach (var (position, _) in chopped[0].Expression.Where(x => x.Value == 0))
                await Assert.That(new[] { 0, 0.15 }.Any(x => Math.Abs((position - x) / 0.25 - Math.Round((position - x) / 0.25)) < 1e-6)).IsTrue().Because($"seed {song.Seed} at {position}");
        }

        await Assert.That(gated).IsGreaterThan(10);
    }

    [Test]
    public async Task AGatedPart_ChangesItsChords_OnlyOnTheBarsBeats_InAnIntroToo()
    {
        var gated = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Gating))))
        foreach (var timeline in song.Song.Notes!.Values.Where(n => n.Any(y => y.Value.State.GetStateValue(CompositionStateKinds.Gate) != 0)))
        foreach (var section in song.Map.Sections.Prepend(new SectionSpan(-1, 0, song.Map.Sections[0].Start, song.Map.Sections[0].Meter)))
        foreach (var note in timeline.Where(x => x.Position >= section.Start - 1e-9 && x.Position < section.End - 1e-9))
        {
            gated++;
            var inBar = (note.Position - section.Start) % section.Meter.BarDuration;
            await Assert.That(section.Meter.Pulses.Any(x => Math.Abs(x - inBar) < 1e-6)).IsTrue().Because($"seed {song.Seed} at {note.Position}");
        }

        await Assert.That(gated).IsGreaterThan(100);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var (name, genre) in new[] { "trance", "electronic", "synthwave" }.Select(x => (x, GenrePresets.Genre(x))).Append(("every song gating", Gating)))
        {
            var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre)));
            var gated = songs.Where(x => x.Song.Notes!.Values.Any(n => n.Any(y => y.Value.State.GetStateValue(CompositionStateKinds.Gate) != 0))).ToArray();
            // in every section a gated part plays, from its first note to the section's end: how much of it sounds, and its
            // notes off the bar's beats, after a gap, ending inside a 16th's sounding part, single, and with a semitone in them
            var (span, sounding, notes, offBeat, gaps, cut, single, second) = (0.0, 0.0, 0, 0, 0, 0, 0, 0);
            foreach (var song in gated)
            foreach (var timeline in song.Song.Notes!.Values.Where(n => n.Any(y => y.Value.State.GetStateValue(CompositionStateKinds.Gate) != 0)))
            foreach (var section in song.Map.Sections)
            {
                var inSection = timeline.Where(x => x.Position >= section.Start - 1e-9 && x.Position < section.End - 1e-9).ToArray();
                if (inSection.Length == 0)
                    continue;
                span += section.End - inSection[0].Position;
                var reached = inSection[0].Position;
                foreach (var note in inSection)
                {
                    var end = Math.Min(note.Position + note.Value.Duration, section.End);
                    sounding += Math.Max(0, end - Math.Max(note.Position, reached));
                    if (note.Position > reached + 1e-6)
                        gaps++;
                    reached = Math.Max(reached, end);
                    var inBar = (note.Position - section.Start) % section.Meter.BarDuration;
                    if (!section.Meter.Pulses.Any(x => Math.Abs(x - inBar) < 1e-6))
                        offBeat++;
                    var inStep = (note.Position + note.Value.Duration) / 0.25 % 1 * 0.25;
                    if (inStep is > 1e-6 and < 0.15 - 1e-6)
                        cut++;
                    if (note.Value.Pitches.Length == 1)
                        single++;
                    var classes = note.Value.Pitches.Select(x => x % 12).ToHashSet();
                    if (classes.Any(x => classes.Contains((x + 1) % 12)))
                        second++;
                    notes++;
                }
            }

            Console.WriteLine($"{name}: gated {gated.Length / (double)songs.Length:P0}; of a gated part's section from its first note sounding {sounding / span:P0}, " +
                              $"of its {notes} notes {offBeat / (double)notes:P0} off the beat, {gaps / (double)notes:P0} after a gap, {cut / (double)notes:P0} ending inside a 16th's sound, {single / (double)notes:P0} a single note, {second / (double)notes:P0} a chord with a semitone");
        }

        await Task.CompletedTask;
    }
}
