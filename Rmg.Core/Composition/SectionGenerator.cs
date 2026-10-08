using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     A section of a song: its harmony (its unconventionality, chords, home and progression), its state over the
///     song's, the drums it plays, the state that changes by bar, and the patterns of its tracks, played twice.
/// </summary>
internal sealed class SectionGenerator
{
    /// <summary>The track of a trace entry that records a decision for the whole section, such as its energy.</summary>
    public const int SectionTrace = -2;

    // the stream of the song's own shift of its lines' freedom to change register, apart from every section's
    private const int SongRegisterFreedomStream = -1;

    private readonly IGenerationContext _context;
    private readonly ulong _seed;

    private readonly SongTracks _tracks;

    // how far the song strays from convention, facet by facet, which a section moves a little
    private readonly Unconventionality _songFacets;
    private readonly MelodyBusyness _songMelodyBusyness;
    private readonly Scale _songScale;
    private readonly StateMap _songStateMap;
    private readonly ImmutableDictionary<int, StateMap> _sectionEnergies;
    private readonly Tilt _songPercussion;
    private readonly int _key;
    private readonly ImmutableHashSet<TrackRole> _absent;
    private readonly DrumJob _drumJob;
    private readonly Genre _genre;
    // the generators of a section's bar state and patterns, one for every meter a section is in
    private readonly Func<Meter, (BarStateGenerator Bars, PatternGenerator Patterns)> _generators;

    // the song's shift of its lines' freedom to change register, before a section's, in spreads (LineProfile.RegisterFreedomSpread)
    private readonly double _songRegisterFreedomShift;

    // the drum half of making a section
    private readonly SectionDrumGenerator _drums;


    /// <param name="seed">The seed of the sections' random sequences, from which every section derives its own by its id.</param>
    /// <param name="songPercussion">How far the song leans to sections of percussion only (<see cref="PercussionSections" />).</param>
    public SectionGenerator(
        IGenerationContext context,
        ulong seed,
        SongTracks tracks,
        Unconventionality songFacets,
        MelodyBusyness songMelodyBusyness,
        Scale songScale,
        StateMap songStateMap,
        ImmutableDictionary<int, StateMap> sectionEnergies,
        Tilt songPercussion,
        int key,
        ImmutableHashSet<TrackRole> absent,
        DrumJob drumJob,
        Genre genre
    )
    {
        _absent = absent;
        _drumJob = drumJob;
        _genre = genre;
        _context = context;
        _seed = seed;
        _tracks = tracks;
        _songFacets = songFacets;
        _songMelodyBusyness = songMelodyBusyness;
        _songScale = songScale;
        _songStateMap = songStateMap;
        _sectionEnergies = sectionEnergies;
        _songPercussion = songPercussion;
        _key = key;
        _songRegisterFreedomShift = Generators.SplineValue()(context.CreateContext(Seeds.Derive(seed, SongRegisterFreedomStream)));
        _generators = ((Func<Meter, (BarStateGenerator, PatternGenerator)>)(meter => (new BarStateGenerator(meter, genre), new PatternGenerator(context, tracks.Definitions, meter, genre))))
            .CacheGeneratedValues();
        _drums = new SectionDrumGenerator(tracks, genre, songStateMap, sectionEnergies, songPercussion, absent, Stream, StreamSeed, Facets, CreateSectionTrackLayer,
            meter => _generators(meter).Patterns);
    }

    /// <summary>The chance a section's guitars and picked bass chug, their repeats palm-muted, a genre's that the songs never play (<see cref="GenreChoices.Riff" />).</summary>
    public static Probabilities.ByConvention ChugChance { get; } = new(0, 0, 0);

    /// <summary>The chance a section's bass plays in octaves, its notes off the beat the note before an octave up, as a disco or funk bass does, by the groove facet: rare in the songs.</summary>
    public static Probabilities.ByConvention OctaveBassChance { get; } = new(0, 0.05, 0.05);

    /// <summary>The chance a section's riff plays in power chords, a genre's that the songs never play (<see cref="GenreChoices.Riff" />).</summary>
    public static Probabilities.ByConvention PowerChordChance { get; } = new(0, 0, 0);

    /// <summary>The chance a section plays the song's first section's harmony, a loop, a genre's that the songs never play.</summary>
    public static Probabilities.ByConvention Loop { get; } = new(0, 0, 0);

    /// <summary>
    ///     The chance a section vamps on its home chord, every chord of its pattern the home, the cadence's raise let go of,
    ///     as a funk band or a techno track sits on one chord; a genre's, which the songs never play (<see cref="GenreChoices.Vamp" />).
    /// </summary>
    public static Probabilities.ByConvention Vamp { get; } = new(0, 0, 0);

    // the song's first section's harmony, which a section of a loop plays, and its meter
    private (SectionHarmony Harmony, Meter Meter)? _firstHarmony;

    /// <summary>The random sequence a decision of a section draws from, apart from its own and every other decision's.</summary>
    private IGenerationContext Stream(int sectionId, SectionStream stream)
    {
        return _context.CreateContext(StreamSeed(sectionId, stream));
    }

    /// <summary>The seed of <see cref="Stream" />, for a decision that derives its own sequences from it.</summary>
    private ulong StreamSeed(int sectionId, SectionStream stream)
    {
        return Seeds.Derive(Seeds.Derive(_seed, sectionId), (int)stream);
    }

