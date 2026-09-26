using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     The form of a song around its sections: how it starts and how it ends. It sees the sections before they are put
///     one after another, so it can put bars before and after them, and it tells the fills where the lines are that
///     they mark. An intro puts the first section's drums or a count-in before it, or leaves tracks out of its first
///     phrase, to come in after it. The ending plays the home bar of the last section, whose home is the song's tonic: every track's first
///     note of it, moved to the downbeat and held, the bass on the root and the melody's last note on it too.
/// </summary>
internal sealed class SongFormGenerator
{
    private const double BarDuration = 4;

    private readonly IGenerationContext _context;
    private readonly RhythmicUnconventionality _songRhythm;

    public SongFormGenerator(IGenerationContext context, RhythmicUnconventionality songRhythm)
    {
        _context = context;
        _songRhythm = songRhythm;
    }

    /// <param name="sectionIds">The sections in the song's order.</param>
    /// <param name="sections">Every section in the song's order, as generated.</param>
    public SongForm Generate(IReadOnlyList<int> sectionIds, IReadOnlyList<GeneratedSection> sections)
    {
        ImmutableArray<FillSection> fillSections =
        [
            ..sectionIds.Zip(sections, (id, section) => new FillSection(id, section.Timeline.Duration, section.Rhythm, section.DrumTuplet))
        ];
        var intro = Pick(FormLayers.Intros);
        var ending = Pick(FormLayers.WeighEndings(_songRhythm.ChanceScale));

        var blocks = ImmutableArray.CreateBuilder<TrackEventStateTimelineMap<StateMap>>();
        var first = sections[0];
        var introBlock = intro switch
        {
            IntroKind.DrumsFirst => CreateDrumsFirst(first, Pick(FormLayers.DrumsFirstBars) * BarDuration),
            IntroKind.CountIn => CreateCountIn(first, _context.TestProbability(FormLayers.HalfCountInChance)),
            _ => null
        };
        if (introBlock is not null)
            blocks.Add(introBlock);
        blocks.AddRange(sections.Select(x => x.Timeline));

        // the first section starts after the intro's bars
        var origin = introBlock?.Duration ?? 0;
        var edits = new TimelineEdits(_context, origin);
        var lines = FillGenerator.GetSectionLines(fillSections, origin).ToBuilder();
        var end = origin + sections.Sum(x => x.Timeline.Duration);
        var tempo = StateTimelineMap.Create(end);
        var introDescription = $"{intro} intro, {origin} beats";

        switch (intro)
        {
            case IntroKind.DrumsFirst:
                // the band comes in on a fill and a landing
                lines.Insert(0, new FillLine(origin, fillSections[0], sectionIds[0], FillTable.Section, LandingRule.Forced));
                break;
            case IntroKind.CountIn:
                lines.Insert(0, new FillLine(origin, fillSections[0], sectionIds[0], FillTable.None, LandingRule.Section));
                break;
            case IntroKind.ChordsFirst or IntroKind.Build:
            {
                // the first phrase leaves tracks out, which come in at its end, the drums with a fill and a landing
                var phraseEnd = origin + BarStateGenerator.PatternDuration;
                var withBass = intro == IntroKind.ChordsFirst && _context.TestProbability(FormLayers.ChordsFirstBassChance);
                foreach (var track in first.Timeline.TrackTimelineMap.Keys)
                {
                    var entry = GetIntroEntry(intro, track, withBass);
                    if (entry > 0)
                        edits.Clear(track, origin, origin + entry);
                }

                var phraseLine = lines.Select((x, i) => (x, i)).First(x => x.x.Position.IsEqualToByEpsilon(phraseEnd)).i;
                lines[phraseLine] = new FillLine(phraseEnd, fillSections[0], sectionIds[0], FillTable.Section, LandingRule.Forced);
                if (withBass)
                    introDescription += ", with the bass";
                break;
            }
        }

        StateTrace.Record("Song intro", FillGenerator.DrumsTrace, sectionIds[0], 0, StateMap.Default, 0, introDescription);

        var description = $"{ending} ending";
        if (ending != EndingKind.Open)
        {
            var (length, duration) = ending switch
            {
                EndingKind.RingOut => (Pick(FormLayers.RingOutLengths), 0.0),
                _ => (FormLayers.ButtonLength, BarDuration)
            };
            blocks.Add(CreateEnding(sections[^1], sections.Take(sections.Count - 1), length, Math.Max(length, duration)));

            if (ending == EndingKind.Stop)
            {
                // the band stops before the line, and the drums land on it with the rest
                var stopLength = Pick(FormLayers.StopLengths);
                var stop = end - stopLength;
                description += $", stopping {stopLength} beats before it";
                foreach (var track in sections[^1].Timeline.TrackTimelineMap.Keys)
                {
                    edits.Cut(track, stop);
                    edits.Clear(track, stop, end);
                }
            }

            lines.Add(new FillLine(end, fillSections[^1], sectionIds[^1], ending == EndingKind.Stop ? FillTable.None : FillTable.Section, LandingRule.Forced));

            description += $", at beat {end}, held {length} beats";
            if (ending == EndingKind.RingOut && _context.TestProbability(FormLayers.RitardandoChance))
            {
                tempo = CreateRitardando(end, end + length);
                description += ", slowing down";
            }
        }

        StateTrace.Record("Song ending", FillGenerator.DrumsTrace, sectionIds[^1], 0, StateMap.Default, 0, description);
        return new SongForm(blocks.ToImmutable(), lines.ToImmutable(), origin, edits, tempo);
    }

