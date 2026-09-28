using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     The form of a song around its sections: how it starts and how it ends. It sees the sections before they are put
///     one after another, so it can put bars before and after them, and it tells the fills where the lines are that
///     they mark. An intro puts the first section's drums or a count-in before it, or leaves tracks out of its first
///     phrase, to come in after it. The ending plays the home bar of the last section, whose home is the song's tonic:
///     every track's first note of it, moved to the downbeat and held, the bass on the root and the melody's last note on
///     it too. It decides the form before the sections are generated, and puts the song together after.
/// </summary>
internal sealed class SongFormGenerator
{
    private readonly IGenerationContext _context;
    private readonly RhythmicUnconventionality _songRhythm;

    // what every track plays, by its number, so that the form can bring in or leave out the drums or the bass
    private readonly IReadOnlyDictionary<int, TrackRole> _roles;

    public SongFormGenerator(IGenerationContext context, RhythmicUnconventionality songRhythm, IReadOnlyDictionary<int, TrackRole> roles)
    {
        _context = context;
        _songRhythm = songRhythm;
        _roles = roles;
    }

    /// <summary>
    ///     What the song's form will be, decided before its sections: its intro and ending, and how they play. The song's
    ///     last section then leads home to the tonic, where the ending lands.
    /// </summary>
    /// <param name="sectionIds">The sections in the song's order.</param>
    public FormPlan Plan(IReadOnlyList<int> sectionIds)
    {
        var intro = Pick(FormLayers.Intros);
        var ending = Pick(FormLayers.WeighEndings(_songRhythm.Tilt));
        var drumsFirstBars = intro == IntroKind.DrumsFirst ? Pick(FormLayers.DrumsFirstBars) : 0;
        var halfCountIn = intro == IntroKind.CountIn && _context.TestProbability(FormLayers.HalfCountInChance);
        var withBass = intro == IntroKind.ChordsFirst && _context.TestProbability(FormLayers.ChordsFirstBassChance);

        double held = 0, stop = 0;
        var slowsDown = false;
        if (ending != EndingKind.Open)
        {
            held = ending == EndingKind.RingOut ? Pick(FormLayers.RingOutLengths) : FormLayers.ButtonLength;
            if (ending == EndingKind.Stop)
                stop = Pick(FormLayers.StopLengths);
            slowsDown = ending == EndingKind.RingOut && _context.TestProbability(FormLayers.RitardandoChance);
        }

        return new FormPlan(intro, drumsFirstBars, halfCountIn, withBass, ending, held, stop, slowsDown, sectionIds[^1]);
    }