    /// <summary>The section: its 4-bar pattern played twice, with what the fills need to know of its rhythm.</summary>
    /// <param name="plan">The section's place in the song's form, which some of its draws keep to.</param>
    public GeneratedSection Generate(SectionPlan plan)
    {
        // every section draws from its own sequence, so a change to one leaves the others as they are
        var sectionId = plan.Id;
        var meter = plan.Meter;
        var context = _context.CreateContext(Seeds.Derive(_seed, sectionId));
        // how far the section strays from convention, facet by facet, each from a sequence of its own
        var facets = Facets(sectionId);
        StateTrace.Record(TracePoints.SectionUnconventionality, SectionTrace, sectionId, 0, StateMap.Default, 0, string.Join(" ", facets.Facets.OrderBy(x => x.Key).Select(x => $"{x.Key} {x.Value:F2}")), facets);
        var unconventionality = new HarmonicUnconventionality(facets[Facet.Chords], _genre);
        StateTrace.Record(TracePoints.SectionHarmony, SectionTrace, sectionId, 0, StateMap.Default, 0, $"{unconventionality.Chords:F2}", unconventionality);
        // the section's rhythm strays as its groove does
        var rhythm = new RhythmicUnconventionality(facets[Facet.Groove]) { GrooveHold = RhythmicUnconventionality.HoldOf(_genre) };
        var chords = LayerStates.CreateChordPool(unconventionality)(context);

        var (songStateMap, energy) = GetEnergy(sectionId, rhythm);
        var tilt = SectionEnergy.Tilt(energy, rhythm.Coupling);

        var (scale, home, harmonicRhythm, progression, chordScales, shifts, _, _) = DrawHarmony(plan, context, facets, energy, tilt);

        // the section's change of the song's feel, now and then, from a sequence of its own, by its feel facet
        var feelChange = Feels.DrawChange(Stream(sectionId, SectionStream.Feel), Feels.Of(songStateMap), Feels.SectionChange, facets[Facet.Feel], _genre);
        if (feelChange is { } feelOfSection)
            StateTrace.Record(TracePoints.Feel, SectionTrace, sectionId, 0, StateMap.Default, 0, $"{feelOfSection}", feelOfSection);
        var sectionStateMap = CreateSectionStateMap(
            songStateMap,
            new StateMapBuilder("Section")
                .AddRhythmLayer(rhythm.Lean(RhythmLayers.Section))
                .AddNoteWalkLayer()
                .Add(CompositionStateKinds.ChordPool.Index, LayerStates.ChordPoolIndex)
                .Add(StateKinds.Velocity, VelocityLayers.CreateGenerator(VelocityLayers.Section, tilt))
                .Add(CompositionStateKinds.NoteDynamics, Math.Pow(rhythm.ChanceScale, VelocityLayers.DynamicsLean))
                .AddNoteDurationLayer()
                .ToStateMap(context)
                .MergeWith(feelChange is { } changed ? Feels.At(StateDepths.Section, changed) : StateMap.Default),
            new StateMapBuilder("Section")
                .Add(CompositionStateKinds.ChordPool.Collection, chords)
                .Add(StateKinds.ChordRoot, home)
                // the melody higher the more energy the section has than the song, a chorus over its verse
                .Add(CompositionStateKinds.LineLift, MelodyLayers.EnergyRegister * _sectionEnergies[sectionId].GetStateValue(CompositionStateKinds.Energy))
                .ToStateMap(context)
        );
        var (sectionRoles, kit, isPercussionOnly, doubles, feelLeads) = _drums.DrawDrumParts(sectionId, plan.Drums, rhythm);
        var activeDrumTrackNumbers = kit.Drums.Select(DrumGroups.GetTrackNumber).ToImmutableHashSet();

        // the section state reaches the notes through the track state maps, so the bar state holds only the state
        // that changes by bar
        var bassLeading = Math.Clamp(
            _tracks.BassLeading + BassLeadingLayers.CreateGenerator(BassLeadingLayers.Section)(context),
            0,
            1
        );
        var barStateTimelineMap = _generators(meter).Bars.Generate(context, scale, progression, harmonicRhythm, home, chordScales, shifts, unconventionality, bassLeading, facets);
        var contour = barStateTimelineMap.GetStateTimeline(CompositionStateKinds.LineRegister).Select(x => x.Value).ToImmutableArray();
        StateTrace.Record(TracePoints.MelodyContour, SectionTrace, sectionId, 0, StateMap.Default, 0, string.Join(", ", contour), contour);

        // every track follows the section's phrase scheme, so they repeat their bars in the same places
        var scheme = PhraseSchemes.Pick(context, facets[Facet.Form], _genre);
        StateTrace.Record(TracePoints.PhraseScheme, SectionTrace, sectionId, 0, StateMap.Default, 0, scheme.ToString(), scheme);
        var sectionRhythm = new SectionRhythm(rhythm, scheme, _songMelodyBusyness.GenerateSection(context, tilt), tilt, facets);

        var (sectionStrokes, barDrums) = _drums.DrawDrumBars(sectionId, activeDrumTrackNumbers, kit, scheme, rhythm, tilt);

        // a heavy band's breakdown now and then, a genre's, from a sequence of its own
        var breakdown = Breakdowns.MayBreakDown(plan.Role, plan.KeepsSongScale)
                        && Stream(sectionId, SectionStream.Breakdown).TestProbability(Breakdowns.Chance.At(facets[Facet.Form], _genre.Chance(GenreChoices.Breakdown, "sections")));
        StateTrace.Record(TracePoints.Breakdown, SectionTrace, sectionId, 0, StateMap.Default, 0, $"{breakdown}", breakdown);
        // the drums in half time now and then, in the quieter sections, or double time, in the louder, from a sequence of its
        // own, drawn in a breakdown too, which plays in half time
        var timeFeel = Groove.DrawTimeFeel(Stream(sectionId, SectionStream.TimeFeel), tilt, sectionRhythm.Facets[Facet.Groove], _genre);
        if (breakdown)
            timeFeel = 1;
        StateTrace.Record(TracePoints.TimeFeel, SectionTrace, sectionId, 0, StateMap.Default, 0, $"{timeFeel}", timeFeel);
        // the drums from a sequence of their own, so that the pitched tracks are the same whichever drums the song plays
        var drums = _drums.GenerateDrums(Stream(sectionId, SectionStream.Drums), sectionId, sectionStateMap, activeDrumTrackNumbers, doubles, feelLeads, sectionRoles, sectionStrokes, barDrums, barStateTimelineMap, sectionRhythm, timeFeel, meter).ToArray();
        var (powerChords, chugs, octaveBass, hooks) = DrawDevices(sectionId, plan.Role, facets);
        // a breakdown's riff chugging on its pedal
        chugs |= breakdown;
        var pitched = GeneratePitchedTracks(context, sectionId, sectionStateMap, barStateTimelineMap, harmonicRhythm, sectionRhythm, meter, hooks, powerChords, chugs).ToArray();
        // the section's pattern played once, twice or four times, its melody as a question and its answer, from a sequence
        // of its own, the less conventional the section the likelier it plays other than twice
        var plays = DrawPlays(sectionId, plan.Role, plan.PlaysLean, meter.Phrases);
        StateTrace.Record(TracePoints.SectionLength, SectionTrace, sectionId, 0, StateMap.Default, 0, $"{plays}", plays);
        var timeline = KeepRenderState([..drums.Select(x => x.Timeline), ..pitched.Select(x => x.Bars.Timeline)], barStateTimelineMap, meter).Repeat(plays);
        // the parts the section leaves out the first time it plays, from a sequence of its own, the likelier the less energy
        // it has; a later appearance draws them again (Appear)
        var arrangementSeed = StreamSeed(sectionId, SectionStream.Arrangement);
        var resting = Arrangement.DrawRests(new GenerationContext(arrangementSeed), tilt, plan.Role, _absent, _genre, facets[Facet.Form]);
        ImmutableArray<SectionLine> lines = [..pitched.Select(x => x.Line).OfType<SectionLine>()];
        var section = new GeneratedSection(
            timeline,
            rhythm,
            facets,
            GetGrooves(drums.SelectMany(x => x.Feels), meter),
            energy,
            isPercussionOnly,
            sectionRoles.Keys.ToImmutableDictionary(x => x, x => _drums.SectionRole(sectionRoles, x)),
            lines,
            plays,
            doubles.ToImmutableDictionary(x => x.Key, x => x.Value.Lead),
            resting,
            _absent,
            plan.Role,
            arrangementSeed,
            _tracks.Definitions.ToImmutableDictionary(x => x.Key, x => x.Value.Role),
            meter,
            powerChords,
            chugs,
            octaveBass,
            breakdown,
            _drumJob,
            _genre
        );
        // every part, which each appearance leaves out of it what it rests
        return section;
    }

