using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Drums;

/// <summary>A genre's figures (<see cref="Figures" />): a player's rhythm in a section given by its figure, the kick with the riff, a double kick, a clave.</summary>
public sealed class FiguresTest
{
    /// <summary>A figure at a steady step on its meter's pulses where the step is a quarter or more, 6/8's boom on 1 and 4, its 8ths every 8th.</summary>
    [Test]
    public async Task AStepOfAQuarterOrMore_StrikesOnTheMetersPulses()
    {
        double[] In(string name, FigurePlayer player, Meter meter) => [..Figures.Of(player, name)!.StepsIn(meter)];
        await Assert.That(In("halves", FigurePlayer.Bass, Meter.FourFour)).IsEquivalentTo([0.0, 2]);
        await Assert.That(In("halves", FigurePlayer.Bass, Meter.SixEight)).IsEquivalentTo([0.0, 1.5]);
        await Assert.That(In("halves", FigurePlayer.Bass, Meter.TwelveEight)).IsEquivalentTo([0.0, 3]);
        await Assert.That(In("halves", FigurePlayer.Bass, Meter.ThreeFour)).IsEquivalentTo([0.0, 2]);
        await Assert.That(In("fourOnTheFloor", FigurePlayer.Ground, Meter.FourFour)).IsEquivalentTo([0.0, 1, 2, 3]);
        await Assert.That(In("fourOnTheFloor", FigurePlayer.Ground, Meter.SixEight)).IsEquivalentTo([0.0, 1.5]);
        await Assert.That(In("eighths", FigurePlayer.Bass, Meter.SixEight)).IsEquivalentTo([0.0, 0.5, 1, 1.5, 2, 2.5]);
    }

