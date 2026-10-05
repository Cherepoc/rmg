using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     Generates songs, in stages that each draw from the song's seed in turn: the tracks and their instruments
///     (<see cref="SongTracks" />), the song's harmony and state, its structure of parts and sections, and every
///     section (<see cref="SectionGenerator" />) with its bar state (<see cref="BarStateGenerator" />) and its tracks'
///     patterns (<see cref="PatternGenerator" />). A section that comes back is played as it was first generated.
/// </summary>
public static class SongGenerator
{
    // the song's speed as a multiple of 120 BPM, from 90 to about 175 BPM: 120 itself is the likeliest, and the finer
    // a multiplier's fraction, the less likely it is
    private static readonly Func<IGenerationContext, double> TempoGenerator = Generators.DyadicMultiplier(
        WeightUtil.CreateGeometricRankWeightFunc(0, 0, 1.0, 0.75),
        4,
        0.75,
        1.5
    );

    public static Song GenerateSong()
    {
        return GenerateSong(Seeds.Random());
    }

    /// <summary>
    ///     Generates a song. The same seed always results in the same song in this version; changes to the generator
    ///     may turn a seed into a different song, which is accepted to keep the generator simple.
    /// </summary>
    public static Song GenerateSong(ulong seed)
    {
        return GenerateSong(seed, ProgressionSettings.Default);
    }

    /// <summary>
    ///     Generates a song with what is given used as given, such as its unconventionality's base, from 0 for the
    ///     plainest to 1 for the wildest, or the parts it has, and the rest drawn as <see cref="GenerateSong(int)" />
    ///     draws it.
    /// </summary>
    public static Song GenerateSong(ulong seed, SongOverrides overrides) => GenerateSong(seed, overrides, CancellationToken.None);

    /// <summary>
    ///     Generates a song as <see cref="GenerateSong(ulong, SongOverrides)" /> does, given up at the next stage once
    ///     nobody waits for it any more.
    /// </summary>
    public static Song GenerateSong(ulong seed, SongOverrides overrides, CancellationToken cancellation)
    {
        if (overrides.Base is < 0 or > 1 or double.NaN)
            throw new ArgumentOutOfRangeException(nameof(overrides), overrides.Base, "An unconventionality is from 0 to 1.");
        if (overrides.Facets?.Values.Any(x => x is < 0 or > 1 or double.NaN) == true)
            throw new ArgumentOutOfRangeException(nameof(overrides), "A facet is from 0 to 1.");
        if (overrides.MeterOption is < 0 || overrides.MeterOption >= Meter.Options.Length)
            throw new ArgumentOutOfRangeException(nameof(overrides), overrides.MeterOption, $"A meter is one of the {Meter.Options.Length} options.");
        if (overrides.Tempo is <= 0 or double.NaN or double.PositiveInfinity)
            throw new ArgumentOutOfRangeException(nameof(overrides), overrides.Tempo, "A tempo is a positive multiple of the base tempo.");
        if (overrides.Key is < 0 or > 11)
            throw new ArgumentOutOfRangeException(nameof(overrides), overrides.Key, "A key is from 0 to 11.");
        if (overrides.Swing is < 0 or > 1 or double.NaN)
            throw new ArgumentOutOfRangeException(nameof(overrides), overrides.Swing, "A swing is from 0 to 1.");
        if (overrides.Scale is { } given && !Scales.All.Contains(given))
            throw new ArgumentOutOfRangeException(nameof(overrides), given.Name, "A scale is one of the scales.");
        if (overrides.Genre?.Choices.Any(x => x.Value.Values.Any(v => new[] { v.Plain, v.Tuned, v.Wild }.Any(a => a is < 0 or > 1 or double.NaN))) == true)
            throw new ArgumentOutOfRangeException(nameof(overrides), "A genre's amounts are from 0 to 1.");
        if (overrides.DrumPieces is { } pieces && (pieces.IsEmpty || !pieces.All(DrumPieces.IsPiece)))
            throw new ArgumentOutOfRangeException(nameof(overrides), "Drums given are one or more of the drum pieces.");

        return GenerateSong(seed, ProgressionSettings.Default, overrides, cancellation);
    }

    internal static Song GenerateSong(ulong seed, ProgressionSettings progressionSettings) => GenerateSong(seed, progressionSettings, SongOverrides.None, CancellationToken.None);