    /// <summary>
    ///     The section's harmony: its scale, its home, how often its chords change and its progression, turned around now and
    ///     then, the cadence raised, every chord's scale, passing chords and planing now and then, or the song's first
    ///     section's where it loops; each decision from a sequence of its own but the home's and the progression's, the
    ///     section's own.
    /// </summary>
    private SectionHarmony DrawHarmony(SectionPlan plan, IGenerationContext context, Unconventionality facets, double energy, Tilt tilt)
    {
        var sectionId = plan.Id;
        var meter = plan.Meter;
        var scale = plan.KeepsSongScale ? _songScale : PickScale(sectionId, facets[Facet.Scale], energy, plan.Role);
        // the section's chords move around its home, which every track's root starts from; the song's last section
        // leads home to its tonic, where the song ends
        var home = Progressions.GenerateHome(context, scale, plan.Role, _genre, facets[Facet.Progression]);
        if (plan.HasTonicHome)
            home = 0;
        // how often its chords change, from a sequence of its own, faster the more energy it has
        var harmonicRhythm = HarmonicRhythm.Draw(Stream(sectionId, SectionStream.HarmonicRhythm), tilt, meter, facets[Facet.Progression], _genre);
        var progression = Progressions.Generate(context, scale, home, Progressions.Strictness.At(facets[Facet.Progression]), harmonicRhythm.Count, meter.Phrases,
            motion => _genre.Shift(GenreChoices.RootMotions, Progressions.Motions[motion].Name), step => _genre.Shift(GenreChoices.RootDegrees, Progressions.Degrees[step].Name),
            step => _genre.Shift(GenreChoices.CadenceDegrees, Progressions.Degrees[step].Name));
        // a turnaround now and then, the pattern's last bar leading back to its start, by the progression facet and a genre's,
        // from a sequence of its own
        var turnaround = Stream(sectionId, SectionStream.Turnaround).TestProbability(Progressions.Turnaround.At(facets[Facet.Progression], _genre.Chance(GenreChoices.Turnaround, "sections")));
        if (turnaround)
            progression = Progressions.TurnAround(progression, harmonicRhythm.Changes.Count(x => x >= meter.PatternDuration - meter.BarDuration - 1e-9));
        StateTrace.Record(TracePoints.Turnaround, SectionTrace, sectionId, 0, StateMap.Default, 0, $"{turnaround}", turnaround);
        // the cadence raising the seventh for a major chord on the fifth, where the scale allows it, by the progression
        // facet, from a sequence of its own
        var raisable = Progressions.GetCadenceRaisedStep(scale.Offsets, home, progression[^1]);
        var raisedStep = raisable is not null && Stream(sectionId, SectionStream.CadenceRaise).TestProbability(Progressions.CadenceRaise.At(facets[Facet.Progression], _genre.Chance(GenreChoices.CadenceRaise, "sections")))
            ? raisable
            : null;
        // every chord's scale: the cadence's raise, and the others' applied or borrowed now and then, from a sequence of
        // their own
        // a section of dominants, a genre's, from a sequence of its own, so that a song in none draws as it did
        var dominants = Stream(sectionId, SectionStream.DominantChords)
            .TestProbability(ChordScales.Dominant.At(facets[Facet.Progression], _genre.Chance(GenreChoices.ChordScales, "dominant")));
        var chordScales = ChordScales.Draw(Stream(sectionId, SectionStream.ChordScales), Stream(sectionId, SectionStream.ChordOwnScales), scale, home, progression, raisedStep,
            dominants, facets[Facet.Progression], _genre);
        // the home held over the first two chords now and then, a slower start, the rest changing as they were, from a
        // sequence of its own
        if (progression.Length >= 4 && Stream(sectionId, SectionStream.HeldHome).TestProbability(Progressions.HeldHome.At(facets[Facet.Progression])))
            (progression, chordScales) = (progression.SetItem(1, progression[0]), chordScales.SetItem(1, chordScales[0]));
        // passing chords now and then, the next chord's dominant, by the progression facet, the energy and a genre's, from a
        // sequence of their own
        var passingChance = tilt.Chance(PassingChords.Chance.At(facets[Facet.Progression], _genre.Chance(GenreChoices.PassingChords, "chords")), PassingChords.EnergyLean);
        var (passingRhythm, passingProgression, passingScales, shifts) = PassingChords.Draw(Stream(sectionId, SectionStream.PassingChords), passingChance, facets[Facet.Progression],
            harmonicRhythm, progression, chordScales, scale, home, _genre);
        (harmonicRhythm, progression, chordScales) = (passingRhythm, passingProgression, passingScales);
        // a wild section planing now and then, every chord the home's shape moved, by the progression facet and a genre's,
        // from a sequence of its own, the song's last too, which ends in the language the song spoke, sliding home at its end
        // a section vamping on its home, a genre's, from a sequence of its own, as planing does without its moves, the
        // cadence's raise with it, the progression's other draws made all the same
        if (Stream(sectionId, SectionStream.Vamp).TestProbability(Vamp.At(facets[Facet.Progression], _genre.Chance(GenreChoices.Vamp, "sections"))))
            (progression, chordScales, shifts, raisable, raisedStep) = ([..progression.Select(_ => 0)], [..chordScales.Select(_ => ChordScale.Section)], [..shifts.Select(_ => 0)], null, null);
        var planingContext = Stream(sectionId, SectionStream.Planing);
        // the song's last section planing back home, its last move a slide onto the tonic's shape on the tonic, but one of
        // two chords, which has no move to slide by
        var planes = planingContext.TestProbability(Planing.Chance.At(facets[Facet.Progression], _genre.Chance(GenreChoices.Planing, "sections")))
                     && (!plan.HasTonicHome || progression.Length >= 3);
        var planed = Planing.Draw(planingContext, progression.Length, plan.HasTonicHome);
        if (planes)
            (progression, chordScales, shifts) = ([..progression.Select(_ => 0)], [..chordScales.Select(_ => ChordScale.Section)], planed);
        StateTrace.Record(TracePoints.Planing, SectionTrace, sectionId, 0, StateMap.Default, 0, planes ? string.Join(" ", planed) : "none", planes ? planed : null);
        // a section of a loop plays the first section's harmony where its meter is the first's, its own drawn all the same
        // so that its other draws stay, a genre's, from a sequence of its own (GenreChoices.Loop)
        var loops = Stream(sectionId, SectionStream.Loop).TestProbability(Loop.At(facets[Facet.Form], _genre.Chance(GenreChoices.Loop, "sections")));
        var harmony = new SectionHarmony(scale, home, harmonicRhythm, progression, chordScales, shifts, raisable, raisedStep);
        if (plan.KeepsSongScale)
            _firstHarmony = (harmony, meter);
        else if (loops && (_firstHarmony ?? throw new InvalidOperationException("A section loops before the song's first is made.")).Meter.Equals(meter))
            harmony = _firstHarmony.Value.Harmony;
        (scale, home, harmonicRhythm, progression, chordScales, shifts, raisable, raisedStep) = harmony;
        StateTrace.Record(TracePoints.SectionScale, SectionTrace, sectionId, 0, StateMap.Default, 0, scale.Name, scale);
        StateTrace.Record(TracePoints.HarmonicRhythm, SectionTrace, sectionId, 0, StateMap.Default, 0, $"{harmonicRhythm.Span}", harmonicRhythm);
        StateTrace.Record(TracePoints.Progression, SectionTrace, sectionId, 0, StateMap.Default, 0, string.Join(" ", progression), progression);
        StateTrace.Record(TracePoints.CadenceRaise, SectionTrace, sectionId, 0, StateMap.Default, 0, $"{raisable} {raisedStep is not null}", (raisable, raisedStep is not null));
        StateTrace.Record(TracePoints.ChordScales, SectionTrace, sectionId, 0, StateMap.Default, 0, string.Join(" ", chordScales.Select(x => x.Kind)), chordScales);
        return harmony;
    }