    /// <summary>
    ///     The song put together as planned: the intro's bars, the sections and the ending's bars one after another,
    ///     where each is, what the form changes once they are, the tempo, and the lines the fills mark.
    /// </summary>
    /// <param name="sectionIds">The sections in the song's order.</param>
    /// <param name="sections">Every section in the song's order, as generated.</param>
    public SongAssembly Assemble(FormPlan plan, IReadOnlyList<int> sectionIds, IReadOnlyList<GeneratedSection> sections)
    {
        var first = sections[0];
        var introBlock = plan.Intro switch
        {
            IntroKind.DrumsFirst => CreateDrumsFirst(first, plan.DrumsFirstBars * Meter.BarDuration, _roles),
            IntroKind.CountIn => CreateCountIn(first, plan.HalfCountIn, _roles),
            _ => null
        };

        // the first section starts after the intro's bars, and the ending after the last
        var origin = introBlock?.Duration ?? 0;
        var spans = ImmutableArray.CreateBuilder<SectionSpan>();
        var start = origin;
        foreach (var (id, section) in sectionIds.Zip(sections))
        {
            spans.Add(new SectionSpan(id, start, section.Timeline.Duration));
            start += section.Timeline.Duration;
        }

        var end = start;
        var endingDuration = plan.Ending switch
        {
            EndingKind.Open => 0,
            EndingKind.RingOut => plan.Held,
            _ => Math.Max(plan.Held, Meter.BarDuration)
        };
        var map = new SongMap(
            new IntroSpan(plan.Intro, origin, plan.WithBass),
            spans.ToImmutable(),
            new EndingSpan(plan.Ending, end, endingDuration, plan.Held, plan.Stop, plan.SlowsDown)
        );

        var blocks = ImmutableArray.CreateBuilder<TrackEventStateTimelineMap<StateMap>>();
        if (introBlock is not null)
            blocks.Add(introBlock);
        blocks.AddRange(sections.Select(x => x.Timeline));

        ImmutableArray<FillSection> fillSections =
        [
            ..map.Sections.Zip(sections, (span, section) => new FillSection(span.SectionId, span.Duration, section.Rhythm, section.Groove, section.Energy, section.IsPercussionOnly))
        ];
        var edits = new TimelineEdits(_context, map);
        var lines = FillGenerator.GetSectionLines(fillSections, origin).ToBuilder();
        var tempo = StateTimelineMap.Create(end);
        var introDescription = $"{plan.Intro} intro, {origin} beats";

        switch (plan.Intro)
        {
            case IntroKind.DrumsFirst:
                // the band comes in on a fill and a landing
                lines.Insert(0, new FillLine(origin, fillSections[0], fillSections[0], 0, IsLandingForced: true));
                break;
            case IntroKind.CountIn:
                lines.Insert(0, new FillLine(origin, fillSections[0], fillSections[0], 0, HasFill: false));
                break;
            case IntroKind.ChordsFirst or IntroKind.Build:
            {
                // the first phrase leaves tracks out, which come in at its end, the drums with a fill and a landing
                var phraseEnd = origin + Meter.PatternDuration;
                foreach (var track in first.Timeline.TrackTimelineMap.Keys)
                {
                    var entry = GetIntroEntry(plan.Intro, _roles[track], plan.WithBass);
                    if (entry > 0)
                        edits.Clear(track, origin, origin + entry);
                }

                var phraseLine = lines.Select((x, i) => (x, i)).First(x => x.x.Position.IsEqualToByEpsilon(phraseEnd)).i;
                lines[phraseLine] = new FillLine(phraseEnd, fillSections[0], fillSections[0], 0, IsLandingForced: true);
                if (plan.WithBass)
                    introDescription += ", with the bass";
                break;
            }
        }

        StateTrace.Record(TracePoints.SongIntro, FillGenerator.DrumsTrace, sectionIds[0], 0, StateMap.Default, 0, introDescription);

        var description = $"{plan.Ending} ending";
        if (plan.Ending != EndingKind.Open)
        {
            blocks.Add(CreateEnding(sections[^1], sections.Take(sections.Count - 1), plan.Held, endingDuration, _roles));

            if (plan.Ending == EndingKind.Stop)
            {
                // the band stops before the line, and the drums land on it with the rest
                var stop = end - plan.Stop;
                description += $", stopping {plan.Stop} beats before it";
                foreach (var track in sections[^1].Timeline.TrackTimelineMap.Keys)
                {
                    edits.Cut(track, stop);
                    edits.Clear(track, stop, end);
                }
            }

            lines.Add(
                new FillLine(end, fillSections[^1], fillSections[^1], 0, plan.Ending != EndingKind.Stop, IsLandingForced: true)
            );

            description += $", at beat {end}, held {plan.Held} beats";
            if (plan.SlowsDown)
            {
                tempo = CreateRitardando(end, end + plan.Held);
                description += ", slowing down";
            }
        }

        StateTrace.Record(TracePoints.SongEnding, FillGenerator.DrumsTrace, sectionIds[^1], 0, StateMap.Default, 0, description);
        return new SongAssembly(map, blocks.ToImmutable(), lines.ToImmutable(), edits, tempo);
    }

    /// <summary>
    ///     When a track comes in, in beats into the first phrase, as an intro leaves it out: the chords from the start, and
    ///     in a build the bass and the drums bar by bar; the others after the phrase; 0 for from the start.
    /// </summary>
    internal static double GetIntroEntry(IntroKind intro, TrackRole role, bool withBass)
    {
        var phrase = Meter.PatternDuration;
        if (role == TrackRole.Chords)
            return 0;
        if (intro == IntroKind.ChordsFirst)
            return role == TrackRole.Bass && withBass ? 0 : phrase;

        return role switch
        {
            TrackRole.Bass => FormLayers.BuildBassBar * Meter.BarDuration,
            TrackRole.Drum => FormLayers.BuildDrumsBar * Meter.BarDuration,
            _ => phrase
        };
    }

    /// <summary>The first section's drums alone, notes and state, for the intro's bars.</summary>
    private static TrackEventStateTimelineMap<StateMap> CreateDrumsFirst(
        GeneratedSection first,
        double duration,
        IReadOnlyDictionary<int, TrackRole> roles
    )
    {
        var bars = first.Timeline.Trim(duration);
        return TrackEventStateTimelineMap.Create(
            duration,
            bars.TrackTimelineMap.Where(x => roles[x.Key] == TrackRole.Drum),
            bars.CommonStateTimelineMap
        );
    }

