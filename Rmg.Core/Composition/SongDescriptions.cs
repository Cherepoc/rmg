using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     A song told as a listener would (<see cref="SongDescription" />), from what its generation decided, as its trace
///     records it, and from the song as it plays; it decides nothing of its own, so describing a song leaves it as it is.
/// </summary>
public static class SongDescriptions
{
    private static readonly string[] KeyNames = ["C", "C♯", "D", "E♭", "E", "F", "F♯", "G", "A♭", "A", "B♭", "B"];

    /// <summary>The song the seed makes with what is given, and as the mix plays it, described.</summary>
    public static SongDescription Describe(ulong seed, SongOverrides overrides, SongMix mix, CancellationToken cancellation)
    {
        using var trace = StateTrace.Start(explains: false);
        var song = SongGenerator.GenerateSong(seed, overrides, cancellation);
        var rendered = Render.RenderSong(song, mix, cancellation);
        return Of(song, rendered, trace.Entries);
    }

    /// <summary>A song described, from its rendering and the trace its generation recorded.</summary>
    internal static SongDescription Of(Song song, RenderedSong rendered, IReadOnlyList<StateTraceEntry> trace)
    {
        var map = song.Map!;
        var draws = song.Draws!;
        T Value<T>(string point) => (T)trace.First(x => x.Point == point).Value!;
        var common = song.TrackEventStateTimelineMap.CommonStateTimelineMap;
        var keys = common.GetStateTimeline(StateKinds.KeyOffset);
        var tempo = rendered.TempoTimeline;
        double Bpm(double position) => Math.Round(tempo.GetEffectiveValueAt(position) * Meter.BaseTempo);

        var (_, songScale) = Value<(HarmonicUnconventionality, Scale)>(TracePoints.SongHarmony);
        var sectionScales = trace.Where(x => x.Point == TracePoints.SectionScale).GroupBy(x => x.Section).ToDictionary(x => x.Key, x => (Scale)x.First().Value!);
        string KeyAt(double position, Scale scale) => $"{KeyNames[keys.GetEffectiveValueAt(position).Mod(12)]} {scale.Name}";

        var origin = map.Origin;
        var songKey = KeyAt(origin, songScale);
        var songBpm = Bpm(origin);
        var songFeel = Value<int>(TracePoints.Feel);

        // the sections' energies against the song's: its quietest third quiet and its loudest loud
        var energies = trace.Where(x => x.Point == TracePoints.SectionEnergy).GroupBy(x => x.Section).ToDictionary(x => x.Key, x => ((SectionEnergyTrace)x.First().Value!).Energy);
        var levels = Levels(energies);
        var structure = Value<SongStructure>(TracePoints.SongForm);
        var letters = structure.SectionIds.Distinct().Select((id, i) => (id, i)).ToDictionary(x => x.id, x => (char)('A' + x.i));
        var rests = trace.Where(x => x.Point == TracePoints.Arrangement).Select(x => (ImmutableHashSet<TrackRole>)x.Value!).ToArray();
        var textures = trace.Where(x => x.Point == TracePoints.Texture).Select(x => ((int Index, TextureKind Kind, ImmutableArray<ImmutableHashSet<TrackRole>>))x.Value!).ToDictionary(x => x.Index, x => x.Kind);
        var solos = trace.Where(x => x.Point == TracePoints.Solo).Select(x => ((int Index, SoloPlan Plan))x.Value!).ToDictionary(x => x.Index, x => x.Plan);
        var percussionOnly = trace.Where(x => x.Point == TracePoints.PercussionOnly).GroupBy(x => x.Section).ToDictionary(x => x.Key, x => (bool)x.First().Value!);
        var timeFeels = trace.Where(x => x.Point == TracePoints.TimeFeel).GroupBy(x => x.Section).ToDictionary(x => x.Key, x => (int)x.First().Value!);
        var feels = trace.Where(x => x.Point == TracePoints.Feel && x.Track == SectionGenerator.SectionTrace).GroupBy(x => x.Section).ToDictionary(x => x.Key, x => (int)x.First().Value!);
        var parts = song.TrackDefinitions.Where(x => song.Notes!.TryGetValue(x.Key, out var notes) && notes.Count > 0)
            .GroupBy(x => x.Value.Role).ToDictionary(x => x.Key, x => x.Select(y => y.Key).ToArray());

        var sections = map.Sections.Select((span, index) =>
        {
            var id = span.SectionId;
            var resting = index < rests.Length ? rests[index] : [];
            var role = structure.Roles[id];
            var scale = sectionScales.GetValueOrDefault(id, songScale);
            var key = KeyAt(span.Start, scale);
            var bpm = Bpm(span.Start);
            var drums = resting.Contains(TrackRole.Drum) || !parts.ContainsKey(TrackRole.Drum) ? "resting"
                : solos.TryGetValue(index, out var drumSolo) && drumSolo.IsDrumSolo ? "a drum solo"
                : percussionOnly.GetValueOrDefault(id) && draws.DrumSetup != DrumSetup.Percussion ? "percussion only"
                : timeFeels.GetValueOrDefault(id) switch { < 0 => "half time", > 0 => "double time", _ => null };
            var playing = parts.Where(x => !resting.Contains(x.Key) && x.Value.Any(track => song.Notes![track].Any(n => n.Position >= span.Start && n.Position < span.End)))
                .Select(x => PartName(x.Key)).Distinct().ToImmutableArray();
            return new SectionDescription(
                index + 1,
                role == SectionRole.Free ? $"Section {letters[id]}" : RoleName(role),
                (int)Math.Round(span.Duration / span.Meter.BarDuration),
                levels[id],
                key == songKey ? null : key,
                span.Meter == map.Meter ? null : TimeSignature(span.Meter),
                Math.Abs(bpm - songBpm) < 0.5 ? null : bpm,
                feels.TryGetValue(id, out var feel) && feel != songFeel ? FeelName(feel) : null,
                drums,
                TextureName(textures.GetValueOrDefault(index, TextureKind.Steady)),
                playing,
                solos.TryGetValue(index, out var solo) ? [..solo.Soloists.Select(PartName)] : [],
                []
            );
        }).ToImmutableArray();

        return new SongDescription(
            Math.Round(Seconds(tempo, rendered.Duration), 1),
            sections.Sum(x => x.Bars),
            songBpm,
            TimeSignature(map.Meter),
            FeelName(songFeel),
            SwingName(Value<Swing>(TracePoints.Swing)),
            songKey,
            [
                ..Value<ImmutableArray<KeyChange>>(TracePoints.KeyChange).Select(change =>
                {
                    var place = map.Sections.Select((x, i) => (x, i)).Last(x => x.x.Start <= change.Position + 1e-9).i;
                    return new KeyChangeDescription(place + 1, change.Semitones, KeyAt(change.Position + 1e-9, sectionScales.GetValueOrDefault(map.Sections[place].SectionId, songScale)));
                })
            ],
            new UnconventionalityDescription(
                Math.Round(draws.Unconventionality.Base, 2),
                Word(draws.Unconventionality.Base),
                [..Enum.GetValues<Facet>().Select(x => new FacetDescription(x.ToString(), Math.Round(draws.Unconventionality[x], 2), Word(draws.Unconventionality[x])))]
            ),
            [
                // a riff's twin as the riff, one part, its instrument among the riff's where it is another
                ..rendered.Tracks.Where(x => !x.IsPercussionInstrument && x.NoteTimeline.Count > 0).GroupBy(x => PartName(x.Role))
                    .Select(x => new PartDescription(
                        x.Key,
                        GeneralMidi.Programs[x.First().PitchInstrumentCode],
                        [
                            ..x.SelectMany(y => y.ProgramChanges.Select(z => z.Program).Prepend(y.PitchInstrumentCode))
                                .Where(y => y != x.First().PitchInstrumentCode).Distinct().Select(y => GeneralMidi.Programs[y])
                        ]
                    ))
            ],
            parts.ContainsKey(TrackRole.Drum)
                ? new DrumsDescription(
                    SetupName(draws.DrumSetup),
                    GeneralMidi.KitName(rendered.Tracks.First(x => x.IsPercussionInstrument).PitchInstrumentCode),
                    [..DrumGroups.AllDrums.Where(x => song.Notes!.TryGetValue(DrumGroups.GetTrackNumber(x), out var n) && n.Count > 0).Select(x => x.Name)]
                )
                : null,
            IntroName(map.Intro, map.Meter),
            EndingName(map.Ending),
            sections,
            MelodyOf(song, trace)
        );
    }