    /// <summary>
    ///     The section's devices, each a genre's the more and from a sequence of its own: its riff in power chords, its guitars'
    ///     repeats palm-muted, its bass in octaves and its melody on a hook.
    /// </summary>
    private (bool PowerChords, bool Chugs, bool OctaveBass, bool Hooks) DrawDevices(int sectionId, SectionRole role, Unconventionality facets)
    {
        // its riff in power chords, a genre's, which the songs never play, from a sequence of its own
        var powerChords = Stream(sectionId, SectionStream.PowerChords).TestProbability(PowerChordChance.At(facets[Facet.Sound], _genre.Chance(GenreChoices.Riff, "powerChords")));
        StateTrace.Record(TracePoints.RiffPowerChords, SectionTrace, sectionId, 0, StateMap.Default, 0, $"{powerChords}", powerChords);
        // its guitars' and its picked bass's repeats palm-muted, a genre's, which the songs never play, from a sequence of its own
        var chugs = Stream(sectionId, SectionStream.Chugs).TestProbability(ChugChance.At(facets[Facet.Sound], _genre.Chance(GenreChoices.Riff, "chugs")));
        StateTrace.Record(TracePoints.Chugs, SectionTrace, sectionId, 0, StateMap.Default, 0, $"{chugs}", chugs);
        // the bass in octaves now and then, a genre's the more, from a sequence of its own
        var octaveBass = Stream(sectionId, SectionStream.OctaveBass).TestProbability(OctaveBassChance.At(facets[Facet.Groove], _genre.Chance(GenreChoices.OctaveBass, "sections")));
        StateTrace.Record(TracePoints.OctaveBass, SectionTrace, sectionId, 0, StateMap.Default, 0, $"{octaveBass}", octaveBass);
        // its melody on a hook now and then, a chorus by its role or any section by a genre's, from a sequence of its own
        var hooks = Stream(sectionId, SectionStream.Hook).TestProbability(MelodyLayers.HookChanceOf(role)
            .At(facets[Facet.Melody], _genre.Chance(GenreChoices.Hook, role == Composition.SectionRole.Chorus ? "choruses" : "sections")));
        StateTrace.Record(TracePoints.MelodyHook, SectionTrace, sectionId, 0, StateMap.Default, 0, $"{hooks}", hooks);
        return (powerChords, chugs, octaveBass, hooks);
    }

    /// <summary>
    ///     A section's drums, drawn before the sections are made, so that the song sees them all (<see cref="DrumCoverage" />):
    ///     whether it plays percussion only and its kit (<see cref="SectionDrumGenerator.DrawDrums" />).
    /// </summary>
    public SectionDrums DrawDrums(int sectionId, SectionRole role, bool? isPercussionOnly = null) => _drums.DrawDrums(sectionId, role, isPercussionOnly);

    /// <summary>
    ///     How many times a section plays its pattern, from a sequence of its own, by the form facet of the section's
    ///     unconventionality, the same before the section is made as when it is (<see cref="SectionLength" />).
    /// </summary>
    /// <param name="lean">How far the song's length leans the plays (<see cref="SectionLength.Draw" />).</param>
    /// <param name="phrases">
    ///     How many phrases the section's pattern has: its plays drawn in phrases, a four-bar pattern's, and played as
    ///     near in its longer pattern, once at least, so that a longer pattern plays as long a section.
    /// </param>
    public int DrawPlays(int sectionId, SectionRole role, double lean, int phrases)
    {
        var drawn = SectionLength.Draw(Stream(sectionId, SectionStream.Length), Facets(sectionId)[Facet.Form], role, lean, _genre);
        return phrases == 1 ? drawn : Math.Max(1, (int)Math.Round(drawn / (double)phrases));
    }

    /// <summary>How far a section strays from convention, facet by facet, each from a sequence of its own.</summary>
    private Unconventionality Facets(int sectionId)
    {
        return _songFacets.GenerateSection(facet => _context.CreateContext(Seeds.Derive(StreamSeed(sectionId, SectionStream.Unconventionality), (int)facet)));
    }

    /// <summary>
    ///     How loud and busy the section is meant to be: the song's state with the section's layers of the energy, and
    ///     their sum, which leans the section's draws as far as its rhythm follows it.
    /// </summary>
    private (StateMap SongStateMap, double Energy) GetEnergy(int sectionId, RhythmicUnconventionality rhythm)
    {
        var songStateMap = _songStateMap.MergeWith(_sectionEnergies[sectionId]);
        var energy = songStateMap.GetStateValue(CompositionStateKinds.Energy);
        StateTrace.Record(
            TracePoints.SectionEnergy,
            SectionTrace,
            sectionId,
            0,
            songStateMap.Subset([CompositionStateKinds.Energy]),
            0,
            $"energy {energy:F2}, pull {energy * rhythm.Coupling:F2}",
            new SectionEnergyTrace(energy, energy * rhythm.Coupling)
        );
        return (songStateMap, energy);
    }

    /// <summary>
    ///     The section's scale: the song's, or now and then another on its tonic, leaning brighter the more energy the
    ///     section has, as far as its harmony follows it, and a chorus or a bridge changing the likelier; drawn from a
    ///     sequence of its own, so that the section's other draws stay as they are.
    /// </summary>
    private Scale PickScale(int sectionId, double unconventionality, double energy, SectionRole role)
    {
        return Scales.PickSection(
            Stream(sectionId, SectionStream.Scale),
            _songScale,
            unconventionality,
            SectionEnergy.Tilt(energy, Unconventionality.Coupling(unconventionality)),
            SectionContrast.Of(role),
            _genre
        );
    }

    /// <summary>
    ///     The section's tracks as the song keeps them: what Render reads of their notes, now that the melody is placed,
    ///     and of the bar state, whose state for the generation, such as the chord pool's pick, stays here.
    /// </summary>
    private static TrackEventStateTimelineMap<StateMap> KeepRenderState(
        IEnumerable<TrackEventStateTimelineMap<StateMap>> tracks,
        StateTimelineMap barStateTimelineMap,
        Meter meter
    )
    {
        var timeline = TrackEventStateTimelineMap.Merge(
            [..tracks, barStateTimelineMap.OfScope(StateScope.Render).ToTrackEventStateTimelineMap<StateMap>(meter.PatternDuration)]
        );
        return timeline.MapTrackEvents(
            timeline.TrackTimelineMap.Keys.ToDictionary(
                x => x,
                _ => (Func<EventTimeline<StateMap>, EventTimeline<StateMap>>)(notes => notes.MapValues(x => x.OfScope(StateScope.Render)))
            )
        );
    }

    /// <summary>
    ///     The rhythm the fills play from, in the pattern's last bar, before both of the section's lines: every drum's
    ///     state, which sets the notes of a run the drum plays, and the snare's, which the run's rhythm is, or where the
    ///     section plays no snare, that of the drum with the most notes there.
    /// </summary>
    internal static FillGrooves GetGrooves(IEnumerable<BarFeel> feels, Meter meter)
    {
        var snares = DrumGroups.Snare.Drums.Select(DrumGroups.GetTrackNumber).ToHashSet();
        var lastBar = feels.Where(x => x.Bar == meter.PatternBarCount - 1).ToArray();
        var source = lastBar
            .Where(x => x.NoteCount > 0)
            .OrderByDescending(x => snares.Contains(x.Track))
            .ThenByDescending(x => x.NoteCount)
            .ThenBy(x => x.Track)
            .Select(x => x.Rhythm)
            .FirstOrDefault() ?? ResolvedRhythm.DefaultState;
        return new FillGrooves(source, lastBar.ToImmutableDictionary(x => x.Track, x => x.Rhythm));
    }

