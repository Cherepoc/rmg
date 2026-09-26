using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Tests.InstrumentRoles;

public sealed class InstrumentRolesTest
{
    private static readonly InstrumentRole[] Roles =
        [Rmg.Core.Composition.InstrumentRoles.Chords, Rmg.Core.Composition.InstrumentRoles.Melody, Rmg.Core.Composition.InstrumentRoles.Bass];

    [Test]
    public async Task Roles_HoldNoSoundEffectsOrDrums_AndNoInstrumentTwice()
    {
        foreach (var role in Roles)
        {
            // 112 and up are percussive instruments and sound effects
            await Assert.That(role.Instruments.All(x => x.Program is >= 0 and < 112)).IsTrue().Because(role.Name);
            await Assert.That(role.Instruments.All(x => x.Weight > 0)).IsTrue().Because(role.Name);
            await Assert.That(role.Instruments.Select(x => x.Program).Distinct().Count()).IsEqualTo(role.Instruments.Length).Because(role.Name);
        }
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
        var counts = Enumerable.Range(0, drawCount).Select(_ => role.Pick(context, 33).Program).CountBy(x => x).ToDictionary();
        var weightSum = role.Instruments.Where(x => x.Program != 33).Sum(x => x.Weight);

        await Assert.That(counts.ContainsKey(33)).IsFalse();
        foreach (var instrument in role.Instruments.Where(x => x.Program != 33))
            await Assert.That(counts.GetValueOrDefault(instrument.Program) / (double)drawCount)
                .IsEqualTo(instrument.Weight / weightSum)
                .Within(0.01)
                .Because(instrument.Name);
    }

    [Test]
    public async Task Pick_WithEveryInstrumentExcluded_ResultsIn_InvalidOperation()
    {
        var role = new InstrumentRole("Solo", [new RoleInstrument(0, "Piano", 1)]);

        await Assert.That(() => role.Pick(new GenerationContext(0), 0)).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Songs_PlayEachPitchedTrackWithAnInstrumentOfItsRole_TheMelodyNotAsTheChords()
    {
        for (var seed = 0; seed < 50; seed++)
        {
            var tracks = SongGenerator.GenerateSong(seed).TrackDefinitions;
            int Program(int track) => ((PitchInstrumentTrack)tracks[track]).InstrumentCode;

            await Assert.That(Rmg.Core.Composition.InstrumentRoles.Chords.Instruments.Any(x => x.Program == Program(4))).IsTrue();
            await Assert.That(Rmg.Core.Composition.InstrumentRoles.Melody.Instruments.Any(x => x.Program == Program(5))).IsTrue();
            await Assert.That(Rmg.Core.Composition.InstrumentRoles.Bass.Instruments.Any(x => x.Program == Program(6))).IsTrue();
            await Assert.That(Program(5)).IsNotEqualTo(Program(4)).Because($"seed {seed}");
        }
    }
}