    /// <summary>Every section's energy in a word, by its rank among the song's: its quietest third quiet, its loudest loud.</summary>
    private static Dictionary<int, string> Levels(Dictionary<int, double> energies)
    {
        var ordered = energies.Values.Distinct().Order().ToArray();
        return energies.ToDictionary(x => x.Key, x =>
        {
            var rank = Array.IndexOf(ordered, x.Value);
            return ordered.Length < 2 ? "medium" : rank < ordered.Length / 3.0 ? "quiet" : rank >= 2 * ordered.Length / 3.0 ? "loud" : "medium";
        });
    }

    /// <summary>How long the song plays, its tempo as it changes over it, from its start, where none has changed it yet.</summary>
    private static double Seconds(StateTimeline<double> tempo, double duration)
    {
        double[] starts = [0, ..tempo.Select(x => x.Position).Where(x => x > 0 && x < duration)];
        return starts.Select((start, i) => (start, end: i + 1 < starts.Length ? starts[i + 1] : duration))
            .Sum(x => (x.end - x.start) * 60 / (tempo.GetEffectiveValueAt(x.start) * Meter.BaseTempo));
    }

    private static string Word(double value) => value < 1 / 3.0 ? "plain" : value > 2 / 3.0 ? "wild" : "middling";

    private static string TimeSignature(Meter meter) => $"{meter.TimeSignature.Numerator}/{meter.TimeSignature.Denominator}";

