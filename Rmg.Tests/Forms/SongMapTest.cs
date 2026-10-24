using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Forms;

public sealed class SongMapTest
{
    private static readonly SongMap Map = new(
        Meter.FourFour,
        new IntroSpan(IntroKind.Entries, 8, new IntroWindow(2, true)),
        [new SectionSpan(0, 8, 32, Meter.FourFour, 0), new SectionSpan(1, 40, 32, Meter.FourFour, 0)],
        new EndingSpan(EndingKind.Button, 72, 4, 1)
    );

    [Test]
    public async Task Map_FindsTheSection_AndTheBarOfItsPattern()
    {
        await Assert.That(Map.Origin).IsEqualTo(8);
        await Assert.That(Map.Duration).IsEqualTo(76);
        await Assert.That(Map.SectionAt(4)).IsNull();
        await Assert.That(Map.SectionAt(40)!.SectionId).IsEqualTo(1);
        await Assert.That(Map.SectionAt(72)).IsNull();
        // the bars count from the first section, the intro's backwards from it
        await Assert.That(Map.PatternBarAt(8)).IsEqualTo(0);
        await Assert.That(Map.PatternBarAt(21)).IsEqualTo(3);
        await Assert.That(Map.PatternBarAt(4)).IsEqualTo(3);
        await Assert.That(Map.BeatInBar(21.5)).IsEqualTo(1.5);
    }

    /// <summary>A position plays as its section, an intro's window as the first section's first bars, a hook as its chorus, and the ending as the last section from its start.</summary>
    [Test]
    public async Task APosition_PlaysAsItsSection_AnIntroAsItsSource_TheEndingAsTheLast()
    {
        // an entries intro the first section's first bars, its span moved to the song's start
        await Assert.That(Map.PlayingAt(4)).IsEqualTo(new Play(0, Map.Sections[0] with { Start = 0, Duration = 8, Cut = 0 }, false));
        await Assert.That(Map.PlayingAt(40)).IsEqualTo(new Play(1, Map.Sections[1], false));
        await Assert.That(Map.PlayingAt(73)).IsEqualTo(new Play(1, Map.Sections[1] with { Start = 72, Duration = 4, Cut = 0 }, true));

        // a hook intro its chorus's, the second section here
        var hooked = Map with { Intro = new IntroSpan(IntroKind.Hook, 8, Source: 1) };
        await Assert.That(hooked.PlayingAt(4)).IsEqualTo(new Play(1, Map.Sections[1] with { Start = 0, Duration = 8, Cut = 0 }, false));
        // and an intro of its own none
        await Assert.That((Map with { Intro = new IntroSpan(IntroKind.CountIn, 8) }).PlayingAt(4)).IsNull();
    }

    [Test]
    public async Task ASectionInAMeterOfItsOwn_CountsItsBarsInIt_FromItsStart()
    {
        // 4/4 for 16 beats, then 3/4 for 12, and an ending after it
        var threeFour = Meter.Options.Select(x => x.Meter).First(x => x.BarDuration == 3);
        var map = new SongMap(
            Meter.FourFour,
            new IntroSpan(IntroKind.Cold, 0),
            [new SectionSpan(0, 0, 16, Meter.FourFour, 0), new SectionSpan(1, 16, 12, threeFour, 0)],
            new EndingSpan(EndingKind.Button, 28, 4, 1)
        );

        await Assert.That(map.MeterAt(15)).IsEqualTo(Meter.FourFour);
        await Assert.That(map.MeterAt(16)).IsEqualTo(threeFour);
        await Assert.That(map.MeterAt(30)).IsEqualTo(threeFour);
        await Assert.That(map.PatternBarAt(19.5)).IsEqualTo(1);
        await Assert.That(map.BeatInBar(19.5)).IsEqualTo(0.5);
        await Assert.That(map.PatternBarAt(29)).IsEqualTo(0);
    }

    [Test]
    public async Task GeneratedSongs_HaveAMap_ThatCoversThemWhole()
    {
        for (var seed = 0; seed < 16; seed++)
        {
            var song = TestCorpus.Get(seed).Song;
            var map = song.Map!;
            ImmutableArray<SectionSpan> sections = map.Sections;

            await Assert.That(map.Duration).IsEqualTo(song.Duration);
            await Assert.That(sections[0].Start).IsEqualTo(map.Intro.Duration);
            await Assert.That(sections.Zip(sections.Skip(1)).All(x => x.First.End == x.Second.Start)).IsTrue();
            await Assert.That(map.Ending.Start).IsEqualTo(sections[^1].End);
            // an intro has bars of its own for a count-in, a hook's quote of its chorus, or where its parts come in before
            // the first section
            var hasBars = map.Intro.Kind is IntroKind.CountIn or IntroKind.Hook || map.Intro.Kind == IntroKind.Entries && map.Intro.Window.IsBefore;
            await Assert.That(map.Intro.Duration > 0).IsEqualTo(hasBars);
            await Assert.That(map.Ending.Duration > 0).IsEqualTo(FormLayers.HasFinalChord(map.Ending.Kind));
        }
    }

    /// <summary>
    ///     What plays as a section, over the corpus's songs: an intro of entries or a hook first, as the section it is made
    ///     of, from the song's start to the first section; every section as itself; the ending last, as the last section,
    ///     over its own bars; and a position in any of them playing as that one.
    /// </summary>
    [Test]
    public async Task ThePlays_AreTheSections_AnIntroAndTheEnding_AsTheirSources()
    {
        var (intros, endings) = (0, 0);
        foreach (var song in TestCorpus.Range(64))
        {
            var map = song.Map;
            var plays = map.Plays.ToArray();
            var playsIntro = map.Origin > 1e-9 && (map.Intro.Kind == IntroKind.Hook || map.Window is not null);
            await Assert.That(plays.Length).IsEqualTo(map.Sections.Length + (playsIntro ? 1 : 0) + (map.Ending.Duration > 1e-9 ? 1 : 0)).Because($"seed {song.Seed}");
            if (playsIntro)
            {
                intros++;
                await Assert.That(plays[0].Span.SectionId).IsEqualTo(map.Sections[plays[0].Index].SectionId).Because($"seed {song.Seed}");
                await Assert.That((plays[0].Span.Start, plays[0].Span.End)).IsEqualTo((0.0, map.Origin)).Because($"seed {song.Seed}");
            }

            if (map.Ending.Duration > 1e-9)
            {
                endings++;
                await Assert.That(plays[^1] is { IsEnding: true } ending && ending.Index == map.Sections.Length - 1 && Math.Abs(ending.Span.Start - map.Ending.Start) < 1e-9)
                    .IsTrue().Because($"seed {song.Seed}");
            }

            foreach (var play in plays)
                await Assert.That(map.PlayingAt(play.Span.Start + 1e-6)?.Span.Start).IsEqualTo(play.Span.Start).Because($"seed {song.Seed} at {play.Span.Start}");
        }

        await Assert.That(intros).IsGreaterThan(0);
        await Assert.That(endings).IsGreaterThan(0);
    }
}
