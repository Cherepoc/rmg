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
        string KeyAt(double position, Scale scale) => $"{ChordNames.KeyNames[keys.GetEffectiveValueAt(position).Mod(12)]} {scale.Name}";

        var origin = map.Origin;
        var songKey = KeyAt(origin, songScale);
        var songBpm = Bpm(origin);
        var songFeel = Value<int>(TracePoints.Feel);

        // the sections' energies against the song's: its quietest third quiet and its loudest loud
        var energies = trace.Where(x => x.Point == TracePoints.SectionEnergy).GroupBy(x => x.Section).ToDictionary(x => x.Key, x => ((SectionEnergyTrace)x.First().Value!).Energy);
        var levels = Thirds(energies, "quiet", "medium", "loud");
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

        // a section's chords: its progression's degrees, each named as the chords' part plays it where the section first
        // has it play, by the notes it sounds over the chord's span, and by its scale's triad where the part does not
        var progressions = trace.Where(x => x.Point == TracePoints.Progression).GroupBy(x => x.Section).ToDictionary(x => x.Key, x => (ImmutableArray<int>)x.First().Value!);
        var rhythms = trace.Where(x => x.Point == TracePoints.HarmonicRhythm).GroupBy(x => x.Section).ToDictionary(x => x.Key, x => (HarmonicRhythm)x.First().Value!);
        var chordNotes = parts.GetValueOrDefault(TrackRole.Chords, []).SelectMany(x => song.Notes![x]).ToArray();
        ImmutableArray<ChordDescription> ChordsOf(int id)
        {
            if (!progressions.TryGetValue(id, out var degrees) || !rhythms.TryGetValue(id, out var rhythm))
                return [];

            var scale = sectionScales.GetValueOrDefault(id, songScale);
            var spans = map.Sections.Where(x => x.SectionId == id).ToArray();
            return
            [
                ..degrees.Select((degree, i) =>
                {
                    var step = degree.Mod(scale.Offsets.Length);
                    foreach (var span in spans)
                    {
                        var start = span.Start + rhythm.Changes[i];
                        var end = start + rhythm.LengthOf(i);
                        var root = (keys.GetEffectiveValueAt(start) + scale.Offsets[step]).Mod(12);
                        var intervals = chordNotes.Where(n => n.IsWithin(start, end))
                            .SelectMany(n => n.Value.Pitches).Select(pitch => (pitch - root).Mod(12)).ToHashSet();
                        if (intervals.Count < 2)
                            continue;

                        var played = ChordNames.Of(intervals);
                        return new ChordDescription(ChordNames.Numeral(step, scale.Offsets, played), ChordNames.KeyNames[root] + played.Suffix);
                    }

                    return new ChordDescription(ChordNames.Numeral(step, scale.Offsets, ChordNames.Of(ChordNames.Triad(step, scale.Offsets))), null);
                })
            ];
        }

        // how many notes a beat a section plays, its parts together, against the song's other sections
        var densities = Thirds(map.Sections.Select((span, index) => (index, notes: song.Notes!.Values.Sum(x => x.Count(n => n.Position >= span.Start && n.Position < span.End)) / span.Duration))
            .ToDictionary(x => x.index, x => x.notes), "sparse", "medium", "busy");
        // every part's notes where they start, as the song decides them, before its swing moves them
        var onsets = parts.GroupBy(x => PartName(x.Key))
            .ToDictionary(x => x.Key, x => x.SelectMany(y => y.Value).SelectMany(track => song.Notes![track].Select(n => n.Position)).Distinct().Order().ToArray());
        RunDescription? Run(int index, double slowest) => FastestRun(onsets, map.Sections[index], index + 1, Bpm(map.Sections[index].Start) / 60, slowest);

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
                // a step up of the period half time, as a breakdown's, and a step down double
                : timeFeels.GetValueOrDefault(id) switch { > 0 => "half time", < 0 => "double time", _ => null };
            var playing = parts.Where(x => !resting.Contains(x.Key) && x.Value.Any(track => song.Notes![track].Any(n => n.Position >= span.Start && n.Position < span.End)))
                .Select(x => PartName(x.Key)).Distinct().ToImmutableArray();
            return new SectionDescription(
                index + 1,
                role == SectionRole.Free ? $"Section {letters[id]}" : RoleName(role),
                span.BarCount,
                levels[id],
                densities[index],
                key == songKey ? null : key,
                span.Meter.HasSameBar(map.Meter) ? null : TimeSignature(span.Meter),
                Math.Abs(bpm - songBpm) < 0.5 ? null : bpm,
                feels.TryGetValue(id, out var feel) && feel != songFeel ? FeelName(feel) : null,
                drums,
                TextureName(textures.GetValueOrDefault(index, TextureKind.Steady)),
                playing,
                solos.TryGetValue(index, out var solo) ? [..solo.Soloists.Select(PartName)] : [],
                ChordsOf(id),
                // 16ths run through many a section, so that only a run faster stands out
                Run(index, 4.01)
            );
        }).ToImmutableArray();

        return new SongDescription(
            Math.Round(rendered.Seconds, 1),
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
            IntroName(map.Intro),
            EndingName(map.Ending),
            sections,
            MelodyOf(song, trace),
            Enumerable.Range(0, map.Sections.Length).Select(x => Run(x, 4)).OfType<RunDescription>().MaxBy(x => x.NotesPerSecond)
        );
    }

    /// <summary>
    ///     Every section's value in a word, by its place among the song's from its lowest to its highest: the lowest third
    ///     low, the highest high, so that of two the higher is high.
    /// </summary>
    private static Dictionary<int, string> Thirds(Dictionary<int, double> values, string low, string middle, string high)
    {
        var ordered = values.Values.Distinct().Order().ToArray();
        return values.ToDictionary(x => x.Key, x =>
        {
            var place = ordered.Length < 2 ? 0.5 : Array.IndexOf(ordered, x.Value) / (double)(ordered.Length - 1);
            return place < 1 / 3.0 ? low : place > 2 / 3.0 ? high : middle;
        });
    }

    /// <summary>
    ///     A section's fastest run: four notes or more a part plays evenly spaced, at least as many a beat as the slowest
    ///     given, and the parts that run as fast; none where no part does.
    /// </summary>
    private static RunDescription? FastestRun(Dictionary<string, double[]> onsets, SectionSpan span, int place, double beatsPerSecond, double slowest)
    {
        const int least = 4;
        const double tolerance = 1e-6;
        var fastest = onsets.Select(part =>
        {
            var on = part.Value.Where(x => x >= span.Start && x < span.End).ToArray();
            var shortest = double.MaxValue;
            for (var i = 0; i + least - 1 < on.Length;)
            {
                var gap = on[i + 1] - on[i];
                var j = i + 1;
                while (j + 1 < on.Length && Math.Abs(on[j + 1] - on[j] - gap) < tolerance)
                    j++;
                if (j - i + 1 >= least && gap < shortest)
                    shortest = gap;
                i = j;
            }

            return (part: part.Key, perBeat: Math.Round(1 / shortest, 2));
        }).Where(x => x.perBeat >= slowest).GroupBy(x => x.perBeat).MaxBy(x => x.Key);
        return fastest is null ? null : new RunDescription([..fastest.Select(x => x.part)], ValueName(fastest.Key), Math.Round(fastest.Key * beatsPerSecond, 1), place);
    }

    /// <summary>
    ///     A note's value by how many play a beat: 16ths, 32nds and further, their triplets, or a tuplet of 16ths, such as
    ///     quintuplets, or the number a beat where it is none of these.
    /// </summary>
    private static string ValueName(double perBeat)
    {
        var n = (int)Math.Round(perBeat);
        if (Math.Abs(perBeat - n) > 0.01)
            return $"{perBeat:0.#} notes a beat";
        if (int.IsPow2(n))
            return $"{Ordinal(4 * n)}s";
        if (n % 3 == 0 && int.IsPow2(n / 3))
            return $"{Ordinal(8 * n / 3)} triplets";
        return n switch { 5 => "quintuplets", 7 => "septuplets", _ => $"{n}-tuplets" };
    }

    private static string Ordinal(int n) => n % 10 == 2 && n % 100 != 12 ? $"{n}nd" : $"{n}th";

    private static string Word(double value) => value < 1 / 3.0 ? "plain" : value > 2 / 3.0 ? "experimental" : "middling";

    private static string TimeSignature(Meter meter) => $"{meter.TimeSignature.Numerator}/{meter.TimeSignature.Denominator}";

    private static string FeelName(int feel) => Feels.Label(feel);

    private static string SwingName(Swing swing) =>
        swing.Delay == 0 ? "none" : $"swung {(swing.Period switch { 1 => "8ths", 0.5 => "16ths", 2 => "quarters", var p => $"pairs of {p:0.##} beats" })}, {swing.Delay:0.##} of a beat late";

    private static string RoleName(SectionRole role) => role switch { SectionRole.PreChorus => "Pre-chorus", SectionRole.PostChorus => "Post-chorus", _ => role.ToString() };

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

    private static string IntroName(IntroSpan intro) => intro.Kind switch
    {
        IntroKind.CountIn => "a count-in",
        IntroKind.Entries => $"the band coming in part by part over {intro.Window.Bars} bars",
        IntroKind.Hook => "the chorus's hook",
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
            EndingKind.Big => "the home chord held under a drum roll, then a last hit together",
            EndingKind.Cadenza => "the melody running alone over the band's last chord, then a last hit together",
            _ => "fading out over the last section"
        };
        return ending.SlowsDown ? $"{name}, slowing down" : name;
    }

    private static MelodyDescription? MelodyOf(Song song, IReadOnlyList<StateTraceEntry> trace)
    {
        var track = song.TrackDefinitions.Single(x => x.Value.Role == TrackRole.Melody).Key;
        if (!song.Notes!.TryGetValue(track, out var notes) || notes.Count == 0)
            return null;

        var pitches = notes.SelectMany(x => x.Value.Pitches).ToArray();
        // the bars it plays in, each by where it starts, counted from its section's start in its section's meter
        var bars = notes.Select(x => Math.Round(x.Position - song.Map!.BeatInBar(x.Position), 6))
            .Distinct().Count();
        var improvisation = (double)trace.First(x => x.Point == TracePoints.MelodyImprovisation).Value!;
        return new MelodyDescription(
            NoteName(pitches.Min()),
            NoteName(pitches.Max()),
            Math.Round(notes.Count / (double)Math.Max(1, bars), 1),
            improvisation < 1 / 3.0 ? "little" : improvisation > 2 / 3.0 ? "much" : "some"
        );
    }

    private static string NoteName(int pitch) => $"{ChordNames.KeyNames[pitch.Mod(12)]}{pitch / 12 - 1}";
}