    private static string FeelName(int feel)
    {
        if (feel == Feels.Straight)
            return "straight";
        return feel.ToTuplet() switch
        {
            3 => "triplets",
            5 => "quintuplets",
            7 => "septuplets",
            1 => $"grouped in {feel.ToRhythmPeriodValue():0.##}s",
            var tuplet => $"{tuplet}-tuplets"
        };
    }

    private static string SwingName(Swing swing) =>
        swing.Delay == 0 ? "none" : $"swung {(swing.Period switch { 1 => "8ths", 0.5 => "16ths", 2 => "quarters", var p => $"pairs of {p:0.##} beats" })}, {swing.Delay:0.##} of a beat late";

    private static string RoleName(SectionRole role) => role == SectionRole.PreChorus ? "Pre-chorus" : role.ToString();

    private static string PartName(TrackRole role) => role switch
    {
        TrackRole.CounterMelody => "counter-melody",
        TrackRole.RiffTwin => "riff",
        TrackRole.Drum => "drums",
        TrackRole.Rhythm => "rhythm part",
        TrackRole.Solo => "soloist",
        _ => role.ToString().ToLowerInvariant()
    };

    private static string SetupName(DrumSetup setup) => setup switch
    {
        DrumSetup.Kit => "drum kit",
        DrumSetup.KitAndPercussion => "drum kit and percussion",
        _ => "percussion"
    };

    private static string TextureName(TextureKind kind) => kind switch
    {
        TextureKind.Build => "building up part by part",
        TextureKind.Thin => "thinning out",
        TextureKind.Alone => "one part alone",
        _ => "steady"
    };

    private static string IntroName(IntroSpan intro, Meter meter) => intro.Kind switch
    {
        IntroKind.CountIn => "a count-in on the hi-hat",
        IntroKind.Entries => $"the band coming in part by part over {intro.Window.Bars} bars",
        _ => "the whole band at once"
    };

    private static string EndingName(EndingSpan ending)
    {
        var name = ending.Kind switch
        {
            EndingKind.Open => "stopping on the last section's cadence, unresolved",
            EndingKind.Button => "a short hit on the home chord",
            EndingKind.RingOut => "the home chord held",
            EndingKind.Stop => "the band stopping, then hitting the home chord together",
            _ => "fading out over the last section"
        };
        return ending.SlowsDown ? $"{name}, slowing down" : name;
    }

    private static MelodyDescription? MelodyOf(Song song, IReadOnlyList<StateTraceEntry> trace)
    {
        var track = song.TrackDefinitions.FirstOrDefault(x => x.Value.Role == TrackRole.Melody).Key;
        if (!song.Notes!.TryGetValue(track, out var notes) || notes.Count == 0)
            return null;

        var pitches = notes.SelectMany(x => x.Value.Pitches).ToArray();
        var bars = notes.Select(x => (int)Math.Floor(x.Position / song.MeterAt(x.Position).BarDuration + 1e-9)).Distinct().Count();
        var improvisation = (double)trace.First(x => x.Point == TracePoints.MelodyImprovisation).Value!;
        return new MelodyDescription(
            NoteName(pitches.Min()),
            NoteName(pitches.Max()),
            Math.Round(notes.Count / (double)Math.Max(1, bars), 1),
            improvisation < 1 / 3.0 ? "little" : improvisation > 2 / 3.0 ? "much" : "some"
        );
    }

    private static string NoteName(int pitch) => $"{KeyNames[pitch.Mod(12)]}{pitch / 12 - 1}";
}
