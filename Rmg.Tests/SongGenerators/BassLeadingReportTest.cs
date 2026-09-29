using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Tests.SongGenerators;

/// <summary>
///     How the bass leads into its chord changes, by how much its instrument leads (<see cref="BassLeadingLayers" />):
///     at every bar line where the chord changes and a bass note starts, how often the bar asks to lead into it, how
///     often a note starts in the last beat before it, where the approach plays, and how the line joins the change.
/// </summary>
public sealed class BassLeadingReportTest
{
    private const int SongCount = 100;

    /// <param name="Changes">Bar lines where the chord changes and a bass note starts.</param>
    /// <param name="Asked">Of those, the bar before asks to lead in (<see cref="StateKinds.ChordApproach" />).</param>
    /// <param name="LastBeat">The last note before the change starts in the last beat.</param>
    /// <param name="Led">Both: the approach plays.</param>
    /// <param name="ByStep">The last note is a step (one or two semitones) from the first after the change.</param>
    /// <param name="Resolving">The last note is not of its own chord, and steps onto the new chord's.</param>
    /// <param name="OffChord">The first note after the change is not of the new chord.</param>
    /// <param name="LastBeatLoudness">The velocities of the notes in the last beat, each over its bar's first note's, summed.</param>
    internal sealed record Measures(int Changes, int Asked, int LastBeat, int Led, int ByStep, int Resolving, int OffChord, double LastBeatLoudness);

    internal static Dictionary<double, Measures> Measure(IEnumerable<CorpusSong> songs)
    {
        var byLeading = new Dictionary<double, Measures>();
        foreach (var song in songs)
        {
            var program = ((PitchInstrumentTrack)song.Song.TrackDefinitions[SongTracks.BassTrack]).InstrumentCode;
            var leading = Core.Composition.InstrumentRoles.Bass.Instruments.Single(x => x.Program == program).Leading;
            var m = byLeading.GetValueOrDefault(leading, new Measures(0, 0, 0, 0, 0, 0, 0, 0));
            var bass = song.Song.Notes![SongTracks.BassTrack].ToArray();
            // the rendered velocities, by position
            var velocities = song.Notes(SongTracks.BassTrack).GroupBy(x => x.Position).ToDictionary(x => x.Key, x => x.Max(y => y.Value.Velocity));
            for (var i = 0; i + 1 < bass.Length; i++)
            {
                var (last, next) = (bass[i], bass[i + 1]);
                var change = (Math.Floor(last.Position / Meter.BarDuration) + 1) * Meter.BarDuration;
                if (Math.Abs(next.Position - change) > 1e-9)
                    continue;

                var (_, _, lastClasses) = Realizer.GetChordNotes(last.Value.State);
                var (_, _, nextClasses) = Realizer.GetChordNotes(next.Value.State);
                if (lastClasses.SetEquals(nextClasses))
                    continue;

                var (lastPitch, nextPitch) = (last.Value.Pitches[0], next.Value.Pitches[0]);
                var isAsked = last.Value.State.GetStateValue(StateKinds.ChordApproach) != 0;
                var isLastBeat = last.Position >= change - 1;
                var isStep = Math.Abs(lastPitch - nextPitch) is 1 or 2;
                var isOnChord = nextClasses.Contains(nextPitch.Mod(12));
                var barFirst = bass.First(x => x.Position >= change - Meter.BarDuration - 1e-9);
                m = new Measures(
                    m.Changes + 1,
                    m.Asked + (isAsked ? 1 : 0),
                    m.LastBeat + (isLastBeat ? 1 : 0),
                    m.Led + (isAsked && isLastBeat ? 1 : 0),
                    m.ByStep + (isStep ? 1 : 0),
                    m.Resolving + (isStep && isOnChord && !lastClasses.Contains(lastPitch.Mod(12)) ? 1 : 0),
                    m.OffChord + (isOnChord ? 0 : 1),
                    m.LastBeatLoudness + (isLastBeat ? velocities[last.Position] / velocities[barFirst.Position] : 0)
                );
            }

            byLeading[leading] = m;
        }

        return byLeading;
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var (leading, m) in Measure(TestCorpus.Range(SongCount)).OrderBy(x => x.Key))
        {
            double Of(int x) => x / (double)m.Changes;
            Console.WriteLine($"leading {leading:F2}, {m.Changes} changes: asked {Of(m.Asked):P0}, a note in the last beat {Of(m.LastBeat):P0}, " +
                              $"led {Of(m.Led):P0}; by step {Of(m.ByStep):P0}, resolving {Of(m.Resolving):P0}, off the new chord {Of(m.OffChord):P0}; " +
                              $"a note in the last beat as loud as its bar's first times {m.LastBeatLoudness / m.LastBeat:F2}");
        }

        await Task.CompletedTask;
    }
}