    /// <summary>The pitched tracks, each making its patterns on its own, over the section's state.</summary>
    private IEnumerable<(int Track, GeneratedBars Bars, SectionLine? Line)> GeneratePitchedTracks(
        IGenerationContext context,
        int sectionId,
        StateMap sectionStateMap,
        StateTimelineMap barStateTimelineMap,
        HarmonicRhythm harmonicRhythm,
        SectionRhythm sectionRhythm,
        Meter meter,
        bool hooks,
        bool powerChords,
        bool chugs
    )
    {
        foreach (var trackNumber in _tracks.NonGroupedTrackNumbers)
        {
            // a riff's twin has no notes of its own, but the riff's, moved, once the song is put together (LineDoubling), and
            // the solo part none but a solo's (Solos), and they draw nothing
            if (!_tracks.Definitions[trackNumber].Role.HasNotesOfItsOwn())
            {
                var empty = TrackEventStateTimelineMap.Create(
                    meter.PatternDuration,
                    [new KeyValuePair<int, EventStateTimelineMap<StateMap>>(trackNumber, EventTimeline.Create<StateMap>(meter.PatternDuration).ToEventStateTimelineMap(StateMap.Default))],
                    StateTimelineMap.Create(meter.PatternDuration)
                );
                yield return (trackNumber, new GeneratedBars(empty, []), null);
                continue;
            }

            var trackStateMap = TrackStateMap(context, trackNumber, sectionId, sectionStateMap, sectionRhythm, powerChords, chugs);
            if (_tracks.Definitions[trackNumber].Role == TrackRole.Pad)
            {
                yield return (trackNumber, GeneratePad(trackNumber, trackStateMap, barStateTimelineMap, harmonicRhythm, meter), null);
                continue;
            }


            var trackStateMaps = new Dictionary<int, StateMap> { [trackNumber] = trackStateMap };
            // the melody answers its question: the answer's later bars draw their rhythm afresh now and then, and its notes
            // there are mutated, by the section's amount, each decision from the answer's own sequence; but a hook, whose
            // answer is its question again
            var isMelody = _tracks.Definitions[trackNumber].Role == TrackRole.Melody;
            var amount = isMelody && hooks ? 0 : MelodyLayers.AnswerAmounts.At(sectionRhythm.Facets[Facet.Melody]);
            var answerContext = Stream(sectionId, SectionStream.MelodyAnswer);
            var answerSeed = answerContext.GenerateSeed();
            var questionEnd = barStateTimelineMap.GetEffectiveStateMapAt(meter.PatternDuration - meter.BarDuration).GetStateValue(CompositionStateKinds.MelodyPhraseEnd);
            var answer = isMelody ? LinePattern.DrawAnswer(answerContext, amount, questionEnd) : null;
            // a riff's bars follow a riff's scheme, from a sequence of its own, rather than the section's phrase
            var isRiff = _tracks.Definitions[trackNumber].Role == TrackRole.Riff;
            // and a hook's repeats are its first bar's, none varied
            var trackRhythm = isRiff ? sectionRhythm with { Scheme = Stream(sectionId, SectionStream.Riff).Pick(RiffLayers.Schemes) }
                : isMelody && hooks ? sectionRhythm with { Scheme = sectionRhythm.Scheme.Unvaried() }
                : sectionRhythm;
            var seeds = PatternGenerator.DrawSeeds(context, trackStateMaps.Keys, trackRhythm.Scheme);
            GeneratedBars BuildBars(ImmutableArray<int> rhythmKeys) => _generators(meter).Patterns.BuildBars(
                seeds,
                sectionId,
                trackStateMaps.ToImmutableDictionary(),
                BarDrums.None,
                ImmutableDictionary<int, Doubling>.Empty,
                ImmutableDictionary<int, int>.Empty,
                barStateTimelineMap,
                trackRhythm,
                answer,
                rhythmKeys
            );
            var bars = BuildBars([]);
            TrackEventStateTimelineMap<StateMap> Rebuilt(ImmutableArray<int> rhythmKeys)
            {
                using var pause = StateTrace.Pause();
                return BuildBars(rhythmKeys).Timeline.Repeat(2);
            }

            // a line, placed with the song's, its pattern played twice as the section does, its figures played again as its
            // scheme has them: the bass leading and landing as its chords have it, the riff and the counter-melody leading
            // into the chord changes by step now and then, each from a sequence of its own
            var role = _tracks.Definitions[trackNumber].Role;
            if (role is TrackRole.Bass or TrackRole.Riff or TrackRole.CounterMelody)
            {
                // the bass's leading its chords', the others' drawn by their parts' leading
                var (profile, leadsBy, improvisation) = role switch
                {
                    TrackRole.Bass => (BassLeadingLayers.Line, ((double, SectionStream)?)null, SectionStream.BassImprovisation),
                    TrackRole.Riff => (RiffLayers.Line, (RiffLayers.Leading, SectionStream.RiffLeading), SectionStream.RiffImprovisation),
                    _ => (CounterLayers.Line, (CounterLayers.Leading, SectionStream.CounterLeading), SectionStream.CounterImprovisation)
                };
                ImmutableArray<ChordApproach> approaches = leadsBy is var (chance, leads)
                    ? LinePattern.DrawApproaches(Stream(sectionId, leads), chance, harmonicRhythm.Count)
                    : [..harmonicRhythm.Changes.Select(x => (ChordApproach)barStateTimelineMap.GetEffectiveStateMapAt(x).GetStateValue(StateKinds.ChordApproach))];
                ImmutableArray<ChordArrival> landings = role == TrackRole.Bass
                    ? [..harmonicRhythm.Changes.Select(x => (ChordArrival)barStateTimelineMap.GetEffectiveStateMapAt(x).GetStateValue(StateKinds.ChordArrival))]
                    : [..Enumerable.Repeat(ChordArrival.Free, harmonicRhythm.Count)];
                var line = new SectionLine(trackNumber, profile, bars.Timeline.Repeat(2), Rebuilt, trackRhythm.Scheme.Across(meter.Phrases).Letters, harmonicRhythm, approaches, landings,
                    profile.RegisterFreedom, StreamSeed(sectionId, improvisation), false);
                yield return (trackNumber, bars with { Timeline = line.Appear(0, 0).Trim(meter.PatternDuration) }, line);
                continue;
            }

            if (!isMelody)
            {
                yield return (trackNumber, bars, null);
                continue;
            }

            // the melody's notes are placed once its bars are made, in their order, over the question and its answer,
            // leading into the chord changes within a phrase as often as the section has it
            var leadingContext = Stream(sectionId, SectionStream.MelodyLeading);
            var leading = Math.Clamp(MelodyLayers.Leading + Generators.SplineValue()(leadingContext) * MelodyLayers.LeadingSpread, 0, 1);
            StateTrace.Record(TracePoints.MelodyLeading, SectionTrace, sectionId, 0, StateMap.Default, 0, $"{leading:F2}", leading);
            // how freely the melody changes register where a phrase starts: the line's, as the song and the section move it
            var freedomContext = Stream(sectionId, SectionStream.RegisterFreedom);
            var freedom = Math.Clamp(
                MelodyLayers.Line.RegisterFreedom + (_songRegisterFreedomShift + Generators.SplineValue()(freedomContext)) * MelodyLayers.Line.RegisterFreedomSpread,
                0,
                1
            );
            StateTrace.Record(TracePoints.LineRegisterFreedom, SectionTrace, sectionId, 0, StateMap.Default, 0, $"{freedom:F2}", freedom);
            // a later appearance builds its bars afresh where its rhythm is improvised, as recorded the first time
            var melody = new SectionLine(
                trackNumber,
                MelodyLayers.Line,
                LinePattern.Answer(bars.Timeline, trackNumber, MelodyLayers.Line, answerSeed, amount, meter),
                rhythmKeys =>
                {
                    using var pause = StateTrace.Pause();
                    return LinePattern.Answer(BuildBars(rhythmKeys).Timeline, trackNumber, MelodyLayers.Line, answerSeed, amount, meter);
                },
                sectionRhythm.Scheme.Across(meter.Phrases).Letters,
                harmonicRhythm,
                LinePattern.DrawApproaches(leadingContext, leading, harmonicRhythm.Count),
                [..Enumerable.Repeat(ChordArrival.Free, harmonicRhythm.Count)],
                freedom,
                StreamSeed(sectionId, SectionStream.MelodyImprovisation),
                hooks
            );
            StateTrace.Record(TracePoints.MelodyAnswer, SectionTrace, sectionId, 0, StateMap.Default, 0, $"{amount:F2}", amount);
            yield return (trackNumber, bars with { Timeline = melody.Appear(0, 0).Trim(meter.PatternDuration) }, melody);
        }
    }

