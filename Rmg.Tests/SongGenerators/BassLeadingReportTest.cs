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

/// <summary>
///     How the bass lands on its chord changes, by how much its instrument leads and whether the section is plain or wild
///     (by its melody's answer amount, which rises with the section's unconventionality): how often the landing is drawn
///     as the root (<see cref="StateKinds.ChordArrival" />), and how often the note played is the new chord's root, of
///     all landings and of those left free, which play their figure's note.
/// </summary>
public sealed class BassArrivalReportTest
{
    private const int SongCount = 100;

    [Test]
    [Explicit]
    public async Task Report()
    {
        var counts = new Dictionary<(double Leading, bool IsWild), (int Changes, int RootDrawn, int OnRoot, int Free, int FreeOnRoot)>();
        foreach (var song in TestCorpus.Range(SongCount))
        {
            var program = ((PitchInstrumentTrack)song.Song.TrackDefinitions[SongTracks.BassTrack]).InstrumentCode;
            var leading = Core.Composition.InstrumentRoles.Bass.Instruments.Single(x => x.Program == program).Leading;
            var amounts = song.Trace.Where(x => x.Point == TracePoints.MelodyAnswer).ToDictionary(x => x.Section, x => (double)x.Value!);
            var bass = song.Song.Notes![SongTracks.BassTrack].ToArray();
            for (var i = 0; i + 1 < bass.Length; i++)
            {
                var (last, next) = (bass[i], bass[i + 1]);
                var change = (Math.Floor(last.Position / Meter.BarDuration) + 1) * Meter.BarDuration;
                if (Math.Abs(next.Position - change) > 1e-9 || song.Map.SectionAt(next.Position) is not { } section)
                    continue;

                var (_, _, lastClasses) = Realizer.GetChordNotes(last.Value.State);
                var (nextChord, _, nextClasses) = Realizer.GetChordNotes(next.Value.State);
                if (lastClasses.SetEquals(nextClasses))
                    continue;

                var key = (leading, amounts[section.SectionId] > MelodyLayers.AnswerAmount);
                var arrival = (ChordArrival)next.Value.State.GetStateValue(StateKinds.ChordArrival);
                var isOnRoot = next.Value.Pitches[0].Mod(12) == nextChord.Root.Mod(12);
                var c = counts.GetValueOrDefault(key);
                counts[key] = (
                    c.Changes + 1,
                    c.RootDrawn + (arrival == ChordArrival.Root ? 1 : 0),
                    c.OnRoot + (isOnRoot ? 1 : 0),
                    c.Free + (arrival == ChordArrival.Free ? 1 : 0),
                    c.FreeOnRoot + (arrival == ChordArrival.Free && isOnRoot ? 1 : 0)
                );
            }
        }

        foreach (var ((leading, isWild), c) in counts.OrderBy(x => x.Key.Leading).ThenBy(x => x.Key.IsWild))
            Console.WriteLine($"leading {leading:F2} {(isWild ? "wild " : "plain")}, {c.Changes} changes: root drawn {c.RootDrawn / (double)c.Changes:P0}, " +
                              $"on the root {c.OnRoot / (double)c.Changes:P0}; free {c.Free / (double)c.Changes:P0}, of which on the root {c.FreeOnRoot / (double)Math.Max(1, c.Free):P0}");
        await Task.CompletedTask;
    }
}

/// <summary>
///     How the bass line moves: its mean move from note to note, how many of its moves leap a fifth or more and an
///     octave or more, how far a song's bass spans, and how much of a recurring section's bass plays the pitches of its
///     first appearance.
/// </summary>
public sealed class BassLineReportTest
{
    private const int SongCount = 100;

    [Test]
    [Explicit]
    public async Task Report()
    {
        var moves = new List<int>();
        var spans = new List<int>();
        int recurring = 0, recurringSame = 0;
        foreach (var song in TestCorpus.Range(SongCount))
        {
            var bass = song.Song.Notes![SongTracks.BassTrack].ToArray();
            moves.AddRange(bass.Zip(bass.Skip(1), (a, b) => Math.Abs(b.Value.Pitches[0] - a.Value.Pitches[0])));
            spans.Add(bass.Max(x => x.Value.Pitches[0]) - bass.Min(x => x.Value.Pitches[0]));
            var pitches = bass.ToDictionary(x => Math.Round(x.Position, 6), x => x.Value.Pitches[0]);
            var firsts = song.Map.Sections.GroupBy(x => x.SectionId).ToDictionary(x => x.Key, x => x.First());
            foreach (var span in song.Map.Sections.Where(x => x != firsts[x.SectionId]))
            {
                var first = firsts[span.SectionId];
                foreach (var (position, pitch) in pitches.Where(x => x.Key >= first.Start && x.Key < first.End))
                    if (pitches.TryGetValue(Math.Round(span.Start + position - first.Start, 6), out var again))
                    {
                        recurring++;
                        recurringSame += again == pitch ? 1 : 0;
                    }
            }
        }

        Console.WriteLine($"bass: mean move {moves.Average():F2} semitones, a fifth or more {moves.Count(x => x >= 7) / (double)moves.Count:P1}, " +
                          $"an octave or more {moves.Count(x => x >= 12) / (double)moves.Count:P1}; a song's bass spans {spans.Average():F1} semitones, {spans.Max()} at most; " +
                          $"a recurring section plays its first appearance's pitches {recurringSame / (double)recurring:P1}");
        await Task.CompletedTask;
    }
}