    /// <summary>
    ///     A bar of clicks on the beats, or on the last two, over the first section's drum state: on the hi-hat's pedal,
    ///     or where the song has no hi-hat, on another dry sound it has (<see cref="FormLayers.CountInSounds" />).
    /// </summary>
    private static TrackEventStateTimelineMap<StateMap> CreateCountIn(
        GeneratedSection first,
        bool isHalf,
        IReadOnlyDictionary<int, TrackRole> roles
    )
    {
        var bar = first.Timeline.Trim(Meter.BarDuration);
        // or on the first sound of the song's first drum, such as a percussion song's with none of them
        var (drum, sound) = FormLayers.CountInSounds.FirstOrDefault(x => bar.TrackTimelineMap.ContainsKey(DrumGroups.GetTrackNumber(x.Drum)));
        if (drum is null)
        {
            drum = DrumGroups.GetDrum(bar.TrackTimelineMap.Keys.Where(x => roles[x] == TrackRole.Drum).Min());
            sound = drum.Sounds[0].Code;
        }
        var clickTrack = DrumGroups.GetTrackNumber(drum);
        var click = StateMap.FromStates(
            [
                StateKinds.Velocity.CreateState(FormLayers.CountInVelocity),
                StateKinds.ArticulationIndex.CreateState(drum.GetArticulationIndex(sound))
            ]
        );
        var tracks = bar.TrackTimelineMap
            .Where(x => roles[x.Key] == TrackRole.Drum)
            .Select(x => new KeyValuePair<int, EventStateTimelineMap<StateMap>>(
                    x.Key,
                    x.Value.WithEvents(
                        EventTimeline.Create(
                            Meter.BarDuration,
                            x.Key == clickTrack ? Enumerable.Range(isHalf ? 2 : 0, isHalf ? 2 : 4).Select(beat => click.ToTimelineItem(beat)) : []
                        )
                    )
                )
            );
        return TrackEventStateTimelineMap.Create(Meter.BarDuration, tracks, bar.CommonStateTimelineMap);
    }

    private T Pick<T>(ImmutableArray<Weighted<T>> weights)
    {
        return weights[Generators.WeightedIndex(weights)(_context)].Value;
    }

    /// <summary>
    ///     A note whose scale step is set, as the melody's is, moved to the chord's root, where a melody ends: the root
    ///     in the register of the note it is made from.
    /// </summary>
    internal static TimelineItem<StateMap> LandOnRoot(TimelineItem<StateMap> note)
    {
        if (!note.Value.Kinds.Contains(StateKinds.ScaleStep))
            return note;

        var root = (int)Math.Round(note.Value.GetStateValue(StateKinds.ScaleStep) / (double)Scales.StepCount) * Scales.StepCount;
        return note.Value.Except([StateKinds.ScaleStep])
            .MergeWith(StateMap.FromStates([StateKinds.ScaleStep.CreateState(root)]))
            .ToTimelineItem(note.Position);
    }