    /// <summary>
    ///     A pitched track's state in the section: its own section layer, its voice leading, the melody's stepwiseness and
    ///     busyness, the melody's and the riff's line scales, the riff in power chords, the chords broken now and then, and
    ///     its fullness by the section's energy, over the section's state.
    /// </summary>
    private StateMap TrackStateMap(IGenerationContext context, int trackNumber, int sectionId, StateMap sectionStateMap, SectionRhythm sectionRhythm, bool powerChords, bool chugs)
    {
        // a section's chords move more smoothly or more in blocks than the song's, and its melody more or less by step,
        // and is as busy as the section has it
        var sectionTrackLayer = new StateMapBuilder("Section track", perTrack: true)
            .Add(StateKinds.VoiceLeading, VoiceLeadingLayers.CreateGenerator(VoiceLeadingLayers.Section));
        if (_tracks.Definitions[trackNumber].Role == TrackRole.Melody)
        {
            sectionRhythm.MelodyBusyness.AddTo(
                sectionTrackLayer.Add(CompositionStateKinds.LineStepwiseness, MelodyLayers.CreateGenerator(MelodyLayers.Section)),
                sectionRhythm.Energy
            );
        }
        // the melody's and the riff's line scales, each from a sequence of its own, by the section's scale facet
        if (_tracks.Definitions[trackNumber].Role is TrackRole.Melody or TrackRole.Riff)
        {
            var stream = _tracks.Definitions[trackNumber].Role == TrackRole.Melody ? SectionStream.LineScale : SectionStream.RiffLineScale;
            var lineScale = LineScales.Draw(Stream(sectionId, stream), sectionRhythm.Facets[Facet.Scale], _genre, _tracks.Definitions[trackNumber].Role == TrackRole.Riff);
            StateTrace.Record(TracePoints.LineScale, trackNumber, sectionId, 0, StateMap.Default, 0, $"{lineScale}", lineScale);
            sectionTrackLayer.Add(CompositionStateKinds.LineScale, (int)lineScale);
            // a riff that chugs on a pedal, its power chords by its notes (Chugs); one that does not in power chords throughout
            if (chugs && _tracks.Definitions[trackNumber].Role == TrackRole.Riff)
                sectionTrackLayer.Add(CompositionStateKinds.LinePedal, 1);
            else if (powerChords && _tracks.Definitions[trackNumber].Role == TrackRole.Riff)
                sectionTrackLayer.Add(CompositionStateKinds.PowerChord, 1);
        }
        // a line's strong beats on a tension now and then, as the genre has its part, by the section's melody facet
        if (LinePattern.Tensions.Where(x => x.Role == _tracks.Definitions[trackNumber].Role).Select(x => (ByConvention?)x.Chance).FirstOrDefault() is { } tension)
        {
            var chance = tension.At(sectionRhythm.Facets[Facet.Melody], _genre.Chance(GenreChoices.Tension, GenreChoices.Name(_tracks.Definitions[trackNumber].Role.ToString())));
            if (chance > 0)
                sectionTrackLayer.Add(CompositionStateKinds.LineTension, chance);
        }
        // a line's figures played inverted where they come back now and then, as the part and the genre have it, by the section's melody facet
        if (LinePattern.Inversions.Where(x => x.Role == _tracks.Definitions[trackNumber].Role).Select(x => (ByConvention?)x.Chance).FirstOrDefault() is { } inversions
            && inversions.At(sectionRhythm.Facets[Facet.Melody], _genre.Chance(GenreChoices.Inversion, GenreChoices.Name(_tracks.Definitions[trackNumber].Role.ToString()))) is var inversion and > 0)
            sectionTrackLayer.Add(CompositionStateKinds.LineInversion, inversion);
        // a line's phrases ending on a goal, as the part has it, by the section's melody facet
        if (LinePattern.Goals.Where(x => x.Role == _tracks.Definitions[trackNumber].Role).Select(x => (ByConvention?)x.Chance).FirstOrDefault() is { } goals
            && goals.At(sectionRhythm.Facets[Facet.Melody]) is var goal and > 0)
            sectionTrackLayer.Add(CompositionStateKinds.LineGoal, goal);
        // the chords broken now and then, each part that plays them from a sequence of its own, by the section's chords facet
        if (_tracks.Definitions[trackNumber].Role.PlaysChords())
        {
            var stream = _tracks.Definitions[trackNumber].Role == TrackRole.Chords ? SectionStream.Arpeggio : SectionStream.RhythmArpeggio;
            var arpeggio = Arpeggios.Draw(Stream(sectionId, stream), sectionRhythm.Facets[Facet.Chords], _genre);
            StateTrace.Record(TracePoints.Arpeggio, SectionTrace, sectionId, 0, StateMap.Default, 0, $"{arpeggio}", arpeggio);
            sectionTrackLayer.Add(CompositionStateKinds.Arpeggio, (int)arpeggio);
            // and the colour it lays on its broken chord, from a sequence of its own
            var colour = Arpeggios.DrawColour(new GenerationContext(Seeds.Derive(StreamSeed(sectionId, SectionStream.Colour), trackNumber)), sectionRhythm.Facets[Facet.Chords], _genre);
            if (arpeggio != ArpeggioPattern.None && colour != ChordColour.None)
                sectionTrackLayer.Add(CompositionStateKinds.Colour, (int)colour);
        }
        // fuller and busier the more energy the section has, as its drums are
        return CreateSectionTrackLayer(context, trackNumber, sectionRhythm, sectionRhythm.Energy, null)
            .MergeWith(sectionStateMap)
            .MergeWith(sectionTrackLayer.ToStateMap(context));
    }

    /// <summary>
    ///     A pad's pattern: a chord at every change of chord, held until the next, over the state of the section and the
    ///     chord its pool picks there, as the chords' notes take theirs.
    /// </summary>
    private static GeneratedBars GeneratePad(int trackNumber, StateMap trackStateMap, StateTimelineMap barStateTimelineMap, HarmonicRhythm harmonicRhythm, Meter meter)
    {
        var notes = harmonicRhythm.Changes.Select((change, index) => trackStateMap
            .MergeWith(PatternGenerator.PickChord(trackStateMap, barStateTimelineMap.GetEffectiveStateMapAt(change)))
            .With(CompositionStateKinds.BeatRank, 0)
            .With(StateKinds.HeldDuration, harmonicRhythm.LengthOf(index))
            .ToTimelineItem(change)
        );
        var timeline = TrackEventStateTimelineMap.Create(
            meter.PatternDuration,
            [
                new KeyValuePair<int, EventStateTimelineMap<StateMap>>(
                    trackNumber,
                    EventTimeline.Create(meter.PatternDuration, notes).ToEventStateTimelineMap(StateMap.Default)
                )
            ],
            StateTimelineMap.Create(meter.PatternDuration)
        );
        return new GeneratedBars(timeline, []);
    }