    /// <summary>How many sections play a figure of a quarter's step or more in a meter whose pulses are not its quarters, which the step's 16ths missed.</summary>
    [Test]
    [Explicit]
    public async Task ReportStepsOnPulses()
    {
        foreach (var genre in new[] { "celtic", "folk", "bluegrass", "country", "symphonic" })
        {
            var counts = TestCorpus.InParallel(Enumerable.Range(0, 64), seed =>
            {
                var song = TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre)));
                var figures = song.Trace.Where(x => x.Point == TracePoints.Figures).ToDictionary(x => x.Section, x => (ImmutableDictionary<FigurePlayer, string>)x.Value!);
                return song.Map.Sections.Count(span => figures.TryGetValue(span.SectionId, out var played)
                                                       && !span.Meter.Pulses.SequenceEqual(Enumerable.Range(0, span.Meter.Pulses.Length).Select(x => (double)x))
                                                       && played.Any(x => Figures.Of(x.Key, x.Value) is { Step: >= 4 }));
            });
            Console.WriteLine($"{genre}: {counts.Sum()} sections in a meter of other pulses playing a quarter's step or more, in {counts.Count(x => x > 0)} of 64 songs");
        }

        await Task.CompletedTask;
    }

    /// <summary>
    ///     A loose figure (<see cref="Figures.Loosen" />) at the plain end's strictness is the figure exactly, its onsets kept and
    ///     the player's own left out; and at any strictness a place both strike is kept.
    /// </summary>
    [Test]
    public async Task ALooseFigure_AtThePlainEnd_IsTheFigureExactly_AndKeepsWhereBothStrike()
    {
        foreach (var seed in Enumerable.Range(0, 256))
        {
            var context = new GenerationContext((ulong)seed);
            double[] own = [..Enumerable.Range(0, 16).Where(_ => context.GenerateDouble() < 0.4).Select(x => x * 0.25)];
            double[] onsets = [..Enumerable.Range(0, 16).Where(_ => context.GenerateDouble() < 0.3).Select(x => x * 0.25)];
            await Assert.That(Figures.Loosen(own, onsets, Figures.Strictness.At(0), new GenerationContext((ulong)seed))).IsEquivalentTo(onsets, CollectionOrdering.Matching).Because($"seed {seed}");
            foreach (var strictness in new[] { 0, 0.5, 1 })
            {
                var loose = Figures.Loosen(own, onsets, Figures.Strictness.At(strictness), new GenerationContext((ulong)seed));
                await Assert.That(own.Intersect(onsets).All(loose.Contains)).IsTrue().Because($"seed {seed} at {strictness}");
            }
        }
    }

    /// <summary>
    ///     The figures a player varies (<see cref="Figure.Loose" />): those across a half bar or a bar, but a clave, the swing
    ///     ride and the waltz kept exact, a figure that follows another player, and a pulse the same every beat.
    /// </summary>
    [Test]
    public async Task TheLooseFigures_AreThePatternsAcrossTheBeat_NotAClaveAFollowerOrAPulse()
    {
        var loose = Figures.All.Where(x => x.Loose).Select(x => $"{x.Player} {x.Name}").Order().ToArray();
        await Assert.That(loose).IsEquivalentTo(
            ["Backbeat breakbeat", "Bass bossaBass", "Bass halves", "Bass tresillo", "Bass tumbao", "Chords backbeat", "Chords charleston", "Chords sixEight", "Ground breakbeat", "Ground surdo", "Ground twoStep"],
            CollectionOrdering.Matching);
    }

    /// <summary>
    ///     A name two players' figures share is a figure they play together (<see cref="Figures" />: an earlier player's
    ///     figure is a later one's where it has one of the name), so a name is shared only where meant: the blast, the
    ///     breakbeat and the waltz.
    /// </summary>
    [Test]
    public async Task AFiguresName_IsSharedByPlayers_OnlyWhereTheyPlayItTogether()
    {
        var shared = Figures.All.GroupBy(x => x.Name).Where(x => x.Select(f => f.Player).Distinct().Count() > 1).Select(x => x.Key).Order().ToArray();
        await Assert.That(shared).IsEquivalentTo(["blast", "breakbeat", "waltz"], CollectionOrdering.Matching);
    }

    /// <summary>The sections, by their ids, whose player plays the figure named, but a drum solo's, whose drums the solo plays.</summary>
    private static ImmutableHashSet<int> Playing(CorpusSong song, FigurePlayer player, string figure) =>
    [
        ..song.Trace.Where(t => t.Point == TracePoints.Figures && ((ImmutableDictionary<FigurePlayer, string>)t.Value!).GetValueOrDefault(player) == figure).Select(t => t.Section)
            .Except(song.Trace.Where(t => t.Point == TracePoints.DrumSolo).Select(t => t.Section))
    ];

    /// <summary>
    ///     Every hit of the tracks given in the sections given, but a phrase's last bar and the bar before any line the drums
    ///     mark, an intro's window's end too, where a fill may take the drums, and a phrase's first beat, where the fill into
    ///     it lands, with its section and its bar's start.
    /// </summary>
    /// <param name="window">Whether an intro's window before the first section, which plays its first bars, is walked too.</param>
    private static IEnumerable<(SectionSpan Span, double Bar, double Position)> Hits(CorpusSong song, IReadOnlySet<int> tracks, IReadOnlySet<int> sections, bool window = false)
    {
        // the lines the drums mark, an intro's window's end among them, where a fill may take the bar before
        var lines = song.FillLines();
        bool OnLine(double position) => lines.Any(l => Math.Abs(l - position) < 1e-6);
        return (window ? song.Map.PlayedSections : song.Map.Sections).Where(x => sections.Contains(x.SectionId)).SelectMany(span => song.Song.Notes!.Where(x => tracks.Contains(x.Key)).SelectMany(x => x.Value)
            .Where(x => x.Position > span.Start + 1e-9 && x.Position < span.End - 1e-9
                        && Math.Abs(Math.IEEERemainder(x.Position - span.Start, span.Meter.PhraseDuration)) > 1e-6 && !OnLine(x.Position))
            .Select(x => (span, Bar: span.Start + Math.Floor((x.Position - span.Start) / span.Meter.BarDuration + 1e-9) * span.Meter.BarDuration, x.Position))
            .Where(x => Math.Abs(Math.IEEERemainder(x.Bar + span.Meter.BarDuration - span.Start, span.Meter.PhraseDuration)) > 1e-6 && !OnLine(x.Bar + span.Meter.BarDuration)));
    }

    /// <summary>Every figure of fixed onsets, by its player and its name, but the pair of claves whose sides a test of their own checks.</summary>
    public static IEnumerable<(FigurePlayer Player, string Name)> Fixed() =>
        Figures.All.Where(x => x.Follows is null && x.Player != FigurePlayer.Claves).Select(x => (x.Player, x.Name));

    /// <summary>
    ///     A calibration: a figure of fixed onsets laid in a section, its player's hits there on the figure's 16ths, most
    ///     of them, but where a fill takes the drums or a line walks (measured over 32 at first, every figure 61% to 100%, the
    ///     waltz's bass, the chick on 2 and 4 and the bossa nova's clave the fewest, walking or filling between).
    /// </summary>
    /// <summary>A figure's meter as a genre names it, four's where it has none.</summary>
    private static string MeterName(Meter? meter) => meter == Meter.ThreeFour ? "3/4" : meter == Meter.SixEight ? "6/8" : "4/4";

    [Test]
    [MethodDataSource(nameof(Fixed))]
    public async Task AFigure_IsPlayedOnItsSixteenths(FigurePlayer player, string name)
    {
        var figure = Figures.All.Single(x => x.Player == player && x.Name == name);
        var preset = player switch { FigurePlayer.Riff => "metal", _ => "rock" };
        var genre = GenrePresets.With(preset, GenreChoices.Figures[player], (name, 1));
        genre = genre with
        {
            Choices = genre.Choices.SetItem(GenreChoices.Meter.Name, ImmutableDictionary<string, Rmg.Core.Probabilities.ByConvention>.Empty.Add(MeterName(figure.In), new(1, 1, 1)))
        };
        var onsets = figure.Onsets.Select(x => x % 16).ToHashSet();
        var (hits, on) = (0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre))))
        {
            var tracks = player switch
            {
                FigurePlayer.Ground => DrumGroups.Kick.Tracks.ToHashSet(),
                FigurePlayer.Backbeat => DrumGroups.Snare.Tracks.ToHashSet(),
                FigurePlayer.Time => DrumGroups.Timekeepers.Tracks.ToHashSet(),
                FigurePlayer.Riff => [song.Song.Track(TrackRole.Riff)],
                FigurePlayer.Chords => [song.Song.Track(TrackRole.Chords)],
                _ => [song.Song.Track(TrackRole.Bass)]
            };
            foreach (var (_, bar, position) in Hits(song, tracks, Playing(song, player, name)))
            {
                hits++;
                on += onsets.Contains((int)Math.Round((position - bar) * 4) % figure.Sixteenths % 16) ? 1 : 0;
            }
        }

        Console.WriteLine($"{player} {name}: {on} of {hits} hits on its 16ths, {(hits == 0 ? 0 : on / (double)hits):P0}");
        await Assert.That(hits).IsGreaterThan(0);
        await Assert.That(on / (double)hits).IsGreaterThan(0.5);
    }

    /// <summary>
    ///     A calibration: the kick follows the riff's pattern before the lines are placed, which leave a few of the riff's
    ///     notes out after it, so that nearly all its hits are on the riff's notes as they end up.
    /// </summary>
    [Test]
    public async Task AKickWithTheRiff_StrikesNearlyOnlyWhereTheRiffsNotesStart()
    {
        var kicks = DrumGroups.Kick.Drums.Select(DrumGroups.GetTrackNumber).ToHashSet();
        var (hits, on) = (0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("nuMetal")))))
        {
            // the riff where it plays, else the bass
            var leader = song.Song.TrackDefinitions.Where(x => x.Value.Role == TrackRole.Riff && song.Song.Notes!.GetValueOrDefault(x.Key)?.Count > 0).Select(x => (int?)x.Key).FirstOrDefault()
                         ?? song.Track(TrackRole.Bass);
            // the riff's notes as its pattern places them, before a held note takes the next into it
            var onsets = song.Song.TrackEventStateTimelineMap.TrackTimelineMap[leader].EventTimeline.Select(x => x.Position).ToArray();
            // an intro's window too, which plays the first section's first bars as it does
            foreach (var (span, bar, position) in Hits(song, kicks, Playing(song, FigurePlayer.Ground, "withRiff"), window: true))
            {
                // but in a bar the riff rests in, where the kick plays its own
                if (!onsets.Any(x => x >= bar - 1e-9 && x < bar + span.Meter.BarDuration - 1e-9))
                    continue;
                hits++;
                // as the notes are placed, an onset a pocket's offset away at most
                if (onsets.Any(x => Math.Abs(x - position) < 0.13))
                    on++;
            }
        }

        Console.WriteLine($"of {hits} hits of a kick with the riff, {on / (double)hits:P1} on the riff's notes");
        await Assert.That(hits).IsGreaterThan(50);
        await Assert.That(on / (double)hits).IsGreaterThan(0.8);
    }

    [Test]
    public async Task ADoubleKick_StrikesOnlyOnTheSixteenths()
    {
        var kicks = DrumGroups.Kick.Drums.Select(DrumGroups.GetTrackNumber).ToHashSet();
        var hits = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("metal")))))
        foreach (var (_, bar, position) in Hits(song, kicks, Playing(song, FigurePlayer.Ground, "doubleKick")))
        {
            hits++;
            var sixteenth = (position - bar) / 0.25;
            await Assert.That(Math.Abs(sixteenth - Math.Round(sixteenth))).IsLessThan(0.1).Because($"seed {song.Seed} at {position}");
        }

        await Assert.That(hits).IsGreaterThan(50);
    }

    [Test]
    public async Task ACymbalRidingABlast_StrikesOnTheEighths()
    {
        var keepers = DrumGroups.Timekeepers.Drums.Select(DrumGroups.GetTrackNumber).ToHashSet();
        var (hits, on) = (0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("blackMetal")))))
        foreach (var (_, bar, position) in Hits(song, keepers, Playing(song, FigurePlayer.Time, "blast")))
        {
            hits++;
            var eighth = (position - bar) / 0.5;
            on += Math.Abs(eighth - Math.Round(eighth)) < 0.1 ? 1 : 0;
        }

        Console.WriteLine($"of {hits} hits of a cymbal riding a blast, {on / (double)hits:P1} on the 8ths");
        await Assert.That(hits).IsGreaterThan(50);
        await Assert.That(on).IsEqualTo(hits);
    }

    /// <summary>A clave strikes only on its onsets: Latin's on the claves, Bossa nova's on the snare's stroke, its cross-stick.</summary>
    [Test]
    [Arguments("latin", FigurePlayer.Claves, new[] { "clave32", "clave23" }, null)]
    [Arguments("latin", FigurePlayer.Claves, new[] { "bell68" }, "6/8")]
    [Arguments("bossa", FigurePlayer.Backbeat, new[] { "bossaClave" }, null)]
    public async Task AClave_StrikesOnlyOnItsThreeAndTwo(string genre, FigurePlayer player, string[] names, string? meter)
    {
        // a figure of another meter's songs given it
        var given = GenrePresets.Genre(genre);
        if (meter is not null)
            given = given with { Choices = given.Choices.SetItem(GenreChoices.Meter.Name, ImmutableDictionary<string, Rmg.Core.Probabilities.ByConvention>.Empty.Add(meter, new(1, 1, 1))) };
        IReadOnlySet<int> tracks = player == FigurePlayer.Claves
            ? new HashSet<int> { DrumGroups.GetTrackNumber(DrumDefinitions.Claves) }
            : DrumGroups.Snare.Drums.Select(DrumGroups.GetTrackNumber).ToHashSet();
        var (hits, sections, playing) = (0, 0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: given))))
        {
            sections += song.Trace.Count(t => t.Point == TracePoints.Figures);
            playing += names.Sum(name => Playing(song, player, name).Count);
        }

        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: given))))
        foreach (var name in names)
        {
            var figure = Figures.All.Single(x => x.Player == player && x.Name == name);
            // the bars before each section, an intro's window's among them, as a clave's side runs on across them
            var played = song.Map.PlayedSections.ToArray();
            var barsBefore = played.Select((x, i) => (x, Bars: played.Take(i).Sum(y => y.BarCount))).ToDictionary(x => x.x, x => x.Bars);
            foreach (var (span, bar, position) in Hits(song, tracks, Playing(song, player, name)))
            {
                hits++;
                // its 16th in the figure's bars, one or two, counted from the song's first bar
                var index = barsBefore[span] + span.BarOf(bar);
                var sixteenth = (position - bar) / 0.25 + index % (figure.Sixteenths / span.Meter.Sixteenths) * span.Meter.Sixteenths;
                await Assert.That(figure.Onsets.Any(x => Math.Abs(x - sixteenth) < 0.1)).IsTrue().Because($"seed {song.Seed} at {position}, {name}");
            }
        }

        Console.WriteLine($"{genre}: {playing} of {sections} sections play a clave, {hits} hits");
        await Assert.That(hits).IsGreaterThan(30);
    }

    /// <summary>
    ///     A calibration: a rolling bass plays the 16ths after the kick, off the beat, but for the notes the stages after
    ///     the figures place, a line's pickup or a push: 92% of its notes (October 2026).
    /// </summary>
    [Test]
    public async Task ARollingBass_PlaysOffTheBeat()
    {
        var (notes, off) = (0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("trance")))))
        {
            var bass = song.Track(TrackRole.Bass);
            var rolling = Playing(song, FigurePlayer.Bass, "rolling");
            foreach (var span in song.Map.Sections.Where(x => rolling.Contains(x.SectionId)))
            foreach (var note in song.Song.Notes!.GetValueOrDefault(bass)?.Where(x => x.Position >= span.Start - 1e-9 && x.Position < span.End - 1e-9) ?? [])
            {
                notes++;
                off += Math.Abs(note.Position - Math.Round(note.Position)) > 1e-6 ? 1 : 0;
            }
        }

        Console.WriteLine($"of {notes} notes of a rolling bass, {off / (double)notes:P1} off the beat");
        await Assert.That(notes).IsGreaterThan(200);
        await Assert.That(off / (double)notes).IsGreaterThan(0.8);
    }

    /// <summary>
    ///     A calibration: a tumbao's, a bossa nova's or a tresillo's bass strikes on its figure's 16ths, but for the notes the stages after
    ///     the figures place, a line's pickup or a push.
    /// </summary>
    [Test]
    [Arguments("latin", "tumbao")]
    [Arguments("bossa", "bossaBass")]
    [Arguments("jazzFunk", "tresillo")]
    public async Task ALatinBass_StrikesOnItsFiguresSixteenths(string genre, string name)
    {
        var figure = Figures.All.Single(x => x.Player == FigurePlayer.Bass && x.Name == name);
        var (notes, on) = (0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre)))))
        {
            var bass = song.Track(TrackRole.Bass);
            var playing = Playing(song, FigurePlayer.Bass, name);
            foreach (var span in song.Map.Sections.Where(x => playing.Contains(x.SectionId) && x.Meter.HasSameBar(Meter.FourFour)))
            foreach (var note in song.Song.Notes!.GetValueOrDefault(bass)?.Where(x => x.Position >= span.Start - 1e-9 && x.Position < span.End - 1e-9) ?? [])
            {
                notes++;
                var sixteenth = (note.Position - span.Start) % 4 / 0.25;
                on += figure.Onsets.Any(x => Math.Abs(x - sixteenth) < 0.1) ? 1 : 0;
            }
        }

        Console.WriteLine($"{genre}: of {notes} notes of its {name}, {on / (double)Math.Max(1, notes):P1} on its 16ths");
        // measured 72% (Latin's, its pushes an eighth early and its pickups the rest), 96% (Bossa nova's) and 96% (Jazz-funk's
        // tresillo), October 2026
        await Assert.That(notes).IsGreaterThan(200);
        await Assert.That(on / (double)notes).IsGreaterThan(0.6);
    }

    /// <summary>
    ///     A calibration: a waltz, a Country song's in three, its chords striking on 2 and 3 and its bass on 1, but for the
    ///     notes the stages after the figures place, a line's pickup or a push; and in no bar of four.
    /// </summary>
    [Test]
    public async Task AWaltz_StrikesItsChordsOn2And3_AndItsBassOn1()
    {
        var (notes, on, waltzes) = (0, 0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("country"), Meter: Meter.ThreeFour))))
        {
            var playing = Playing(song, FigurePlayer.Chords, "waltz");
            foreach (var span in song.Map.Sections.Where(x => playing.Contains(x.SectionId)))
            {
                await Assert.That(span.Meter.Groups.SequenceEqual(Meter.ThreeFour.Groups)).IsTrue().Because($"seed {song.Seed} section {span.SectionId}");
                waltzes++;
                foreach (var (role, onsets) in new[] { (TrackRole.Chords, new[] { 4, 8 }), (TrackRole.Bass, new[] { 0 }) })
                foreach (var note in song.Song.Notes!.GetValueOrDefault(song.Track(role))?.Where(x => x.Position >= span.Start - 1e-9 && x.Position < span.End - 1e-9) ?? [])
                {
                    notes++;
                    var sixteenth = (note.Position - span.Start) % span.Meter.BarDuration / 0.25;
                    on += onsets.Any(x => Math.Abs(x - sixteenth) < 0.1) ? 1 : 0;
                }
            }
        }

        Console.WriteLine($"{waltzes} waltzing sections, {on / (double)Math.Max(1, notes):P1} of their {notes} notes on the waltz's beats");
        // measured 79.3% of 2266 notes in 58 sections of 32 songs, October 2026; 79.3% to 80.2% over 64 at offsets 0 and a million
        await Assert.That(waltzes).IsGreaterThan(10);
        await Assert.That(on / (double)notes).IsGreaterThan(0.6);
    }

    [Test]
    public async Task AFigureSeveralPlayersPlay_IsPlayedByAll_ABlastsKickWithItsSnareAndItsCymbal_ButInHalfOrDoubleTime()
    {
        var (blasts, alone) = (0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("blackMetal")))))
        foreach (var entry in song.Trace.Where(t => t.Point == TracePoints.Figures))
        {
            var figures = (ImmutableDictionary<FigurePlayer, string>)entry.Value!;
            if (figures.GetValueOrDefault(FigurePlayer.Ground) != "blast")
                continue;
            // in its own time with them, and in half or double time the kick's 8ths alone, under the backbeat it stretches
            var stretched = (int)song.Trace.First(t => t.Point == TracePoints.TimeFeel && t.Section == entry.Section).Value! != 0;
            (blasts, alone) = (blasts + (stretched ? 0 : 1), alone + (stretched ? 1 : 0));
            await Assert.That(figures.GetValueOrDefault(FigurePlayer.Backbeat) == "blast").IsEqualTo(!stretched).Because($"seed {song.Seed}");
            await Assert.That(figures.GetValueOrDefault(FigurePlayer.Time) == "blast").IsEqualTo(!stretched).Because($"seed {song.Seed}");
        }

        await Assert.That(blasts).IsGreaterThan(5);
        await Assert.That(alone).IsGreaterThan(0);
    }

    /// <summary>A section running a figure across its bar lines plays no figure, its cycle kept.</summary>
    [Test]
    public async Task APolymeterSection_PlaysNoFigureOfFixedOnsets_ButAKickWithItsRiff()
    {
        var polymeters = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("idm")))))
        {
            var plans = (ImmutableDictionary<int, Polymeter.Plan>)song.Trace.Single(x => x.Point == TracePoints.Polymeter).Value!;
            foreach (var id in plans.Keys)
            {
                polymeters++;
                var figures = (ImmutableDictionary<FigurePlayer, string>)song.Trace.Single(x => x.Point == TracePoints.Figures && x.Section == id).Value!;
                await Assert.That(figures.All(x => Figures.All.Any(f => f.Player == x.Key && f.Name == x.Value && f.Follows is not null))).IsTrue().Because($"seed {song.Seed} section {id}");
            }
        }

        await Assert.That(polymeters).IsGreaterThan(3);
    }

    /// <summary>A bass playing a figure keeps it: the riff is not doubled into it there (Techno's offbeat bass, listening's).</summary>
    [Test]
    public async Task ABassPlayingAFigure_IsNotDoubledWithTheRiff()
    {
        var (figured, doubled) = (0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("techno")))))
        {
            var withRiff = song.Trace.Where(t => t.Point == TracePoints.LineDoubling && ((int, TrackRole, TrackRole, int))t.Value! is (_, TrackRole.Riff, TrackRole.Bass, _))
                .Select(t => t.Section).ToHashSet();
            doubled += withRiff.Count;
            foreach (var section in song.Trace.Where(t => t.Point == TracePoints.Figures && ((ImmutableDictionary<FigurePlayer, string>)t.Value!).ContainsKey(FigurePlayer.Bass)).Select(t => t.Section))
            {
                figured++;
                await Assert.That(withRiff.Contains(section)).IsFalse().Because($"seed {song.Seed} section {section}");
            }
        }

        Console.WriteLine($"Techno: {figured} sections whose bass plays a figure, {doubled} whose bass doubles the riff");
        await Assert.That(figured).IsGreaterThan(30);
    }
}
