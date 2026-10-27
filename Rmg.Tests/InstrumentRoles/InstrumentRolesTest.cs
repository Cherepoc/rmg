using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Tests.InstrumentRoles;

public sealed class InstrumentRolesTest
{
    // every pitched part's instruments
    private static readonly InstrumentRole[] Roles = [..Enum.GetValues<TrackRole>().Select(InstrumentsOverTime.RoleOf).OfType<InstrumentRole>().Distinct()];

    [Test]
    public async Task Roles_HoldNoSoundEffectsOrDrums_AndNoInstrumentTwice()
    {
        foreach (var role in Roles)
        {
            // 112 and up are percussive instruments and sound effects, but the steel drums, which play a tune
            await Assert.That(role.Instruments.All(x => x.Program is >= 0 and < 112 or 114)).IsTrue().Because(role.Name);
            // every instrument played somewhere, the wildest songs at least
            await Assert.That(role.Instruments.All(x => x.Weight.Wild > 0)).IsTrue().Because(role.Name);
            await Assert.That(role.Instruments.Select(x => x.Program).Distinct().Count()).IsEqualTo(role.Instruments.Length).Because(role.Name);
        }
    }

    [Test]
    public async Task APlainSoundsSong_PlaysNoInstrumentWithCharacter()
    {
        // the plainest sound, its instruments those its plain end weighs, a switch's too
        var plain = new SongOverrides(Facets: TestCorpus.FacetsOf((Facet.Sound, 0)));
        var checkedPrograms = 0;
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, plain)))
        foreach (var (track, definition) in song.Song.TrackDefinitions)
        {
            if (definition is not PitchInstrumentTrack part || InstrumentsOverTime.RoleOf(part.Role) is not { } role)
                continue;
            // but a note sounding as another part, a counter-melody in the melody's sound
            var programs = song.Song.Notes!.GetValueOrDefault(track)?.Where(x => x.Value.State.GetStateValue(CompositionStateKinds.SoundsAs) == 0).Select(x => InstrumentsOverTime.ProgramOf(x.Value.State, part.InstrumentCode)).Distinct() ?? [];
            // every one its role lists and plain, but an articulation's variant of one it plays, the same player's (InstrumentsOverTime.Variants)
            var played = programs.Append(part.InstrumentCode).ToHashSet();
            foreach (var program in played)
            {
                checkedPrograms++;
                if (role.Instruments.FirstOrDefault(x => x.Program == program) is { } listed)
                    await Assert.That(listed.Weight.Plain).IsGreaterThan(0).Because($"seed {song.Seed}, {part.Role} {program}");
                else
                    await Assert.That(InstrumentsOverTime.Variants.Any(x => played.Contains(x.Key) && x.Value.Contains(program))).IsTrue().Because($"seed {song.Seed}, {part.Role} {program} unlisted");
            }
        }

        await Assert.That(checkedPrograms).IsGreaterThan(32);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        // the parts on an instrument that has character, never the plainest songs', by fifths of the sound facet
        var parts = TestCorpus.Measure(256, song => song.Song.TrackDefinitions.Values.OfType<PitchInstrumentTrack>().Where(x => InstrumentsOverTime.RoleOf(x.Role) is not null)
            .Select(x => (Fifth: Math.Min(4, (int)(song.Song.Draws!.Unconventionality[Facet.Sound] * 5)), x.InstrumentCode, Character: InstrumentsOverTime.RoleOf(x.Role)!.Instruments.Single(i => i.Program == x.InstrumentCode).Weight.Plain == 0))
            .ToArray()).SelectMany(x => x).ToArray();
        foreach (var fifth in Enumerable.Range(0, 5))
        {
            var of = parts.Where(x => x.Fifth == fifth).ToArray();
            Console.WriteLine($"sound fifth {fifth}: {of.Length} parts, {100.0 * of.Count(x => x.Character) / Math.Max(1, of.Length):F1}% on an instrument with character");
        }
        Console.WriteLine("most played: " + string.Join(", ", parts.Where(x => x.Character).GroupBy(x => GeneralMidi.Programs[x.InstrumentCode]).OrderByDescending(x => x.Count()).Take(12).Select(x => $"{x.Key} {x.Count()}")));
        await Task.CompletedTask;
    }

    [Test]
    public async Task Bass_IsPlayedByLowInstruments()
    {
        // the basses (32 to 39), the contrabass, the tuba and the bassoon
        int[] low = [..Enumerable.Range(32, 8), 43, 58, 70];

        await Assert.That(Rmg.Core.Composition.InstrumentRoles.Bass.Instruments.All(x => low.Contains(x.Program))).IsTrue();
    }

    [Test]
    public async Task Pick_FollowsTheWeights_AndLeavesOutTheExcluded()
    {
        const int drawCount = 50_000;
        var role = Rmg.Core.Composition.InstrumentRoles.Bass;
        var context = new GenerationContext(1);
        var counts = Enumerable.Range(0, drawCount).Select(_ => role.Pick(context, 0.5, null, 33).Program).CountBy(x => x).ToDictionary();
        var weightSum = role.Instruments.Where(x => x.Program != 33).Sum(x => x.Weight.Tuned);

        await Assert.That(counts.ContainsKey(33)).IsFalse();
        foreach (var instrument in role.Instruments.Where(x => x.Program != 33))
            await Assert.That(counts.GetValueOrDefault(instrument.Program) / (double)drawCount)
                .IsEqualTo(instrument.Weight.Tuned / weightSum)
                .Within(0.01)
                .Because(instrument.Name);
    }

    [Test]
    public async Task Pick_WithEveryInstrumentExcluded_ResultsIn_InvalidOperation()
    {
        var role = new InstrumentRole("Solo", [new RoleInstrument(0, "Piano", 1)]);

        await Assert.That(() => role.Pick(new GenerationContext(0), 0.5, null, 0)).Throws<InvalidOperationException>();
    }

    /// <summary>
    ///     A pool that keeps nothing apart from every part given lets its part share a sound, the first given kept apart the
    ///     longest: a riff out of guitars of its own takes the chords' and never the melody's.
    /// </summary>
    [Test]
    public async Task Pick_WithAPoolOfNothingElse_SharesTheLastGivensSound_NeverTheFirsts()
    {
        var role = new InstrumentRole("Riff", [new RoleInstrument(29, "Overdriven Guitar", 1), new RoleInstrument(30, "Distortion Guitar", 1)]);
        var context = new GenerationContext(0);
        var picks = Enumerable.Range(0, 64).Select(_ => role.Pick(context, 0.5, x => x.Program is 29 or 30 ? 1 : 0, 29, 30).Program).ToHashSet();

        await Assert.That(picks.SetEquals([30])).IsTrue();
    }

    /// <summary>The melody never on the chords' instrument (each on one of its role's, AnyInstrumentTest).</summary>
    [Test]
    public async Task Songs_PlayTheMelody_NotAsTheChords()
    {
        for (var seed = 0; seed < 64; seed++)
        {
            var tracks = TestCorpus.Get(seed).Song.TrackDefinitions.Values.OfType<PitchInstrumentTrack>().ToArray();
            int Program(TrackRole role) => tracks.Single(x => x.Role == role).InstrumentCode;

            await Assert.That(Program(TrackRole.Melody)).IsNotEqualTo(Program(TrackRole.Chords)).Because($"seed {seed}");
        }
    }
}
