using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Grooves;

/// <summary>
///     A kit section's backbeat and time: every section leads each role its drums can play, and its bars sound both, but
///     where a breakdown or stop time takes one out; how often a bar sounds one without the other, and why: no drum of the
///     section holds the role, the role's lead drawn to another role, or the role's drums silent in the bar. A bar no drum
///     plays in, as an arrangement leaves the drums out, counts for neither.
/// </summary>
public sealed class KitRolesTest
{
    private const int Count = 64;

    private sealed record Bar(int Seed, bool Backbeat, bool Time, string Cause, string Section)
    {
        /// <summary>Whether a breakdown or stop time may take its backbeat or its time out.</summary>
        public bool IsExcepted => Section.Length > 0;
    }

    [Test]
    public async Task ASection_LeadsEveryRoleItsFreeDrumsCanPlay_WithADrumPlayingIt()
    {
        var sections = 0;
        foreach (var song in TestCorpus.Range(Count))
        foreach (var entry in song.Trace.Where(x => x.Point == TracePoints.Kit))
        {
            var kit = (SectionKit)entry.Value!;
            var family = (bool)song.Trace.First(x => x.Point == TracePoints.PercussionOnly && x.Section == entry.Section).Value! ? DrumFamily.Percussion : DrumFamily.Kit;
            var grooving = kit.Roles.Keys.Where(x => x.Family.HasFlag(family) && DrumGroups.GroupOf(x).GrooveChance >= 1).ToArray();
            // a role a grooving drum can play, one that leads no other role, as a section of percussion alone may draw its clap
            // to the ground
            foreach (var role in DrumKitGenerator.LeadRoles.Where(r => grooving.Any(x => (x.MainRole == r || kit.Roles[x] == r) && !kit.Leads.Any(l => l.Key != r && l.Value == x))))
            {
                await Assert.That(kit.Leads.ContainsKey(role)).IsTrue().Because($"seed {song.Seed}, section {entry.Section}, {role}");
                await Assert.That(kit.Roles[kit.Leads[role]]).IsEqualTo(role);
                await Assert.That(kit.Drums.Contains(kit.Leads[role])).IsTrue();
            }

            sections++;
        }

        await Assert.That(sections).IsGreaterThan(Count);
    }

    [Test]
    public async Task AKitSectionsBars_SoundTheirBackbeatAndTimeTogether()
    {
        // measured 99.0% of 64 songs' bars
        var bars = TestCorpus.Measure(Count, x => Measure(x).ToArray()).SelectMany(x => x).Where(x => !x.IsExcepted).ToArray();
        var both = bars.Count(x => x.Backbeat && x.Time) / (double)bars.Length;
        Console.WriteLine($"{bars.Length} bars, both {both:P1}");
        await Assert.That(both).IsGreaterThan(0.9);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        Print("the corpus", TestCorpus.Range(Count * 4));
        foreach (var name in new[] { "blackMetal", "metal", "rock", "jazz" })
            Print(name, TestCorpus.InParallel(Enumerable.Range(0, Count), seed => TestCorpus.Get(seed, new SongOverrides(Genre: Genres.GenrePresets.Genre(name)))));
        await Task.CompletedTask;
    }

    private static void Print(string name, CorpusSong[] songs)
    {
        var bars = songs.SelectMany(Measure).ToArray();
        string Share(Func<Bar, bool> of) => $"{bars.Count(of) / (double)Math.Max(1, bars.Length):P1}";
        Console.WriteLine($"{name}: {bars.Length} kit bars with drums of {bars.Select(x => x.Seed).Distinct().Count()} songs: both {Share(x => x.Backbeat && x.Time)}, " +
                          $"backbeat without time {Share(x => x.Backbeat && !x.Time)} in {bars.Where(x => x.Backbeat && !x.Time).Select(x => x.Seed).Distinct().Count()} songs, " +
                          $"time without backbeat {Share(x => !x.Backbeat && x.Time)} in {bars.Where(x => !x.Backbeat && x.Time).Select(x => x.Seed).Distinct().Count()} songs, " +
                          $"neither {Share(x => !x.Backbeat && !x.Time)}");
        foreach (var group in bars.Where(x => !(x.Backbeat && x.Time)).GroupBy(x => (x.Cause, x.Section)).OrderByDescending(x => x.Count()))
            Console.WriteLine($"  {group.Key.Cause}{group.Key.Section}: {group.Count() / (double)bars.Length:P1} of bars, {group.Select(x => x.Seed).Distinct().Count()} songs");
    }

    private static IEnumerable<Bar> Measure(CorpusSong song)
    {
        if ((DrumJob)song.Trace.Single(x => x.Point == TracePoints.DrumJob).Value! != DrumJob.Groove || !song.PlaysDrums)
            yield break;

        var notes = song.Song.Notes!;
        var drums = song.Song.TrackDefinitions.Where(x => x.Value.Role == TrackRole.Drum && notes.ContainsKey(x.Key)).Select(x => x.Key).ToArray();
        DrumRole SongRole(int track) => DrumRoles.Of(song.Song.TrackDefinitions[track].StateMap);
        T Section<T>(string point, int id) => (T)song.Trace.First(x => x.Point == point && x.Section == id).Value!;
        var solos = song.Trace.Where(x => x.Point == TracePoints.DrumSolo).Select(x => x.Section).ToHashSet();
        var spans = song.Map.Sections;
        for (var index = 0; index < spans.Length; index++)
        {
            var span = spans[index];
            if (!song.HasDrums(span) || Section<bool>(TracePoints.PercussionOnly, span.SectionId) || solos.Contains(span.SectionId))
                continue;

            var changed = Section<ImmutableDictionary<int, DrumRole>>(TracePoints.DrumRoles, span.SectionId);
            var kit = Section<SectionKit>(TracePoints.Kit, span.SectionId);
            DrumRole RoleOf(int track) => changed.TryGetValue(track, out var role) ? role : SongRole(track);
            var label = (Section<bool>(TracePoints.Breakdown, span.SectionId) ? ", a breakdown" : "") + (Section<System.Collections.Immutable.ImmutableArray<double>>(TracePoints.StopTime, span.SectionId).Length > 0 ? ", stop time" : "");
            var from = song.DrumsFrom(index);
            var bars = span.BarCount;
            for (var bar = from * span.Meter.PatternBarCount; bar < bars; bar++)
            {
                var (start, end) = span.Bar(bar);
                var sounding = drums.Where(t => notes[t].Any(x => x.Position >= start - 1e-9 && x.Position < end - 1e-9)).ToArray();
                if (sounding.Length == 0)
                    continue;
                var backbeat = sounding.Any(t => RoleOf(t) == DrumRole.Backbeat);
                var time = sounding.Any(t => RoleOf(t) == DrumRole.Time);
                string Cause(DrumRole role)
                {
                    var holders = kit.Drums.Select(DrumGroups.GetTrackNumber).Where(t => RoleOf(t) == role).ToArray();
                    if (holders.Length > 0)
                        return $"{role} silent";
                    return kit.Leads.GetValueOrDefault(role) is { } lead
                        ? $"{role}'s lead drawn to {RoleOf(DrumGroups.GetTrackNumber(lead))}"
                        : $"no {role} lead";
                }

                var cause = (backbeat, time) switch
                {
                    (true, false) => Cause(DrumRole.Time),
                    (false, true) => Cause(DrumRole.Backbeat),
                    (false, false) => $"{Cause(DrumRole.Backbeat)}; {Cause(DrumRole.Time)}",
                    _ => ""
                };
                yield return new Bar(song.Seed, backbeat, time, cause, label);
            }
        }
    }
}
