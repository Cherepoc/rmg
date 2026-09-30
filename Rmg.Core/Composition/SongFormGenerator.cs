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

    // the sequence an intro's entries are drawn from, apart from the rest of the form
    private readonly IGenerationContext _introContext;

    private readonly RhythmicUnconventionality _songRhythm;

    // what every track plays, by its number, so that the form can bring in or leave out the drums or the bass
    private readonly IReadOnlyDictionary<int, TrackRole> _roles;

    public SongFormGenerator(
        IGenerationContext context,
        IGenerationContext introContext,
        RhythmicUnconventionality songRhythm,
        IReadOnlyDictionary<int, TrackRole> roles
    )
    {
        _context = context;
        _introContext = introContext;
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
        var intro = _context.Pick(FormLayers.Intros);
        var ending = _context.Pick(FormLayers.WeighEndings(_songRhythm.Tilt));
        var window = intro == IntroKind.Entries ? _introContext.Pick(FormLayers.IntroWindows) : default;
        var halfCountIn = intro == IntroKind.CountIn && _context.TestProbability(FormLayers.HalfCountInChance);

        double held = 0, stop = 0;
        var slowsDown = false;
        if (FormLayers.HasFinalChord(ending))
        {
            held = ending == EndingKind.RingOut ? _context.Pick(FormLayers.RingOutLengths) : FormLayers.ButtonLength;
            if (ending == EndingKind.Stop)
                stop = _context.Pick(FormLayers.StopLengths);
            slowsDown = ending == EndingKind.RingOut && _context.TestProbability(FormLayers.RitardandoChance);
        }

        return new FormPlan(intro, window, halfCountIn, ending, held, stop, slowsDown, sectionIds[^1]);
    }

    /// <summary>
    ///     The song put together as planned: the intro's bars, the sections and the ending's bars one after another,
    ///     where each is, what the form changes once they are, the tempo, and the lines the fills mark.
    /// </summary>
    /// <param name="sectionIds">The sections in the song's order.</param>
    /// <param name="sections">Every section in the song's order, as generated.</param>
    public SongAssembly Assemble(FormPlan plan, IReadOnlyList<int> sectionIds, IReadOnlyList<GeneratedSection> sections)
    {
        // the song's meter, as its sections have it
        var meter = sections[0].Meter;
        var first = sections[0];
        // an intro of entries before the first section plays its first bars, the parts cleared until they come in
        var windowDuration = plan.Window.Bars * meter.BarDuration;
        var introBlock = plan.Intro switch
        {
            IntroKind.Entries when plan.Window.IsBefore => first.Timeline.Trim(windowDuration),
            IntroKind.CountIn => CreateCountIn(first, plan.HalfCountIn, _roles),
            _ => null
        };
        ImmutableArray<IntroEntry> entries = plan.Intro == IntroKind.Entries ? DrawEntries(first, plan.Window) : [];

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
            _ when !FormLayers.HasFinalChord(plan.Ending) => 0,
            EndingKind.RingOut => plan.Held,
            _ => Math.Max(plan.Held, meter.BarDuration)
        };
        var map = new SongMap(
            meter,
            new IntroSpan(plan.Intro, origin, plan.Window, entries),
            spans.ToImmutable(),
            new EndingSpan(plan.Ending, end, endingDuration, plan.Held, plan.Stop, plan.SlowsDown)
        );

        var blocks = ImmutableArray.CreateBuilder<TrackEventStateTimelineMap<StateMap>>();
        if (introBlock is not null)
            blocks.Add(introBlock);
        blocks.AddRange(sections.Select(x => x.Timeline));

        ImmutableArray<FillSection> fillSections =
        [
            ..map.Sections.Zip(sections, (span, section) => new FillSection(span.SectionId, span.Duration, section.Rhythm, section.Facets, section.Groove, section.Energy, section.IsPercussionOnly, section.HasDrums))
        ];
        var edits = new TimelineEdits(_context, meter, map);
        var lines = FillGenerator.GetSectionLines(fillSections, meter, origin).ToBuilder();
        var songState = StateTimelineMap.Create(end);
        var introDescription = $"{plan.Intro} intro, {origin} beats";

        switch (plan.Intro)
        {
            // the drums land where the first section starts, but in a first section whose drums rest
            case IntroKind.CountIn when fillSections[0].HasDrums:
                lines.Insert(0, new FillLine(origin, fillSections[0], fillSections[0], 0, HasFill: false));
                break;
            case IntroKind.Entries:
            {
                // every part left out of its window until it comes in, and the parts left come in at its end, the
                // drums with a fill and a landing
                var windowStart = plan.Window.IsBefore ? 0 : origin;
                var windowEnd = windowStart + windowDuration;
                foreach (var entry in entries.Where(x => x.Entry > 0))
                foreach (var track in entry.Tracks)
                    edits.Clear(track, windowStart, windowStart + entry.Entry);

                // where the first section's drums play, the line the window ends on, or a new one where no line is there,
                // as where the section changes into one whose drums rest; in one whose drums rest, the parts simply come in
                if (fillSections[0].HasDrums)
                {
                    var landing = new FillLine(windowEnd, fillSections[0], fillSections[0], 0, IsLandingForced: true);
                    var windowLine = lines.Select((x, i) => (x, i)).FirstOrDefault(x => x.x.Position.IsEqualToByEpsilon(windowEnd), (null!, -1)).i;
                    if (windowLine >= 0)
                        lines[windowLine] = landing;
                    else
                        lines.Insert(lines.Count(x => x.Position < windowEnd), landing);
                }

                introDescription += $", {plan.Window.Bars} bars {(plan.Window.IsBefore ? "before the first section" : "into it")}: " +
                                    string.Join(", ", entries.Select(x => $"{x.Part} at {x.Entry}"));
                break;
            }
        }

        StateTrace.Record(TracePoints.SongIntro, FillGenerator.DrumsTrace, sectionIds[0], 0, StateMap.Default, 0, introDescription);

        var description = $"{plan.Ending} ending";
        if (FormLayers.HasFinalChord(plan.Ending))
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
                songState = songState.MergeWith(CreateRitardando(end, end + plan.Held));
                description += ", slowing down";
            }
        }

        // a fading song plays its last section once more, as the song plans it, and fades out over it, over eight bars
        // at least, a section of one play's pattern played twice
        if (plan.Ending == EndingKind.Fade)
        {
            var fadeStart = Math.Min(spans[^1].Start, end - 2 * meter.PatternDuration);
            songState = songState.MergeWith(CreateFade(fadeStart, end));
            description += $", fading from beat {fadeStart} to {end}";
        }

        StateTrace.Record(TracePoints.SongEnding, FillGenerator.DrumsTrace, sectionIds[^1], 0, StateMap.Default, 0, description);
        // the band grows louder into a louder section
        songState = songState.MergeWith(CreateLifts(lines, end, meter));

        return new SongAssembly(map, blocks.ToImmutable(), lines.ToImmutable(), edits, songState);
    }

    /// <summary>
    ///     When the parts of the band come in, over an intro's window: those that play in it, in an order drawn part by
    ///     part, each likelier as <see cref="FormLayers.IntroParts" /> weighs it, leaned by the song's rhythm; a drawn
    ///     number of them, at least one and one fewer than all, come in over the window, spread evenly from its start,
    ///     and the rest together at its end, so that the window never plays the whole band.
    /// </summary>
    internal ImmutableArray<IntroEntry> DrawEntries(GeneratedSection first, IntroWindow window)
    {
        var windowDuration = window.Bars * first.Meter.BarDuration;
        var parts = first.Timeline.TrackTimelineMap
            .Where(x => x.Value.EventTimeline.Any(note => note.Position < windowDuration))
            .GroupBy(x => GetPart(x.Key, first))
            .ToDictionary(x => x.Key, x => x.Select(y => y.Key).Order().ToImmutableArray());
        var listed = FormLayers.IntroParts.ToDictionary(x => x.Part);
        foreach (var part in parts.Keys.Where(x => !listed.ContainsKey(x)))
            throw new InvalidOperationException($"The intro has no weight for the part {part}.");

        var options = _songRhythm.Tilt.Weigh(
                FormLayers.IntroParts.Where(x => parts.ContainsKey(x.Part)).Select(x => new Weighted<IntroPart>(x.Weight, x.Part)),
                x => listed[x].Lean
            )
            .ToList();
        var order = new List<IntroPart>();
        while (options.Count > 0)
        {
            var index = Generators.WeightedIndex([..options])(_introContext);
            order.Add(options[index].Value);
            options.RemoveAt(index);
        }

        var inWindow = order.Count <= 1 ? order.Count : Generators.Int(1, order.Count)(_introContext);
        return
        [
            ..order.Select((part, i) => new IntroEntry(
                    part,
                    parts[part],
                    i < inWindow ? Math.Floor(i * window.Bars / (double)inWindow) * first.Meter.BarDuration : windowDuration
                )
            )
        ];
    }

    /// <summary>
    ///     The part of the band a track plays: its role, and a drum's role in the section, a drum bound to a lead its
    ///     lead's, as it plays on the lead's beats and comes in with it.
    /// </summary>
    private IntroPart GetPart(int track, GeneratedSection section)
    {
        if (section.Bindings.TryGetValue(track, out var lead))
            track = lead;
        var role = _roles[track];
        return role == TrackRole.Drum ? new IntroPart(role, section.DrumRoles[track]) : new IntroPart(role);
    }

    /// <summary>
    ///     A bar of clicks on the meter's pulses, or on its second half of them, over the first section's drum state: on the hi-hat's pedal,
    ///     or where the song has no hi-hat, on another dry sound it has (<see cref="FormLayers.CountInSounds" />).
    /// </summary>
    private static TrackEventStateTimelineMap<StateMap> CreateCountIn(
        GeneratedSection first,
        bool isHalf,
        IReadOnlyDictionary<int, TrackRole> roles
    )
    {
        var bar = first.Timeline.Trim(first.Meter.BarDuration);
        // or on the first sound of the song's first drum, such as a percussion song's with none of them
        var (drum, sound) = FormLayers.CountInSounds.FirstOrDefault(x => bar.TrackTimelineMap.ContainsKey(DrumGroups.GetTrackNumber(x.Drum)));
        if (drum is null)
        {
            drum = DrumGroups.GetDrum(bar.TrackTimelineMap.Keys.Where(x => roles[x] == TrackRole.Drum).Min());
            sound = drum.Sounds[0].Code;
        }
        var clickTrack = DrumGroups.GetTrackNumber(drum);
        var pulses = first.Meter.Pulses;
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
                            first.Meter.BarDuration,
                            x.Key == clickTrack ? pulses.Skip(isHalf ? pulses.Length / 2 : 0).Select(pulse => click.ToTimelineItem(pulse)) : []
                        )
                    )
                )
            );
        return TrackEventStateTimelineMap.Create(first.Meter.BarDuration, tracks, bar.CommonStateTimelineMap);
    }

    /// <summary>
    ///     A line's note that ends the song, asked to land on its chord's root, which the line places nearest the note
    ///     before (<see cref="LinePattern.Place" />), going on from it rather than starting a phrase afresh.
    /// </summary>
    internal static TimelineItem<StateMap> LandOnRoot(TimelineItem<StateMap> note)
    {
        return note.Value
            .With(CompositionStateKinds.LineLanding, (int)ChordArrival.Root)
            .With(CompositionStateKinds.LinePhraseStart, (int)PhraseStart.None)
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
        var homeBar = lastSection.Timeline.Trim(lastSection.Meter.BarDuration);
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
                        // on the home chord itself
                        .Select(note => (homeShape is null ? note.Value : note.Value.Except(shapeKinds).MergeWith(homeShape))
                            .MergeWith(final)
                            .ToTimelineItem(0)
                        )
                        .Select(LandOnRoot);
                return new KeyValuePair<int, EventStateTimelineMap<StateMap>>(
                    x.Key,
                    EventStateTimelineMap.Create(lastSection.Meter.BarDuration, EventTimeline.Create(lastSection.Meter.BarDuration, notes), x.Value.StateTimelineMap)
                );
            }
        );
        return TrackEventStateTimelineMap.Merge(
            [
                TrackEventStateTimelineMap.Create(lastSection.Meter.BarDuration, tracks, homeBar.CommonStateTimelineMap),
                TrackEventStateTimelineMap.Create<StateMap>(duration)
            ]
        );
    }

    /// <summary>The band fading out from the start given to the end, a step every <see cref="FormLayers.FadeStep" />, to silence.</summary>
    /// <summary>
    ///     The lifts into the louder sections: the band's loudness rising over the bar before a change of section, from
    ///     as loud as it plays to louder by how much more energy the next section has, as far as the ending section's rhythm
    ///     follows its energy, a step every <see cref="FormLayers.FadeStep" />, the next section then as loud as it plays.
    /// </summary>
    private static StateTimelineMap CreateLifts(IEnumerable<FillLine> lines, double end, Meter meter)
    {
        var items = new List<TimelineItem<double>>();
        foreach (var line in lines.Where(x => x.Ending != x.Next && x.Position < end))
        {
            var lift = (line.Next.Energy - line.Ending.Energy) * line.Ending.Rhythm.Coupling;
            if (lift <= 0)
                continue;

            var count = (int)Math.Round(meter.BarDuration / FormLayers.FadeStep);
            var start = line.Position - meter.BarDuration;
            items.AddRange(Enumerable.Range(1, count).Select(i => (FormLayers.LiftVelocity * lift * i / count).ToTimelineItem(start + i * FormLayers.FadeStep - FormLayers.FadeStep)));
            items.Add(0.0.ToTimelineItem(line.Position));
        }

        IStateTimeline velocity = StateTimeline.Create(end, StateKinds.Velocity, items.OrderBy(x => x.Position)).WithLayer("Lift");
        return new[] { velocity }.ToStateTimelineMap(end);
    }

    private static StateTimelineMap CreateFade(double start, double end)
    {
        var count = (int)Math.Round((end - start) / FormLayers.FadeStep);
        IStateTimeline fade = StateTimeline.Create(
                end,
                StateKinds.Fade,
                Enumerable.Range(0, count).Select(i => (1 - i / (double)count).ToTimelineItem(start + i * FormLayers.FadeStep))
            )
            .WithLayer("Ending");
        return new[] { fade }.ToStateTimelineMap(end);
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
/// <param name="Window">Where the band comes in part by part, for an intro of entries.</param>
/// <param name="HalfCountIn">Whether a count-in clicks only the last two beats.</param>
/// <param name="Held">How long the final chord is held, in beats; 0 for an ending with none, open or fading.</param>
/// <param name="Stop">How long the band is silent before a stopped ending's chord, in beats.</param>
/// <param name="SlowsDown">Whether the bar before a ringing ending slows down.</param>
/// <param name="TonicHomeSectionId">The section whose home is the song's tonic, the last, which leads home to the ending.</param>
internal sealed record FormPlan(
    IntroKind Intro,
    IntroWindow Window,
    bool HalfCountIn,
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
/// <param name="SongState">How the song's state changes over it, over its own, such as the tempo slowing before an ending, or the band fading out.</param>
internal sealed record SongAssembly(
    SongMap Map,
    ImmutableArray<TrackEventStateTimelineMap<StateMap>> Blocks,
    ImmutableArray<FillLine> Lines,
    TimelineEdits Edits,
    StateTimelineMap SongState
);
