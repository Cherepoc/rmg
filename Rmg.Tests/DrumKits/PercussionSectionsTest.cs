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
        foreach (var song in TestCorpus.Range(128))
        {
            var only = song.Trace.Where(x => x.Point == TracePoints.PercussionOnly).ToDictionary(x => x.Section, x => (bool)x.Value!);
            // but the first under an intro of entries, whose drums may come in with the section after it
            var entering = song.Map.Intro.Kind == IntroKind.Entries && !song.Map.Intro.Window.IsBefore ? song.Map.Sections[0] : null;
            foreach (var span in song.Map.Sections.Where(x => only[x.SectionId] && song.HasDrums(x) && x != entering))
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
        // the songs' own, and songs given the kit and a single hand drum, too few for a section of their own
        var few = 0;
        var given = new SongOverrides(DrumPieces: ["kick", "acousticSnare", "hiHat", "conga"]);
        foreach (var song in TestCorpus.Range(128).Concat(TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, given))))
        {
            var percussion = song.Song.TrackDefinitions.Keys.Count(x => song.IsDrum(x) && IsPercussion(x));
            if (percussion is 0 or >= PercussionSections.MinDrums)
                continue;
            few++;
            await Assert.That(song.Trace.Where(x => x.Point == TracePoints.PercussionOnly).Any(x => (bool)x.Value!)).IsFalse().Because($"seed {song.Seed}");
        }

        await Assert.That(few).IsGreaterThan(10);
    }

    [Test]
    public async Task ASongThatLeansToPercussion_PlaysMoreOfIt_AndAQuietSectionMoreThanALoudOne()
    {
        double Share(Tilt song, Tilt energy)
        {
            var context = new GenerationContext(1);
            return Enumerable.Range(0, 4_000).Count(_ => PercussionSections.Draw(context, song, 0.5, energy, 3)) / 4_000.0;
        }

        await Assert.That(Share(new Tilt(PercussionSections.SongSpread), Tilt.None)).IsGreaterThan(Share(Tilt.None, Tilt.None) * 4);
        await Assert.That(Share(Tilt.None, Tilt.None)).IsEqualTo(PercussionSections.Chance).Within(0.015);
        await Assert.That(Share(new Tilt(4), Tilt.Of(SectionEnergy.HighOdds, -0.5))).IsGreaterThan(Share(new Tilt(4), Tilt.Of(SectionEnergy.HighOdds, 0.5)));
        await Assert.That(PercussionSections.Draw(new GenerationContext(1), new Tilt(100), 1, Tilt.None, PercussionSections.MinDrums - 1)).IsFalse();
    }

    [Test]
    public async Task TheWildestSection_PlaysPercussionOnly_ByItsShare_SoThatTheKitIsHeardToo_AndThePlainestNever()
    {
        double Share(Tilt song, double unconventionality)
        {
            var context = new GenerationContext(1);
            return Enumerable.Range(0, 4_000).Count(_ => PercussionSections.Draw(context, song, unconventionality, Tilt.None, 3)) / 4_000.0;
        }

        await Assert.That(Share(Tilt.None, 1)).IsEqualTo(PercussionSections.WildShare).Within(0.03);
        // however far the song leans to it
        await Assert.That(Share(new Tilt(100), 1)).IsEqualTo(PercussionSections.WildShare).Within(0.03);
        await Assert.That(Share(new Tilt(100), 0)).IsEqualTo(0);
    }

    [Test]
    public async Task ACountIn_AlwaysClicks()
    {
        var countIns = 0;
        foreach (var song in TestCorpus.Range(256).Where(x => x.Map.Intro.Kind == IntroKind.CountIn))
        {
            countIns++;
            var clicks = song.Song.Notes!.Where(x => song.IsDrum(x.Key)).SelectMany(x => x.Value).Count(x => x.Position < song.Origin);
            await Assert.That(clicks).IsGreaterThanOrEqualTo(2);
        }

        await Assert.That(countIns).IsGreaterThan(0);
    }
}
