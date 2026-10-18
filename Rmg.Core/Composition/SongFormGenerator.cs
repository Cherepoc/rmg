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

    // how finely a line's place keys its drop's sequences
    private const int LinePlacesPerBeat = 48;

    // the sequence whether a lift is built to and dropped at is drawn from (FormLayers.Drop)
    private readonly IGenerationContext _dropContext;

    // a drop's kind, silent or filtered, from a sequence of its own
    private readonly IGenerationContext _dropKindContext;

    // whether a build rolls on the snare, from a sequence of its own (FormLayers.BuildRoll)
    private readonly IGenerationContext _buildRollContext;

    // the sequence whether the song opens on one part alone, and which, is drawn from (FormLayers.OpeningAlone)
    private readonly IGenerationContext _openingContext;

    // the genre whose intros and endings the song's are drawn among, where it names them
    private readonly Genre _genre;

    private readonly RhythmicUnconventionality _songRhythm;

    // what every track plays, by its number, so that the form can bring in or leave out the drums or the bass
    private readonly IReadOnlyDictionary<int, TrackRole> _roles;

    // whether the song has drums, which a landing on the final chord plays
    private readonly bool _hasDrums;

    // whether its drums keep time, which a count-in and an intro's entries need, drums that only mark the form playing
    // in its sections alone (DrumMarks)
    private readonly bool _drumsKeepTime;

    // the sequence another intro is drawn from where the song has no chorus for its hook to quote (WithoutHook)
    private readonly IGenerationContext _hookContext;

    // the parts the song plays, one of which it may open on alone
    private readonly IReadOnlySet<TrackRole> _playing;

    public SongFormGenerator(
        IGenerationContext context,
        IGenerationContext introContext,
        IGenerationContext dropContext,
        IGenerationContext dropKindContext,
        IGenerationContext buildRollContext,
        IGenerationContext openingContext,
        Genre genre,
        RhythmicUnconventionality songRhythm,
        IReadOnlyDictionary<int, TrackRole> roles,
        bool hasDrums,
        bool drumsKeepTime,
        IGenerationContext hookContext,
        IReadOnlySet<TrackRole> playing
    )
    {
        _context = context;
        _introContext = introContext;
        _dropContext = dropContext;
        _dropKindContext = dropKindContext;
        _buildRollContext = buildRollContext;
        _openingContext = openingContext;
        _genre = genre;
        _songRhythm = songRhythm;
        _roles = roles;
        _hasDrums = hasDrums;
        _drumsKeepTime = hasDrums && drumsKeepTime;
        _hookContext = hookContext;
        _playing = playing;
    }

    /// <summary>A plan whose hook has no chorus to quote with another of the genre's intros, from a sequence of its own, a count-in where the drums keep time, half of one now and then.</summary>
    public FormPlan WithoutHook(FormPlan plan)
    {
        ImmutableArray<Weighted<IntroKind>> others = [..FormLayers.WeighIntros(_songRhythm.Value, _genre).Where(x => x.Value != IntroKind.Hook && (x.Value != IntroKind.CountIn || _drumsKeepTime))];
        var intro = others.IsEmpty ? IntroKind.Cold : _hookContext.Pick(others);
        // a count-in taken in the hook's place clicking its last two beats as one drawn so would
        return plan with { Intro = intro, HalfCountIn = _hookContext.TestProbability(FormLayers.HalfCountInChance) && intro == IntroKind.CountIn };
    }

    /// <summary>
    ///     What the song's form will be, decided before its sections: its intro and ending, and how they play. The song's
    ///     last section then leads home to the tonic, where the ending lands.
    /// </summary>
    /// <param name="sectionIds">The sections in the song's order.</param>
    public FormPlan Plan(IReadOnlyList<int> sectionIds)
    {
        var intro = _context.Pick(FormLayers.WeighIntros(_songRhythm.Value, _genre));
        var ending = _context.Pick(FormLayers.WeighEndings(_songRhythm.Value, _genre));
        // the entries' window and a count-in's half drawn whatever the intro, so that the draws after them are as many
        var window = _introContext.Pick(FormLayers.IntroWindows);
        var halfCountIn = _context.TestProbability(FormLayers.HalfCountInChance) && intro == IntroKind.CountIn;
        // a song without drums that keep time has nobody to count it in, so it starts cold, drawn as it would be all the same
        if (intro == IntroKind.CountIn && !_drumsKeepTime)
            (intro, halfCountIn) = (IntroKind.Cold, false);
        // or one part alone opening it, the band coming in after it, a genre's, from a sequence of its own, over the
        // window drawn for the entries or one of its own: among the parts the song plays, the drums where they keep time
        TrackRole? first = null;
        if (_openingContext.TestProbability(FormLayers.OpeningAlone.At(_songRhythm.Value, _genre.Chance(GenreChoices.OpeningAlone, "songs"))))
        {
            ImmutableArray<Weighted<TrackRole>> parts = [..ByConvention.Weigh(FormLayers.OpeningParts, _songRhythm.Value, _genre.Pool<TrackRole>(GenreChoices.OpeningPart))
                .Where(x => _playing.Contains(x.Value) && (x.Value != TrackRole.Drum || _drumsKeepTime))];
            first = parts.IsEmpty ? null : _openingContext.Pick(parts);
        }

        if (first is not null)
            (intro, window, halfCountIn) = (IntroKind.Entries, intro == IntroKind.Entries ? window : _openingContext.Pick(FormLayers.IntroWindows), false);

        double held = 0, stop = 0;
        double ringOutBars = 0;
        var slowsDown = false;
        if (FormLayers.HasFinalChord(ending))
        {
            // a ringing ending's bars, which the last section's meter makes beats once its sections are drawn
            if (FormLayers.HoldsFinalChord(ending))
                ringOutBars = _context.Pick(FormLayers.RingOutLengths);
            else
                held = FormLayers.ButtonLength;
            if (ending == EndingKind.Stop)
                stop = _context.Pick(FormLayers.StopLengths);
            slowsDown = FormLayers.HoldsFinalChord(ending) && _context.TestProbability(FormLayers.RitardandoChance);
        }

        return new FormPlan(intro, window, halfCountIn, first, ending, held, ringOutBars, stop, slowsDown, sectionIds[^1]);
    }

    /// <summary>
    ///     The chorus a hook intro quotes: the song's first, in the first section's bar, where neither a chorus nor an
    ///     intro opens the song; none where it has no such chorus, and the song starts cold.
    /// </summary>
    internal static int? HookOf(IReadOnlyList<int> sectionIds, IReadOnlyDictionary<int, SectionRole> roles, IReadOnlyDictionary<int, Meter> meters)
    {
        if (roles[sectionIds[0]] is SectionRole.Chorus or SectionRole.Intro)
            return null;
        var chorus = sectionIds.Cast<int?>().FirstOrDefault(x => roles[x!.Value] == SectionRole.Chorus);
        return chorus is { } id && meters[id].HasSameBar(meters[sectionIds[0]]) ? id : null;
    }

    /// <summary>
    ///     How long the intro planned plays before the first section, in beats, which a length given takes in: an intro of
    ///     entries in bars of its own, a count-in's bar, or the chorus's first phrase; none for one that starts with it.
    /// </summary>
    /// <param name="first">The first section's meter.</param>
    /// <param name="hook">The meter of the chorus a hook intro quotes; none for another intro.</param>
    internal static double IntroDuration(FormPlan plan, Meter first, Meter? hook) => plan.Intro switch
    {
        IntroKind.Entries when plan.Window.IsBefore => plan.Window.FittedTo(first).Bars * first.BarDuration,
        IntroKind.CountIn => first.BarDuration,
        IntroKind.Hook => (hook ?? throw new ArgumentNullException(nameof(hook))).PhraseDuration,
        _ => 0
    };

    /// <summary>
    ///     A line with the notes of a hook intro at the pitches the chorus it quotes plays them at, as its line placed over
    ///     the song has them afresh, but its last, which leads into the first section; as it is in a song of another intro.
    /// </summary>
    /// <param name="chorus">Where the chorus the intro quotes starts.</param>
    internal static TrackEventStateTimelineMap<StateMap> QuoteHook(TrackEventStateTimelineMap<StateMap> song, int track, SongMap map, double chorus)
    {
        if (map.Intro.Kind != IntroKind.Hook || !song.TrackTimelineMap.TryGetValue(track, out var line))
            return song;
        var notes = line.EventTimeline;
        var origin = map.Origin;
        var heard = notes.Where(x => x.Position > chorus - 1e-9 && x.Position < chorus + origin - 1e-9).ToDictionary(x => Math.Round(x.Position - chorus, 6), x => x.Value);
        var last = notes.Where(x => x.Position < origin - 1e-9).Select(x => x.Position).DefaultIfEmpty(0).Max();
        var quoted = EventTimeline.Create(notes.Duration, notes.Select(x =>
            x.Position < last - 1e-9 && heard.TryGetValue(Math.Round(x.Position, 6), out var played)
                ? x.Value.With(StateKinds.ScaleStep, played.GetStateValue(StateKinds.ScaleStep)).With(StateKinds.Alteration, played.GetStateValue(StateKinds.Alteration)).ToTimelineItem(x.Position)
                : x));
        return song.MapTrackEvents(new Dictionary<int, Func<EventTimeline<StateMap>, EventTimeline<StateMap>>> { [track] = _ => quoted });
    }

    /// <summary>A section's timeline cut short, its notes after the cut left out and a note held past it held to it.</summary>
    private static TrackEventStateTimelineMap<StateMap> CutShort(TrackEventStateTimelineMap<StateMap> timeline, double duration) =>
        timeline.Trim(duration).MapTrackEvents(timeline.TrackTimelineMap.Keys.ToDictionary(x => x, _ => (Func<EventTimeline<StateMap>, EventTimeline<StateMap>>)(notes =>
            EventTimeline.Create(notes.Duration, notes.Select(x => x.Value.GetStateValue(StateKinds.HeldDuration) is var held && x.Position + held > duration + 1e-9
                ? x.Value.With(StateKinds.HeldDuration, duration - x.Position).ToTimelineItem(x.Position)
                : x)))));

    /// <summary>
    ///     The song put together as planned: the intro's bars, the sections and the ending's bars one after another,
    ///     where each is, what the form changes once they are, the tempo, and the lines the fills mark.
    /// </summary>
    /// <param name="sectionIds">The sections in the song's order.</param>
    /// <param name="sections">Every section in the song's order, as generated.</param>
    /// <param name="shortBars">The sections whose last bar is cut short wherever they play but at the song's end (<see cref="ShortBars" />).</param>
    public SongAssembly Assemble(FormPlan plan, IReadOnlyList<int> sectionIds, IReadOnlyList<GeneratedSection> sections, IReadOnlySet<int> shortBars)
    {
        // the meter the song starts in, its first section's, which its intro plays in, and the one it ends in
        var meter = sections[0].Meter;
        var last = sections[^1].Meter;
        var first = sections[0];
        plan = plan with { Window = plan.Window.FittedTo(first.Meter) };
        // the chorus a hook intro quotes, its first appearance (HookOf)
        var hook = sections.Select((x, i) => (Section: x, Index: i)).FirstOrDefault(x => x.Section.Role == SectionRole.Chorus);
        if (plan.Intro == IntroKind.Hook && hook.Section is null)
            throw new InvalidOperationException("A hook intro was planned for a song without a chorus to quote.");
        // an intro of entries before the first section plays its first bars, the parts cleared until they come in
        var windowDuration = plan.Window.Bars * meter.BarDuration;
        var introBlock = plan.Intro switch
        {
            IntroKind.Entries when plan.Window.IsBefore => first.Timeline.Trim(windowDuration),
            IntroKind.CountIn => CreateCountIn(first, plan.HalfCountIn, _roles),
            IntroKind.Hook => hook.Section.Timeline.Trim(hook.Section.Meter.PhraseDuration),
            _ => null
        };
        ImmutableArray<IntroEntry> entries = plan.Intro == IntroKind.Entries ? DrawEntries(first, plan.Window, plan.First) : [];

        // the first section starts after the intro's bars, and the ending after the last
        var origin = introBlock?.Duration ?? 0;
        var spans = ImmutableArray.CreateBuilder<SectionSpan>();
        var timelines = new List<TrackEventStateTimelineMap<StateMap>>();
        var start = origin;
        for (var index = 0; index < sections.Count; index++)
        {
            var (id, section) = (sectionIds[index], sections[index]);
            // its last bar cut short before the next section, its notes there with it, a note held past it to its end; but
            // the song's last, and its first where an intro's entries come in over its bars
            var countsBars = index == 0 && plan.Intro == IntroKind.Entries && !plan.Window.IsBefore;
            var cut = shortBars.Contains(id) && index < sections.Count - 1 && !countsBars ? ShortBars.Cut(section.Meter) : 0;
            var timeline = cut > 0 ? CutShort(section.Timeline, section.Timeline.Duration - cut) : section.Timeline;
            spans.Add(new SectionSpan(id, start, timeline.Duration, section.Meter, cut));
            timelines.Add(timeline);
            start += timeline.Duration;
        }

        // the final chord's length in beats, a ringing ending's in the last section's bars
        var held = plan.RingOutBars > 0 ? plan.RingOutBars * last.BarDuration : plan.Held;
        var end = start;
        var endingDuration = plan.Ending switch
        {
            _ when !FormLayers.HasFinalChord(plan.Ending) => 0,
            // a big ending's held chord and its last hit's bar
            EndingKind.Big => held + last.BarDuration,
            // a bar of the last section's meter at least, as the final chord's block is
            _ => Math.Max(held, last.BarDuration)
        };
        var map = new SongMap(
            meter,
            new IntroSpan(plan.Intro, origin, plan.Window, entries, plan.Intro == IntroKind.Hook ? hook.Index : 0),
            spans.ToImmutable(),
            new EndingSpan(plan.Ending, end, endingDuration, held, plan.Stop, plan.SlowsDown)
        );

        var blocks = ImmutableArray.CreateBuilder<TrackEventStateTimelineMap<StateMap>>();
        if (introBlock is not null)
            blocks.Add(introBlock);
        blocks.AddRange(timelines);

        ImmutableArray<FillSection> fillSections =
        [
            ..map.Sections.Zip(sections, (span, section) => new FillSection(span.SectionId, span.Duration, section.Rhythm, section.Facets, section.Groove, section.Energy, section.IsPercussionOnly, section.HasDrums, section.Solo is { IsDrumSolo: true }, span.Meter, section.DrumsFrom))
        ];
        var edits = new TimelineEdits(_context, meter, map);
        var lines = FillGenerator.GetSectionLines(fillSections, origin).ToBuilder();
        var songState = StateTimelineMap.Create(end);
        var introDescription = $"{plan.Intro} intro, {origin} beats";

        switch (plan.Intro)
        {
            // the drums land where the first section starts, but in a first section whose drums rest
            case IntroKind.CountIn when fillSections[0].HasDrums && fillSections[0].DrumsFrom == 0:
                lines.Insert(0, new FillLine(origin, fillSections[0], fillSections[0], 0, HasFill: false));
                break;
            // the hook's drums fill into the first section, as the chorus's would into the section after it
            case IntroKind.Hook when fillSections[0].HasDrums && fillSections[0].DrumsFrom == 0:
                lines.Insert(0, new FillLine(origin, fillSections[hook.Index], fillSections[0], 0));
                introDescription += $", the chorus's first phrase, section {hook.Index}";
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
                // as where the section changes into one whose drums rest; in one whose drums rest, the parts simply come in;
                // a fill in the second half of the bar at most where one part opens the song alone for a bar, which it has
                // to itself until then, as a fill stopping drums that open alone would otherwise leave the bar silent
                if (fillSections[0].HasDrums)
                {
                    var room = plan.First is not null && windowDuration <= first.Meter.BarDuration + 1e-9 ? first.Meter.BarDuration / 2 : double.PositiveInfinity;
                    var landing = new FillLine(windowEnd, fillSections[0], fillSections[0], 0, IsLandingForced: true, Room: room);
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

        StateTrace.RecordSection(TracePoints.SongIntro, FillGenerator.DrumsTrace, sectionIds[0], introDescription, null);

        var description = $"{plan.Ending} ending";
        if (FormLayers.HasFinalChord(plan.Ending))
        {
            // a big ending's held chord, then its last hit, a button's
            var big = plan.Ending == EndingKind.Big;
            blocks.Add(CreateEnding(sections[^1], sections.Take(sections.Count - 1), held, big ? held : endingDuration, _roles));
            if (big)
                blocks.Add(CreateEnding(sections[^1], sections.Take(sections.Count - 1), FormLayers.ButtonLength, last.BarDuration, _roles));

            if (plan.Ending == EndingKind.Stop)
            {
                // the band stops before the line, every part of it, a soloist too, and the drums land on it with the rest; a
                // pitched part kept silent there once its notes are decided, as a held note struck again at a change would sound
                var stop = end - plan.Stop;
                description += $", stopping {plan.Stop} beats before it";
                foreach (var (track, role) in _roles)
                    edits.Rest(track, role, stop, end);
            }

            // the drums land on the final chord, where the song has them
            if (_hasDrums)
                lines.Add(
                    new FillLine(end, fillSections[^1], fillSections[^1], 0, plan.Ending != EndingKind.Stop, IsLandingForced: true)
                );

            // a big ending's roll over the held chord, round the toms from the highest, the kick and a crash on every beat,
            // louder as it goes, and the drums landing on its last hit with the band
            if (big && _hasDrums)
            {
                var played = sections.SelectMany(x => x.Timeline.TrackTimelineMap.Keys).ToHashSet();
                var (toms, crash, kick) = (DrumGroups.GetTrackNumber(DrumDefinitions.Tom), DrumGroups.GetTrackNumber(DrumDefinitions.Cymbal),
                    DrumGroups.Kick.Tracks.FirstOrDefault(played.Contains, -1));
                var steps = (int)Math.Round(held / FormLayers.BigRollStep);
                for (var step = 1; step < steps; step++)
                {
                    var (position, velocity) = (end + step * FormLayers.BigRollStep, FormLayers.BuildRollFrom + (FormLayers.BuildRollTo - FormLayers.BuildRollFrom) * step / steps);
                    if (played.Contains(toms))
                        edits.Hit(toms, position, velocity, DrumDefinitions.Tom.Sounds.Length - (step - 1) % DrumDefinitions.Tom.Sounds.Length, sectionIds[^1], HitKind.Build);
                    if (step % (int)Math.Round(1 / FormLayers.BigRollStep) != 0)
                        continue;
                    if (played.Contains(crash))
                        edits.Hit(crash, position, velocity, 1, sectionIds[^1], HitKind.Build);
                    if (kick >= 0)
                        edits.Hit(kick, position, velocity, 0, sectionIds[^1], HitKind.Build);
                }

                lines.Add(new FillLine(end + held, fillSections[^1], fillSections[^1], 0, false, IsLandingForced: true));
                description += ", the drums rolling over it into a last hit";
            }

            description += $", at beat {end}, held {held} beats";
            if (plan.SlowsDown)
            {
                songState = songState.MergeWith(CreateRitardando(end, end + held, last.BarDuration));
                description += ", slowing down";
            }
        }

        // a fading song plays its last section once more, as the song plans it, and fades out over it, over eight bars
        // at least, a section of one play's pattern played twice
        if (plan.Ending == EndingKind.Fade)
        {
            var fadeStart = Math.Min(spans[^1].Start, end - 2 * last.PatternDuration);
            songState = songState.MergeWith(CreateFade(fadeStart, end));
            description += $", fading from beat {fadeStart} to {end}";
        }

        // a stripped song plays its last section once more and its pitched parts leave one by one over its last two patterns,
        // the appearances it adds for it covering them, each at a bar in an order drawn, those heard in the last appearance,
        // a soloist too, every track of a part together, a riff's twin with it, the drums alone in the last bars; where the drums rest there, the
        // last part standing leading out
        if (plan.Ending == EndingKind.Strip)
        {
            var stripStart = end - 2 * last.PatternDuration;
            var final = sections[^1];
            // the solo part, whose notes are played later over the melody's, heard where the melody is
            bool Heard(TrackRole role) => role == TrackRole.Solo ? _roles.Values.Contains(role) && Heard(TrackRole.Melody) : !final.Resting.Contains(role) && final.HasNotes(role);
            // each part's place in the order from a draw of its own, so that a part resting there leaves the others' places
            var order = _context.GenerateSeed();
            TrackRole[] parts = [.._roles.Values.Select(x => x.Source()).Where(x => x != TrackRole.Drum && Heard(x)).Distinct().OrderBy(x => new GenerationContext(Seeds.Derive(order, (long)x)).GenerateDouble())];
            var leaving = Heard(TrackRole.Drum) ? parts.Length : Math.Max(0, parts.Length - 1);
            // on the bar lines of the appearances it covers, a short bar's among them
            double[] barLines = [..map.Sections.Where(x => x.End > stripStart + 1e-9).SelectMany(x => Enumerable.Range(0, x.BarCount).Select(b => x.Bar(b).Start)).Where(x => x >= stripStart - 1e-9 && x < end - 1e-9)];
            for (var i = 0; i < leaving; i++)
            {
                var exit = barLines[(i + 1) * barLines.Length / (leaving + 1)];
                foreach (var (track, role) in _roles.Where(x => x.Value.Source() == parts[i]))
                    edits.Rest(track, role, exit, end);

                description += $", {parts[i]} out at {exit}";
            }
        }

        StateTrace.RecordSection(TracePoints.SongEnding, FillGenerator.DrumsTrace, sectionIds[^1], description, null);
        // a lift the drums announce built to and dropped at, now and then, a genre's: the band out for the bar before it, the
        // lines that walk into the section with it, but for the drums' fill into it, and the drums landing on it
        var drop = FormLayers.Drop.At(_songRhythm.Value, _genre.Chance(GenreChoices.Drop, "lifts"));
        var tracks = sections.SelectMany(x => x.Timeline.TrackTimelineMap.Keys).ToHashSet();
        // the snare a build rolls on: the song's electric or acoustic snare, or its clap
        var snare = FormLayers.BuildRollDrums.Select(DrumGroups.GetTrackNumber).FirstOrDefault(tracks.Contains, -1);
        var drops = new HashSet<double>();
        var builds = new List<(double From, double To)>();
        var filtered = new HashSet<double>();
        var kinds = ByConvention.Weigh(FormLayers.DropKinds, _songRhythm.Value, _genre.Pool<DropKind>(GenreChoices.DropKind));
        // every line's drop and its kind from sequences of its own, by its place, drawn before any line is passed over, so
        // that whether another line may drop leaves them as they were
        var (dropSeed, kindSeed, rollSeed) = (_dropContext.GenerateSeed(), _dropKindContext.GenerateSeed(), _buildRollContext.GenerateSeed());
        var roll = FormLayers.BuildRoll.At(_songRhythm.Value, _genre.Chance(GenreChoices.Drop, "rolls"));
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var place = map.PlaceKey(line.Position, LinePlacesPerBeat);
            var dropping = new GenerationContext(Seeds.Derive(dropSeed, place)).TestProbability(drop);
            var kind = new GenerationContext(Seeds.Derive(kindSeed, place)).Pick(kinds);
            var rolls = snare >= 0 && new GenerationContext(Seeds.Derive(rollSeed, place)).TestProbability(roll);
            if (!line.Lifts || line.Position >= end || !dropping)
                continue;
            // but where the band does not come back on it, the section's first bar leaving every pitched part out, as a
            // texture building it up does
            var landing = map.Sections.Select((x, index) => (x, index)).FirstOrDefault(x => Math.Abs(x.x.Start - line.Position) < 1e-9);
            if (landing.x is { } next && !timelines[landing.index].TrackTimelineMap.Any(x => _roles.TryGetValue(x.Key, out var role) && role != TrackRole.Drum
                    && x.Value.EventTimeline.Any(n => n.Position < next.Meter.BarDuration - 1e-9)))
                continue;
            // the band silent over the bar before it, the bar there is, a short last bar's (ShortBars), or playing on,
            // filtered (MixFilter)
            var gap = line.Position - map.BarBefore(line.Position);
            // the drums' hits kept for their fill, every other part, a soloist's too, kept out of it once the lines walk
            // into the section
            foreach (var (track, role) in _roles.Where(_ => kind == DropKind.Silent))
                edits.Rest(track, role, gap, line.Position);

            // the build: the song's snare rolling over the bars before the silent one, where it rolls, its notes twice as
            // fast each bar and louder as it goes, within the section
            var bar = line.Ending.Meter.BarDuration;
            var rollFrom = Math.Max(gap - FormLayers.BuildRollBars * bar, i > 0 ? lines[i - 1].Position : 0);
            // each bar of the roll at its own rate, every hit counted
            // the snare rolling into the drop whether the drums played the bars before it or not, as a dance track's build
            // brings the drums back over a breakdown
            if (rolls)
                builds.Add((rollFrom, gap));
            if (rolls)
                for (var (barStart, step) = (gap - FormLayers.BuildRollBars * bar, FormLayers.BuildRollStart); barStart < gap - 1e-9; barStart += bar, step /= 2)
                for (var hit = 0; hit < (int)Math.Round(bar / step); hit++)
                {
                    var position = barStart + hit * step;
                    if (position < rollFrom - 1e-9)
                        continue;
                    var share = (position - rollFrom) / (gap - rollFrom);
                    edits.Hit(snare, position, FormLayers.BuildRollFrom + (FormLayers.BuildRollTo - FormLayers.BuildRollFrom) * share, 0, line.Ending.SectionId, HitKind.Build);
                }

            lines[i] = line with { IsLandingForced = true };
            drops.Add(line.Position);
            if (kind == DropKind.Filtered)
                filtered.Add(line.Position);
            StateTrace.RecordSection(kind == DropKind.Silent ? TracePoints.Drop : TracePoints.FilteredDrop, FillGenerator.DrumsTrace, line.Next.SectionId,
                $"into {line.Position}", line.Position);
        }

        // the band grows louder into a louder section, over the phrase before a drop, at every change of section whether
        // the drums mark it or not, a hook intro's into the first among them
        List<(double Position, FillSection Ending, FillSection Next)> changes = [..map.Sections.Skip(1).Select((x, i) => (x.Start, fillSections[i], fillSections[i + 1]))];
        if (plan.Intro == IntroKind.Hook)
            changes.Insert(0, (origin, fillSections[hook.Index], fillSections[0]));
        songState = songState.MergeWith(CreateLifts(changes, end, drops, Math.Pow(FormLayers.GenreLiftReach, _genre.Shift(GenreChoices.Dynamics, "lift")), map.BarBefore));

        return new SongAssembly(map, blocks.ToImmutable(), lines.ToImmutable(), edits, songState, [..drops.Order()], [..filtered.Order()], [..builds]);
    }

    /// <summary>
    ///     When the parts of the band come in, over an intro's window: those that play in it, in an order drawn part by
    ///     part, each likelier as <see cref="FormLayers.IntroParts" /> weighs it, leaned by the song's rhythm, a part its
    ///     rhythm gives no weight, as the plainest rhythm gives the unconventional ones, after the rest; a drawn
    ///     number of them, at least one and one fewer than all, come in over the window, spread evenly from its start,
    ///     and the rest together at its end, so that the window never plays the whole band.
    /// </summary>
    internal ImmutableArray<IntroEntry> DrawEntries(GeneratedSection first, IntroWindow window, TrackRole? opening)
    {
        var windowDuration = window.Bars * first.Meter.BarDuration;
        // but drums that only mark the form, which play in the sections alone
        var parts = first.Timeline.TrackTimelineMap
            .Where(x => x.Value.EventTimeline.Any(note => note.Position < windowDuration) && (_drumsKeepTime || _roles[x.Key] != TrackRole.Drum))
            .GroupBy(x => GetPart(x.Key, first))
            .ToDictionary(x => x.Key, x => x.Select(y => y.Key).Order().ToImmutableArray());
        var listed = FormLayers.IntroParts.ToDictionary(x => x.Part);
        foreach (var part in parts.Keys.Where(x => !listed.ContainsKey(x)))
            throw new InvalidOperationException($"The intro has no weight for the part {part}.");
        // a window no part plays in, as where a song's only part rests at first, has none to bring in
        if (parts.Count == 0)
            return [];

        // every part that plays comes in: those the rhythm weighs first, then those it gives none, by their own weights
        var playing = FormLayers.IntroParts.Where(x => parts.ContainsKey(x.Part)).ToArray();
        var weighed = playing.Where(x => RhythmicUnconventionality.WeightEnds(x.Weight, x.Lean).At(_songRhythm.Value) > 0).ToArray();
        // in an order by weight, each part's place from a draw of its own, so that a part resting in the window leaves the
        // others' places as they were
        var (orderSeed, countSeed) = (_introContext.GenerateSeed(), _introContext.GenerateSeed());
        double Key(Weighted<IntroPart> x) =>
            Math.Pow(new GenerationContext(Seeds.Derive(orderSeed, (long)x.Value.Role * 16 + (x.Value.DrumRole is { } drum ? (long)drum + 1 : 0))).GenerateDouble(), 1 / x.Weight);
        List<IntroPart> order =
        [
            ..(weighed.Length == 0 ? [] : ByConvention.Weigh(weighed.Select(x => (x.Part, RhythmicUnconventionality.WeightEnds(x.Weight, x.Lean))), _songRhythm.Value)).OrderByDescending(Key).Select(x => x.Value),
            ..playing.Except(weighed).Select(x => new Weighted<IntroPart>(x.Weight, x.Part)).OrderByDescending(Key).Select(x => x.Value)
        ];
        // the part the song opens on alone first, the drums' every one, the rest in the order drawn
        var alone = order.Where(x => x.Role == opening).ToArray();
        order = [..alone, ..order.Except(alone)];

        var inWindow = order.Count <= 1 ? order.Count : Generators.Int(1, order.Count)(new GenerationContext(countSeed));
        return
        [
            ..order.Select((part, i) => new IntroEntry(
                    part,
                    parts[part],
                    // a part opening the song alone for a bar at least
                    Math.Max(i < inWindow ? Math.Floor(i * window.Bars / (double)inWindow) * first.Meter.BarDuration : windowDuration,
                        alone.Length > 0 && part.Role != opening ? first.Meter.BarDuration : 0)
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
                            // half a bar of clicks where the bar has four pulses or more, since one alone counts nothing in
                            x.Key == clickTrack ? pulses.Skip(isHalf && pulses.Length >= 4 ? pulses.Length / 2 : 0).Select(pulse => click.ToTimelineItem(pulse)) : []
                        )
                    )
                )
            );
        return TrackEventStateTimelineMap.Create(first.Meter.BarDuration, tracks, bar.CommonStateTimelineMap);
    }

    /// <summary>
    ///     A line's note that ends the song, asked to land on its chord's root, which the line places nearest the note
    ///     before (<see cref="LinePattern.Place" />), going on from it rather than starting a phrase afresh, and no pickup,
    ///     though the note it is made of led into a chord, as it has none after it to lead into.
    /// </summary>
    internal static TimelineItem<StateMap> LandOnRoot(TimelineItem<StateMap> note)
    {
        return note.Value
            .With(CompositionStateKinds.LineLanding, (int)ChordArrival.Root)
            .With(CompositionStateKinds.LinePhraseStart, (int)PhraseStart.None)
            .With(CompositionStateKinds.LinePickup, 0)
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
        // every track plays the home bar's chord as its earliest note has it, the downbeat's, whichever note it takes its
        // own state from
        IStateKind[] shapeKinds = [StateKinds.ChordNotePitchOffsets, StateKinds.ChordVoicingFixed];
        var homeShape = homeBar.TrackTimelineMap
            .Where(x => roles[x.Key] != TrackRole.Drum && x.Value.EventTimeline.Count > 0)
            .OrderBy(x => x.Value.EventTimeline[0].Position)
            .Select(x => x.Value.EventTimeline[0].Value.Subset(shapeKinds))
            .FirstOrDefault();
        GeneratedSection[] before = [..earlierSections.Reverse()];
        // every track the song plays, one the last section rests in on the state of the last section it played in
        var tracks = roles.Keys.Select(track => (Key: track, Value: homeBar.TrackTimelineMap.GetValueOrDefault(track)
                                                                ?? before.FirstOrDefault(x => x.Timeline.TrackTimelineMap.ContainsKey(track))?.Timeline.TrackTimelineMap[track]
                                                                    .Trim(lastSection.Meter.BarDuration)))
            .Where(x => x.Value is not null)
            .Select(x =>
            {
                // a track that rests in the home bar plays its first note of the section, or, if it rests all of it,
                // its last note of the sections before, over the home bar's root
                var template = x.Value!.EventTimeline.Where(_ => homeBar.TrackTimelineMap.ContainsKey(x.Key))
                    .Concat(lastSection.Timeline.TrackTimelineMap.TryGetValue(x.Key, out var own) ? own.EventTimeline : [])
                    .Concat(before.SelectMany(section =>
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

    /// <summary>
    ///     The lifts into the louder sections: the band's loudness rising over the bar before a change of section, from
    ///     as loud as it plays to louder by how much more energy the next section has, as far as the ending section's rhythm
    ///     follows its energy, a step every <see cref="FormLayers.FadeStep" />, the next section then as loud as it plays.
    /// </summary>
    /// <param name="bars">How many bars a lift swells over, a genre's, where no drop builds to it.</param>
    /// <param name="barBefore">How long the bar before a position is, a short last bar's (<see cref="SongMap.BarBefore" />).</param>
    /// <param name="changes">The changes of section, in order: where each is, the section it ends and the one it leads into.</param>
    private static StateTimelineMap CreateLifts(IReadOnlyList<(double Position, FillSection Ending, FillSection Next)> changes, double end, IReadOnlySet<double> drops, double bars,
        Func<double, double> barBefore)
    {
        var items = new List<TimelineItem<double>>();
        for (var i = 0; i < changes.Count; i++)
        {
            var (position, ending, next) = changes[i];
            var lift = ending.LiftInto(next);
            if (position >= end || lift <= 0)
                continue;

            // over the last bar of the section it ends, or the phrase before a drop, within the section
            // the last bar as long as it is, a short one's, and whole bars before it
            var reach = drops.Contains(position) ? FormLayers.BuildBars : bars;
            var length = reach >= 1 ? barBefore(position) + (reach - 1) * ending.Meter.BarDuration : reach * barBefore(position);
            var start = Math.Max(position - length, i > 0 ? changes[i - 1].Position : 0);
            var count = (int)Math.Round((position - start) / FormLayers.FadeStep);
            items.AddRange(Enumerable.Range(1, count).Select(j => (FormLayers.LiftVelocity * lift * j / count).ToTimelineItem(start + j * FormLayers.FadeStep - FormLayers.FadeStep)));
            items.Add(0.0.ToTimelineItem(position));
            StateTrace.RecordSong(TracePoints.Lift, $"{lift:F2} into {position}", (position, lift));
        }

        IStateTimeline velocity = StateTimeline.Create(end, StateKinds.Velocity, items.OrderBy(x => x.Position)).WithLayer("Lift");
        return new[] { velocity }.ToStateTimelineMap(end);
    }

    /// <summary>The band fading out from the start given to the end, a step every <see cref="FormLayers.FadeStep" />, to silence.</summary>
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

    /// <summary>The tempo slowing over the bar before the ending, in its steps over the bar whatever its meter, and staying slow to the end.</summary>
    private static StateTimelineMap CreateRitardando(double line, double end, double bar)
    {
        var steps = FormLayers.Ritardando;
        IStateTimeline ritardando = StateTimeline.Create(
                end,
                StateKinds.Tempo,
                steps.Select((x, i) => x.ToTimelineItem(line - bar + i * bar / steps.Length))
            )
            .WithLayer("Ending");
        return new[] { ritardando }.ToStateTimelineMap(end);
    }
}

/// <summary>What a song's form will be, decided before its sections.</summary>
/// <param name="Window">Where the band comes in part by part, for an intro of entries.</param>
/// <param name="HalfCountIn">Whether a count-in clicks only the last two beats.</param>
/// <param name="First">The part the song opens on alone, the band coming in after it; none for a song opening otherwise.</param>
/// <param name="Held">How long a button's final chord is held, in beats; 0 for a ringing ending's and an ending with none, open or fading.</param>
/// <param name="RingOutBars">How many bars a ringing ending's final chord is held, in the last section's meter; 0 for any other ending.</param>
/// <param name="Stop">How long the band is silent before a stopped ending's chord, in beats.</param>
/// <param name="SlowsDown">Whether the bar before a ringing ending slows down.</param>
/// <param name="TonicHomeSectionId">The section whose home is the song's tonic, the last, which leads home to the ending.</param>
internal sealed record FormPlan(
    IntroKind Intro,
    IntroWindow Window,
    bool HalfCountIn,
    TrackRole? First,
    EndingKind Ending,
    double Held,
    double RingOutBars,
    double Stop,
    bool SlowsDown,
    int TonicHomeSectionId
);

/// <summary>A song put together: where its parts are, its blocks to put one after another, and what the form adds.</summary>
/// <param name="Blocks">The intro, if it has bars of its own, the sections, and the ending, if it has bars of its own.</param>
/// <param name="Lines">The lines the fills mark, in the song's order.</param>
/// <param name="Edits">What the form changes once the blocks are put one after another, such as the bars an intro leaves out.</param>
/// <param name="SongState">How the song's state changes over it, over its own, such as the tempo slowing before an ending, or the band fading out.</param>
/// <param name="Drops">Where a lift drops, the band silent for the bar before it, in the song's order (<see cref="FormLayers.Drop" />).</param>
/// <param name="FilteredDrops">The drops whose bar before the band plays on through the whole mix filtered, rather than silent (<see cref="FormLayers.DropKinds" />).</param>
/// <param name="Builds">Where the snare rolls into a drop, which the stages that play around a fill keep out of (<see cref="Hold.Build" />).</param>
internal sealed record SongAssembly(
    SongMap Map,
    ImmutableArray<TrackEventStateTimelineMap<StateMap>> Blocks,
    ImmutableArray<FillLine> Lines,
    TimelineEdits Edits,
    StateTimelineMap SongState,
    ImmutableArray<double> Drops,
    ImmutableArray<double> FilteredDrops,
    ImmutableArray<(double From, double To)> Builds
);
