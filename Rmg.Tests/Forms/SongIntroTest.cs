using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Rendering;

namespace Rmg.Tests.Forms;

public sealed class SongIntroTest
{
    private const double Phrase = Meter.PatternDuration;

    // the longest fill before the line where the drums come in, an odd span of a bar less a note
    private const double LongestFill = 4.5;

    private static readonly IReadOnlyList<CorpusSong> Songs = TestCorpus.Range(60).ToArray();

    private static TimelineItem<RenderedNote>[] Notes(CorpusSong song, int track) => song.Notes(track);

    private static TimelineItem<RenderedNote>[] Drums(CorpusSong song) => song.Drums;

    private static CorpusSong[] Of(IntroKind intro) => Songs.Where(x => x.Map.Intro.Kind == intro).ToArray();

    [Test]
    [Arguments(IntroKind.ChordsFirst, SongTracks.ChordsTrack, false, 0.0)]
    [Arguments(IntroKind.ChordsFirst, SongTracks.BassTrack, false, Phrase)]
    [Arguments(IntroKind.ChordsFirst, SongTracks.BassTrack, true, 0.0)]
    [Arguments(IntroKind.ChordsFirst, SongTracks.MelodyTrack, true, Phrase)]
    [Arguments(IntroKind.ChordsFirst, DrumGroups.FirstTrackNumber, true, Phrase)]
    [Arguments(IntroKind.Build, SongTracks.ChordsTrack, false, 0.0)]
    [Arguments(IntroKind.Build, SongTracks.BassTrack, false, 4.0)]
    [Arguments(IntroKind.Build, DrumGroups.FirstTrackNumber, false, 8.0)]
    [Arguments(IntroKind.Build, SongTracks.MelodyTrack, false, Phrase)]
    public async Task IntroEntries_ComeInTheirOrder(IntroKind intro, int track, bool withBass, double entry)
    {
        await Assert.That(SongFormGenerator.GetIntroEntry(intro, track, withBass)).IsEqualTo(entry);
    }

    [Test]
    public async Task TheMelody_ComesInAfterTheFirstPhrase_WhereTheIntroLeavesItOut()
    {
        var songs = Songs.Where(x => x.Map.Intro.Kind is IntroKind.ChordsFirst or IntroKind.Build).ToArray();
        foreach (var song in songs)
            await Assert.That(Notes(song, SongTracks.MelodyTrack).Min(x => x.Position)).IsGreaterThanOrEqualTo(song.Origin + Phrase);

        await Assert.That(songs.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task ChordsFirst_LeavesTheDrumsOut_ButForTheirFillIn()
    {
        var songs = Of(IntroKind.ChordsFirst);
        foreach (var song in songs)
        {
            await Assert.That(Drums(song).Min(x => x.Position)).IsGreaterThanOrEqualTo(Phrase - LongestFill);
            if (!song.Map.Intro.WithBass)
                await Assert.That(Notes(song, SongTracks.BassTrack).Min(x => x.Position)).IsGreaterThanOrEqualTo(Phrase);
        }

        await Assert.That(songs.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task Build_BringsTheBassAndTheDrumsInBarByBar()
    {
        var songs = Of(IntroKind.Build);
        foreach (var song in songs)
        {
            await Assert.That(Notes(song, SongTracks.BassTrack).Min(x => x.Position)).IsGreaterThanOrEqualTo(4);
            await Assert.That(Drums(song).Min(x => x.Position)).IsGreaterThanOrEqualTo(8);
        }

        await Assert.That(songs.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task DrumsFirst_PlayAlone_ThenTheBandLands()
    {
        var songs = Of(IntroKind.DrumsFirst);
        foreach (var song in songs)
        {
            var pitched = new[] { SongTracks.ChordsTrack, SongTracks.MelodyTrack, SongTracks.BassTrack }.SelectMany(x => Notes(song, x));

            await Assert.That(song.Origin).IsGreaterThan(0);
            await Assert.That(pitched.Min(x => x.Position)).IsGreaterThanOrEqualTo(song.Origin);
            // the groove may start on a later beat, but plays in the intro's first bar
            await Assert.That(Drums(song).Min(x => x.Position)).IsLessThan(4);
            // the band comes in on a crash, pushed an 8th early now and then
            await Assert.That(Drums(song).Any(x => x.Position >= song.Origin - 0.5 && x.Position <= song.Origin && x.Value.Offset is 49 or 57))
                .IsTrue();
        }

        await Assert.That(songs.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task CountIn_ClicksThePedalHiHat_OnTheBeats()
    {
        var songs = Of(IntroKind.CountIn);
        foreach (var song in songs)
        {
            var clicks = Drums(song).Where(x => x.Position < song.Origin).ToArray();

            await Assert.That(song.Origin).IsEqualTo(4);
            await Assert.That(clicks.Length is 2 or 4).IsTrue();
            await Assert.That(clicks.All(x => x.Value.Offset == DrumSounds.PedalHiHat && x.Position % 1 == 0)).IsTrue();
        }

        await Assert.That(songs.Length).IsGreaterThan(0);
    }
}