    /// <summary>
    ///     A track's own layer in the section, over the state its definition brings, its fullness and density leaning by
    ///     the tilt given, such as a drum's by the section's energy.
    /// </summary>
    /// <param name="role">A drum's role in the section, which holds its groove as it does; none for a pitched track.</param>
    private StateMap CreateSectionTrackLayer(IGenerationContext context, int trackNumber, SectionRhythm sectionRhythm, Tilt tilt, DrumRole? role)
    {
        var layer = sectionRhythm.Unconventionality.Lean(RhythmLayers.SectionTrack).Tilted(tilt);
        return LayerStates.CreateTrackLayer(
            context,
            "Section track",
            SongTracks.GetGenerationStateMap(_tracks.Definitions[trackNumber]),
            VelocityLayers.CreateGenerator(VelocityLayers.SectionTrack),
            role is { } drumRole ? DrumRoles.Hold(layer, drumRole) : layer
        );
    }

    /// <summary>
    ///     A section's state: the song's, then the section's own draws, then what the section adds to the song's
    ///     pools. A pool lists its entries in the order its layers are merged, and an index around 0 picks the first
    ///     ones most often, so the song's entries, such as its chord shapes, come first and are the likeliest, and the
    ///     section's add variety.
    /// </summary>
    internal static StateMap CreateSectionStateMap(StateMap songStateMap, StateMap sectionDraws, StateMap sectionPoolEntries)
    {
        return songStateMap
            .MergeWith(sectionDraws)
            .MergeWith(sectionPoolEntries);
    }
}

/// <summary>A section's place in the song's form, which some of its draws keep to.</summary>
/// <param name="HasTonicHome">Whether its home is the song's tonic, as the song's last section's is, where the song ends.</param>
/// <param name="KeepsSongScale">Whether it plays in the song's scale, as the song's first does, which sets the key.</param>
/// <param name="Role">What it does in the song's form, which leans its length and its home.</param>
/// <param name="Meter">The meter its bars are in.</param>
/// <param name="Drums">Its drums, as the song drew them before its sections (<see cref="SectionGenerator.DrawDrums" />).</param>
/// <param name="PlaysLean">How far the song's length leans how many times the section plays its pattern (<see cref="SectionLength.Draw" />).</param>
/// <summary>
///     A section's harmony (<see cref="SectionGenerator" />): its scale, its home, how often its chords change, its progression,
///     every chord's scale and its move by semitones, and the step its cadence may raise and raises.
/// </summary>
internal sealed record SectionHarmony(
    Scale Scale,
    int Home,
    HarmonicRhythm Rhythm,
    ImmutableArray<int> Progression,
    ImmutableArray<ChordScale> ChordScales,
    ImmutableArray<int> Shifts,
    int? Raisable,
    int? Raised
);

internal sealed record SectionPlan(int Id, bool HasTonicHome, bool KeepsSongScale, SectionRole Role, Meter Meter, SectionDrums Drums, double PlaysLean);

/// <summary>A section's drums: whether it plays its percussion without the drum kit, and the kit it plays.</summary>
/// <param name="PlaysDrums">Whether its drums play where it first plays, rather than rest for a breakdown.</param>
internal sealed record SectionDrums(bool IsPercussionOnly, SectionKit Kit, bool PlaysDrums);