    /// <param name="overrides">What a test sets in place of the song's own draws.</param>
    /// <param name="cancellation">Gives the song up between its stages, which draw nothing to check it.</param>
    internal static Song GenerateSong(ulong seed, ProgressionSettings progressionSettings, SongOverrides overrides, CancellationToken cancellation) =>
        GenerateSong(seed, progressionSettings, overrides, ImmutableHashSet<string>.Empty, (_, _) => { }, cancellation);

    /// <summary>
    ///     The song of the seed, the stages given left out (<see cref="SongStages" />), as a test measures what one does, and
    ///     the song after each stage observed, as a test checks it.
    /// </summary>
    internal static Song GenerateSong(ulong seed, ProgressionSettings progressionSettings, SongOverrides overrides, IReadOnlySet<string> skipped,
        Action<string, SongDraft> observe, CancellationToken cancellation)
    {
        // every stage draws from its own random sequence, derived from the seed by the stage, so a change to what one
        // stage draws leaves what the others draw as it was
        var root = new GenerationContext(seed);
        IGenerationContext Stream(SongStream stream) => CreateStream(seed, stream);
        var genre = overrides.Genre ?? Genre.None;

        // how far the song strays from convention, and every facet of it, drawn around the song's; the rhythm strays as
        // the groove does, which every rhythm layer from the tracks' own on is scaled by
        var unconventionalityBase = overrides.Base ?? Unconventionality.DrawBase(Stream(SongStream.Rhythm));
        var songUnconventionality = Unconventionality.Generate(unconventionalityBase, facet => CreateStream(seed, SongStream.Unconventionality, facet));
        if (overrides.Facets is { } facets)
            songUnconventionality = songUnconventionality with { Facets = songUnconventionality.Facets.SetItems(facets) };
        StateTrace.Record(TracePoints.SongUnconventionality, FillGenerator.DrumsTrace, 0, 0, StateMap.Default, 0, $"{songUnconventionality.Base:F2}", songUnconventionality);
        var rhythmicUnconventionality = new RhythmicUnconventionality(songUnconventionality[Facet.Groove]) { GrooveHold = RhythmicUnconventionality.HoldOf(genre) };
        // a facet's value as the rhythm's unconventionality, for a choice that leans by it
        RhythmicUnconventionality Of(Facet facet) => new(songUnconventionality[facet]);
        StateTrace.Record(TracePoints.SongRhythm, FillGenerator.DrumsTrace, 0, 0, StateMap.Default, 0, $"{rhythmicUnconventionality.Value:F2}", rhythmicUnconventionality);
        // the parts the song leaves out, the pad, the counter-melody and the drums now and then, each from a sequence
        // of its own, and any given in or out
        var absent = SongParts.DrawAbsent(part => new GenerationContext(Seeds.Derive(Seeds.Derive(seed, (int)SongStream.Parts), (int)part)), overrides.Parts, songUnconventionality, genre);
        StateTrace.Record(TracePoints.SongParts, FillGenerator.DrumsTrace, 0, 0, StateMap.Default, 0, string.Join(", ", absent.Order()), absent);
        var tracks = SongTracks.Create(
            Stream(SongStream.Tracks),
            Stream(SongStream.Panning),
            Stream(SongStream.Pad),
            Stream(SongStream.CounterMelody),
            Stream(SongStream.Riff),
            Stream(SongStream.RhythmPart),
            Stream(SongStream.SoloPart),
            !absent.Contains(TrackRole.RiffTwin),
            rhythmicUnconventionality,
            Stream(SongStream.DrumStrokes),
            Stream(SongStream.DrumRoles),
            // the drums given make the setup, which the song still draws its own drums by, so that the rest is as it was
            overrides.DrumPieces is { } pieces ? DrumPieces.SetupOf(pieces) : overrides.DrumSetup ?? DrumSetups.Pick(Stream(SongStream.DrumSetup), rhythmicUnconventionality.Value, genre),
            overrides.DrumPieces is { } given ? DrumPieces.Of(given) : null,
            genre,
            part => new GenerationContext(Seeds.Derive(Seeds.Derive(seed, (int)SongStream.PartFeel), (int)part)),
            songUnconventionality[Facet.Feel],
            songUnconventionality[Facet.Sound]
        );
        StateTrace.Record(TracePoints.DrumSetup, FillGenerator.DrumsTrace, 0, 0, StateMap.Default, 0, tracks.DrumSetup.ToString(), tracks.DrumSetup);

        // the song's chords gather around its unconventionality, and a section's around its own shift of it
        var harmonyContext = Stream(SongStream.Harmony);
        var unconventionality = new HarmonicUnconventionality(songUnconventionality[Facet.Chords], genre);
        // drawn when it is given too, so that the rest is as it was
        var scale = Scales.Pick(harmonyContext, songUnconventionality[Facet.Scale], genre);
        scale = overrides.Scale ?? scale;
        StateTrace.Record(TracePoints.SongHarmony, SongTracks.ChordsTrack, 0, 0, StateMap.Default, 0, $"{unconventionality.Chords:F2} {scale.Name}", (unconventionality, scale));
        // the feel the song plays in, straight or a tuplet, from a sequence of its own, by its feel facet
        var feel = Feels.DrawSong(Stream(SongStream.Feel), songUnconventionality[Facet.Feel], genre);
        StateTrace.Record(TracePoints.Feel, FillGenerator.DrumsTrace, 0, 0, StateMap.Default, 0, $"{feel}", feel);
        var songStateMap = CreateSongStateMap(Stream(SongStream.SongState), unconventionality, rhythmicUnconventionality)
            .MergeWith(Feels.At(StateDepths.Song, feel));
        // how busy the melody is, which a section moves
        var melodyBusyness = GenreRhythm.Of(genre, MelodyBusyness.Generate(Stream(SongStream.Melody)));

        // the song's form: one of the forms songs are written in, its sections playing their roles, or one of its own
        // the meter the song's bars are in
        var meter = overrides.Meter ?? Meter.Draw(Stream(SongStream.Meter), songUnconventionality[Facet.Feel], overrides.MeterOption, genre);
        StateTrace.Record(TracePoints.Meter, FillGenerator.DrumsTrace, 0, 0, StateMap.Default, 0, meter.ToString(), meter);

        // the song's key and tempo, drawn before its form, which how long the song plays takes after
        var commonStateMap = CreateCommonStateMap(Stream(SongStream.Common), songUnconventionality[Facet.Feel], overrides, genre);
        // a length given leans the form to it, by how long an appearance of each role plays on the average at the tempo
        var tempo = commonStateMap.GetStateValue(StateKinds.Tempo);
        double SecondsOf(double beats) => beats * 60 / (Meter.BaseTempo * tempo);
        // a length given, or one in a genre's range at the song's form, drawn from a stream of its own
        var target = overrides.Length
                     ?? (genre.Range(GenreChoices.Length, songUnconventionality[Facet.Form]) is { } lengths ? LengthIn(Stream(SongStream.GenreLength), lengths) : null);
        var length = target is { } seconds
            ? new SongLength(seconds, (role, lean) => SecondsOf(SectionLength.Expected(songUnconventionality[Facet.Form], role, lean) * meter.PatternDuration))
            : null;
        var structure = SongForms.Generate(Stream(SongStream.SongForm), Stream(SongStream.Structure), Stream(SongStream.SongEnds), songUnconventionality[Facet.Form], length, genre);
        StateTrace.Record(TracePoints.SongForm, FillGenerator.DrumsTrace, 0, 0, StateMap.Default, 0, string.Join(" ", structure.SectionIds.Select(x => structure.Roles[x])), structure);
        var sectionIds = structure.SectionIds.ToArray();

        // how loud and busy each section is meant to be, from the song's and where and how often the section plays
        var dynamicsContext = Stream(SongStream.Dynamics);
        songStateMap = songStateMap.MergeWith(SectionEnergy.GenerateSong(dynamicsContext, genre));
        var sectionEnergies = SectionEnergy.GenerateSections(dynamicsContext, sectionIds, SectionEnergy.ContrastOf(genre));

        // how the song swings, which its tempo sets the notes of
        var grooveContext = Stream(SongStream.Groove);
        var swing = Groove.Generate(grooveContext, commonStateMap.GetStateValue(StateKinds.Tempo), songUnconventionality[Facet.Feel], meter, overrides.Swing, genre);
        StateTrace.Record(TracePoints.Swing, FillGenerator.DrumsTrace, 0, 0, StateMap.Default, 0, $"{swing.Delay:F3} of {swing.Period}", swing);

        var sectionGenerator = new SectionGenerator(
            root,
            Seeds.Derive(seed, (int)SongStream.Sections),
            progressionSettings,
            tracks,
            songUnconventionality,
            melodyBusyness,
            scale,
            songStateMap,
            sectionEnergies,
            PercussionSections.GenerateSong(Stream(SongStream.Percussion)),
            commonStateMap.GetStateValue(StateKinds.KeyOffset),
            absent,
            genre
        );
        // how the song starts and ends around its sections, decided before them: the one the song ends with leads home
        // to the tonic, where the ending lands
        var formGenerator = new SongFormGenerator(
            Stream(SongStream.Form),
            Stream(SongStream.Intro),
            Stream(SongStream.HookIntro),
            Stream(SongStream.Drops),
            genre,
            Of(Facet.Form),
            tracks.Definitions.ToDictionary(x => x.Key, x => x.Value.Role),
            !absent.Contains(TrackRole.Drum),
            meter
        );
        var plan = formGenerator.Plan(sectionIds);
        // a section in a meter of its own now and then, a chorus the likelier, each drawing from a stream of its own
        var sectionMeters = SectionMeter.Generate(
            id => new GenerationContext(Seeds.Derive(Seeds.Derive(seed, (int)SongStream.SectionMeter), id)),
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
                Phrases = new GenerationContext(Seeds.Derive(Seeds.Derive(seed, (int)SongStream.SectionPhrases), x.Key))
                    .Pick(ByConvention.Weigh(GenreChoices.PhraseCounts.Select(p => (p, p == 1 ? new ByConvention(1, 1, 1) : default)), songUnconventionality[Facet.Form], phrasePool))
            });
        StateTrace.Record(TracePoints.SectionMeter, FillGenerator.DrumsTrace, 0, 0, StateMap.Default, 0, string.Join(", ", sectionMeters.Select(x => $"{x.Key} {x.Value}")), sectionMeters);
        // a hook intro where the song has a chorus to quote, and a cold start where it has none
        var hook = SongFormGenerator.HookOf(sectionIds, structure.Roles, sectionMeters);
        if (plan.Intro == IntroKind.Hook && hook is null)
            plan = plan with { Intro = IntroKind.Cold };

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
        // how many times more a song that fades plays its last section, by how often the section plays its pattern
        static int Fading(EndingKind ending, int plays) => ending != EndingKind.Fade ? 0 : plays == 1 ? 2 : 1;
        bool Repeats(bool drawn, int fades) =>
            drawn && fades == 0 && sectionIds.Take(sectionIds.Length - 1).Contains(sectionIds[^1]) && structure.Roles[sectionIds[^1]] != SectionRole.Outro;
        // a last section that came back before played once more now and then, drawn here, which the length takes in
        var lastRepeatDrawn = Stream(SongStream.LastRepeat).TestProbability(SongForms.LastRepeatChance.At(songUnconventionality[Facet.Form]));
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
        StateTrace.Record(TracePoints.MelodyImprovisation, SongTracks.MelodyTrack, 0, 0, StateMap.Default, 0, $"{improvisation:F2}", improvisation);
        // a song that fades out plays its last section once more, over which it fades, twice where it plays its pattern
        // only once, so that the fade takes eight bars at least
        var fading = Fading(plan.Ending, generateSection(sectionIds[^1]).Plays);
        // and a last section that came back before played once more now and then, as a last chorus is, where it does not
        // fade, from a stream of its own, the last section's lift moving to it
        var repeats = Repeats(lastRepeatDrawn, fading);
        int[] played = [..sectionIds, ..(repeats ? [sectionIds[^1]] : Array.Empty<int>()), ..Enumerable.Repeat(sectionIds[^1], fading)];
        StateTrace.Record(TracePoints.LastRepeat, SectionGenerator.SectionTrace, 0, 0, StateMap.Default, 0, $"{repeats}", repeats);
        // the later in the song, the more, and with the energy of its own place in the song against the section's average,
        // its parts only joining as it comes back
        double Place(int index) => played.Length > 1 ? index / (double)(played.Length - 1) : 0.5;
        var averagePlaces = played.Select((id, index) => (id, index)).GroupBy(x => x.id).ToDictionary(x => x.Key, x => x.Average(y => Place(y.index)));
        var restedBefore = new Dictionary<int, ImmutableHashSet<TrackRole>>();
        var solos = new Dictionary<int, SoloPlan>();
        var sections = new List<GeneratedSection>();
        // how loud and busy each appearance is meant to be: its section's energy, stepped by its place in the song
        var energies = new List<double>();
        // the parts held back at the song's start, each entering at a later section, from a stream of their own
        var lateEntries = LateEntries.Generate(
            Stream(SongStream.LateEntries),
            played,
            [..played.Select((id, index) => generateSection(id).Energy + SectionEnergy.AppearanceStep(Place(index), averagePlaces[id], SectionEnergy.ContrastOf(genre)))],
            songUnconventionality[Facet.Form],
            absent
        );
        StateTrace.Record(TracePoints.LateEntries, SectionGenerator.SectionTrace, 0, 0, StateMap.Default, 0, string.Join(", ", lateEntries.Select(x => $"{x.Key} at {x.Value}")), lateEntries);
        // every appearance's parts, a part held back yet soloing no sooner than it comes in
        IReadOnlyCollection<TrackRole>[] partsAt = [..played.Select((_, index) => SongParts.All.Except(absent).Except(lateEntries.Where(x => index < x.Value).Select(x => x.Key)).ToArray())];
        // a later appearance played as a solo now and then, each from a sequence of its own, by the section's facets, and
        // one the soloist's where the song has it
        var drawnSolos = Solos.WithSoloist(
            Stream(SongStream.SoloistSolo),
            [..played.Select((id, index) => Solos.Draw(new GenerationContext(Seeds.Derive(Seeds.Derive(seed, (int)SongStream.Solos), index)), index, structure.Roles[id], generateSection(id).Facets, partsAt[index], genre))],
            [..played.Select(id => structure.Roles[id])],
            [..played.Select(id => generateSection(id).Facets[Facet.Melody])],
            [..SongParts.All.Except(absent)]
        );
        for (var index = 0; index < played.Length; index++)
        {
            cancellation.ThrowIfCancellationRequested();
            var id = played[index];
            var solo = drawnSolos[index];
            var energyStep = SectionEnergy.AppearanceStep(Place(index), averagePlaces[id], SectionEnergy.ContrastOf(genre));
            energies.Add(generateSection(id).Energy + energyStep);
            var section = generateSection(id).Appear(
                played.Take(index).Count(x => x == id),
                Tilt.Of(MelodyLayers.ImprovisationGrowth, Place(index)).Chance(improvisation, 1),
                energyStep,
                restedBefore.GetValueOrDefault(id, []),
                // an intro of entries builds the first section's parts up itself
                index == 0 && plan.Intro == IntroKind.Entries,
                solo,
                [..lateEntries.Where(x => index < x.Value).Select(x => x.Key)],
                [..lateEntries.Where(x => index == x.Value).Select(x => x.Key)]
            );
            // as loud as its energy's step from the section's, by the lift's measure of loudness, so that its last chorus
            // plays the louder
            section = section with { Timeline = section.Timeline.MergeStateMap(StateMap.FromStates([StateKinds.Velocity.CreateState(FormLayers.LiftVelocity * energyStep)])) };
            if (solo is not null)
            {
                solos[index] = solo;
                StateTrace.Record(TracePoints.Solo, SectionGenerator.SectionTrace, id, 0, StateMap.Default, 0, $"{string.Join(", ", solo.Soloists)} {solo.Mode}", (index, solo));
            }
            restedBefore[id] = section.Resting;
            StateTrace.Record(TracePoints.Arrangement, SectionGenerator.SectionTrace, id, 0, StateMap.Default, 0, string.Join(", ", section.Resting.Order()), section.Resting);
            StateTrace.Record(TracePoints.Texture, SectionGenerator.SectionTrace, id, 0, StateMap.Default, 0, $"{section.TextureKind}", (index, section.TextureKind, section.PhraseSilent));
            sections.Add(section);
        }

