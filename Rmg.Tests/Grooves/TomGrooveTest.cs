using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Grooves;

/// <summary>A tom groove: the toms keeping a section's time in place of the hi-hat, a genre's (drumRole.time), as surf's, Nu metal's and Doom's do.</summary>
public sealed class TomGrooveTest
{
    private static IEnumerable<(int Section, SectionKit Kit)> Kits(CorpusSong song) =>
        song.Trace.Where(x => x.Point == TracePoints.Kit && x.Value is SectionKit).Select(x => (x.Section, (SectionKit)x.Value!));

    /// <summary>A genre naming the tom to keep time leads some of its sections' time on the tom, which plays there, a quiet or a loud one alike.</summary>
    [Test]
    [Arguments("surf")]
    [Arguments("nuMetal")]
    public async Task AGenresTomGroove_LeadsSomeSectionsTime_OnTheTom(string genre)
    {
        var tom = DrumGroups.GetTrackNumber(DrumDefinitions.Tom);
        var (sections, led) = (0, 0);
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre)))))
        foreach (var (section, kit) in Kits(song))
        {
            sections++;
            if (kit.Leads.GetValueOrDefault(DrumRole.Time) != DrumDefinitions.Tom)
                continue;
            led++;
            // its tom sounding in the section's appearances, where its drums groove, the kick and the snare a bar's worth at least
            // of their groove's notes, not a fill's, not where they rest but for a landing, nor a build's snare roll
            int Notes(int track) => song.Song.Notes!.GetValueOrDefault(track)?.Count(x => song.Map.PlayingAt(x.Position)?.Span.SectionId == section) ?? 0;
            int Hits(int track) => song.Trace.Count(x => x.Point == TracePoints.Fill && x.Track == track && x.Section == section);
            var bars = song.Map.Sections.Where(x => x.SectionId == section).Sum(x => x.BarCount);
            if (new[] { DrumDefinitions.Kick, DrumDefinitions.AcousticSnare, DrumDefinitions.ElectricSnare }.Select(DrumGroups.GetTrackNumber).Sum(x => Notes(x) - Hits(x)) >= bars)
                await Assert.That(Notes(tom)).IsGreaterThan(0).Because($"seed {song.Seed}, section {section}");
        }

        Console.WriteLine($"{genre}: {led} of {sections} sections keeping their time on the tom");
        await Assert.That(led).IsGreaterThan(3);
    }

    /// <summary>The plainest of the songs keep their time on the hi-hat or the ride, never the tom, the songs' own giving the tom no time.</summary>
    [Test]
    public async Task ThePlainestSongs_KeepNoTimeOnTheTom()
    {
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Base: 0))))
        foreach (var (section, kit) in Kits(song))
            await Assert.That(kit.Leads.GetValueOrDefault(DrumRole.Time) == DrumDefinitions.Tom).IsFalse().Because($"seed {song.Seed}, section {section}");
    }
}
