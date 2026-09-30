using Rmg.Core.Songs;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Rendering;

namespace Rmg.Tests.Forms;

public sealed class SongIntroTest
{
    // the longest fill before the line where the drums come in, an odd span of a bar less a note
    private const double LongestFill = 4.5;

    private static readonly IReadOnlyList<CorpusSong> Songs = TestCorpus.Range(64).ToArray();

    private static TimelineItem<RenderedNote>[] Notes(CorpusSong song, int track) => song.Notes(track);

    private static TimelineItem<RenderedNote>[] Drums(CorpusSong song) => song.Drums;

    private static CorpusSong[] Of(IntroKind intro) => Songs.Where(x => x.Map.Intro.Kind == intro).ToArray();

    private static bool PercussionOnly(CorpusSong song, int sectionId) =>
        song.Trace.Any(x => x.Point == TracePoints.PercussionOnly && x.Section == sectionId && (bool)x.Value!);

    private static CorpusSong[] Entries => Of(IntroKind.Entries);

    // where an intro's window starts: at the song's start for bars of its own, or at the first section's
    private static double WindowStart(CorpusSong song) => song.Map.Intro.Window.IsBefore ? 0 : song.Origin;

    [Test]
    public async Task AnIntrosParts_ComeInFromItsStart_InTheirOrder_AndSomeAtItsEnd()
    {
        foreach (var song in Entries)
        {
            var intro = song.Map.Intro;
            var entries = intro.Entries.Select(x => x.Entry).ToArray();
            var window = intro.Window.Bars * song.Map.Meter.BarDuration;

            await Assert.That(entries[0]).IsEqualTo(0);
            await Assert.That(entries.Zip(entries.Skip(1)).All(x => x.First <= x.Second)).IsTrue();
            await Assert.That(entries[^1]).IsEqualTo(window);
            await Assert.That(entries.All(x => x % song.Map.Meter.BarDuration == 0 && x <= window)).IsTrue();
            await Assert.That(intro.Duration).IsEqualTo(intro.Window.IsBefore ? window : 0);
        }

        await Assert.That(Entries.Length).IsGreaterThan(20);
    }

    [Test]
    public async Task EveryPart_IsLeftOutUntilItComesIn_ButForTheDrumsFillIntoTheLanding()
    {
        foreach (var song in Entries)
        {
            var window = song.Map.Intro.Window.Bars * song.Map.Meter.BarDuration;
            foreach (var entry in song.Map.Intro.Entries)
            foreach (var track in entry.Tracks)
            {
                if (!song.Song.Notes!.TryGetValue(track, out var notes) || notes.Count == 0)
                    continue;

                // a fill leads the drums in where the band lands
                var from = WindowStart(song) + entry.Entry - (entry.Part.Role == TrackRole.Drum && entry.Entry == window ? LongestFill : 0);
                await Assert.That(notes.Min(x => x.Position)).IsGreaterThanOrEqualTo(from - 1e-9)
                    .Because($"seed {song.Seed}, {entry.Part} at {entry.Entry}");
            }
        }
    }

    [Test]
    public async Task AnIntroOfItsOwnBars_EndsWithTheBandLanding()
    {
        var songs = Entries.Where(x => x.Map.Intro.Window.IsBefore && x.HasDrums(x.Map.Sections[0])).ToArray();
        foreach (var song in songs)
        {
            // the band comes in on a crash, where the first section's drums play, or now and then the vibraslap, pushed an 8th early now and then, or on the
            // percussion into a section of it
            var landsOn = PercussionOnly(song, song.Map.Sections[0].SectionId)
                ? DrumGroups.Percussion.Drums.SelectMany(x => x.ArticulationCodes)
                : DrumGroups.Accents.Drums.SelectMany(x => x.ArticulationCodes);
            // an 8th early in straight time, and a little more in a tuplet feel, whose step can be longer than an 8th
            await Assert.That(Drums(song).Any(x => x.Position >= song.Origin - 0.75 && x.Position <= song.Origin && landsOn.Contains(x.Value.Offset)))
                .IsTrue()
                .Because($"seed {song.Seed}");
        }

        await Assert.That(songs.Length).IsGreaterThan(5);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var songs = TestCorpus.Range(256).Where(x => x.Map.Intro.Kind == IntroKind.Entries).ToArray();
        var firsts = songs.GroupBy(x => x.Map.Intro.Entries[0].Part.ToString()).OrderByDescending(x => x.Count()).Select(x => $"{x.Key} {x.Count()}");
        var melodyEarly = songs.Count(x => x.Map.Intro.Entries.Any(y => y.Part.Role == TrackRole.Melody && y.Entry < x.Map.Intro.Window.Bars * x.Map.Meter.BarDuration));
        var inWindow = songs.Select(x => x.Map.Intro.Entries.Count(y => y.Entry < x.Map.Intro.Window.Bars * x.Map.Meter.BarDuration) / (double)x.Map.Intro.Entries.Length).Average();
        var windows = songs.GroupBy(x => x.Map.Intro.Window).OrderBy(x => x.Key.Bars).Select(x => $"{x.Key.Bars}{(x.Key.IsBefore ? " before" : " in")} {x.Count()}");
        Console.WriteLine($"{songs.Length} intros of entries; windows {string.Join(", ", windows)}; first in {string.Join(", ", firsts)}; " +
                          $"the melody in the window {melodyEarly}; {inWindow:P0} of the parts come in in it");
        await Task.CompletedTask;
    }

    [Test]
    public async Task CountIn_ClicksThePedalHiHat_OrADrySoundTheSongHas_OnThePulses()
    {
        var songs = Of(IntroKind.CountIn);
        foreach (var song in songs)
        {
            var clicks = Drums(song).Where(x => x.Position < song.Origin).ToArray();
            var sound = FormLayers.CountInSounds.First(x => song.Song.TrackDefinitions.ContainsKey(DrumGroups.GetTrackNumber(x.Drum))).Sound;

            // a bar of them, on the meter's pulses or its second half of them
            var pulses = song.Map.Meter.Pulses;
            var places = clicks.Select(x => x.Position).ToArray();
            await Assert.That(song.Origin).IsEqualTo(song.Map.Meter.BarDuration);
            await Assert.That(places.SequenceEqual(pulses) || places.SequenceEqual(pulses.Skip(pulses.Length / 2))).IsTrue();
            await Assert.That(clicks.All(x => x.Value.Offset == sound)).IsTrue();
        }

        await Assert.That(songs.Length).IsGreaterThan(0);
    }
}