        // every part that solos a soloist of its own, whose solos sound like one player's, each from a sequence of its own,
        // by the melody facet, and the drummer by the fills facet
        var soloists = solos.Values.SelectMany(x => x.Soloists).Distinct().ToDictionary(
            role => role,
            role => Soloist.Generate(
                new GenerationContext(Seeds.Derive(Seeds.Derive(seed, (int)SongStream.Soloist), (int)role)),
                songUnconventionality[role == TrackRole.Drum ? Facet.Fills : Facet.Melody],
                songUnconventionality[Facet.Sound],
                tracks.Definitions.Values.OfType<PitchInstrumentTrack>().FirstOrDefault(x => x.Role == role)?.InstrumentCode ?? -1));
        foreach (var (role, soloist) in soloists)
            StateTrace.Record(TracePoints.Soloist, SectionGenerator.SectionTrace, 0, 0, StateMap.Default, 0, $"{role}", (role, soloist));

        // the song put together as planned, and the lines the drums mark, then every stage after it in its order
        cancellation.ThrowIfCancellationRequested();
        var form = formGenerator.Assemble(plan, played, sections);
        var decisions = new SongDecisions(
            seed, genre, tracks, absent, songUnconventionality, meter, structure, sectionIds, played, sectionMeters, generateSection, sections, energies,
            solos, soloists, form, repeats, commonStateMap, swing, grooveContext);
        var song = SongStages.Run(decisions, new SongDraft(form.Edits.ApplyTo(form.Blocks.Unroll()), null, [], [], []), skipped, observe, cancellation);
        var (songTrackNoteTimelineMap, notes) = (song.Timeline, song.Notes!);

