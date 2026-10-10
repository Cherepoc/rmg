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
            // but in half or double time, whose player keeps its own rhythm stretched or pressed
            .Except(song.Trace.Where(t => t.Point == TracePoints.TimeFeel && (int)t.Value! != 0).Select(t => t.Section))
    ];

    /// <summary>
    ///     Every hit of the tracks given in the sections given, but a phrase's last bar, where a fill may take the drums, and
    ///     a phrase's first beat, where the fill into it lands, with its section and its bar's start.
    /// </summary>
    private static IEnumerable<(SectionSpan Span, double Bar, double Position)> Hits(CorpusSong song, IReadOnlySet<int> tracks, IReadOnlySet<int> sections) =>
        song.Map.Sections.Where(x => sections.Contains(x.SectionId)).SelectMany(span => song.Song.Notes!.Where(x => tracks.Contains(x.Key)).SelectMany(x => x.Value)
            .Where(x => x.Position > span.Start + 1e-9 && x.Position < span.End - 1e-9
                        && Math.Abs(Math.IEEERemainder(x.Position - span.Start, span.Meter.PhraseDuration)) > 1e-6)
            .Select(x => (span, Bar: span.Start + Math.Floor((x.Position - span.Start) / span.Meter.BarDuration + 1e-9) * span.Meter.BarDuration, x.Position))
            .Where(x => Math.Abs(Math.IEEERemainder(x.Bar + span.Meter.BarDuration - span.Start, span.Meter.PhraseDuration)) > 1e-6));

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
            foreach (var (span, bar, position) in Hits(song, kicks, Playing(song, FigurePlayer.Ground, "withRiff")))
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
            foreach (var (span, bar, position) in Hits(song, tracks, Playing(song, player, name)))
            {
                hits++;
                // its 16th in the figure's two bars, counted from the section's start
                var twoBars = span.Start + Math.Floor((bar - span.Start) / (2 * span.Meter.BarDuration) + 1e-9) * 2 * span.Meter.BarDuration;
                var sixteenth = (position - twoBars) / 0.25;
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
    public async Task APolymeterSection_PlaysNoFigure()
    {
        var polymeters = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("idm")))))
        {
            var plans = (ImmutableDictionary<int, Polymeter.Plan>)song.Trace.Single(x => x.Point == TracePoints.Polymeter).Value!;
            foreach (var id in plans.Keys)
            {
                polymeters++;
                var figures = (ImmutableDictionary<FigurePlayer, string>)song.Trace.Single(x => x.Point == TracePoints.Figures && x.Section == id).Value!;
                await Assert.That(figures).IsEmpty().Because($"seed {song.Seed} section {id}");
            }
        }

        await Assert.That(polymeters).IsGreaterThan(3);
    }
}