/// <summary>A section's tracks, and what the fills need to know of its rhythm and energy.</summary>
/// <param name="Rhythm">How far the section's rhythm strays from convention.</param>
/// <param name="Groove">The states of the rhythm the fills play from.</param>
/// <param name="Energy">How loud and busy the section is meant to be (<see cref="SectionEnergy" />).</param>
/// <param name="IsPercussionOnly">Whether the section plays its percussion without the drum kit.</param>
/// <param name="DrumRoles">The role every one of the song's drums plays in the section.</param>
/// <param name="Lines">The section's lines, such as its melody, before they are placed, placed afresh every time it plays.</param>
/// <param name="Plays">How many times the section plays its 4-bar pattern: once, twice or four times (<see cref="SectionLength" />).</param>
/// <param name="Bindings">The drums bound to a lead, by their tracks, and their leads' tracks (<see cref="Doubling" />).</param>
/// <param name="Resting">The parts the section leaves out as it plays (<see cref="Arrangement" />).</param>
/// <param name="Absent">The parts the song leaves out, which every section rests (<see cref="SongParts" />).</param>
/// <param name="Role">What the section does in the song's form.</param>
/// <param name="ArrangementSeed">The seed of the sequences a later appearance draws its parts from.</param>
/// <param name="Roles">What every track plays, by its number.</param>
/// <param name="Meter">The meter the section's bars are in.</param>
/// <param name="Breakdown">Whether the section is a heavy band's breakdown (<see cref="Breakdowns" />), which leaves its lines and pad out.</param>
/// <param name="DrumJob">What the song's drums do, which a part left alone must carry.</param>
internal sealed record GeneratedSection(
    TrackEventStateTimelineMap<StateMap> Timeline,
    RhythmicUnconventionality Rhythm,
    Unconventionality Facets,
    FillGrooves Groove,
    double Energy,
    bool IsPercussionOnly,
    ImmutableDictionary<int, DrumRole> DrumRoles,
    ImmutableArray<SectionLine> Lines,
    int Plays,
    ImmutableDictionary<int, int> Bindings,
    ImmutableHashSet<TrackRole> Resting,
    ImmutableHashSet<TrackRole> Absent,
    SectionRole Role,
    ulong ArrangementSeed,
    ImmutableDictionary<int, TrackRole> Roles,
    Meter Meter,
    bool RiffPowerChords,
    bool Chugs,
    bool OctaveBass,
    bool Breakdown,
    DrumJob DrumJob,
    Genre Genre
)
{
    /// <summary>Whether the drums play in the section, rather than rest for a breakdown.</summary>
    public bool HasDrums => !Resting.Contains(TrackRole.Drum);

    /// <summary>The phrase the drums come in at, a build bringing them in late; 0 where they play from the start.</summary>
    public int DrumsFrom => PhraseSilent.Select((parts, phrase) => parts.Contains(TrackRole.Drum) ? phrase + 1 : 0).DefaultIfEmpty(0).Max();

    /// <summary>The parts each of the appearance's phrases leaves out, over those it rests throughout (<see cref="Composition.Texture" />).</summary>
    public ImmutableArray<ImmutableHashSet<TrackRole>> PhraseSilent { get; init; } = [];

    /// <summary>How the appearance brings its parts in over its phrases.</summary>
    public TextureKind TextureKind { get; init; } = TextureKind.Steady;

    /// <summary>The appearance's solo, none where it plays as it is.</summary>
    public SoloPlan? Solo { get; init; }

    // the sequence an appearance's texture is drawn from, apart from its parts'
    private const int TextureStream = 1000;

    /// <summary>
    ///     The section as it plays the given time, its parts as <see cref="Appear(int, double)" /> plays them, the first
    ///     time as drawn and a later time drawn again by the appearance's own energy, the section's and its step, from a
    ///     sequence of the appearance's own, a part that played the time before playing on, so that parts only join as a
    ///     section comes back.
    /// </summary>
    /// <param name="energyStep">How far the appearance's energy is from the section's, such as more for a later one.</param>
    /// <param name="before">The parts that rested the time before, of which it may leave out no more.</param>
    /// <param name="keepsTexture">Whether it plays steady, whatever its texture draws, as the song's first section after an intro that brings its parts in.</param>
    /// <param name="solo">The appearance's solo, whose soloists, and for a pitched solo the melody, play it throughout.</param>
    /// <param name="held">The parts the song holds back yet, which rest whatever the appearance draws, unless it would fall silent.</param>
    /// <param name="entering">The parts the song brings in at the appearance, which play whatever it draws, so that they are heard coming in.</param>
    public GeneratedSection Appear(
        int appearance,
        double improvisation,
        double energyStep,
        ImmutableHashSet<TrackRole> before,
        bool keepsTexture,
        SoloPlan? solo,
        ImmutableHashSet<TrackRole> held,
        ImmutableHashSet<TrackRole> entering
    )
    {
        var drawn = appearance == 0
            ? Resting
            : Arrangement.DrawRests(new GenerationContext(Seeds.Derive(ArrangementSeed, appearance)), SectionEnergy.Tilt(Energy + energyStep, Rhythm.Coupling), Role, Absent, Genre, Facets[Facet.Form])
                .Intersect(before);
        // a breakdown's lines and pad out every time
        var resting = drawn.Union(held).Union(Breakdown ? Breakdowns.Resting : []).Except(entering);
        // never silent: the parts held back playing where nothing else would, and every part the song has where its rests
        // alone leave none
        bool Silent() => !Roles.Values.Distinct().Any(x => !resting.Contains(x) && HasNotes(x));
        if (Silent())
            resting = resting.Except(held);
        if (Silent())
            resting = resting.Intersect(Absent);
        // how the parts come in over its phrases, from a sequence of its own, by the form facet
        // of the parts that have notes in the section, so that a part alone is never one that has none, such as the riff's
        // twin, the solo part or a line whose patterns came out empty
        bool HasNotes(TrackRole role) => Roles.Any(x => x.Value == role && Timeline.TrackTimelineMap.TryGetValue(x.Key, out var track) && track.EventTimeline.Count > 0);
        var playing = Roles.Values.Distinct().Where(x => !resting.Contains(x) && HasNotes(x)).ToArray();
        var texture = Composition.Texture.Draw(new GenerationContext(Seeds.Derive(Seeds.Derive(ArrangementSeed, appearance), TextureStream)), Facets[Facet.Form], Plays, playing, Genre,
            SectionEnergy.Tilt(Energy + energyStep, Rhythm.Coupling), DrumJob == DrumJob.Marks);
        ImmutableArray<ImmutableHashSet<TrackRole>> steady = [..Enumerable.Repeat(ImmutableHashSet<TrackRole>.Empty, Plays)];
        // a breakdown played steady, its riff through
        if (keepsTexture || Breakdown)
            texture = (TextureKind.Steady, ImmutableHashSet<TrackRole>.Empty, steady);
        // a solo plays through: a drum solo the drums alone, a pitched one its soloists and the melody, whose line it is,
        // improvised through
        if (solo is { IsDrumSolo: true })
            return (this with { Resting = [..Roles.Values.Distinct().Where(x => x != TrackRole.Drum)], PhraseSilent = steady, TextureKind = TextureKind.Alone, Solo = solo }).Appear(appearance, improvisation);
        // and the counter-melody and the pad make room for it, unless they solo
        if (solo is not null)
            return (this with { Resting = resting.Union([TrackRole.CounterMelody, TrackRole.Pad]).Except(solo.Soloists).Remove(TrackRole.Melody), PhraseSilent = steady, TextureKind = TextureKind.Steady, Solo = solo }).Appear(appearance, 1);
        return (this with { Resting = resting.Union(texture.Resting), PhraseSilent = texture.PhraseSilent, TextureKind = texture.Kind }).Appear(appearance, improvisation);
    }

    /// <summary>
    ///     The section as it plays the given time, from 0: its lines placed for that time, mutated from the first by
    ///     the song's improvisation (<see cref="SectionLine.Place" />), and every other track as it was made. A section
    ///     played once plays its lines' question alone; one played four times plays the question and the answer twice,
    ///     the second time as a further appearance, improvised as far as the song improvises.
    /// </summary>
    public GeneratedSection Appear(int appearance, double improvisation)
    {
        // the resting parts' tracks play nothing, and their lines are not placed
        var silent = Timeline.TrackTimelineMap.Keys.Where(x => Resting.Contains(Roles[x])).ToHashSet();
        var timeline = Timeline.MapTrackEvents(silent.ToDictionary(
            x => x,
            _ => (Func<EventTimeline<StateMap>, EventTimeline<StateMap>>)(notes => EventTimeline.Create<StateMap>(notes.Duration))
        ));
        foreach (var line in Lines.Where(x => !silent.Contains(x.Track)))
        {
            // a question and its answer every two plays, each pair a further appearance of the line
            var bars = Plays == 1
                ? line.Appear(appearance, improvisation).Trim(Meter.PatternDuration)
                : Enumerable.Range(0, Plays / 2).Select(pair => line.Appear(Plays / 2 * appearance + pair, improvisation)).Unroll();
            var placed = bars.TrackTimelineMap[line.Track].EventTimeline.MapValues(x => x.OfScope(StateScope.Render));
            timeline = timeline.MapTrackEvents(new Dictionary<int, Func<EventTimeline<StateMap>, EventTimeline<StateMap>>> { [line.Track] = _ => placed });
        }

        // and a phrase's silent parts play nothing in it
        foreach (var (parts, phrase) in PhraseSilent.Select((x, i) => (x, i)).Where(x => !x.x.IsEmpty))
        {
            var (from, to) = (phrase * Meter.PatternDuration, (phrase + 1) * Meter.PatternDuration);
            timeline = timeline.MapTrackEvents(Timeline.TrackTimelineMap.Keys.Where(x => parts.Contains(Roles[x])).ToDictionary(
                x => x,
                _ => (Func<EventTimeline<StateMap>, EventTimeline<StateMap>>)(notes => EventTimeline.Create(notes.Duration, notes.Where(n => n.Position < from - 1e-9 || n.Position >= to - 1e-9)))
            ));
        }

        return this with { Timeline = timeline, Lines = [..Lines.Where(x => !silent.Contains(x.Track))] };
    }
}

/// <summary>
///     A section's rhythm: how far it strays from convention, the scheme its 4-bar pattern follows, how busy its
///     melody is, and how its energy leans its draws; and how far the section strays in every facet.
/// </summary>
internal sealed record SectionRhythm(
    RhythmicUnconventionality Unconventionality,
    PhraseScheme Scheme,
    MelodyBusyness MelodyBusyness,
    Tilt Energy,
    Unconventionality Facets
);

/// <summary>
///     The random sequences of a section's decisions apart from its own, each derived from the section's seed by its
///     number, which must stay as it is: a new decision takes a new number.
/// </summary>
internal enum SectionStream
{
    Scale = 1,
    DrumPresence = 2,
    Percussion = 3,
    DrumStrokes = 4,
    DrumRoles = 5,
    Kit = 6,
    MelodyAnswer = 7,
    MelodyLeading = 8,
    MelodyImprovisation = 9,
    RegisterFreedom = 10,
    BassImprovisation = 11,
    DrumBindings = 12,
    DrumFeels = 13,
    HarmonicRhythm = 14,
    Length = 15,
    Arrangement = 16,
    CounterLeading = 17,
    CounterImprovisation = 18,
    LineScale = 19,
    TimeFeel = 20,
    Unconventionality = 21,
    Feel = 22,
    CadenceRaise = 23,
    Riff = 24,
    RiffLeading = 25,
    RiffImprovisation = 26,
    Arpeggio = 27,
    RiffLineScale = 28,
    RhythmArpeggio = 29,
    Drums = 30,
    ChordScales = 31,
    DominantChords = 32,
    Loop = 33,
    Hook = 34,
    PowerChords = 35,
    Chugs = 36,
    Turnaround = 37,
    PassingChords = 38,
    Planing = 39,
    OctaveBass = 40,
    ChordOwnScales = 41,
    Vamp = 42,
    Colour = 43,
    HeldHome = 44,
    Breakdown = 45
}
