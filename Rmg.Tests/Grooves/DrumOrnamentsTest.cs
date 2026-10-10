using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Grooves;

/// <summary>The snare's flams and drags: soft strokes of its head just before a note on a pulse, never in the plainest songs.</summary>
public sealed class DrumOrnamentsTest
{
    // a genre that flams and drags in every song
    private static readonly Genre Rudimental = new(ImmutableDictionary<string, ImmutableDictionary<string, ByConvention>>.Empty
        .Add("rudiments", ImmutableDictionary<string, ByConvention>.Empty.Add("flams", new ByConvention(1, 1, 1)).Add("drags", new ByConvention(1, 1, 1))));

    private static (bool Flams, bool Drags) Played(CorpusSong song) => ((bool, bool))song.Trace.Single(x => x.Point == TracePoints.DrumOrnaments).Value!;

    private static int StrokesOf(RealizedNote note) => note.State.GetStateValue(CompositionStateKinds.Ornament) is { IsDefault: false } x ? x.Length : 0;

    [Test]
    public async Task ThePlainestSongs_NeitherFlamNorDrag()
    {
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Base: 0))))
        {
            await Assert.That(Played(song)).IsEqualTo((false, false)).Because($"seed {song.Seed}");
            await Assert.That(song.Song.Notes!.Where(x => song.IsDrum(x.Key)).SelectMany(x => x.Value).Any(x => StrokesOf(x.Value) > 0)).IsFalse();
        }
    }

    /// <summary>
    ///     An ornamented drum note is the snare's head in a section, its strokes too, a flam's one stroke on an 8th or a drag's
    ///     two on a pulse, and sounds after its strokes, each its sound, softer, before it.
    /// </summary>
    [Test]
    public async Task ADrumsOrnament_IsTheSnaresHead_ItsStrokesSofterBeforeIt()
    {
        int flams = 0, drags = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Rudimental))))
        {
            await Assert.That(Played(song)).IsEqualTo((true, true));
            var map = song.Song.Map!;
            var swing = Rmg.Core.Rendering.Render.GetSwing(song.Song);
            var drums = song.Drums;
            foreach (var (track, notes) in song.Song.Notes!.Where(x => song.IsDrum(x.Key)))
            foreach (var note in notes.Where(x => StrokesOf(x.Value) > 0))
            {
                var because = $"seed {song.Seed} track {track} at {note.Position}";
                var strokes = StrokesOf(note.Value);
                await Assert.That(track == DrumGroups.GetTrackNumber(DrumDefinitions.AcousticSnare) || track == DrumGroups.GetTrackNumber(DrumDefinitions.ElectricSnare)).IsTrue().Because(because);
                await Assert.That(note.Value.Pitches.Single()).IsEqualTo(DrumGroups.GetDrum(track).Sounds[0].Code).Because(because);
                await Assert.That(song.SpanAt(note.Position) is { } section && note.Position - strokes * note.Value.State.GetStateValue(CompositionStateKinds.OrnamentStep) >= section.Start - 1e-9)
                    .IsTrue().Because(because);
                await Assert.That(strokes is DrumOrnaments.FlamStrokes or DrumOrnaments.DragStrokes).IsTrue().Because(because);
                // a flam on an 8th, a drag on a pulse
                var beat = map.BeatInBar(note.Position);
                await Assert.That(strokes == DrumOrnaments.FlamStrokes ? Math.Abs(beat * 2 - Math.Round(beat * 2)) < 1e-6 : map.MeterAt(note.Position).Pulses.Any(x => Math.Abs(x - beat) < 1e-6))
                    .IsTrue().Because(because);
                if (strokes == DrumOrnaments.FlamStrokes)
                    flams++;
                else
                    drags++;

                // rendered: its strokes before it on its sound, softer
                var at = swing.Apply(note.Position);
                var played = drums.Where(x => x.Value.Offset == note.Value.Pitches[0] && x.Position > at - 0.5 && x.Position < at + 1e-6).ToArray();
                var main = played.Last(x => Math.Abs(x.Position - at) < 1e-6);
                var before = played.Where(x => x.Position < at - 1e-6).TakeLast(strokes).ToArray();
                await Assert.That(before.Length).IsEqualTo(strokes).Because(because);
                await Assert.That(before.All(x => x.Value.Velocity < main.Value.Velocity)).IsTrue().Because(because);
            }
        }

        Console.WriteLine($"of 32 songs flamming and dragging, {flams} flams and {drags} drags");
        await Assert.That(flams).IsGreaterThan(20);
        await Assert.That(drags).IsGreaterThan(20);
    }

    /// <summary>How often the songs flam and drag, the songs' share and the ornaments a song, by genre and by fifths of conventionality.</summary>
    [Test]
    [Arguments("none")]
    [Arguments("rock")]
    [Arguments("metal")]
    [Arguments("jazz")]
    [Arguments("funk")]
    [Arguments("orchestral")]
    [Arguments("techno")]
    [Explicit]
    public async Task Report(string id)
    {
        const int count = 64;
        var genre = id == "none" ? Genre.None : GenrePresets.Genre(id);
        var songs = TestCorpus.InParallel(Enumerable.Range(0, count), seed => TestCorpus.Get(seed, new SongOverrides(Genre: genre)));
        int Count(CorpusSong song, int strokes) => song.Song.Notes!.Where(x => song.IsDrum(x.Key)).SelectMany(x => x.Value).Count(x => StrokesOf(x.Value) == strokes);
        var (flamming, dragging) = (songs.Where(x => Played(x).Flams).ToArray(), songs.Where(x => Played(x).Drags).ToArray());
        Console.WriteLine($"{id}: flamming {flamming.Length / (double)count:P0} of the songs, {flamming.Select(x => Count(x, DrumOrnaments.FlamStrokes)).DefaultIfEmpty().Average():F1} flams a song; " +
                          $"dragging {dragging.Length / (double)count:P0}, {dragging.Select(x => Count(x, DrumOrnaments.DragStrokes)).DefaultIfEmpty().Average():F1} drags a song");
        await Task.CompletedTask;
    }
}
