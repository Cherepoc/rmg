using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Drums;

/// <summary>A genre's figures (<see cref="Figures" />): a player's rhythm in a section given by its figure, the kick with the riff, a double kick, a clave.</summary>
public sealed class FiguresTest
{
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
    [Arguments("latin", FigurePlayer.Claves, new[] { "clave32", "clave23" })]
    [Arguments("bossa", FigurePlayer.Backbeat, new[] { "bossaClave" })]
    public async Task AClave_StrikesOnlyOnItsThreeAndTwo(string genre, FigurePlayer player, string[] names)
    {
        IReadOnlySet<int> tracks = player == FigurePlayer.Claves
            ? new HashSet<int> { DrumGroups.GetTrackNumber(DrumDefinitions.Claves) }
            : DrumGroups.Snare.Drums.Select(DrumGroups.GetTrackNumber).ToHashSet();
        var (hits, sections, playing) = (0, 0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre)))))
        {
            sections += song.Trace.Count(t => t.Point == TracePoints.Figures);
            playing += names.Sum(name => Playing(song, player, name).Count);
        }

        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre)))))
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
                var sixteenth = (position - bar) / 0.25 + index % (figure.Sixteenths / 16) * 16;
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
            foreach (var span in song.Map.Sections.Where(x => playing.Contains(x.SectionId) && x.Meter == Meter.FourFour))
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
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("country"), Meter: Meter.ThreeFour))))
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
        // measured 79.3% of 2266 notes in 58 sections, October 2026
        await Assert.That(waltzes).IsGreaterThan(10);
        await Assert.That(on / (double)notes).IsGreaterThan(0.6);
    }

    [Test]
    public async Task AFigureSeveralPlayersPlay_IsPlayedByAll_ABlastsKickWithItsSnareAndItsCymbal()
    {
        var blasts = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("blackMetal")))))
        foreach (var figures in song.Trace.Where(t => t.Point == TracePoints.Figures).Select(t => (ImmutableDictionary<FigurePlayer, string>)t.Value!))
            if (figures.GetValueOrDefault(FigurePlayer.Ground) == "blast")
            {
                blasts++;
                await Assert.That(figures.GetValueOrDefault(FigurePlayer.Backbeat)).IsEqualTo("blast").Because($"seed {song.Seed}");
                await Assert.That(figures.GetValueOrDefault(FigurePlayer.Time)).IsEqualTo("blast").Because($"seed {song.Seed}");
            }

        await Assert.That(blasts).IsGreaterThan(5);
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
