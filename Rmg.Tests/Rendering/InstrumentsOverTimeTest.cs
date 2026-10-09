using Rmg.Core;
using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Tests.Rendering;

public sealed class InstrumentsOverTimeTest
{
    private static SongOverrides Sound(double sound) => new(Facets: ImmutableDictionary<Facet, double>.Empty.Add(Facet.Sound, sound));

    private static (int Index, TrackRole Role, int Program, ArticulationMode Mode, int Variant)[] Changes(CorpusSong song) =>
        [..song.Trace.Where(x => x.Point == TracePoints.InstrumentOverTime).Select(x => ((int, TrackRole, int, ArticulationMode, int))x.Value!)];

    [Test]
    public async Task ThePlainestSongs_KeepEveryPartsInstrument_AndTheWildest_ChangeThem()
    {
        foreach (var seed in Enumerable.Range(0, 4))
        {
            var plain = TestCorpus.Get(seed, Sound(0));
            await Assert.That(Changes(plain)).IsEmpty();
            // but where a part plays in another's sound, as a twin lead does, which is the arrangement's and not the sound's
            var soundsAs = plain.Song.Notes!.Where(x => x.Value.Any(n => n.Value.State.GetStateValue(CompositionStateKinds.SoundsAs) > 0))
                .Select(x => plain.Song.TrackDefinitions[x.Key].Role).ToHashSet();
            await Assert.That(plain.Rendered.Tracks.All(x => x.ProgramChanges.IsEmpty || soundsAs.Contains(x.Role))).IsTrue();

            var wild = TestCorpus.Get(seed, Sound(1));
            await Assert.That(wild.Rendered.Tracks.Any(x => !x.ProgramChanges.IsEmpty)).IsTrue();
        }
    }

    [Test]
    public async Task AChangeOfInstrument_IsWrittenBeforeItsNote_AndAGivenInstrument_HoldsThroughout()
    {
        var song = TestCorpus.Get(1, Sound(1));
        var track = song.Rendered.Tracks.First(x => !x.ProgramChanges.IsEmpty);
        using var stream = new MemoryStream();
        song.Rendered.Write(stream, null);

        // a program change on the track's channel for every change, and the first
        var channel = song.Rendered.GetPartChannels()[track.Role];
        var bytes = stream.ToArray();
        var programChanges = Enumerable.Range(0, bytes.Length - 1).Count(i => bytes[i] == (0xC0 | channel) && bytes[i + 1] < 128);
        await Assert.That(programChanges).IsGreaterThanOrEqualTo(track.ProgramChanges.Length + 1);

        var given = Render.RenderSong(song.Song, SongMix.None with { Parts = ImmutableDictionary<TrackRole, PartMix>.Empty.Add(track.Role, new PartMix(5, 1, null, true)) });
        await Assert.That(given.Tracks.Single(x => x.Role == track.Role).ProgramChanges).IsEmpty();
    }

    [Test]
    public async Task AGuitarArticulates_OnItsOwnRig_ACleanOneMutedAndADrivenOneInHarmonics_NeverTheOther()
    {
        int[] clean = [26, 27, 28], driven = [29, 30, 31];
        var articulated = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => Changes(TestCorpus.Get(seed, Sound(1)))).SelectMany(x => x)
            .Where(x => x.Mode != ArticulationMode.None && (clean.Contains(x.Program) || driven.Contains(x.Program))).ToArray();

        await Assert.That(articulated.Length).IsGreaterThan(10);
        foreach (var (_, role, program, _, variant) in articulated)
            await Assert.That((clean.Contains(program) ? clean : driven).Contains(variant)).IsTrue().Because($"{role} {program} to {variant}");
    }

    /// <summary>The share of a song's switches that keep to the instrument's family.</summary>
    private static (int Kept, int Switches) Kept(CorpusSong song)
    {
        var switches = Changes(song).Where(x => x.Program != ((PitchInstrumentTrack)song.Song.TrackDefinitions.Values.First(d => d.Role == x.Role)).InstrumentCode).ToArray();
        return (switches.Count(x => InstrumentsOverTime.FamilyOf(((PitchInstrumentTrack)song.Song.TrackDefinitions.Values.First(d => d.Role == x.Role)).InstrumentCode).Contains(x.Program)), switches.Length);
    }

    [Test]
    public async Task ASwitch_KeepsToTheFamily_ThePlainerTheOftener()
    {
        (int, int) Share(double sound) => TestCorpus.InParallel(Enumerable.Range(0, 64), seed => Kept(TestCorpus.Get(seed, Sound(sound)))).Aggregate((0, 0), (a, x) => (a.Item1 + x.Kept, a.Item2 + x.Switches));
        var (middleKept, middle) = Share(0.6);
        var (wildKept, wild) = Share(1);

        await Assert.That(middle).IsGreaterThan(20);
        await Assert.That(middleKept / (double)middle).IsGreaterThan(wildKept / (double)wild + 0.15);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var songs = TestCorpus.Range(256);
        foreach (var band in songs.GroupBy(x => Math.Clamp((int)(x.Unconventionality[Facet.Sound] * 5), 0, 4)).OrderBy(x => x.Key))
        {
            var changes = band.SelectMany(Changes).ToArray();
            Console.WriteLine($"  sound {band.Key}/5, {band.Count()} songs: songs changing {band.Count(x => Changes(x).Length > 0) / (double)band.Count():P0}; articulated {changes.Count(x => x.Mode != ArticulationMode.None)}, by bars {changes.Count(x => x.Mode == ArticulationMode.Bars)}, on accents {changes.Count(x => x.Mode == ArticulationMode.Accents)}");
        }
        await Task.CompletedTask;
    }
}
