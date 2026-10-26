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
    public async Task AGatedPart_SoundsAndFallsSilentOnlyOnTheSwungGrid_AndSoundsOnEveryBarsFirst16th()
    {
        var gated = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Gating))))
        {
            // a part's own track, not its double's, which a driven guitar's second take copies the gate to
            var chopped = song.Rendered.Tracks.Where(x => !x.IsDouble && x.Expression.Any(e => e.Value == 0)).ToArray();
            await Assert.That(chopped.Length).IsLessThanOrEqualTo(1);
            if (chopped.Length == 0)
                continue;
            gated++;
            await Assert.That(chopped[0].Role is TrackRole.Pad or TrackRole.Chords or TrackRole.Rhythm).IsTrue();
            // silent from 0.6 of a 16th, or from its start where it lets none through, as the song swings them, or from an
            // arpeggio's note's start, which falls where its rhythm puts it, in a 16th the gate lets none through
            var starts = song.Song.Notes!.Values.First(n => n.Any(y => y.Value.State.GetStateValue(CompositionStateKinds.Gate) != 0))
                .Select(x => song.Swing.Apply(x.Position)).ToArray();
            foreach (var (position, _) in chopped[0].Expression.Where(x => x.Value == 0))
            {
                var step = Math.Floor(position / 0.25);
                await Assert.That(new[] { step - 1, step, step + 1 }.Any(k => new[] { 0, 0.15 }.Any(x => Math.Abs(song.Swing.Apply(k * 0.25 + x) - position) < 1e-6))
                                  || starts.Any(x => Math.Abs(x - position) < 1e-6))
                    .IsTrue().Because($"seed {song.Seed} at {position}");
            }

            // sounding on every bar's first 16th it holds a note over
            var timeline = song.Song.Notes!.Values.First(n => n.Any(y => y.Value.State.GetStateValue(CompositionStateKinds.Gate) != 0));
            foreach (var section in song.Map.Sections)
            foreach (var note in timeline.Where(x => x.Position >= section.Start - 1e-9 && x.Position < section.End - 1e-9 && x.Value.State.GetStateValue(CompositionStateKinds.Arpeggio).Value == 0 && x.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 0))
            for (var bar = section.Start + Math.Ceiling((note.Position - section.Start - 1e-9) / section.Meter.BarDuration) * section.Meter.BarDuration;
                 bar < note.Position + note.Value.Duration - 1e-9;
                 bar += section.Meter.BarDuration)
                // sounding, a pump ducking it a little
                await Assert.That(chopped[0].Expression.Last(x => x.Position <= song.Swing.Apply(bar) + 1e-6).Value).IsGreaterThan(0).Because($"seed {song.Seed} at {bar}");
        }

        // measured 16 of 16, at offsets 0 and a million
        Console.WriteLine($"{gated} of 16 songs gated");
        await Assert.That(gated).IsGreaterThan(10);
    }

    [Test]
    public async Task AGatedPart_ChangesItsHeldChords_OnlyOnTheBarsBeats_AnArpeggioPlayingAsItFalls()
    {
        var gated = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Gating))))
        foreach (var timeline in song.Song.Notes!.Values.Where(n => n.Any(y => y.Value.State.GetStateValue(CompositionStateKinds.Gate) != 0)))
        foreach (var section in song.Map.Sections)
        foreach (var note in timeline.Where(x => x.Position >= section.Start - 1e-9 && x.Position < section.End - 1e-9 && x.Value.State.GetStateValue(CompositionStateKinds.Arpeggio).Value == 0 && x.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 0))
        {
            gated++;
            var inBar = (note.Position - section.Start) % section.Meter.BarDuration;
            await Assert.That(section.Meter.Pulses.Any(x => Math.Abs(x - inBar) < 1e-6)).IsTrue().Because($"seed {song.Seed} at {note.Position}");
        }

        await Assert.That(gated).IsGreaterThan(100);
    }

    /// <summary>
    ///     A gate chops the parts its genre has it chop alone (<see cref="Expression.GateParts" />): a genre's riff, its held
    ///     chord stuttering, never its chords; Djent's pad, never its riff, of Djent songs whose strong effect is the gate, as
    ///     few of its songs draw it (none of 64 at one corpus); the songs' held chords, never a riff.
    /// </summary>
    [Test]
    public async Task AGate_ChopsOnlyThePartsItsGenreNames()
    {
        var riffGating = Gating.Choices.Add(GenreChoices.GateParts.Name, ImmutableDictionary<string, ByConvention>.Empty.Add(GenreChoices.Name(nameof(TrackRole.Riff)), new ByConvention(1, 1, 1)));
        IEnumerable<TrackRole> Gated(Genre genre, int count) => TestCorpus.InParallel(Enumerable.Range(0, count), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre)))
            .SelectMany(song => song.Song.Notes!.Where(x => x.Value.Any(n => n.Value.State.GetStateValue(CompositionStateKinds.Gate) != 0)).Select(x => song.Song.TrackDefinitions[x.Key].Role));

        var riffs = Gated(new Genre(riffGating), 16).ToArray();
        var djent = Gated(GenrePresets.With("djent", GenreChoices.StrongEffect, ("gate", 1)), 16).ToArray();
        var songs = Gated(Gating, 16).ToArray();
        Console.WriteLine($"of 16 songs gating the riff, {riffs.Length} gated; of 16 gating Djent songs, {djent.Length} gating their pad");
        await Assert.That(riffs).IsNotEmpty();
        await Assert.That(djent).IsNotEmpty();
        await Assert.That(songs).IsNotEmpty();
        await Assert.That(riffs.All(x => x is TrackRole.Riff or TrackRole.RiffTwin)).IsTrue();
        await Assert.That(djent.All(x => x == TrackRole.Pad)).IsTrue();
        await Assert.That(songs.All(x => x is TrackRole.Pad or TrackRole.Chords or TrackRole.Rhythm)).IsTrue();
    }

    /// <summary>
    ///     A gated chord on an instrument that dies away as it rings is struck again, softer, on every bar line it holds
    ///     across, and one that sustains, a pad's, is held: no held chord of a decaying instrument sounds across a bar line.
    /// </summary>
    [Test]
    public async Task AGatedChord_OnADecayingInstrument_IsStruckAgainOnEveryBarLine()
    {
        var (restruck, held) = (0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Gating))))
        foreach (var (track, timeline) in song.Song.Notes!.Where(x => x.Value.Any(n => n.Value.State.GetStateValue(CompositionStateKinds.Gate) != 0)))
        foreach (var section in song.Map.Sections)
        foreach (var note in timeline.Where(x => x.Position >= section.Start - 1e-9 && x.Position < section.End - 1e-9 && x.Value.State.GetStateValue(CompositionStateKinds.Arpeggio).Value == 0 && x.Value.State.GetStateValue(CompositionStateKinds.LineSolo) == 0))
        {
            var program = InstrumentsOverTime.ProgramOf(note.Value.State, ((PitchInstrumentTrack)song.Song.TrackDefinitions[track]).InstrumentCode);
            var bar = section.Meter.BarDuration;
            var crosses = Math.Floor((note.Position - section.Start + 1e-6) / bar) != Math.Floor((note.Position + note.Value.Duration - section.Start - 1e-6) / bar);
            if (!InstrumentTraits.Has(program, InstrumentTrait.Decays))
            {
                held += crosses ? 1 : 0;
                continue;
            }

            await Assert.That(crosses).IsFalse().Because($"seed {song.Seed} at {note.Position}");
            restruck += Math.Abs((note.Position - section.Start) % bar) < 1e-6 && timeline.Any(x => Math.Abs(x.Position + x.Value.Duration - note.Position) < 1e-6
                                                                                                && x.Value.Pitches.SequenceEqual(note.Value.Pitches)) ? 1 : 0;
        }

        Console.WriteLine($"{restruck} chords struck again on a bar line, {held} sustained chords held across one");
        await Assert.That(restruck).IsGreaterThan(0);
        await Assert.That(held).IsGreaterThan(0);
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
            // and its arpeggios' notes apart, how many of them a 16th after the one before
            var (arpeggiated, sixteenths) = (0, 0);
            foreach (var song in gated)
            foreach (var timeline in song.Song.Notes!.Values.Where(n => n.Any(y => y.Value.State.GetStateValue(CompositionStateKinds.Gate) != 0)))
            foreach (var section in song.Map.Sections)
            {
                var inSection = timeline.Where(x => x.Position >= section.Start - 1e-9 && x.Position < section.End - 1e-9).ToArray();
                if (inSection.Length == 0)
                    continue;
                span += section.End - inSection[0].Position;
                var reached = inSection[0].Position;
                var arpeggio = inSection.Where(x => x.Value.State.GetStateValue(CompositionStateKinds.Arpeggio).Value != 0).ToArray();
                arpeggiated += arpeggio.Length;
                sixteenths += arpeggio.Skip(1).Where((x, i) => Math.Abs(x.Position - arpeggio[i].Position - 0.25) < 1e-6).Count();
                foreach (var note in inSection.Except(arpeggio))
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

            // as it is rendered: the bars' first 16ths a held note falls silent on, and the 16ths it sounds from off the song's
            // swung 16ths
            var (downbeats, silent, openings, offGrid) = (0, 0, 0, 0);
            foreach (var song in gated)
            {
                var track = song.Rendered.Tracks.First(x => !x.IsDouble && x.Expression.Any(e => e.Value == 0));
                var timeline = song.Song.Notes!.Values.First(n => n.Any(y => y.Value.State.GetStateValue(CompositionStateKinds.Gate) != 0));
                foreach (var section in song.Map.Sections)
                foreach (var note in timeline.Where(x => x.Position >= section.Start - 1e-9 && x.Position < section.End - 1e-9))
                for (var bar = section.Start + Math.Ceiling((note.Position - section.Start - 1e-9) / section.Meter.BarDuration) * section.Meter.BarDuration;
                     bar < note.Position + note.Value.Duration - 1e-9;
                     bar += section.Meter.BarDuration)
                {
                    var at = song.Swing.Apply(bar);
                    downbeats++;
                    if (track.Expression.LastOrDefault(x => x.Position <= at + 1e-6).Value == 0)
                        silent++;
                }

                foreach (var (position, _) in track.Expression.Where(x => x.Value == 1))
                {
                    openings++;
                    var step = Math.Floor(position / 0.25);
                    if (!new[] { step - 1, step, step + 1 }.Any(k => Math.Abs(song.Swing.Apply(k * 0.25) - position) < 1e-6))
                        offGrid++;
                }
            }

            Console.WriteLine($"{name}: of a held note's bars' first 16ths {silent / (double)downbeats:P0} silent; of the 16ths it sounds from {offGrid / (double)openings:P0} off the swung 16ths");
            Console.WriteLine($"{name}: gated {gated.Length / (double)songs.Length:P0}; of a gated part's section from its first note sounding {sounding / span:P0}, " +
                              $"of its {notes} notes {offBeat / (double)notes:P0} off the beat, {gaps / (double)notes:P0} after a gap, {cut / (double)notes:P0} ending inside a 16th's sound, {single / (double)notes:P0} a single note, {second / (double)notes:P0} a chord with a semitone; " +
                              $"arpeggio notes {arpeggiated}, a 16th after the one before {sixteenths / (double)Math.Max(1, arpeggiated):P0}");
        }

        await Task.CompletedTask;
    }
}