    /// <summary>
    ///     When a track comes in, in beats into the first phrase, as an intro leaves it out: the chords from the start, and
    ///     in a build the bass and the drums bar by bar; the others after the phrase; 0 for from the start.
    /// </summary>
    internal static double GetIntroEntry(IntroKind intro, int track, bool withBass)
    {
        var phrase = BarStateGenerator.PatternDuration;
        if (track == SongTracks.ChordsTrack)
            return 0;
        if (intro == IntroKind.ChordsFirst)
            return track == SongTracks.BassTrack && withBass ? 0 : phrase;

        return track switch
        {
            SongTracks.BassTrack => FormLayers.BuildBassBar * BarDuration,
            >= DrumGroups.FirstTrackNumber => FormLayers.BuildDrumsBar * BarDuration,
            _ => phrase
        };
    }

    /// <summary>The first section's drums alone, notes and state, for the intro's bars.</summary>
    private static TrackEventStateTimelineMap<StateMap> CreateDrumsFirst(GeneratedSection first, double duration)
    {
        var bars = first.Timeline.Trim(duration);
        return TrackEventStateTimelineMap.Create(
            duration,
            bars.TrackTimelineMap.Where(x => x.Key >= DrumGroups.FirstTrackNumber),
            bars.CommonStateTimelineMap
        );
    }

    /// <summary>A bar of the pedal hi-hat on the beats, or on the last two, over the first section's drum state.</summary>
    private static TrackEventStateTimelineMap<StateMap> CreateCountIn(GeneratedSection first, bool isHalf)
    {
        var bar = first.Timeline.Trim(BarDuration);
        var hiHat = DrumGroups.GetTrackNumber(DrumDefinitions.HiHat);
        var click = StateMap.FromStates(
            [
                StateKinds.Velocity.CreateState(FormLayers.CountInVelocity),
                StateKinds.ArticulationIndex.CreateState(DrumDefinitions.HiHat.GetArticulationIndex(DrumSounds.PedalHiHat))
            ]
        );
        var tracks = bar.TrackTimelineMap
            .Where(x => x.Key >= DrumGroups.FirstTrackNumber)
            .Select(x => new KeyValuePair<int, EventStateTimelineMap<StateMap>>(
                    x.Key,
                    x.Value.WithEvents(
                        EventTimeline.Create(
                            BarDuration,
                            x.Key == hiHat ? Enumerable.Range(isHalf ? 2 : 0, isHalf ? 2 : 4).Select(beat => click.ToTimelineItem(beat)) : []
                        )
                    )
                )
            );
        return TrackEventStateTimelineMap.Create(BarDuration, tracks, bar.CommonStateTimelineMap);
    }

