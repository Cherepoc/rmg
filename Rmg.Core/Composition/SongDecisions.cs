using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     What the song decided before it was put together, which every stage after reads and none changes: its seed and
///     streams, its genre, parts, facets and form, its sections as they appear, their energies, solos and soloists, and
///     the song assembled.
/// </summary>
/// <param name="SectionOf">A section as generated, by its id, made once where it first plays.</param>
/// <param name="Sections">Every appearance, in the song's order.</param>
/// <param name="Solos">The appearances played as solos, by their places.</param>
/// <param name="Pinned">The parts the song pins to their figures, a genre's (<see cref="DrumRoles.DrawPinned" />).</param>
/// <param name="Repeats">Whether the last section plays once more, as a last chorus.</param>
/// <param name="Scale">The scale the song starts in.</param>
/// <param name="Sound">The effects the song plays at all, and the part playing its strong one (<see cref="Expression.DrawSound" />).</param>
/// <param name="DrumJob">What the song's drums do, keep time or mark its form, a genre's (<see cref="DrumMarks" />).</param>
internal sealed record SongDecisions(
    ulong Seed,
    Genre Genre,
    SongTracks Tracks,
    ImmutableHashSet<TrackRole> Absent,
    Unconventionality Facets,
    Meter Meter,
    SongStructure Structure,
    IReadOnlyList<int> SectionIds,
    IReadOnlyList<int> Played,
    IReadOnlyDictionary<int, Meter> SectionMeters,
    Func<int, GeneratedSection> SectionOf,
    IReadOnlyList<GeneratedSection> Sections,
    IReadOnlyDictionary<int, SoloPlan> Solos,
    IReadOnlyDictionary<TrackRole, Soloist> Soloists,
    ImmutableHashSet<RhythmPart> Pinned,
    SongAssembly Form,
    bool Repeats,
    StateMap CommonStateMap,
    Swing Swing,
    Scale Scale,
    DrumJob DrumJob,
    SongSound Sound,
    Mood Mood
)
{
    /// <summary>How loud and busy each appearance is meant to be, its section's own as it appears.</summary>
    public IReadOnlyList<double> Energies => field ??= [..Sections.Select(x => x.Energy)];

    /// <summary>
    ///     Where the song plays in stop time, its phrases' start, end and bar, as drawn, decided once for the stages before
    ///     stop time that keep to it (<see cref="Composition.StopTime" />); those after keep to what it played (<see cref="SongDraft.Stopped" />).
    /// </summary>
    public ImmutableArray<(double Start, double End, double BarDuration)> StopTime => _stopTime ??= StopTimeSkipped ? [] : [..Composition.StopTime.Phrases(this)];

    /// <summary>Whether the song is made without its stop time, as a listening test compares it, which every stage keeping to it then keeps to.</summary>
    public bool StopTimeSkipped { get; init; }

    private ImmutableArray<(double Start, double End, double BarDuration)>? _stopTime;

    /// <summary>
    ///     Where the band strikes ensemble hits, each bar's start, end and length, as drawn, decided once for the stages before
    ///     stop time that keep to it, as its own phrases (<see cref="Composition.StopTime.HitBars" />).
    /// </summary>
    public ImmutableArray<(double Start, double End, double BarDuration)> EnsembleHits => field.IsDefault ? field = StopTimeSkipped ? [] : [..Composition.StopTime.HitBars(this)] : field;

    /// <summary>
    ///     The song's first chorus where it plays and its place among the sections, which a hook intro quotes (<see
    ///     cref="IntroKind.Hook" />); none for a song without a chorus.
    /// </summary>
    public (SectionSpan Span, int Index)? FirstChorus =>
        Form.Map.Sections.Select((x, i) => (Span: x, Index: i)).Cast<(SectionSpan Span, int Index)?>().FirstOrDefault(x => Structure.Roles[x!.Value.Span.SectionId] == SectionRole.Chorus);

    /// <summary>
    ///     The section an intro before the first is made of, whose sound it plays in: the chorus a hook intro quotes, or the
    ///     first section, which an intro's entries, a count-in or a cold start lead into.
    /// </summary>
    public SectionSpan IntroSource => Form.Map.Sections[Form.Map.Intro.Source];

    /// <summary>A stage's own stream.</summary>
    public IGenerationContext Stream(SongStream stream) => SongGenerator.CreateStream(Seed, stream);

    /// <summary>A stage's own stream for each place, such as an appearance or a section.</summary>
    public IGenerationContext Stream(SongStream stream, long place) => SongGenerator.CreateStream(Seed, stream, place);

    /// <summary>A stage's own stream for each pair of places, such as a section and a track.</summary>
    public IGenerationContext Stream(SongStream stream, long place, long other) => new GenerationContext(Seeds.Derive(Seeds.Derive(Seeds.Derive(Seed, (int)stream), place), other));

    /// <summary>
    ///     A stage's own stream for each appearance, by its place among those played (<see cref="Played" />), keyed by its
    ///     section and how many times the section played before it, so that an appearance added before it leaves it as it was.
    /// </summary>
    public IGenerationContext AppearanceStream(SongStream stream, int index) => Stream(stream, AppearanceKey(Played, index));

    /// <summary>A stage's own stream for each appearance (<see cref="AppearanceStream(SongStream, int)" />) and another place in it.</summary>
    public IGenerationContext AppearanceStream(SongStream stream, int index, int other) => Stream(stream, AppearanceKey(Played, index), other);

    /// <summary>An appearance's key, by its section and how many times the section played before it.</summary>
    internal static long AppearanceKey(IReadOnlyList<int> played, int index) => Seeds.Key(played[index], played.Take(index).Count(x => x == played[index]));

    /// <summary>A facet's value as the rhythm's unconventionality, for a choice that leans by it.</summary>
    public RhythmicUnconventionality Of(Facet facet) => new(Facets[facet]);

    /// <summary>The parts the song plays, by their tracks.</summary>
    public IReadOnlyDictionary<int, TrackRole> Playing => Tracks.Definitions.Where(x => !Absent.Contains(x.Value.Role)).ToDictionary(x => x.Key, x => x.Value.Role);

    /// <summary>Every part, by its track.</summary>
    public IReadOnlyDictionary<int, TrackRole> Roles => Tracks.Definitions.ToDictionary(x => x.Key, x => x.Value.Role);

    /// <summary>A part's track, by its role.</summary>
    public KeyValuePair<int, IInstrumentTrack> Track(TrackRole role) => Tracks.Definitions.Single(x => x.Value.Role == role);

    /// <summary>The track a part draws as: the riff's for its twin, which plays as the riff does, and any other's own.</summary>
    public int DrawsAs(int track) => Tracks.Definitions[track].Role == TrackRole.RiffTwin ? Track(TrackRole.Riff).Key : track;

    /// <summary>
    ///     Everything the song of the seed decides before it is put together, every stage drawing from its own random
    ///     sequence, derived from the seed by the stage, so that a change to what one stage draws leaves the others' as they
    ///     were; what is given used in place of its draw, which is made all the same.
    /// </summary>
    /// <param name="overrides">What a test or a request sets in place of the song's own draws.</param>
    /// <param name="cancellation">Gives the song up between its sections, which draw nothing to check it.</param>
    internal static SongDecisions Draw(ulong seed, SongOverrides overrides, CancellationToken cancellation)
    {
        // every stage draws from its own random sequence, derived from the seed by the stage, so a change to what one
        // stage draws leaves what the others draw as it was
        var root = new GenerationContext(seed);
        IGenerationContext Stream(SongStream stream) => SongGenerator.CreateStream(seed, stream);
        var genre = overrides.Genre ?? Genre.None;

        // how far the song strays from convention, and every facet of it, drawn around the song's; the rhythm strays as
        // the groove does, which every rhythm layer from the tracks' own on is scaled by
        var unconventionalityBase = overrides.Base ?? Unconventionality.DrawBase(Stream(SongStream.Rhythm));
        var songUnconventionality = Unconventionality.Generate(unconventionalityBase, facet => SongGenerator.CreateStream(seed, SongStream.Unconventionality, (int)facet));
        if (overrides.Facets is { } facets)
            songUnconventionality = songUnconventionality with { Facets = songUnconventionality.Facets.SetItems(facets) };
        // its values read at its facets, a value's plain and wild ends leaning a plain and a wild song
        genre = genre.AtSong(songUnconventionality);
        StateTrace.RecordSong(TracePoints.SongUnconventionality, $"{songUnconventionality.Base:F2}", songUnconventionality);
        // its mood, either drawn within the genre's span, at the song's unconventionality, or the songs' own, each from a
        // sequence of its own, drawn when it is given too, so that the rest is as it was
        double MoodOf(GenreChoice choice, SongStream stream) =>
            SongGenerator.MoodIn(Stream(stream), genre.Range(choice, songUnconventionality.Base) ?? Mood.SongsSpan);
        var drawnMood = new Mood(MoodOf(GenreChoices.Darkness, SongStream.Darkness), MoodOf(GenreChoices.Intensity, SongStream.Intensity));
        var mood = new Mood(overrides.Darkness ?? drawnMood.Darkness, overrides.Intensity ?? drawnMood.Intensity);
        StateTrace.RecordSong(TracePoints.Mood, $"{mood.Darkness:F2} {mood.Intensity:F2}", mood);
        // its options leaned by its mood, every draw after taking them so
        genre = genre.AtMood(mood);
        var rhythmicUnconventionality = new RhythmicUnconventionality(songUnconventionality[Facet.Groove]) { GrooveHold = RhythmicUnconventionality.HoldOf(genre) };
        // a facet's value as the rhythm's unconventionality, for a choice that leans by it
        RhythmicUnconventionality Of(Facet facet) => new(songUnconventionality[facet]);
        StateTrace.RecordSong(TracePoints.SongRhythm, $"{rhythmicUnconventionality.Value:F2}", rhythmicUnconventionality);
        // the parts the song leaves out, the pad, the counter-melody and the drums now and then, each from a sequence
        // of its own, and any given in or out
        var absent = SongParts.DrawAbsent(part => SongGenerator.CreateStream(seed, SongStream.Parts, (int)part), overrides.Parts, songUnconventionality, genre);
        StateTrace.RecordSong(TracePoints.SongParts, string.Join(", ", absent.Order()), absent);
        // the parts the song pins to their figures, a genre's, each from a sequence of its own
        var pinned = DrumRoles.DrawPinned(part => SongGenerator.CreateStream(seed, SongStream.Pins, (int)part), songUnconventionality[Facet.Groove], genre);
        StateTrace.RecordSong(TracePoints.PinnedDrums, string.Join(", ", pinned.Order()), pinned);
        var tracks = SongTracks.Create(
            Stream(SongStream.Drums),
            Stream(SongStream.Panning),
            role => SongGenerator.CreateStream(seed, SongStream.PartTracks, (int)role),
            absent,
            rhythmicUnconventionality,
            track => SongGenerator.CreateStream(seed, SongStream.Drums, track),
            track => SongGenerator.CreateStream(seed, SongStream.DrumStrokes, track),
            track => SongGenerator.CreateStream(seed, SongStream.DrumRoles, track),
            Stream(SongStream.DrumGroup),
            // the drums given make the setup, which the song still draws its own drums by, so that the rest is as it was
            overrides.DrumPieces is { } pieces ? DrumPieces.SetupOf(pieces) : overrides.DrumSetup ?? DrumSetups.Pick(Stream(SongStream.DrumSetup), rhythmicUnconventionality.Value, genre),
            overrides.DrumPieces is { } given ? DrumPieces.Of(given) : null,
            genre,
            part => SongGenerator.CreateStream(seed, SongStream.PartFeel, (int)part),
            songUnconventionality[Facet.Feel],
            songUnconventionality[Facet.Sound],
            pinned
        );
        StateTrace.RecordSong(TracePoints.DrumSetup, tracks.DrumSetup.ToString(), tracks.DrumSetup);

        // the song's chords gather around its unconventionality, and a section's around its own shift of it
        var harmonyContext = Stream(SongStream.Harmony);
        var unconventionality = new HarmonicUnconventionality(songUnconventionality[Facet.Chords], genre);
        // drawn when it is given too, so that the rest is as it was
        var scale = Scales.Pick(harmonyContext, songUnconventionality[Facet.Scale], genre);
        scale = overrides.Scale ?? scale;
        StateTrace.RecordSong(TracePoints.SongHarmony, $"{unconventionality.Chords:F2} {scale.Name}", (unconventionality, scale));
        // the feel the song plays in, straight or a tuplet, from a sequence of its own, by its feel facet
        var feel = Feels.DrawSong(Stream(SongStream.Feel), songUnconventionality[Facet.Feel], genre);
        StateTrace.RecordSong(TracePoints.Feel, $"{feel}", feel);
        var songStateMap = CreateSongStateMap(Stream(SongStream.SongState), unconventionality, rhythmicUnconventionality)
            .MergeWith(Feels.At(StateDepths.Song, feel));
        // how busy the melody is, which a section moves
        var melodyBusyness = GenreRhythm.Of(genre, MelodyBusyness.Generate(Stream(SongStream.Melody)));

        // the song's form: one of the forms songs are written in, its sections playing their roles, or one of its own
        // the meter the song's bars are in
        var meter = overrides.Meter ?? Meter.Draw(Stream(SongStream.Meter), songUnconventionality[Facet.Feel], overrides.MeterOption, genre);
        StateTrace.RecordSong(TracePoints.Meter, meter.ToString(), meter);

        // the song's key and tempo, drawn before its form, which how long the song plays takes after
        // the band of a genre's tempo range the song plays in, where the genre weighs them, from a sequence of its own
        TempoBand? band = genre.Pool<TempoBand>(GenreChoices.TempoBand) is { } bands
            ? Stream(SongStream.TempoBand).Pick(ByConvention.Weigh(SongGenerator.TempoBands, songUnconventionality[Facet.Feel], bands))
            : null;
        var commonStateMap = CreateCommonStateMap(Stream(SongStream.Common), songUnconventionality[Facet.Feel], overrides, genre, band);
        // a length given leans the form to it, by how long an appearance of each role plays on the average at the tempo
        var tempo = commonStateMap.GetStateValue(StateKinds.Tempo);
        double SecondsOf(double beats) => beats * Meter.SecondsPerBeat(tempo);
        // a length given, or one in a genre's range at the song's form, drawn from a stream of its own
        var target = overrides.Length
                     ?? (genre.Range(GenreChoices.Length, songUnconventionality[Facet.Form]) is { } lengths ? SongGenerator.LengthIn(Stream(SongStream.GenreLength), lengths) : null);
        var length = target is { } seconds
            ? new SongLength(seconds, (role, lean) => SecondsOf(SectionLength.Expected(songUnconventionality[Facet.Form], role, lean, genre) * meter.PatternDuration))
            : null;
        var structure = SongForms.Generate(Stream(SongStream.SongForm), Stream(SongStream.Structure), Stream(SongStream.SongEnds), songUnconventionality[Facet.Form], length, genre);
        StateTrace.RecordSong(TracePoints.SongForm, string.Join(" ", structure.SectionIds.Select(x => structure.Roles[x])), structure);
        var sectionIds = structure.SectionIds.ToArray();

        // how loud and busy each section is meant to be, from the song's and where and how often the section plays
        var dynamicsContext = Stream(SongStream.Dynamics);
        songStateMap = songStateMap.MergeWith(SectionEnergy.GenerateSong(dynamicsContext, genre));
        var sectionEnergies = SectionEnergy.GenerateSections(dynamicsContext, sectionIds, SectionEnergy.ContrastOf(genre));

        // how the song swings, which its tempo sets the notes of
        var swing = Groove.Generate(Stream(SongStream.Groove), commonStateMap.GetStateValue(StateKinds.Tempo), songUnconventionality[Facet.Feel], meter, overrides.Swing, genre);
        StateTrace.RecordSong(TracePoints.Swing, $"{swing.Delay:F3} of {swing.Period}", swing);

        // what the drums do, decided before the sections, whose texture leaves no section to drums that only mark the form
        var drumJob = DrumMarks.Draw(Stream(SongStream.DrumJob), songUnconventionality[Facet.Groove], genre);
        StateTrace.RecordSong(TracePoints.DrumJob, drumJob.ToString(), drumJob);

        var sectionGenerator = new SectionGenerator(
            root,
            Seeds.Derive(seed, (int)SongStream.Sections),
            tracks,
            songUnconventionality,
            melodyBusyness,
            scale,
            songStateMap,
            sectionEnergies,
            PercussionSections.GenerateSong(Stream(SongStream.Percussion)),
            commonStateMap.GetStateValue(StateKinds.KeyOffset),
            absent,
            drumJob,
            genre
        );
        // how the song starts and ends around its sections, decided before them: the one the song ends with leads home
        // to the tonic, where the ending lands
        var formGenerator = new SongFormGenerator(
            Stream(SongStream.Form),
            Stream(SongStream.Intro),
            Stream(SongStream.Drops),
            Stream(SongStream.DropKind),
            Stream(SongStream.OpeningAlone),
            genre,
            Of(Facet.Form),
            tracks.Definitions.ToDictionary(x => x.Key, x => x.Value.Role),
            !absent.Contains(TrackRole.Drum),
            drumJob == DrumJob.Groove,
            Stream(SongStream.HookIntro),
            SongParts.All.Except(absent).ToHashSet()
        );
        var plan = formGenerator.Plan(sectionIds);
        // a section in a meter of its own now and then, a chorus the likelier, each drawing from a stream of its own
        var sectionMeters = SectionMeter.Generate(
            id => SongGenerator.CreateStream(seed, SongStream.SectionMeter, id),
            meter,
            songUnconventionality[Facet.Feel],
            sectionIds,
            structure.Roles,
            genre
        );
        // a section's pattern several phrases where a genre has longer progressions, each section's from a stream of its own
        if (genre.Pool<int>(GenreChoices.Phrases, GenreChoices.PhrasesName) is { } phrasePool)
            sectionMeters = sectionMeters.ToImmutableDictionary(x => x.Key, x => x.Value with
            {
                Phrases = SongGenerator.CreateStream(seed, SongStream.SectionPhrases, x.Key)
                    .Pick(ByConvention.Weigh(GenreChoices.PhraseWeights, songUnconventionality[Facet.Form], phrasePool))
            });
        // a section's phrase of three bars or of four and two now and then, each from a stream of its own
        sectionMeters = sectionMeters.ToImmutableDictionary(x => x.Key, x => x.Value with
        {
            PhraseBars = PhraseLengths.Draw(SongGenerator.CreateStream(seed, SongStream.SectionPhraseBars, x.Key), songUnconventionality[Facet.Form], genre, x.Value.Phrases)
        });
        StateTrace.RecordSong(TracePoints.SectionMeter, string.Join(", ", sectionMeters.Select(x => $"{x.Key} {x.Value}")), sectionMeters);
        // a hook intro where the song has a chorus to quote, and where it has none another of the genre's intros
        var hook = SongFormGenerator.HookOf(sectionIds, structure.Roles, sectionMeters);
        if (plan.Intro == IntroKind.Hook && hook is null)
            plan = formGenerator.WithoutHook(plan);
        // an intro's entries over the first section's first phrase, as long as its phrase is, one taken in a hook's place too
        if (plan.Intro == IntroKind.Entries && !plan.Window.IsBefore)
            plan = plan with { Window = plan.Window with { Bars = sectionMeters[sectionIds[0]].PhraseBars } };
        StateTrace.RecordSong(TracePoints.OpeningAlone, $"{plan.First?.ToString() ?? "none"}", plan.First);

        // every section's drums, drawn before the sections are made, every drum of the song's groove in one of them at
        // least, from a stream of its own, where the song plays drums
        var sectionDrums = sectionIds.Distinct().ToImmutableDictionary(id => id, id => sectionGenerator.DrawDrums(id, structure.Roles[id]));
        if (!absent.Contains(TrackRole.Drum))
            sectionDrums = DrumCoverage.Cover(
                Stream(SongStream.DrumCoverage),
                tracks.SongDrums,
                sectionDrums,
                sectionIds.CountBy(x => x).ToDictionary(),
                id => sectionGenerator.DrawDrums(id, structure.Roles[id], isPercussionOnly: false)
            );
        // how many times more a song that fades or strips plays its last section, by how often the section plays its pattern
        static int Fading(EndingKind ending, int plays) => ending is not (EndingKind.Fade or EndingKind.Strip) ? 0 : plays == 1 ? 2 : 1;
        bool Repeats(bool drawn, int fades) =>
            drawn && fades == 0 && sectionIds.Take(sectionIds.Length - 1).Contains(sectionIds[^1]) && structure.Roles[sectionIds[^1]] != SectionRole.Outro;
        // a last section that came back before played once more now and then, drawn here, which the length takes in
        var lastRepeatDrawn = Stream(SongStream.LastRepeat).TestProbability(SongForms.LastRepeatChance.At(songUnconventionality[Facet.Form], genre.Chance(GenreChoices.LastRepeat, "songs")));
        // a length given leans every section's plays alike, the lean whose song comes nearest it, the ending's plays too
        var playsLean = length?.NearestLean(lean =>
        {
            var plays = sectionIds.Distinct().ToDictionary(id => id, id => sectionGenerator.DrawPlays(id, structure.Roles[id], lean, sectionMeters[id].Phrases));
            var fades = Fading(plan.Ending, plays[sectionIds[^1]]);
            var appearances = sectionIds.Length + (Repeats(lastRepeatDrawn, fades) ? 1 : 0);
            // and the intro's bars before the first section
            return SecondsOf(sectionIds.Sum(id => plays[id] * sectionMeters[id].PatternDuration)
                             + (appearances - sectionIds.Length + fades) * plays[sectionIds[^1]] * sectionMeters[sectionIds[^1]].PatternDuration
                             + SongFormGenerator.IntroDuration(plan, sectionMeters[sectionIds[0]], hook is { } quoted ? sectionMeters[quoted] : null));
        }) ?? 0;
        // every section is generated once, where it first plays
        var generateSection = ((Func<int, GeneratedSection>)(id => sectionGenerator.Generate(
                new SectionPlan(id, id == plan.TonicHomeSectionId, id == sectionIds[0], structure.Roles[id], sectionMeters[id], sectionDrums[id], playsLean))))
            .CacheGeneratedValues();
        // the first made first, whose harmony a section of a loop plays (SectionGenerator.Loop); every section draws from
        // sequences of its own, so the order they are made in changes none of them
        generateSection(sectionIds[0]);
        // and its melody placed afresh every time it plays, varied from the first as far as the song improvises
        var improvisation = MelodyLayers.GenerateImprovisation(Stream(SongStream.MelodyImprovisation), songUnconventionality[Facet.Melody]);
        StateTrace.RecordSection(TracePoints.MelodyImprovisation, SongTracks.MelodyTrack, 0, $"{improvisation:F2}", improvisation);
        // a song that fades out plays its last section once more, over which it fades, twice where it plays its pattern
        // only once, so that the fade takes eight bars at least
        var fading = Fading(plan.Ending, generateSection(sectionIds[^1]).Plays);
        // and a last section that came back before played once more now and then, as a last chorus is, where it does not
        // fade, from a stream of its own, the last section's lift moving to it
        var repeats = Repeats(lastRepeatDrawn, fading);
        int[] played = [..sectionIds, ..(repeats ? [sectionIds[^1]] : Array.Empty<int>()), ..Enumerable.Repeat(sectionIds[^1], fading)];
        StateTrace.RecordSong(TracePoints.LastRepeat, $"{repeats}", repeats);
        // and its first pass a breakdown now and then, a genre's, from a stream of its own
        var breakdownChorus = Stream(SongStream.BreakdownChorus).TestProbability(SongForms.BreakdownChorusChance.At(songUnconventionality[Facet.Form], genre.Chance(GenreChoices.LastRepeat, "breakdown")))
                              && repeats;
        StateTrace.RecordSong(TracePoints.BreakdownChorus, $"{breakdownChorus}", breakdownChorus);
        // the later in the song, the more, and with the energy of its own place in the song against the section's average,
        // its parts only joining as it comes back
        double Place(int index) => played.Length > 1 ? index / (double)(played.Length - 1) : 0.5;
        var averagePlaces = played.Select((id, index) => (id, index)).GroupBy(x => x.id).ToDictionary(x => x.Key, x => x.Average(y => Place(y.index)));
        var restedBefore = new Dictionary<int, ImmutableHashSet<TrackRole>>();
        var solos = new Dictionary<int, SoloPlan>();
        var sections = new List<GeneratedSection>();
        // how loud and busy each appearance is meant to be: its section's energy, stepped by its place in the song
        var energySteps = played.Select((id, index) => SectionEnergy.AppearanceStep(Place(index), averagePlaces[id], SectionEnergy.ContrastOf(genre))).ToArray();
        List<double> energies = [..played.Select((id, index) => generateSection(id).Energy + energySteps[index])];
        // the parts held back at the song's start, each entering at a later section, from a stream of their own
        var lateEntries = LateEntries.Generate(
            Stream(SongStream.LateEntries),
            played,
            [..energies],
            songUnconventionality[Facet.Form],
            absent
        );
        // but the part the song opens on alone, which plays from the start, its draws made all the same
        if (plan.First is { } opening)
            lateEntries = lateEntries.Remove(opening);
        StateTrace.RecordSong(TracePoints.LateEntries, string.Join(", ", lateEntries.Select(x => $"{x.Key} at {x.Value}")), lateEntries);
        // every appearance's parts, a part held back yet soloing no sooner than it comes in
        IReadOnlyCollection<TrackRole>[] partsAt = [..played.Select((_, index) => SongParts.All.Except(absent).Except(lateEntries.Where(x => index < x.Value).Select(x => x.Key)).ToArray())];
        // no solo over a breakdown, whose band rests to its chugs, nor over a calm section, whose guitars rest to its broken
        // chords, nor over a breakdown chorus, built up from the voice
        bool PlaysNoSolo(int index) => generateSection(played[index]) is { Breakdown: true } or { Form.Kind: SectionFormKind.Calm } || breakdownChorus && index == sectionIds.Length - 1;
        // a later appearance played as a solo now and then, each from a sequence of its own, by the section's facets, and
        // one the soloist's where the song has it, in an appearance that plays one
        var drawnSolos = Composition.Solos.WithSoloist(
            Stream(SongStream.SoloistSolo),
            [..played.Select((id, index) => Composition.Solos.Draw(SongGenerator.CreateStream(seed, SongStream.Solos, AppearanceKey(played, index)), index, structure.Roles[id], generateSection(id).Facets, partsAt[index], genre))],
            [..played.Select(id => structure.Roles[id])],
            [..played.Select(id => generateSection(id).Facets[Facet.Melody])],
            [..SongParts.All.Except(absent)],
            [..played.Select((_, index) => PlaysNoSolo(index))]
        );
        // and none drawn where it plays none, the appearance played as it is, its draws made all the same
        drawnSolos = [..drawnSolos.Select((x, index) => PlaysNoSolo(index) ? null : x)];
        // a pitched solo trading with the drums now and then, each from a sequence of its own
        drawnSolos = [..drawnSolos.Select((x, index) => Composition.Solos.WithTrade(SongGenerator.CreateStream(seed, SongStream.SoloTrades, AppearanceKey(played, index)), x,
            generateSection(played[index]).Facets[Facet.Melody], partsAt[index], genre))];
        // and played by two parts together a third apart now and then, each from a sequence of its own
        drawnSolos = [..drawnSolos.Select((x, index) => Composition.Solos.WithTwin(SongGenerator.CreateStream(seed, SongStream.SoloTwins, AppearanceKey(played, index)), x,
            generateSection(played[index]).Facets[Facet.Melody], partsAt[index], genre))];
        // drums that only mark the form neither solo nor trade, as they play no groove to solo from, the appearance played as
        // it is, or by its pitched soloists alone
        if (drumJob != DrumJob.Groove)
            drawnSolos = [..drawnSolos.Select(x => x is { IsDrumSolo: true } ? null : x is { TradesWithDrums: true } ? x with { Soloists = [..x.Soloists.Where(s => s != TrackRole.Drum)] } : x)];
        for (var index = 0; index < played.Length; index++)
        {
            cancellation.ThrowIfCancellationRequested();
            var id = played[index];
            var solo = drawnSolos[index];
            var energyStep = energySteps[index];
            var section = generateSection(id).Appear(
                played.Take(index).Count(x => x == id),
                Tilt.Of(MelodyLayers.ImprovisationGrowth, Place(index)).Chance(improvisation, 1),
                energyStep,
                restedBefore.GetValueOrDefault(id, []),
                // an intro of entries builds the first section's parts up itself
                index == 0 && plan.Intro == IntroKind.Entries,
                solo,
                [..lateEntries.Where(x => index < x.Value).Select(x => x.Key)],
                // and the part the song opens on alone in the first
                [..lateEntries.Where(x => index == x.Value).Select(x => x.Key), ..index == 0 && plan.First is { } first ? [first] : Array.Empty<TrackRole>()],
                breakdownChorus && index == sectionIds.Length - 1
            );
            // as loud as its energy's step from the section's, by the lift's measure of loudness, so that its last chorus
            // plays the louder, and its energy the appearance's, which the fills and the lifts into it read
            section = section with
            {
                Timeline = section.Timeline.MergeStateMap(StateMap.FromStates([StateKinds.Velocity.CreateState(FormLayers.LiftVelocity * energyStep)])),
                Energy = section.Energy + energyStep
            };
            if (solo is not null)
            {
                solos[index] = solo;
                StateTrace.RecordSection(TracePoints.Solo, SectionGenerator.SectionTrace, id, $"{string.Join(", ", solo.Soloists)} {solo.Mode}", (index, solo));
            }
            restedBefore[id] = section.Resting;
            StateTrace.RecordSection(TracePoints.Arrangement, SectionGenerator.SectionTrace, id, string.Join(", ", section.Resting.Order()), section.Resting);
            StateTrace.RecordSection(TracePoints.Texture, SectionGenerator.SectionTrace, id, $"{section.TextureKind}", (index, section.TextureKind, section.PhraseSilent));
            sections.Add(section);
        }

        // every part that solos a soloist of its own, whose solos sound like one player's, and the solo part wherever it plays,
        // whose answers are in its solos' vocabulary, each from a sequence of its own, by the melody facet, and the drummer by
        // the fills facet
        var soloing = solos.Values.SelectMany(x => x.Soloists);
        // and the riff where its twin leads a section, played as the riff's solos are
        if (sections.Any(x => x.TwinLead))
            soloing = soloing.Append(TrackRole.Riff);
        if (!absent.Contains(TrackRole.Solo) && tracks.Definitions.Values.Any(x => x.Role == TrackRole.Solo))
            soloing = soloing.Append(TrackRole.Solo);
        var soloists = soloing.Distinct().ToDictionary(
            role => role,
            role => Soloist.Generate(
                SongGenerator.CreateStream(seed, SongStream.Soloist, (int)role),
                songUnconventionality[role == TrackRole.Drum ? Facet.Fills : Facet.Melody],
                songUnconventionality[Facet.Sound],
                role == TrackRole.Drum ? Soloist.DrumsProgram : tracks.Definitions.Values.OfType<PitchInstrumentTrack>().Single(x => x.Role == role).InstrumentCode));
        foreach (var (role, soloist) in soloists)
            StateTrace.RecordSong(TracePoints.Soloist, $"{role}", (role, soloist));

        // the song put together as planned, and the lines the drums mark, then every stage after it in its order
        cancellation.ThrowIfCancellationRequested();
        // a section's last bar cut short before the next now and then, each from a stream of its own
        var shortBars = ShortBars.Draw(id => SongGenerator.CreateStream(seed, SongStream.ShortBars, id), sectionIds.Distinct(), songUnconventionality[Facet.Form], genre);
        StateTrace.RecordSong(TracePoints.ShortBars, string.Join(", ", shortBars.Order()), shortBars);
        var form = formGenerator.Assemble(plan, played, sections, shortBars);
        return new SongDecisions(
            seed, genre, tracks, absent, songUnconventionality, meter, structure, sectionIds, played, sectionMeters, generateSection, sections,
            solos, soloists, pinned, form, repeats, commonStateMap, swing, scale, drumJob,
            // the song's effects, decided before its notes, the part playing its strong one among those it plays
            Expression.DrawSound(Stream(SongStream.Effects), songUnconventionality[Facet.Sound], genre, tracks.Definitions,
                tracks.Definitions.Where(x => !absent.Contains(x.Value.Role)).Select(x => x.Key).ToHashSet(),
                effect => SongGenerator.CreateStream(seed, SongStream.StrongEffects, (int)effect)), mood);
    }

    /// <summary>
    ///     The song's layer of the generation state, which every section starts from: its rhythm, its note walk, and
    ///     the first entries of the chord pool, which the song's chords make the likeliest.
    /// </summary>
    private static StateMap CreateSongStateMap(
        IGenerationContext context,
        HarmonicUnconventionality unconventionality,
        RhythmicUnconventionality rhythmicUnconventionality
    )
    {
        return new StateMapBuilder(Layers.Song, perTrack: false)
            .AddRhythmLayer(rhythmicUnconventionality.Lean(RhythmLayers.Song))
            .Add(CompositionStateKinds.Rhythm.MaxRank, 2)
            .Add(CompositionStateKinds.Rhythm.Fullness, RhythmSettings.Fullness)
            .Add(CompositionStateKinds.Rhythm.Variation, RhythmSettings.Variation)
            .AddNoteWalkLayer()
            .Add(CompositionStateKinds.ChordPool.Collection, LayerStates.CreateChordPool(unconventionality))
            .Add(CompositionStateKinds.ChordPool.Index, LayerStates.ChordPoolIndex)
            .ToStateMap(context);
    }

    /// <summary>The song's key and tempo, drawn, and replaced where given, so that giving one leaves every other draw as it was.</summary>
    /// <param name="feel">The feel facet of the song's unconventionality, by which a genre's range of tempos is eased between its ends.</param>
    /// <param name="band">The third of the genre's tempo range the song plays in; none for the whole of it.</param>
    private static StateMap CreateCommonStateMap(IGenerationContext context, double feel, SongOverrides overrides, Genre genre, TempoBand? band)
    {
        var drawn = new StateMapBuilder(Layers.Song, perTrack: false)
            .Add(StateKinds.KeyOffset, Generators.Int(0, 12))
            // the dyadic draw of the tempo, or, where a genre gives a range of it, a tempo in the range at the song's feel,
            // leaning to its middle, or to an end as the genre has it
            .Add(StateKinds.Tempo, genre.Range(GenreChoices.Tempo, feel) is { } range ? c => SongGenerator.TempoIn(c, band is { } b ? SongGenerator.InBand(range, b) : range) : SongGenerator.TempoGenerator)
            .AddNoteDurationLayer()
            .ToStateMap(context);
        if (overrides.Key is { } key)
            drawn = drawn.With(StateKinds.KeyOffset, key);
        return overrides.Tempo is { } tempo ? drawn.With(StateKinds.Tempo, tempo) : drawn;
    }

}
