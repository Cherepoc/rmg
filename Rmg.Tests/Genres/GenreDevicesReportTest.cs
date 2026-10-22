using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Tests.Genres;

/// <summary>
///     How often every preset plays each of the engine's devices, beside the songs with no genre: its harmony's colours, its
///     form's devices and its parts' expression, and the song as a whole, its solos against its sung sections, its parts'
///     instruments and its melody's notes, so that a genre playing a device its style does not use shows; a value
///     marked * the preset inherits, its choice unnamed, the songs' own.
/// </summary>
public sealed class GenreDevicesReportTest
{
    private const int Count = 32;

    /// <summary>A song's share of each device, by its name.</summary>
    internal static Dictionary<string, double> Measure(CorpusSong song)
    {
        double Sections(string point, Func<object?, bool> plays) =>
            song.Trace.Where(x => x.Point == point).Select(x => plays(x.Value) ? 1.0 : 0).DefaultIfEmpty(double.NaN).Average();
        // a section's own harmony, as it plays: a looping section plays the first section's, whatever it drew
        var looping = song.Trace.Where(x => x.Point == TracePoints.Loop && (bool)x.Value!).Select(x => x.Section).ToHashSet();
        double Harmony(string point, Func<object?, bool> plays) =>
            song.Trace.Where(x => x.Point == point && !looping.Contains(x.Section)).Select(x => plays(x.Value) ? 1.0 : 0).DefaultIfEmpty(double.NaN).Average();
        var scales = song.Trace.Where(x => x.Point == TracePoints.ChordScales).SelectMany(x => (ImmutableArray<ChordScale>)x.Value!).ToArray();
        double Chords(ChordScaleKind kind) => scales.Length > 0 ? scales.Count(x => x.Kind == kind) / (double)scales.Length : double.NaN;
        var chordNotes = song.Song.Notes!.GetValueOrDefault(song.Song.Track(TrackRole.Chords))?.ToArray() ?? [];
        var chords = chordNotes.Where(x => x.Value.Pitches.Length >= 3).ToArray();
        var lines = new[] { song.Track(TrackRole.Melody), song.Track(TrackRole.CounterMelody), song.Track(TrackRole.Riff), song.Track(TrackRole.Solo) }
            .SelectMany(x => song.Song.Notes!.GetValueOrDefault(x) ?? EventTimeline.Create<RealizedNote>(0, [])).ToArray();
        double Lines(Func<RealizedNote, bool> plays) => lines.Length > 0 ? lines.Count(x => plays(x.Value)) / (double)lines.Length : double.NaN;
        // the line parts' notes as they are placed, those leaning on a tension and those landing on one
        var devices = song.Trace.Where(x => x.Point == TracePoints.LineDevices).Select(x => ((int Leans, int Tense, int Notes))x.Value!).ToArray();
        double Placed(Func<(int Leans, int Tense, int Notes), int> plays) => devices.Sum(x => x.Notes) is var all and > 0 ? devices.Sum(plays) / (double)all : double.NaN;
        var bassNotes = song.Song.Notes!.GetValueOrDefault(song.Song.Track(TrackRole.Bass))?.ToArray() ?? [];
        var lifts = song.Trace.Count(x => x.Point == TracePoints.LiftCue);
        var sections = song.Song.Map!.Sections.ToArray();
        var polymeters = (System.Collections.IDictionary)song.Trace.Single(x => x.Point == TracePoints.Polymeter).Value!;
        var tempos = (ImmutableArray<double>)song.Trace.Single(x => x.Point == TracePoints.SectionTempo).Value!;
        var meter = (Meter)song.Trace.Single(x => x.Point == TracePoints.Meter).Value!;
        // a piano's chords held a beat or longer, which its part rolls or strikes together
        // the song as a whole: its parts' instruments, the melody's, and the melody's notes a bar where it plays and no solo does
        int? Program(TrackRole role) => song.Song.TrackDefinitions.Values.FirstOrDefault(x => x.Role == role) is PitchInstrumentTrack p ? p.InstrumentCode : null;
        var programs = song.Song.TrackDefinitions.Values.OfType<PitchInstrumentTrack>().Where(x => song.Song.Notes!.GetValueOrDefault(song.Track(x.Role))?.Count > 0).Select(x => x.InstrumentCode).ToArray();
        var pitchedSolos = song.Solos.Values.Where(x => !x.IsDrumSolo).ToArray();
        var melodyNotes = song.Song.Notes!.GetValueOrDefault(song.Track(TrackRole.Melody));
        var sung = sections.Select((span, i) => (span, i)).Where(x => !song.Solos.ContainsKey(x.i))
            .Select(x => (melodyNotes?.Count(n => n.IsWithin(x.span.Start, x.span.End)) ?? 0) / (x.span.Duration / x.span.Meter.BarDuration)).Where(x => x > 0).ToArray();
        var form = (SongStructure)song.Trace.Single(x => x.Point == TracePoints.SongForm).Value!;
        var held = song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key] is PitchInstrumentTrack).SelectMany(x => x.Value.Where(n =>
            n.Value.Pitches.Length >= 3 && n.Value.Duration >= 1 - 1e-9
            && InstrumentsOverTime.ProgramOf(n.Value.State, ((PitchInstrumentTrack)song.Song.TrackDefinitions[x.Key]).InstrumentCode) is >= 0 and <= 7)).ToArray();
        return new Dictionary<string, double>
        {
            ["chords applied dominants"] = Chords(ChordScaleKind.Applied),
            ["chords borrowed"] = Chords(ChordScaleKind.Borrowed),
            ["chords in a scale of their own"] = Chords(ChordScaleKind.Own),
            ["chords of four notes or more"] = chords.Length > 0 ? chords.Count(x => x.Value.Pitches.Select(p => p % 12).Distinct().Count() >= 4) / (double)chords.Length : double.NaN,
            ["chords parts arpeggiated"] = Sections(TracePoints.Arpeggio, x => (ArpeggioPattern)x! != ArpeggioPattern.None),
            ["sections with a passing chord"] = Sections(TracePoints.HarmonicRhythm, x => ((HarmonicRhythm)x!).Passing.Length > 0),
            ["sections turned around"] = Harmony(TracePoints.Turnaround, x => (((bool, bool Changes))x!).Changes),
            ["sections planing"] = Harmony(TracePoints.Planing, x => x is not null),
            ["sections with an inner line"] = sections.Length > 0
                ? sections.Average(span => chordNotes.Any(n => span.IsWithin(n.Position) && n.Value.State.GetStateValue(CompositionStateKinds.InnerLine) > 0) ? 1.0 : 0)
                : double.NaN,
            ["sections over a pedal"] = sections.Length > 0 ? sections.Average(span => bassNotes.Any(n => span.IsWithin(n.Position) && n.Value.State.GetStateValue(CompositionStateKinds.BassPedal) > 0) ? 1.0 : 0) : double.NaN,
            ["songs reharmonising"] = song.Trace.Single(x => x.Point == TracePoints.Reharmonisation).Value is null ? 0 : 1,
            ["songs changing key"] = ((ImmutableArray<KeyChange>)song.Trace.Single(x => x.Point == TracePoints.KeyChange).Value!).IsEmpty ? 0 : 1,
            ["sections in stop time"] = Sections(TracePoints.StopTime, x => ((System.Collections.Immutable.ImmutableArray<double>)x!).Length > 0),
            ["sections pushing"] = Sections(TracePoints.Push, x => (bool)x!),
            ["lifts dropped"] = lifts > 0 ? song.Trace.Count(x => x.Point == TracePoints.Drop) / (double)lifts : double.NaN,
            ["sections of 16 bars or more"] = song.Song.Map!.Sections.Average(x => x.Duration >= 16 * x.Meter.BarDuration - 1e-9 ? 1.0 : 0),
            ["sections of 32 bars"] = song.Song.Map!.Sections.Average(x => x.Duration >= 32 * x.Meter.BarDuration - 1e-9 ? 1.0 : 0),
            ["appearances building or thinning"] = song.Trace.Where(x => x.Point == TracePoints.Texture)
                .Select(x => ((int, TextureKind Kind, ImmutableArray<ImmutableHashSet<TrackRole>>))x.Value!).Average(x => x.Kind is TextureKind.Build or TextureKind.Thin ? 1.0 : 0),
            ["verses without a melody"] = song.Trace.Where(x => x.Point == TracePoints.SongForm).Select(x => (SongStructure)x.Value!).Single() is var structure
                && song.Song.Map!.Sections.Select((span, i) => (span, i)).Where(x => structure.Roles.GetValueOrDefault(x.span.SectionId) == SectionRole.Verse).ToArray() is { Length: > 0 } verses
                ? verses.Average(x => song.Resting(x.span).Contains(TrackRole.Melody) ? 1.0 : 0)
                : double.NaN,
            ["songs with a solo"] = song.Solos.Count > 0 ? 1 : 0,
            ["sections after the first soloed"] = sections.Length > 1 ? song.Solos.Count / (double)(sections.Length - 1) : double.NaN,
            ["songs soloing more than singing"] = song.Solos.Count > sections.Length - song.Solos.Count ? 1 : 0,
            ["solos on the melody's instrument"] = pitchedSolos.Length > 0 ? pitchedSolos.Average(x => Program(x.Soloists[0]) == Program(TrackRole.Melody) ? 1.0 : 0) : double.NaN,
            ["songs of one instrument on 3 parts"] = programs.GroupBy(x => x).Any(x => x.Count() >= 3) ? 1 : 0,
            ["melody notes a bar where it plays"] = sung.Length > 0 ? sung.Average() : double.NaN,
            ["songs with a bridge"] = form.Roles.Values.Contains(SectionRole.Bridge) ? 1 : 0,
            ["songs with a chorus"] = form.Roles.Values.Contains(SectionRole.Chorus) ? 1 : 0,
            ["sections ghosting"] = Sections(TracePoints.Ghosts, x => (bool)x!),
            ["sections whose groove mutates"] = Sections(TracePoints.Mutations, x => (((int, int Made))x!).Made > 0),
            ["sections with a figure"] = Sections(TracePoints.Figures, x => ((System.Collections.ICollection)x!).Count > 0),
            ["sections in half or double time"] = Sections(TracePoints.TimeFeel, x => (int)x! != 0),
            ["songs flamming or dragging"] = song.Trace.Single(x => x.Point == TracePoints.DrumOrnaments).Value is (bool flams, bool drags) && (flams || drags) ? 1 : 0,
            ["songs whose drums mark the form"] = ((System.Collections.ICollection)song.Trace.Single(x => x.Point == TracePoints.DrumMarks).Value!).Count > 0 ? 1 : 0,
            ["songs swung"] = song.Swing.Delay > 0 ? 1 : 0,
            ["songs swung in 16ths"] = song.Swing.Delay > 0 && song.Swing.Period < 1 ? 1 : 0,
            ["sections heavy, their clean guitars driven"] = Sections(TracePoints.SectionForm, x => (SectionFormKind)x! == SectionFormKind.Heavy),
            ["sections calm, their driven guitars acoustic"] = Sections(TracePoints.SectionForm, x => (SectionFormKind)x! == SectionFormKind.Calm),
            ["line notes bent or slid"] = Lines(x => x.State.GetStateValue(CompositionStateKinds.Bend) > 0),
            ["bass's changes of pitch slid into"] = song.Song.Notes!.GetValueOrDefault(song.Song.Track(TrackRole.Bass))?.ToArray() is { Length: > 1 } bass
                && bass.Skip(1).Where((x, i) => x.Value.Pitches[0] != bass[i].Value.Pitches[0]).ToArray() is { Length: > 0 } changes
                ? changes.Count(x => x.Value.State.GetStateValue(CompositionStateKinds.Bend) == (int)BendKind.Slide) / (double)changes.Length
                : double.NaN,
            ["line notes with a vibrato"] = Lines(x => x.State.GetStateValue(CompositionStateKinds.Vibrato) > 0 || x.State.GetStateValue(CompositionStateKinds.WideVibrato) > 0),
            ["line notes slurred"] = Lines(x => x.State.GetStateValue(CompositionStateKinds.Legato) > 0),
            ["sections polymetric"] = sections.Average(x => polymeters.Contains(x.SectionId) ? 1.0 : 0),
            ["appearances at a tempo of their own"] = tempos.Average(x => Math.Abs(x - 1) > 1e-9 ? 1.0 : 0),
            ["sections in a meter of their own"] = sections.Average(x => x.Meter.HasSameBar(meter) ? 0 : 1.0),
            ["sections the solo part answers"] = Sections(TracePoints.SoloAnswers, x => (((int, bool Answers))x!).Answers),
            ["lines in octaves"] = Sections(TracePoints.Octave, x => (((int, TrackRole, double, bool InOctaves))x!).InOctaves),
            ["sections harmonising the melody"] = sections.Length == 0 ? double.NaN
                : song.Trace.Where(x => x.Point == TracePoints.LineDoubling).Count(x => (((int, TrackRole Source, TrackRole, int))x.Value!).Source == TrackRole.Melody) / (double)sections.Length,
            ["songs repeating the last section"] = (bool)song.Trace.Single(x => x.Point == TracePoints.LastRepeat).Value! ? 1 : 0,
            // of every pitched part's sections, those it plays in an instrument switched to (the trace's, a pinch's harmonics and a form's guitar apart)
            ["parts' sections switched"] = song.Song.TrackDefinitions.Values.Count(x => x is PitchInstrumentTrack) * sections.Length is var parts and > 0
                ? song.Trace.Count(x => x.Point == TracePoints.InstrumentOverTime && song.Song.TrackDefinitions[x.Track] is PitchInstrumentTrack part
                                        && (((int, TrackRole, int Program, ArticulationMode, int))x.Value!).Program != part.InstrumentCode) / (double)parts
                : double.NaN,
            ["songs with a strong effect"] = ((SongEffects)song.Trace.Single(x => x.Point == TracePoints.SongEffects).Value!).Strong != StrongEffect.None ? 1 : 0,
            ["held piano chords rolled"] = held.Length > 0 ? held.Count(x => x.Value.State.GetStateValue(CompositionStateKinds.Strum) > 0) / (double)held.Length : double.NaN,
            ["reverb sent"] = song.Song.Notes!.Values.SelectMany(x => x).Select(x => x.Value.State.GetStateValue(CompositionStateKinds.Reverb) - 1.0).Where(x => x >= 0).DefaultIfEmpty(double.NaN).Average(),
            ["sections with the riff in power chords"] = Sections(TracePoints.RiffPowerChords, x => (bool)x!),
            ["sections whose riff chugs"] = Sections(TracePoints.Chugs, x => (bool)x!),
            ["sections whose riff drones"] = Sections(TracePoints.Drone, x => (bool)x!),
            ["sections with an octave bass"] = Sections(TracePoints.OctaveBass, x => (bool)x!),
            ["sections with an alternating bass"] = Sections(TracePoints.AlternatingBass, x => (bool)x!),
            ["sections with a melody hook"] = Sections(TracePoints.MelodyHook, x => (bool)x!),
            ["sections calling and answered"] = Sections(TracePoints.CallAndResponse, x => (bool)x!),
            ["songs ornamenting their lines"] = ((ImmutableArray<Ornament>)song.Trace.Single(x => x.Point == TracePoints.Ornaments).Value!).IsEmpty ? 0 : 1,
            ["parts' sections in a scale of their own"] = Sections(TracePoints.LineScale, x => (LineScale)x! != LineScale.Section),
            ["sections harmonising in three voices"] = sections.Length == 0 ? double.NaN : song.Trace.Count(x => x.Point == TracePoints.BlockHarmony) / (double)sections.Length,
            ["line notes leaning"] = Placed(x => x.Leans),
            ["line notes on a tension"] = Placed(x => x.Tense),
            ["melody notes stacked"] = song.Song.Notes!.GetValueOrDefault(song.Song.Track(TrackRole.Melody))?.ToArray() is { Length: > 0 } melody
                ? melody.Count(x => x.Value.State.GetStateValue(CompositionStateKinds.LineVoices) > 1) / (double)melody.Length
                : double.NaN,
            ["sections vamping"] = Harmony(TracePoints.Vamp, x => (bool)x!),
            ["sections looping"] = Sections(TracePoints.Loop, x => (bool)x!),
            ["sections raising the cadence"] = Sections(TracePoints.CadenceRaise, x => (((int?, bool Raised))x!).Raised),
            ["sections at home off the tonic"] = Harmony(TracePoints.SectionHome, x => (int)x! != 0),
            ["sections changing chords every two bars"] = Harmony(TracePoints.HarmonicRhythm, x => ((HarmonicRhythm)x!).Bars >= 2),
            ["songs opening on the hook"] = song.Song.Map!.Intro.Kind == IntroKind.Hook ? 1 : 0,
            ["songs opening on one part"] = song.Trace.Single(x => x.Point == TracePoints.OpeningAlone).Value is not null ? 1 : 0,
            ["songs counted in"] = song.Song.Map!.Intro.Kind == IntroKind.CountIn ? 1 : 0,
            ["songs ending on a button"] = song.Song.Map!.Ending.Kind == EndingKind.Button ? 1 : 0,
            ["songs ringing out"] = song.Song.Map!.Ending.Kind == EndingKind.RingOut ? 1 : 0,
            ["songs fading"] = song.Song.Map!.Ending.Kind == EndingKind.Fade ? 1 : 0,
            ["songs stripped at the end"] = song.Song.Map!.Ending.Kind == EndingKind.Strip ? 1 : 0,
            ["sections broken down"] = Sections(TracePoints.Breakdown, x => (bool)x!),
            ["sections cut short"] = sections.Length > 0 ? sections.Average(x => x.Cut > 1e-9 ? 1.0 : 0) : double.NaN,
            ["sections in phrases of three or six"] = sections.Length > 0 ? sections.Average(x => x.Meter.PhraseBars != Meter.DefaultPhraseBars ? 1.0 : 0) : double.NaN,
            ["drops filtered"] = song.Trace.Count(x => x.Point is TracePoints.Drop or TracePoints.FilteredDrop) is var drops and > 0
                ? song.Trace.Count(x => x.Point == TracePoints.FilteredDrop) / (double)drops
                : double.NaN,
            ["appearances of one part alone"] = song.Trace.Where(x => x.Point == TracePoints.Texture)
                .Select(x => ((int, TextureKind Kind, ImmutableArray<ImmutableHashSet<TrackRole>>))x.Value!).Average(x => x.Kind == TextureKind.Alone ? 1.0 : 0),
            ["songs whose mix is filtered"] = (MixFilterPlan)song.Trace.Single(x => x.Point == TracePoints.MixFilter).Value! is var plan
                && (plan.Opening || plan.Closing || !plan.Dips.IsEmpty || !plan.Drops.IsEmpty) ? 1 : 0,
            ["songs in a genre's room"] = song.Song.Draws!.Room is null ? 0 : 1,
            ["songs with an insertion effect"] = song.Song.Draws!.Insertion is null ? 0 : 1,
            ["songs with a drum solo"] = song.Solos.Values.Any(x => x.IsDrumSolo) ? 1 : 0,
            ["solos trading with the drums"] = song.Solos.Count > 0 ? song.Solos.Values.Average(x => x.TradesWithDrums ? 1.0 : 0) : double.NaN,
            ["solos twinned"] = song.Solos.Count > 0 ? song.Solos.Values.Average(x => x.Soloists.Length > 1 && x.Mode == SoloMode.Together ? 1.0 : 0) : double.NaN,
            ["drum solos whose feet keep time"] = song.Trace.Where(x => x.Point == TracePoints.DrumSolo).Select(x => ((DrumSolo)x.Value!).Feet ? 1.0 : 0).DefaultIfEmpty(double.NaN).Average(),
            ["sections struck in ensemble hits"] = Sections(TracePoints.EnsembleHits, x => !((ImmutableArray<double>)x!).IsEmpty),
            ["phrase lines kicked out"] = song.Trace.Count(x => x.Point == TracePoints.FillDecision) is var lines2 and > 0
                ? (song.Trace.SingleOrDefault(x => x.Point == TracePoints.KickOuts)?.Value is ImmutableArray<(double, double)> kickOuts ? kickOuts.Length : 0) / (double)lines2
                : double.NaN,
            ["bars with a hat roll"] = sections.Sum(x => x.Bars.Count()) is var bars and > 0
                ? (song.Trace.SingleOrDefault(x => x.Point == TracePoints.HatRolls)?.Value is ImmutableArray<(double, double, int)> rolls ? rolls.Length : 0) / (double)bars
                : double.NaN,
            ["sections the twin leads"] = Sections(TracePoints.TwinLead, x => (bool)x!),
            ["sections the counter-melody imitates"] = sections.Length == 0 ? double.NaN : song.Trace.Count(x => x.Point == TracePoints.Imitation) / (double)sections.Length,
            ["changes of section run into"] = song.Song.Map!.Sections.Length > 1
                ? song.Trace.Count(x => x.Point == TracePoints.LineFill && x.Track == song.Track(TrackRole.CounterMelody) && ((ValueTuple<double, double[]>)x.Value!).Item2.Length == LineFills.RunNotes)
                  / (double)(song.Song.Map!.Sections.Length - 1)
                : double.NaN,
            ["sections sung in 8ths"] = Sections(TracePoints.SungEighths, x => (bool)x!),
            ["sections whose bass walks a boogie"] = Sections(TracePoints.Boogie, x => (bool)x!),
            ["songs building their last chorus"] = (bool)song.Trace.Single(x => x.Point == TracePoints.BreakdownChorus).Value! ? 1 : 0,
            ["songs pinning a part to its figure"] = ((System.Collections.ICollection)song.Trace.Single(x => x.Point == TracePoints.PinnedDrums).Value!).Count > 0 ? 1 : 0,
            ["phrase lines filled"] = song.Trace.Where(x => x.Point == TracePoints.FillDecision).Select(x => ((FillDecision)x.Value!).Span > 0 ? 1.0 : 0).DefaultIfEmpty(double.NaN).Average(),
            ["unconventionality"] = song.Song.Draws!.Unconventionality.Base
        };
    }

    /// <summary>The genre choice each measure is a genre's by, which a preset that does not name it inherits; none for a measure of no choice.</summary>
    private static readonly Dictionary<string, string> ChoiceOf = new()
    {
        ["chords applied dominants"] = "chordScales", ["chords borrowed"] = "chordScales", ["chords in a scale of their own"] = "chordScales",
        ["chords of four notes or more"] = "chordShape", ["chords parts arpeggiated"] = "arpeggio", ["sections with a passing chord"] = "passingChords",
        ["sections turned around"] = "turnaround", ["sections planing"] = "planing", ["sections with an inner line"] = "harmonyDevices",
        ["sections over a pedal"] = "harmonyDevices", ["songs reharmonising"] = "harmonyDevices", ["songs changing key"] = "keyChanges",
        ["sections in stop time"] = "stopTime", ["sections pushing"] = "push", ["lifts dropped"] = "drop", ["sections of 16 bars or more"] = "sectionLength",
        ["sections of 32 bars"] = "sectionLength", ["appearances building or thinning"] = "texture", ["verses without a melody"] = "voice",
        ["songs with a solo"] = "solos", ["sections after the first soloed"] = "solos", ["songs soloing more than singing"] = "solos",
        ["solos on the melody's instrument"] = "instrument", ["songs of one instrument on 3 parts"] = "instrument", ["melody notes a bar where it plays"] = "rhythm.melody",
        ["songs with a bridge"] = "form", ["songs with a chorus"] = "form", ["sections ghosting"] = "ghosts", ["sections whose groove mutates"] = "mutation",
        ["sections in half or double time"] = "feelChances", ["songs flamming or dragging"] = "rudiments", ["songs whose drums mark the form"] = "drumJob",
        ["songs swung"] = "feelChances", ["songs swung in 16ths"] = "feelValues",
        ["sections heavy, their clean guitars driven"] = "louderForms", ["sections calm, their driven guitars acoustic"] = "louderForms",
        ["line notes bent or slid"] = "effects", ["bass's changes of pitch slid into"] = "glide",
        ["line notes with a vibrato"] = "effects", ["line notes slurred"] = "articulation", ["sections polymetric"] = "polymeter",
        ["appearances at a tempo of their own"] = "sectionChanges", ["sections in a meter of their own"] = "sectionChanges",
        ["sections the solo part answers"] = "soloAnswers", ["lines in octaves"] = "doubling", ["sections harmonising the melody"] = "doubling",
        ["songs repeating the last section"] = "lastRepeat", ["parts' sections switched"] = "instrumentSwitches", ["songs with a strong effect"] = "strongEffect",
        ["held piano chords rolled"] = "articulation", ["reverb sent"] = "space",
        ["sections with the riff in power chords"] = "riff", ["sections whose riff chugs"] = "riff", ["sections whose riff drones"] = "riff",
        ["sections with an octave bass"] = "octaveBass", ["sections with an alternating bass"] = "alternatingBass", ["sections with a melody hook"] = "hook",
        ["sections calling and answered"] = "callAndResponse", ["songs ornamenting their lines"] = "ornaments", ["parts' sections in a scale of their own"] = "lineScale",
        ["sections harmonising in three voices"] = "doubling", ["line notes leaning"] = "appoggiatura",
        ["line notes on a tension"] = "tension", ["sections vamping"] = "vamp", ["sections looping"] = "loop", ["sections raising the cadence"] = "cadenceRaise",
        ["sections at home off the tonic"] = "sectionHome", ["sections changing chords every two bars"] = "harmonicRhythm",
        ["songs opening on the hook"] = "intro", ["songs opening on one part"] = "openingAlone", ["songs counted in"] = "intro",
        ["songs ending on a button"] = "ending", ["songs ringing out"] = "ending", ["songs fading"] = "ending", ["songs stripped at the end"] = "ending",
        ["sections broken down"] = "breakdown", ["sections cut short"] = "sectionChanges", ["sections in phrases of three or six"] = "phraseBars",
        ["drops filtered"] = "dropKind", ["appearances of one part alone"] = "alone", ["songs whose mix is filtered"] = "mixFilter",
        ["songs in a genre's room"] = "room", ["songs with an insertion effect"] = "insertion", ["songs with a drum solo"] = "soloists",
        ["solos trading with the drums"] = "soloists", ["solos twinned"] = "solos", ["drum solos whose feet keep time"] = "drumSoloFeet", ["phrase lines filled"] = "fills",
        ["sections with a figure"] = "figure", ["sections struck in ensemble hits"] = "ensembleHits", ["phrase lines kicked out"] = "kickOut",
        ["bars with a hat roll"] = "hatRolls", ["sections the twin leads"] = "doubling", ["sections the counter-melody imitates"] = "doubling",
        ["changes of section run into"] = "lineRun", ["sections sung in 8ths"] = "sung", ["sections whose bass walks a boogie"] = "alternatingBass",
        ["songs building their last chorus"] = "lastRepeat", ["songs pinning a part to its figure"] = "pins"
    };

    /// <summary>The measures that are no genre choice's, which every preset plays as its songs draw.</summary>
    private static readonly HashSet<string> NoChoice = ["unconventionality", "melody notes stacked"];

    /// <summary>
    ///     Whether a preset names the choice a measure is by: a choice's own name, or a family's, the figures' of every
    ///     player (<c>figure.*</c>).
    /// </summary>
    private static bool Names(GenrePresets.Preset preset, string choice) => preset.Choices.Keys.Any(x => x == choice || x.StartsWith(choice + "."));

    /// <summary>The measures that are numbers rather than shares.</summary>
    private static readonly HashSet<string> Numbers = ["reverb sent", "unconventionality", "melody notes a bar where it plays"];

    /// <summary>
    ///     The device and idiom reports measure a song of every preset and of none, which the review and listening read,
    ///     and every measure's choice is one of the genre's choices.
    /// </summary>
    [Test]
    public async Task TheReports_MeasureASongOfEveryPreset_EachMeasureByAChoiceThereIs()
    {
        Genre?[] genres = [null, ..GenrePresets.All.Select(GenrePresets.Genre)];
        var songs = TestCorpus.InParallel(Enumerable.Range(0, genres.Length), i => TestCorpus.Get(0, new SongOverrides(Genre: genres[i])));
        var measures = songs.Select(Measure).ToArray();
        foreach (var song in songs)
            await Assert.That(GenreIdiomsReportTest.Measure(song)).IsNotEmpty();
        await Assert.That(measures.All(x => x.Keys.ToHashSet().SetEquals(measures[0].Keys))).IsTrue();
        foreach (var (measure, choice) in ChoiceOf)
        {
            await Assert.That(measures[0].ContainsKey(measure)).IsTrue().Because(measure);
            await Assert.That(GenreChoices.All.Any(x => x.Name == choice || x.Name.StartsWith(choice + "."))).IsTrue().Because($"{measure}: {choice}");
        }
        // and the other way: every measure is a choice's, or none's
        foreach (var measure in measures[0].Keys)
            await Assert.That(ChoiceOf.ContainsKey(measure) || NoChoice.Contains(measure)).IsTrue().Because(measure);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var columns = new List<(string Name, Dictionary<string, double>[] Songs)>
        {
            ("none", TestCorpus.InParallel(Enumerable.Range(0, Count), seed => Measure(TestCorpus.Get(seed))))
        };
        columns.AddRange(GenrePresets.All.Select(preset =>
            (preset.Name + "|" + preset.Id, TestCorpus.InParallel(Enumerable.Range(0, Count), seed => Measure(TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(preset))))))));

        bool Inherits(string column, string measure) =>
            column.Split('|') is [_, var id] && ChoiceOf.TryGetValue(measure, out var choice) && !Names(GenrePresets.All.Single(x => x.Id == id), choice);
        Console.WriteLine($"{"",-36}" + string.Join("", columns.Select(x => $"{x.Name.Split('|')[0],11}")));
        foreach (var measure in columns[0].Songs[0].Keys)
            Console.WriteLine($"{measure,-36}" + string.Join("", columns.Select(c =>
                (c.Songs.Select(x => x[measure]).Where(double.IsFinite).DefaultIfEmpty(double.NaN).Average() is var value && Numbers.Contains(measure)
                    ? $"{value:0.00}"
                    : $"{value:P0}") + (Inherits(c.Name, measure) ? "*" : " ") is var cell ? $"{cell,11}" : "")));

        await Task.CompletedTask;
    }
}