    private T Pick<T>(ImmutableArray<Weighted<T>> weights)
    {
        return weights[Generators.WeightedIndex(weights)(_context)].Value;
    }

    /// <summary>
    ///     The ending's bars: the home bar of the song's last section, every pitched track's first note of it moved to
    ///     the downbeat and held, or its first of the section if it rests in that bar, or its last before the section if
    ///     it rests in all of it, the melody's as its last note; the
    ///     drums' state with no notes, for the fills to land on; and the bass on the chord's root.
    /// </summary>
    /// <param name="length">How long the chord is held, in beats.</param>
    /// <param name="duration">How long the ending is, in beats.</param>
    /// <param name="earlierSections">The sections before the last, in the song's order.</param>
    internal static TrackEventStateTimelineMap<StateMap> CreateEnding(
        GeneratedSection lastSection,
        IEnumerable<GeneratedSection> earlierSections,
        double length,
        double duration
    )
    {
        var homeBar = lastSection.Timeline.Trim(BarDuration);
        var final = StateMap.FromStates([StateKinds.HeldDuration.CreateState(length), StateKinds.MelodyFinal.CreateState(1)]);
        // every track plays the home bar's chord, whichever note it takes its own state from
        IStateKind[] shapeKinds = [StateKinds.ChordNotePitchOffsets, StateKinds.ChordVoicingFixed];
        var homeShape = homeBar.TrackTimelineMap
            .Where(x => x.Key < DrumGroups.FirstTrackNumber && x.Value.EventTimeline.Count > 0)
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
                var notes = x.Key >= DrumGroups.FirstTrackNumber
                    ? []
                    : template.Take(1)
                        // on the home chord itself, without the note's own step of the walk of the root
                        .Select(note => (homeShape is null ? note.Value : note.Value.Except(shapeKinds).MergeWith(homeShape))
                            .Except([StateKinds.ChordRootNoteOffset])
                            .MergeWith(final)
                            .ToTimelineItem(0)
                        );
                return new KeyValuePair<int, EventStateTimelineMap<StateMap>>(
                    x.Key,
                    EventStateTimelineMap.Create(BarDuration, EventTimeline.Create(BarDuration, notes), x.Value.StateTimelineMap)
                );
            }
        );
        // the bass lands on the root, whatever the bar's own arrival
        var commonStateTimelineMap = homeBar.CommonStateTimelineMap
            .Except([StateKinds.ChordArrival])
            .MergeStateMap(StateMap.FromStates([StateKinds.ChordArrival.CreateState((int)ChordArrival.Root)]));
        return TrackEventStateTimelineMap.Merge(
            [
                TrackEventStateTimelineMap.Create(BarDuration, tracks, commonStateTimelineMap),
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

/// <summary>A song's form: its blocks to put one after another, the lines the fills mark, and the changes it makes.</summary>
/// <param name="Blocks">The intro, if it has bars of its own, the sections, and the ending, if it has bars of its own.</param>
/// <param name="Origin">Where the first section starts, after the intro's bars.</param>
/// <param name="Edits">What the form changes once the blocks are put one after another, such as the bars an intro leaves out.</param>
/// <param name="Tempo">How the tempo changes over the song, such as the slowing before an ending, over the song's own.</param>
internal sealed record SongForm(
    ImmutableArray<TrackEventStateTimelineMap<StateMap>> Blocks,
    ImmutableArray<FillLine> Lines,
    double Origin,
    TimelineEdits Edits,
    StateTimelineMap Tempo
);
