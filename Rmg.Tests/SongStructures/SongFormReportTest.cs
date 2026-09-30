using Rmg.Core.Composition;

namespace Rmg.Tests.SongStructures;

/// <summary>
///     The songs' forms: how many take one of the forms songs are written in, how long their sections play, their
///     energy by role, and how often the chorus is the song's loudest section.
/// </summary>
public sealed class SongFormReportTest
{
    [Test]
    public async Task ASongOfAForm_PlaysASectionForEveryRole_NoneFollowingItself()
    {
        foreach (var song in TestCorpus.Range(30))
        {
            var structure = (SongStructure)song.Trace.Single(x => x.Point == TracePoints.SongForm).Value!;
            var roles = structure.SectionIds.Select(x => structure.Roles[x]).ToArray();
            await Assert.That(structure.SectionIds.Zip(structure.SectionIds.Skip(1)).All(x => x.First != x.Second)).IsTrue();
            if (roles.All(x => x == SectionRole.Free))
                continue;

            await Assert.That(structure.Roles.Values.Distinct().Count()).IsEqualTo(structure.Roles.Count);
            await Assert.That(SongForms.Forms.Any(x => x.Value.SequenceEqual(roles))).IsTrue();
        }
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var songs = TestCorpus.Range(200).ToArray();
        var energies = new Dictionary<SectionRole, List<double>>();
        var plays = new Dictionary<SectionRole, List<int>>();
        var (formed, chorusLoudest, withChorus, bridgesAway, bridges) = (0, 0, 0, 0, 0);
        var bars = new List<double>();
        foreach (var song in songs)
        {
            var structure = (SongStructure)song.Trace.Single(x => x.Point == TracePoints.SongForm).Value!;
            var energy = song.Trace.Where(x => x.Point == TracePoints.SectionEnergy).ToDictionary(x => x.Section, x => ((SectionEnergyTrace)x.Value!).Energy);
            var lengths = song.Trace.Where(x => x.Point == TracePoints.SectionLength).ToDictionary(x => x.Section, x => (int)x.Value!);
            bars.Add(song.Map.Sections.Sum(x => x.Duration) / song.Map.Meter.BarDuration);
            if (structure.Roles.Values.Any(x => x != SectionRole.Free))
                formed++;
            foreach (var (id, role) in structure.Roles)
            {
                (energies.TryGetValue(role, out var e) ? e : energies[role] = []).Add(energy[id]);
                (plays.TryGetValue(role, out var p) ? p : plays[role] = []).Add(lengths[id]);
            }

            if (structure.Roles.FirstOrDefault(x => x.Value == SectionRole.Chorus) is { Value: SectionRole.Chorus } chorus)
            {
                withChorus++;
                chorusLoudest += energy[chorus.Key] >= energy.Values.Max() - 1e-9 ? 1 : 0;
            }
        }

        Console.WriteLine($"{formed} of {songs.Length} songs take a form; the chorus the loudest section in {chorusLoudest} of {withChorus}");
        foreach (var (role, e) in energies.OrderBy(x => x.Key))
            Console.WriteLine($"{role}: {e.Count} sections, energy {e.Average():F2}, plays once {plays[role].Count(x => x == 1) / (double)plays[role].Count:P0}, " +
                              $"twice {plays[role].Count(x => x == 2) / (double)plays[role].Count:P0}, four times {plays[role].Count(x => x == 4) / (double)plays[role].Count:P0}");
        var keyChanges = songs.Select(x => (KeyChange?)x.Trace.Single(t => t.Point == TracePoints.KeyChange).Value).OfType<KeyChange>().ToArray();
        Console.WriteLine($"{keyChanges.Length} songs go up a key for their last section, {keyChanges.Count(x => x.Semitones == 2)} a whole step");
        var sorted = bars.Order().ToArray();
        Console.WriteLine($"the sections' bars a song: median {sorted[sorted.Length / 2]}, 10% {sorted[sorted.Length / 10]}, 90% {sorted[sorted.Length * 9 / 10]}");
        await Task.CompletedTask;
    }
}