        // the drum kit's sound, the standard kit's, or one of a genre's pool of them, from a stream of its own
        var kit = genre.Pool<int>(GenreChoices.Kit, GenreChoices.KitNameOf) is { } kits
            ? Stream(SongStream.Kit).Pick(ByConvention.Weigh(GenreChoices.Kits.Select(x => (x.Program, x.Program == 0 ? new ByConvention(1, 1, 1) : default)), songUnconventionality[Facet.Sound], kits))
            : 0;

        return new Song(songTrackNoteTimelineMap.Duration, meter, tracks.Definitions, songTrackNoteTimelineMap, form.Map, notes, new SongDraws(songUnconventionality, [..SongParts.All.Except(absent)], tracks.DrumSetup, [..tracks.SongDrums.Select(DrumPieces.NameOf)], scale, swing, kit));
    }

    /// <summary>The random sequence a stage of the song of the given seed draws from.</summary>
    internal static IGenerationContext CreateStream(ulong seed, SongStream stream)
    {
        return new GenerationContext(Seeds.Derive(seed, (int)stream));
    }

    /// <summary>A stream's own sequence for a facet of the song's unconventionality.</summary>
    internal static IGenerationContext CreateStream(ulong seed, SongStream stream, Facet facet)
    {
        return new GenerationContext(Seeds.Derive(Seeds.Derive(seed, (int)stream), (int)facet));
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
        return new StateMapBuilder("Song")
            .AddRhythmLayer(rhythmicUnconventionality.Lean(RhythmLayers.Song))
            .Add(CompositionStateKinds.Rhythm.MaxRank, 2)
            .Add(CompositionStateKinds.Rhythm.Fullness, RhythmSettings.Fullness)
            .Add(CompositionStateKinds.Rhythm.Variation, RhythmSettings.Variation)
            .AddNoteWalkLayer()
            .Add(CompositionStateKinds.ChordPool.Collection, LayerStates.CreateChordPool(unconventionality))
            .Add(CompositionStateKinds.ChordPool.Index, LayerStates.ChordPoolIndex)
            .ToStateMap(context);
    }

    /// <summary>
    ///     The song's state that Render reads, the same for every track: its key and tempo. Its scale is each section's,
    ///     most often the song's.
    /// </summary>
    /// <summary>The song's key and tempo, drawn, and replaced where given, so that giving one leaves every other draw as it was.</summary>
    /// <param name="feel">The feel facet of the song's unconventionality, by which a genre's range of tempos is eased between its ends.</param>
    private static StateMap CreateCommonStateMap(IGenerationContext context, double feel, SongOverrides overrides, Genre genre)
    {
        var drawn = new StateMapBuilder("Song")
            .Add(StateKinds.KeyOffset, Generators.Int(0, 12))
            // the dyadic draw of the tempo, or, where a genre gives a range of it, a tempo in the range at the song's feel,
            // leaning to its middle, or to an end as the genre has it
            .Add(StateKinds.Tempo, genre.Range(GenreChoices.Tempo, feel) is { } range ? c => TempoIn(c, range) : TempoGenerator)
            .AddNoteDurationLayer()
            .ToStateMap(context);
        if (overrides.Key is { } key)
            drawn = drawn.With(StateKinds.KeyOffset, key);
        return overrides.Tempo is { } tempo ? drawn.With(StateKinds.Tempo, tempo) : drawn;
    }

    /// <summary>The tempos a song may play at, as multiples of <see cref="Meter.BaseTempo" />, slowest first: every one the draw can land on.</summary>
    public static ImmutableArray<double> TempoOptions { get; } =
    [
        ..Enumerable.Range(0, 5)
            .SelectMany(rank => DyadicRankDistribution.GetMultiplierDistributionSlice(rank, 0.75, 1.5))
            .Select(x => x.Position)
            .Distinct()
            .Order()
    ];

    /// <summary>How far a range's lean of 1 skews a draw across it: its place to the power of e to the minus this.</summary>
    public const double RangeLeanSkew = 1;

    /// <summary>
    ///     A tempo in a range of amounts (<see cref="GenreChoices.TempoOf" />), leaning to its middle, as the mean of two
    ///     draws across it does, or to an end by its lean, at a whole number of beats a minute.
    /// </summary>
    internal static double TempoIn(IGenerationContext context, (double From, double To, double Lean) range)
    {
        var tempo = GenreChoices.TempoOf(Across(context, range));
        return Math.Round(tempo * Meter.BaseTempo) / Meter.BaseTempo;
    }

    /// <summary>A length in a range of amounts (<see cref="GenreChoices.LengthOf" />), in seconds, drawn as a tempo is in its range.</summary>
    internal static double LengthIn(IGenerationContext context, (double From, double To, double Lean) range) => GenreChoices.LengthOf(Across(context, range));

    /// <summary>An amount in a range, leaning to its middle, as the mean of two draws across it does, or to an end by its lean.</summary>
    private static double Across(IGenerationContext context, (double From, double To, double Lean) range) =>
        range.From + (range.To - range.From) * Math.Pow((context.GenerateDouble() + context.GenerateDouble()) / 2, Math.Exp(-RangeLeanSkew * range.Lean));
}

