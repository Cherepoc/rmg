using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Forms;

public sealed class SongMapTest
{
    private static readonly SongMap Map = new(
        Meter.FourFour,
        new IntroSpan(IntroKind.Entries, 8, new IntroWindow(2, true)),
        [new SectionSpan(0, 8, 32), new SectionSpan(1, 40, 32)],
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
            // an intro has bars of its own for a count-in, or where its parts come in before the first section
            var hasBars = map.Intro.Kind == IntroKind.CountIn || map.Intro.Kind == IntroKind.Entries && map.Intro.Window.IsBefore;
            await Assert.That(map.Intro.Duration > 0).IsEqualTo(hasBars);
            await Assert.That(map.Ending.Duration > 0).IsEqualTo(FormLayers.HasFinalChord(map.Ending.Kind));
        }
    }
}
