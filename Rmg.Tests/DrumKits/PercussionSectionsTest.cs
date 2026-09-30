using Rmg.Core.Songs;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;

namespace Rmg.Tests.DrumKits;

public sealed class PercussionSectionsTest
{
    private static bool IsPercussion(int track) => DrumGroups.GetDrum(track).Family.HasFlag(DrumFamily.Percussion);

    [Test]
    public async Task ASectionOfPercussionOnly_PlaysNoDrumKit_ButALandingPushedIntoTheNext()
    {
        var sections = 0;
        foreach (var song in TestCorpus.Range(100))
        {
            var only = song.Trace.Where(x => x.Point == TracePoints.PercussionOnly).ToDictionary(x => x.Section, x => (bool)x.Value!);
            foreach (var span in song.Map.Sections.Where(x => only[x.SectionId] && song.HasDrums(x)))
            {
                sections++;
                var kitNotes = song.Song.Notes!
                    .Where(x => song.Song.TrackDefinitions[x.Key].Role == TrackRole.Drum && !IsPercussion(x.Key))
                    .SelectMany(x => x.Value)
                    .Count(x => x.Position >= span.Start && x.Position < span.End - 1);
                await Assert.That(kitNotes).IsEqualTo(0);
                var percussionNotes = song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key].Role == TrackRole.Drum && IsPercussion(x.Key)).SelectMany(x => x.Value).Count(x => x.Position >= span.Start && x.Position < span.End);
                await Assert.That(percussionNotes).IsGreaterThan(0);
            }
        }

        await Assert.That(sections).IsGreaterThan(0);
    }

    [Test]
    public async Task OnlyASongWithEnoughPercussion_HasSectionsOfIt()
    {
        foreach (var song in TestCorpus.Range(100))
        {
            var percussion = song.Song.TrackDefinitions.Keys.Count(x => x >= DrumGroups.FirstTrackNumber && IsPercussion(x));
            if (percussion < PercussionSections.MinDrums)
                await Assert.That(song.Trace.Where(x => x.Point == TracePoints.PercussionOnly).Any(x => (bool)x.Value!)).IsFalse();
        }
    }

    [Test]
    public async Task ASongThatLeansToPercussion_PlaysMoreOfIt_AndAQuietSectionMoreThanALoudOne()
    {
        double Share(Tilt song, Tilt energy)
        {
            var context = new GenerationContext(1);
            return Enumerable.Range(0, 4_000).Count(_ => PercussionSections.Draw(context, song, Tilt.None, energy, 3)) / 4_000.0;
        }

        await Assert.That(Share(new Tilt(PercussionSections.SongSpread), Tilt.None)).IsGreaterThan(Share(Tilt.None, Tilt.None) * 4);
        await Assert.That(Share(Tilt.None, Tilt.None)).IsEqualTo(PercussionSections.Chance).Within(0.015);
        await Assert.That(Share(new Tilt(4), Tilt.Of(SectionEnergy.HighOdds, -0.5))).IsGreaterThan(Share(new Tilt(4), Tilt.Of(SectionEnergy.HighOdds, 0.5)));
        await Assert.That(PercussionSections.Draw(new GenerationContext(1), new Tilt(100), Tilt.None, Tilt.None, PercussionSections.MinDrums - 1)).IsFalse();
    }

    [Test]
    public async Task ACountIn_AlwaysClicks()
    {
        var countIns = 0;
        foreach (var song in TestCorpus.Range(200).Where(x => x.Map.Intro.Kind == IntroKind.CountIn))
        {
            countIns++;
            var clicks = song.Song.Notes!.Where(x => x.Key >= DrumGroups.FirstTrackNumber).SelectMany(x => x.Value).Count(x => x.Position < song.Origin);
            await Assert.That(clicks).IsGreaterThanOrEqualTo(2);
        }

        await Assert.That(countIns).IsGreaterThan(0);
    }
}