    /// <summary>
    ///     The ending's bars: the home bar of the song's last section, every pitched track's first note of it moved to
    ///     the downbeat and held, or its first of the section if it rests in that bar, or its last before the section if
    ///     it rests in all of it, the melody's as its last note, on the root; the drums' state with no notes, for the
    ///     fills to land on; and the bass on the chord's root.
    /// </summary>
    /// <param name="length">How long the chord is held, in beats.</param>
    /// <param name="duration">How long the ending is, in beats.</param>
    /// <param name="earlierSections">The sections before the last, in the song's order.</param>
    /// <param name="roles">What every track plays, by its number.</param>
    internal static TrackEventStateTimelineMap<StateMap> CreateEnding(
        GeneratedSection lastSection,
        IEnumerable<GeneratedSection> earlierSections,
        double length,
        double duration,
        IReadOnlyDictionary<int, TrackRole> roles
    )
    {
        var homeBar = lastSection.Timeline.Trim(Meter.BarDuration);
        var final = StateMap.FromStates([StateKinds.HeldDuration.CreateState(length)]);
        // every track plays the home bar's chord, whichever note it takes its own state from
        IStateKind[] shapeKinds = [StateKinds.ChordNotePitchOffsets, StateKinds.ChordVoicingFixed];
        var homeShape = homeBar.TrackTimelineMap
            .Where(x => roles[x.Key] != TrackRole.Drum && x.Value.EventTimeline.Count > 0)
            .Select(x => x.Value.EventTimeline[0].Value.Subset(shapeKinds))
            .FirstOrDefault();
        var tracks = homeBar.TrackTimelineMap.Select(x =>
            {
                // a track that rests in the home bar plays its first note of the section, or, if it rests all of it,
                // its last note of the sections before, over the home bar's root
                var template = x.Value.EventTimeline.Concat(lastSection.Timeline.TrackTimelineMap[x.Key].EventTimeline)
                    .Concat(earlierSections.Reverse().SelectMany(section =>
                            section.Timeline.TrackTimelineMap.TryGetValue(x.Key, out var timeline) ? timeline.EventTimeline.Reverse() : []
                        )
                    );
                var notes = roles[x.Key] == TrackRole.Drum
                    ? []
                    : template.Take(1)
                        // on the home chord itself, without the note's own step of the walk of the root
                        .Select(note => (homeShape is null ? note.Value : note.Value.Except(shapeKinds).MergeWith(homeShape))
                            .Except([StateKinds.ChordRootNoteOffset])
                            .MergeWith(final)
                            .ToTimelineItem(0)
                        )
                        .Select(LandOnRoot);
                return new KeyValuePair<int, EventStateTimelineMap<StateMap>>(
                    x.Key,
                    EventStateTimelineMap.Create(Meter.BarDuration, EventTimeline.Create(Meter.BarDuration, notes), x.Value.StateTimelineMap)
                );
            }
        );
        // the bass lands on the root, whatever the bar's own arrival
        var commonStateTimelineMap = homeBar.CommonStateTimelineMap
            .Except([StateKinds.ChordArrival])
            .MergeStateMap(StateMap.FromStates([StateKinds.ChordArrival.CreateState((int)ChordArrival.Root)]));
        return TrackEventStateTimelineMap.Merge(
            [
                TrackEventStateTimelineMap.Create(Meter.BarDuration, tracks, commonStateTimelineMap),
                TrackEventStateTimelineMap.Create<StateMap>(duration)
            ]
        );
    }

    /// <summary>The tempo slowing over the bar before the ending, beat by beat, and staying slow to the end.</summary>
    private static StateTimelineMap CreateRitardando(double line, double end)
    {
        var steps = FormLayers.Ritardando;
        IStateTimeline ritardando = StateTimeline.Create(
                end,
                StateKinds.Tempo,
                steps.Select((x, i) => x.ToTimelineItem(line - steps.Length + i))
            )
            .WithLayer("Ending");
        return new[] { ritardando }.ToStateTimelineMap(end);
    }
}

/// <summary>What a song's form will be, decided before its sections.</summary>
/// <param name="DrumsFirstBars">How many bars the drums play alone, for an intro of the drums first.</param>
/// <param name="HalfCountIn">Whether a count-in clicks only the last two beats.</param>
/// <param name="WithBass">Whether the bass joins the chords, for an intro of the chords first.</param>
/// <param name="Held">How long the final chord is held, in beats; 0 for an open ending.</param>
/// <param name="Stop">How long the band is silent before a stopped ending's chord, in beats.</param>
/// <param name="SlowsDown">Whether the bar before a ringing ending slows down.</param>
/// <param name="TonicHomeSectionId">The section whose home is the song's tonic, the last, which leads home to the ending.</param>
internal sealed record FormPlan(
    IntroKind Intro,
    int DrumsFirstBars,
    bool HalfCountIn,
    bool WithBass,
    EndingKind Ending,
    double Held,
    double Stop,
    bool SlowsDown,
    int TonicHomeSectionId
);

/// <summary>A song put together: where its parts are, its blocks to put one after another, and what the form adds.</summary>
/// <param name="Blocks">The intro, if it has bars of its own, the sections, and the ending, if it has bars of its own.</param>
/// <param name="Lines">The lines the fills mark, in the song's order.</param>
/// <param name="Edits">What the form changes once the blocks are put one after another, such as the bars an intro leaves out.</param>
/// <param name="Tempo">How the tempo changes over the song, such as the slowing before an ending, over the song's own.</param>
internal sealed record SongAssembly(
    SongMap Map,
    ImmutableArray<TrackEventStateTimelineMap<StateMap>> Blocks,
    ImmutableArray<FillLine> Lines,
    TimelineEdits Edits,
    StateTimelineMap Tempo
);