/// <summary>How the state along a section's 4-bar pattern changes. The steps are in bars.</summary>
/// <param name="ChordShapeStep">How often the chord shape of the progression can change.</param>
/// <param name="NoteStateStep">How often the velocity and note duration of the progression can change.</param>
/// <param name="PoolSize">How many values each state picks from along the pattern; 0 draws a new value every step.</param>
internal sealed record ProgressionSettings(double ChordShapeStep, double NoteStateStep, int PoolSize)
{
    public static ProgressionSettings Default { get; } = new(1, 1, 4);
}

/// <summary>
///     The random sequences of a song's stages, each derived from the song's seed by its number, which must stay as it
///     is: a new stage takes a new number.
/// </summary>
internal enum SongStream
{
    Rhythm = 1,
    Tracks = 2,
    Harmony = 3,
    SongState = 4,
    Melody = 5,
    Common = 6,
    Structure = 7,
    Sections = 8,
    Form = 9,
    Fills = 10,
    Dynamics = 11,
    Percussion = 12,
    DrumStrokes = 13,
    DrumRoles = 14,
    DrumSetup = 15,
    MelodyImprovisation = 16,
    Intro = 17,
    Groove = 18,
    Panning = 19,
    SongForm = 20,
    Pad = 21,
    KeyChange = 22,
    CounterMelody = 23,
    BassFills = 24,
    Meter = 25,
    Unconventionality = 26,
    Feel = 27,
    Parts = 28,
    Riff = 29,
    LineDoubling = 30,
    Solos = 31,
    SoloLines = 32,
    InstrumentsOverTime = 33,
    Expression = 34,
    PartSound = 35,
    SectionKey = 36,
    RhythmPart = 37,
    SectionTempo = 38,
    SectionMeter = 39,
    Polymeter = 40,
    Effects = 41,
    SoloPart = 42,
    Soloist = 43,
    DrumSolo = 44,
    SoloChords = 45,
    LateEntries = 46,
    LineChords = 47,
    SoloAnswers = 48,
    MelodyFills = 49,
    KeyPreparation = 50,
    SoloMotifs = 51,
    TempoRamps = 52,
    DrumSoloStacks = 53,
    CounterFills = 54,
    MelodyChords = 55,
    LastRepeat = 56,
    InstrumentFamilies = 57,
    TwinTimbres = 58,
    SongEnds = 59,
    DrumCoverage = 60,
    UnheardLandings = 61,
    Kit = 62,
    PartFeel = 63,
    SoloistSolo = 64,
    DrumSoloFeet = 65,
    DrumSoloPlayers = 66,
    GenreLength = 67,
    SectionPhrases = 68,
    HookIntro = 69,
    Strum = 70,
    Drops = 71,
    StopTime = 72,
    Reharmonisation = 73
}
